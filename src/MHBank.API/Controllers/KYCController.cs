using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KYCController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<KYCController> _logger;

    public KYCController(ApplicationDbContext context, ILogger<KYCController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitKYC()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            _logger.LogInformation("✅ تم رفع مستندات KYC للمستخدم: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تم رفع المستندات بنجاح. سيتم المراجعة خلال 24-48 ساعة.",
                Status = "Pending"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في رفع مستندات KYC");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetKYCStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // محاكاة: تحقق إذا تمت الموافقة من Admin
            // في الواقع يجب حفظ الحالة في قاعدة البيانات
            return Ok(new
            {
                Success = true,
                Status = "Verified", // أو "Pending" أو "Rejected"
                Message = "تم قبول طلب التوثيق ✅",
                ApprovedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب حالة KYC");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على إشعارات KYC للمستخدم
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetKYCNotifications()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // جلب الإشعارات الحقيقية من قاعدة البيانات
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId.Value && n.Type == NotificationType.KYC)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    Type = n.Type.ToString(),
                    n.CreatedAt,
                    n.IsRead
                })
                .ToListAsync();

            _logger.LogInformation("📬 إرسال {Count} إشعار KYC للمستخدم: {UserId}", notifications.Count, userId);

            return Ok(new
            {
                Success = true,
                Notifications = notifications
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب إشعارات KYC");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrEmpty(userIdClaim) ? null : Guid.Parse(userIdClaim);
    }

    /// <summary>
    /// موافقة الأدمن على طلب KYC (للإختبار - استخدم من Swagger)
    /// </summary>
    [HttpPost("admin/approve/{userId}")]
    [AllowAnonymous] // للإختبار فقط - في الواقع يحتاج [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminApproveKYC(Guid userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { Success = false, Message = "المستخدم غير موجود" });

            // إنشاء إشعار للمستخدم
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "توثيق الهوية ✅",
                Message = "تمت الموافقة على طلب توثيق الهوية (KYC). يمكنك الآن استخدام جميع خدمات البنك.",
                Type = NotificationType.KYC,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تمت الموافقة على KYC للمستخدم: {UserId} - {Email}", userId, user.Email);

            return Ok(new
            {
                Success = true,
                Message = $"✅ تمت الموافقة على KYC للمستخدم {user.Email}",
                UserId = userId,
                Email = user.Email,
                NotificationSent = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في الموافقة على KYC");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// رفض طلب KYC
    /// </summary>
    [HttpPost("admin/reject/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminRejectKYC(Guid userId, [FromBody] RejectKYCRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { Success = false, Message = "المستخدم غير موجود" });

            _logger.LogInformation("❌ تم رفض KYC للمستخدم: {UserId}. السبب: {Reason}", userId, request.Reason);

            return Ok(new
            {
                Success = true,
                Message = $"تم رفض KYC للمستخدم {user.Email}",
                Reason = request.Reason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في رفض KYC");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على جميع طلبات KYC (للأدمن)
    /// </summary>
    [HttpGet("admin/pending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPendingKYCRequests()
    {
        try
        {
            // في الواقع يجب جلب الطلبات من جدول KYC
            // لكن للبساطة نرجع جميع المستخدمين
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.PhoneNumber,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Count = users.Count,
                Requests = users
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب طلبات KYC");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }
}

public record RejectKYCRequest
{
    public string Reason { get; init; } = string.Empty;
}