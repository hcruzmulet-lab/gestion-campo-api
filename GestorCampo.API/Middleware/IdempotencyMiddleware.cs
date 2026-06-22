// GestorCampo.API/Middleware/IdempotencyMiddleware.cs
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using StackExchange.Redis;

namespace GestorCampo.API.Middleware;

/// <summary>
/// Redis-backed idempotency for entity-create POSTs. The mobile outbox retries
/// create requests after a lost ACK; when the client sends a stable
/// <c>Idempotency-Key</c> header (its local tempId) we replay the original
/// stored response instead of re-executing the handler, preventing duplicate
/// orders / visits / clients.
/// </summary>
public class IdempotencyMiddleware
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConnectionMultiplexer redis)
    {
        // Only POSTs carrying a non-empty Idempotency-Key are eligible.
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _next(context);
            return;
        }

        // Scope by the authenticated user so two users reusing the same tempId
        // can't collide. Read the same "sub" claim the controllers use
        // (MapInboundClaims = false keeps it un-remapped).
        var userId = context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"idem:{userId}:{idempotencyKey}";

        IDatabase db;
        try
        {
            db = redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                var stored = JsonSerializer.Deserialize<CachedResponse>(cached!);
                if (stored != null)
                {
                    // Cache HIT: replay the original response, skip the handler.
                    context.Response.StatusCode = stored.StatusCode;
                    if (!string.IsNullOrEmpty(stored.ContentType))
                        context.Response.ContentType = stored.ContentType;
                    await context.Response.WriteAsync(stored.Body);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            // Redis down / unreachable: never break the request, just proceed.
            _logger.LogWarning(ex, "Idempotency cache lookup failed; proceeding without idempotency");
            await _next(context);
            return;
        }

        // Cache MISS: buffer the response so we can both forward it and store it.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        finally
        {
            // Always flush the buffered body back to the original stream, even if
            // _next() throws. The common early-throw case (e.g. auth filter) writes
            // nothing to the buffer, so the upstream exception handler receives an
            // empty flush and is unaffected. If _next() wrote a partial body before
            // throwing, that partial content is still forwarded — correct behaviour
            // since the exception will propagate and the error middleware will
            // overwrite the response anyway.
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;
        }

        // Only cache successful (2xx) responses so validation 400s / transient
        // 500s can be retried fresh.
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            try
            {
                buffer.Position = 0;
                var bodyText = await new StreamReader(buffer).ReadToEndAsync();
                var payload = JsonSerializer.Serialize(new CachedResponse
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType,
                    Body = bodyText
                });
                await db.StringSetAsync(cacheKey, payload, Ttl, When.Always, CommandFlags.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Idempotency cache store failed");
            }
        }
    }

    private sealed class CachedResponse
    {
        public int StatusCode { get; set; }
        public string? ContentType { get; set; }
        public string Body { get; set; } = string.Empty;
    }
}
