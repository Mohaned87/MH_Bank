using System.Collections.Concurrent;

namespace MHBank.API.Middleware;

/// <summary>
/// Middleware بسيط لحد الطلبات (Rate Limiting)
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;

    // تخزين عدد الطلبات لكل IP
    private static readonly ConcurrentDictionary<string, RequestInfo> _requests = new();

    // الإعدادات
    private const int MAX_REQUESTS_PER_MINUTE = 60;
    private const int MAX_LOGIN_ATTEMPTS_PER_HOUR = 5;
    private const int CLEANUP_INTERVAL_MINUTES = 5;

    private static DateTime _lastCleanup = DateTime.UtcNow;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = GetClientIpAddress(context);
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // تنظيف البيانات القديمة
        CleanupOldRequests();

        // فحص حد الطلبات
        if (!IsAllowed(ipAddress, path))
        {
            _logger.LogWarning("⚠️ تم حظر IP مؤقتاً بسبب كثرة الطلبات: {IP}", ipAddress);

            context.Response.StatusCode = 429; // Too Many Requests
            await context.Response.WriteAsJsonAsync(new
            {
                Message = "عدد كبير من الطلبات. يرجى المحاولة بعد دقيقة.",
                Error = "RateLimitExceeded",
                RetryAfter = 60
            });
            return;
        }

        // السماح بالمرور
        await _next(context);
    }

    private bool IsAllowed(string ipAddress, string path)
    {
        var now = DateTime.UtcNow;

        // الحصول على أو إنشاء معلومات الطلبات
        var requestInfo = _requests.GetOrAdd(ipAddress, _ => new RequestInfo());

        lock (requestInfo)
        {
            // فحص خاص لمحاولات تسجيل الدخول
            if (path.Contains("/api/auth/login"))
            {
                // حذف المحاولات القديمة (أكثر من ساعة)
                requestInfo.LoginAttempts.RemoveAll(t => (now - t).TotalHours > 1);

                // فحص عدد المحاولات
                if (requestInfo.LoginAttempts.Count >= MAX_LOGIN_ATTEMPTS_PER_HOUR)
                {
                    return false;
                }

                requestInfo.LoginAttempts.Add(now);
            }

            // حذف الطلبات القديمة (أكثر من دقيقة)
            requestInfo.Requests.RemoveAll(t => (now - t).TotalMinutes > 1);

            // فحص عدد الطلبات
            if (requestInfo.Requests.Count >= MAX_REQUESTS_PER_MINUTE)
            {
                return false;
            }

            requestInfo.Requests.Add(now);
            return true;
        }
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // محاولة الحصول على IP الحقيقي (خلف Proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    private void CleanupOldRequests()
    {
        var now = DateTime.UtcNow;

        // تنظيف كل 5 دقائق
        if ((now - _lastCleanup).TotalMinutes < CLEANUP_INTERVAL_MINUTES)
            return;

        _lastCleanup = now;

        // حذف IPs التي لم تُستخدم منذ أكثر من ساعة
        var toRemove = _requests
            .Where(kvp =>
            {
                lock (kvp.Value)
                {
                    return kvp.Value.Requests.Count == 0 &&
                           kvp.Value.LoginAttempts.Count == 0;
                }
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _requests.TryRemove(key, out _);
        }

        _logger.LogInformation("🧹 تنظيف Rate Limit Cache - تم حذف {Count} IP", toRemove.Count);
    }

    private class RequestInfo
    {
        public List<DateTime> Requests { get; set; } = new();
        public List<DateTime> LoginAttempts { get; set; } = new();
    }
}