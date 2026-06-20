using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GestorCampo.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace GestorCampo.Tests.Middleware;

public class IdempotencyMiddlewareTests
{
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _db = new();

    public IdempotencyMiddlewareTests()
    {
        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
    }

    private static DefaultHttpContext BuildContext(
        string method, string? idempotencyKey, string? userId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = "/api/orders";
        if (idempotencyKey != null)
            ctx.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (userId != null)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(JwtRegisteredClaimNames.Sub, userId) }, "TestAuth");
            ctx.User = new ClaimsPrincipal(identity);
        }
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static string ReadBody(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        return new StreamReader(ctx.Response.Body).ReadToEnd();
    }

    [Fact]
    public async Task Post_WithKey_CacheMiss_InvokesNextOnce_AndStoresResponse()
    {
        var userId = Guid.NewGuid().ToString();
        var ctx = BuildContext("POST", "temp-123", userId);
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var calls = 0;
        RequestDelegate next = async c =>
        {
            calls++;
            c.Response.StatusCode = 201;
            c.Response.ContentType = "application/json";
            await c.Response.WriteAsync("{\"id\":\"abc\"}");
        };

        var sut = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        await sut.InvokeAsync(ctx, _redis.Object);

        calls.Should().Be(1);
        ReadBody(ctx).Should().Be("{\"id\":\"abc\"}");
        _db.Verify(d => d.StringSetAsync(
            $"idem:{userId}:temp-123",
            It.IsAny<RedisValue>(),
            It.Is<TimeSpan?>(t => t == TimeSpan.FromHours(24)),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Post_WithKey_CacheHit_DoesNotInvokeNext_AndReplaysStoredResponse()
    {
        var userId = Guid.NewGuid().ToString();
        var ctx = BuildContext("POST", "temp-123", userId);
        var stored = JsonSerializer.Serialize(new
        {
            StatusCode = 201,
            ContentType = "application/json",
            Body = "{\"id\":\"replayed\"}"
        });
        _db.Setup(d => d.StringGetAsync(
                (RedisKey)$"idem:{userId}:temp-123", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)stored);

        var calls = 0;
        RequestDelegate next = c => { calls++; return Task.CompletedTask; };

        var sut = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        await sut.InvokeAsync(ctx, _redis.Object);

        calls.Should().Be(0);
        ctx.Response.StatusCode.Should().Be(201);
        ctx.Response.ContentType.Should().Be("application/json");
        ReadBody(ctx).Should().Be("{\"id\":\"replayed\"}");
        _db.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
            It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithoutKey_PassesThrough_NeverTouchesRedis()
    {
        var ctx = BuildContext("POST", idempotencyKey: null, userId: Guid.NewGuid().ToString());

        var calls = 0;
        RequestDelegate next = c => { calls++; return Task.CompletedTask; };

        var sut = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        await sut.InvokeAsync(ctx, _redis.Object);

        calls.Should().Be(1);
        _redis.Verify(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Post_NonSuccessResponse_IsNotCached()
    {
        var userId = Guid.NewGuid().ToString();
        var ctx = BuildContext("POST", "temp-456", userId);
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        RequestDelegate next = async c =>
        {
            c.Response.StatusCode = 400;
            await c.Response.WriteAsync("{\"error\":\"bad\"}");
        };

        var sut = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        await sut.InvokeAsync(ctx, _redis.Object);

        ReadBody(ctx).Should().Be("{\"error\":\"bad\"}");
        _db.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
            It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
    }
}
