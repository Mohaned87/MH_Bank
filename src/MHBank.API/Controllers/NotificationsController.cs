using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Infrastructure.Data;
using MHBank.Infrastructure.Services;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        ApplicationDbContext context,
        NotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// الحصول على جميع الإشعارات
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? unreadOnly = false)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var query = _context.Notifications
                .Where(n => n.UserId == userId.Value);

            if (unreadOnly == true)
            {
                query = query.Where(n => !n.IsRead);
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .CountAsync();

            return Ok(new
            {
                Success = true,
                UnreadCount = unreadCount,
                TotalCount = notifications.Count,
                Notifications = notifications.Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    Type = n.Type.ToString(),
                    n.IsRead,
                    n.CreatedAt,
                    n.ReadAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب الإشعارات");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على عدد الإشعارات غير المقروءة
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var count = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .CountAsync();

            return Ok(new
            {
                UnreadCount = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب عدد الإشعارات");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// وضع علامة مقروء على إشعار واحد
    /// </summary>
    [HttpPatch("{id}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);

            if (notification == null)
                return NotFound(new { Message = "الإشعار غير موجود" });

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                Success = true,
                Message = "تم وضع علامة مقروء"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث الإشعار");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// وضع علامة مقروء على جميع الإشعارات
    /// </summary>
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم وضع علامة مقروء على {Count} إشعار", unreadNotifications.Count);

            return Ok(new
            {
                Success = true,
                Message = $"تم وضع علامة مقروء على {unreadNotifications.Count} إشعار",
                Count = unreadNotifications.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث الإشعارات");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// حذف إشعار
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);

            if (notification == null)
                return NotFound(new { Message = "الإشعار غير موجود" });

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = "تم حذف الإشعار"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في حذف الإشعار");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// حذف جميع الإشعارات المقروءة
    /// </summary>
    [HttpDelete("clear-read")]
    public async Task<IActionResult> ClearRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var readNotifications = await _context.Notifications
                .Where(n => n.UserId == userId.Value && n.IsRead)
                .ToListAsync();

            _context.Notifications.RemoveRange(readNotifications);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم حذف {Count} إشعار مقروء", readNotifications.Count);

            return Ok(new
            {
                Success = true,
                Message = $"تم حذف {readNotifications.Count} إشعار",
                Count = readNotifications.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في حذف الإشعارات");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إنشاء إشعار تجريبي (للاختبار)
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> CreateTestNotification()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            await _notificationService.CreateNotificationAsync(
                userId.Value,
                "إشعار تجريبي",
                "هذا إشعار تجريبي لاختبار النظام",
                Core.Entities.NotificationType.General
            );

            return Ok(new
            {
                Success = true,
                Message = "تم إنشاء إشعار تجريبي"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إنشاء إشعار تجريبي");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    // ═══════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrEmpty(userIdClaim) ? null : Guid.Parse(userIdClaim);
    }
}