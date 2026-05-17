// GestorCampo.API/Program.cs
using System.Text;
using Amazon.S3;
using GestorCampo.Application.AuditLogs;
using GestorCampo.Application.Auth;
using GestorCampo.Application.Clients;
using GestorCampo.Application.Common;
using GestorCampo.Application.Dashboard;
using GestorCampo.Application.Orders;
using GestorCampo.Application.Products;
using GestorCampo.Application.Sync;
using GestorCampo.Application.Tracking;
using GestorCampo.Application.Users;
using GestorCampo.Application.Visits;
using GestorCampo.API.Middleware;
using GestorCampo.Domain.Interfaces.Providers;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Interfaces.Services;
using GestorCampo.Infrastructure.Adapters;
using GestorCampo.Infrastructure.Persistence;
using GestorCampo.Infrastructure.Persistence.Repositories;
using GestorCampo.Infrastructure.Services;
using GestorCampo.Infrastructure.Services.Storage;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITrackingRepository, TrackingRepository>();
builder.Services.AddScoped<ISyncLogRepository, SyncLogRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Adapters
builder.Services.AddScoped<IClientProvider, NullClientProvider>();
builder.Services.AddScoped<IProductProvider, NullProductProvider>();
builder.Services.AddScoped<IOrderExporter, NullOrderExporter>();

// Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<VisitService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<TrackingService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AgentStatusService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<AuditLogService>();

// Storage + geofence + attachments (Plan 7)
builder.Services.Configure<S3StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<S3StorageOptions>>().Value;
    var config = new AmazonS3Config
    {
        ServiceURL = opts.Endpoint,
        ForcePathStyle = opts.ForcePathStyle,
        UseHttp = opts.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
    };
    return new AmazonS3Client(opts.AccessKey, opts.SecretKey, config);
});
builder.Services.AddScoped<IFileStorage, S3FileStorage>();
builder.Services.AddSingleton<GeofenceService>();
builder.Services.AddScoped<IVisitAttachmentRepository, VisitAttachmentRepository>();
builder.Services.AddScoped<VisitAttachmentService>();

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.MapInboundClaims = false; // keep "role" claim name as-is; without this the handler remaps it to ClaimTypes.Role and [Authorize(Roles=...)] fails
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

// Rate Limiting — auth endpoints
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
});

// Hangfire (InMemory — replace with Hangfire.PostgreSql for production)
builder.Services.AddHangfire(config =>
    config.UseInMemoryStorage());
builder.Services.AddHangfireServer();

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(opts =>
    opts.AddPolicy("Default", p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Controllers + OpenAPI
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "GestorCampo API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});

var app = builder.Build();

// Migrate DB on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Seed SuperAdmin if no users exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

    if (!db.Users.Any())
    {
        var adminId = Guid.NewGuid();
        db.Users.Add(new GestorCampo.Domain.Entities.User
        {
            Id = adminId,
            Name = "Super Admin",
            Email = "admin@gestor.com",
            PasswordHash = passwordService.Hash("Admin1234!"),
            Role = GestorCampo.Domain.Enums.UserRole.SuperAdmin,
            IsActive = true,
            EmailVerified = true,
            CreatedBy = adminId,
            UpdatedBy = adminId,
        });
        await db.SaveChangesAsync();
    }
}

// Dev seed: agents, visits, tracking data for dashboard testing
if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("DevSeed:Dashboard"))
{
    await GestorCampo.API.DevSeed.DashboardDevSeed.SeedAsync(app.Services);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Default");
app.UseRateLimiter();
app.UseMiddleware<AuditMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

RecurringJob.AddOrUpdate<SyncService>("sync-clients", s => s.SyncClientsAsync(CancellationToken.None), Cron.Daily);
RecurringJob.AddOrUpdate<SyncService>("sync-products", s => s.SyncProductsAsync(CancellationToken.None), Cron.Daily);

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program { } // for integration tests
