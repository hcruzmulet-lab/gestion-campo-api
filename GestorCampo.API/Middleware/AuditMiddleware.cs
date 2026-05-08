// GestorCampo.API/Middleware/AuditMiddleware.cs
using System.Security.Claims;
using GestorCampo.Domain.Entities;
using GestorCampo.Infrastructure.Persistence;

namespace GestorCampo.API.Middleware;

public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        await _next(context);

        if (!ShouldAudit(context)) return;

        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var log = new AuditLog
        {
            UserId = userId != null ? Guid.Parse(userId) : null,
            Action = $"{context.Request.Method} {context.Response.StatusCode}",
            Module = ExtractModule(context.Request.Path),
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    private static bool ShouldAudit(HttpContext ctx) =>
        !ctx.Request.Path.StartsWithSegments("/swagger") &&
        !ctx.Request.Path.StartsWithSegments("/health") &&
        ctx.Request.Method != "GET";

    private static string ExtractModule(PathString path)
    {
        var parts = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts?.Length >= 2 ? parts[1] : "unknown";
    }
}
