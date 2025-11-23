using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using MHBank.API.Services;
using System.Security.Claims;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/Admin/KYC")]
[Authorize(Roles = "Admin")]
public class AdminKYCController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminKYCController> _logger;
    private readonly ISignalRNotificationService _signalRNotificationService;

    public AdminKYCController(
        ApplicationDbContext context,
        ILogger<AdminKYCController> logger,
        ISignalRNotificationService signalRNotificationService)
    {
        _context = context;
        _logger = logger;
        _signalRNotificationService = signalRNotificationService;
    }

    private string? GetCurrentAdminId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// قائمة الطلبات المعلقة (Pending)
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.KYCRequests
                .Include(k => k.Account)
                .Include(k => k.User)
                .Include(k => k.Documents)
                .Where(k => k.Status == KYCStatus.Pending)
                .OrderBy(k => k.SubmittedAt);

            var total = await query.CountAsync();

            var requests = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(k => new
                {
                    k.Id,
                    k.Status,
                    k.CreatedAt,
                    k.SubmittedAt,
                    k.IsAutoVerified,
                    Account = new
                    {
                        k.Account.Id,
                        k.Account.AccountNumber,
                        k.Account.AccountType
                    },
                    User = new
                    {
                        k.User.Id,
                        k.User.Email,
                        k.User.PhoneNumber,
                        k.User.FirstName,
                        k.User.LastName,
                        k.User.DateOfBirth,
                        k.User.Address
                    },
                    DocumentsCount = k.Documents.Count,
                    HasSelfie = k.Documents.Any(d => d.Type == DocumentType.Selfie),
                    HasID = k.Documents.Any(d =>
                        d.Type == DocumentType.NationalID || d.Type == DocumentType.Passport)
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Requests = requests
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting pending KYC requests");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// قائمة جميع الطلبات (للإحصائيات)
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllRequests(
        [FromQuery] KYCStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.KYCRequests
                .Include(k => k.Account)
                .Include(k => k.User)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(k => k.Status == status.Value);

            var total = await query.CountAsync();

            var requests = await query
                .OrderByDescending(k => k.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(k => new
                {
                    k.Id,
                    k.Status,
                    k.CreatedAt,
                    k.SubmittedAt,
                    k.ReviewedAt,
                    k.ReviewedByAdminId,
                    k.RejectionReason,
                    Account = new
                    {
                        k.Account.AccountNumber,
                        k.Account.AccountType
                    },
                    User = new
                    {
                        k.User.Email,
                        FullName = k.User.FirstName + " " + k.User.LastName
                    }
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Page = page,
                PageSize = pageSize,
                Total = total,
                Requests = requests
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting all KYC requests");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تفاصيل طلب محدد (مع الصور)
    /// </summary>
    [HttpGet("{requestId}")]
    public async Task<IActionResult> GetRequestDetails(Guid requestId)
    {
        try
        {
            var request = await _context.KYCRequests
                .Include(k => k.Account)
                .Include(k => k.User)
                .Include(k => k.Documents)
                .FirstOrDefaultAsync(k => k.Id == requestId);

            if (request == null)
                return NotFound(new { Success = false, Message = "الطلب غير موجود" });

            return Ok(new
            {
                Success = true,
                Request = new
                {
                    request.Id,
                    request.Status,
                    request.CreatedAt,
                    request.SubmittedAt,
                    request.ReviewedAt,
                    request.ReviewedByAdminId,
                    request.ReviewNotes,
                    request.RejectionReason,
                    request.IsAutoVerified,
                    request.CopiedFromRequestId,
                    Account = new
                    {
                        request.Account.Id,
                        request.Account.AccountNumber,
                        request.Account.AccountType,
                        request.Account.Balance,
                        request.Account.Currency,
                        request.Account.OpenedAt
                    },
                    User = new
                    {
                        request.User.Id,
                        request.User.Email,
                        request.User.PhoneNumber,
                        request.User.FirstName,
                        request.User.LastName,
                        request.User.DateOfBirth,
                        request.User.Address,
                        request.User.City,
                        request.User.Country,
                        request.User.PostalCode
                    },
                    Documents = request.Documents.Select(d => new
                    {
                        d.Id,
                        d.Type,
                        d.FileName,
                        d.MimeType,
                        d.FileSize,
                        d.UploadedAt,
                        d.IsVerified,
                        d.VerificationNotes,
                        // الصورة بصيغة Base64
                        d.Base64Data
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting KYC request details");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// موافقة على الطلب
    /// </summary>
    [HttpPost("{requestId}/approve")]
    public async Task<IActionResult> ApproveRequest(
        Guid requestId,
        [FromBody] ReviewRequest? reviewRequest = null)
    {
        try
        {
            var adminId = GetCurrentAdminId();
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized();

            var request = await _context.KYCRequests
                .Include(k => k.Account)
                .Include(k => k.User)
                .FirstOrDefaultAsync(k => k.Id == requestId);

            if (request == null)
                return NotFound(new { Success = false, Message = "الطلب غير موجود" });

            if (request.Status != KYCStatus.Pending)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "يمكن الموافقة فقط على الطلبات المعلقة"
                });
            }

            // تحديث حالة الطلب
            request.Status = KYCStatus.Approved;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByAdminId = adminId;
            request.ReviewNotes = reviewRequest?.Notes;

            // تحديث حالة الحساب
            request.Account.KYCStatus = KYCStatus.Approved;
            request.Account.KYCApprovedAt = DateTime.UtcNow;

            // إنشاء إشعار للمستخدم
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Title = "تم الموافقة على طلب التوثيق ✅",
                Message = $"تم الموافقة على طلب توثيق الحساب {request.Account.AccountNumber}. حسابك الآن موثق بالكامل!",
                Type = NotificationType.KYC_Approved,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تمت الموافقة على طلب KYC: {RequestId} بواسطة {AdminId}",
                requestId, adminId);

            // إرسال إشعار SignalR للمستخدم
            await _signalRNotificationService.SendKYCNotificationAsync(
                request.UserId,
                "تم الموافقة على طلب التوثيق ✅",
                $"تم الموافقة على طلب توثيق الحساب {request.Account.AccountNumber}. حسابك الآن موثق بالكامل!",
                isApproved: true
            );

            return Ok(new
            {
                Success = true,
                Message = "تمت الموافقة على الطلب بنجاح",
                Request = new
                {
                    request.Id,
                    request.Status,
                    request.ReviewedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error approving KYC request");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// رفض الطلب
    /// </summary>
    [HttpPost("{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(
        Guid requestId,
        [FromBody] RejectRequest rejectRequest)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rejectRequest.Reason))
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "يجب تحديد سبب الرفض"
                });
            }

            var adminId = GetCurrentAdminId();
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized();

            var request = await _context.KYCRequests
                .Include(k => k.Account)
                .Include(k => k.User)
                .FirstOrDefaultAsync(k => k.Id == requestId);

            if (request == null)
                return NotFound(new { Success = false, Message = "الطلب غير موجود" });

            if (request.Status != KYCStatus.Pending)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "يمكن الرفض فقط على الطلبات المعلقة"
                });
            }

            // تحديث حالة الطلب
            request.Status = KYCStatus.Rejected;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByAdminId = adminId;
            request.RejectionReason = rejectRequest.Reason;
            request.ReviewNotes = rejectRequest.Notes;

            // تحديث حالة الحساب
            request.Account.KYCStatus = KYCStatus.Rejected;

            // إنشاء إشعار للمستخدم
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Title = "تم رفض طلب التوثيق ❌",
                Message = $"تم رفض طلب توثيق الحساب {request.Account.AccountNumber}.\n\nالسبب: {rejectRequest.Reason}\n\nيمكنك تقديم طلب جديد بعد تصحيح المعلومات.",
                Type = NotificationType.KYC_Rejected,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            _logger.LogWarning("⚠️ تم رفض طلب KYC: {RequestId} بواسطة {AdminId}. السبب: {Reason}",
                requestId, adminId, rejectRequest.Reason);

            // إرسال إشعار SignalR للمستخدم
            await _signalRNotificationService.SendKYCNotificationAsync(
                request.UserId,
                "تم رفض طلب التوثيق ❌",
                $"تم رفض طلب توثيق الحساب {request.Account.AccountNumber}.\n\nالسبب: {rejectRequest.Reason}\n\nيمكنك تقديم طلب جديد بعد تصحيح المعلومات.",
                isApproved: false
            );

            return Ok(new
            {
                Success = true,
                Message = "تم رفض الطلب",
                Request = new
                {
                    request.Id,
                    request.Status,
                    request.ReviewedAt,
                    request.RejectionReason
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error rejecting KYC request");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إحصائيات KYC
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var total = await _context.KYCRequests.CountAsync();
            var pending = await _context.KYCRequests.CountAsync(k => k.Status == KYCStatus.Pending);
            var approved = await _context.KYCRequests.CountAsync(k => k.Status == KYCStatus.Approved);
            var rejected = await _context.KYCRequests.CountAsync(k => k.Status == KYCStatus.Rejected);
            var notStarted = await _context.KYCRequests.CountAsync(k => k.Status == KYCStatus.NotStarted);

            // آخر 7 أيام
            var last7Days = DateTime.UtcNow.AddDays(-7);
            var recentSubmissions = await _context.KYCRequests
                .Where(k => k.SubmittedAt >= last7Days)
                .CountAsync();

            return Ok(new
            {
                Success = true,
                Statistics = new
                {
                    Total = total,
                    Pending = pending,
                    Approved = approved,
                    Rejected = rejected,
                    NotStarted = notStarted,
                    RecentSubmissions = recentSubmissions,
                    ApprovalRate = total > 0 ? Math.Round((double)approved / total * 100, 2) : 0
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting KYC statistics");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
        }
    }
}

// ═══════════════════════════════════════════
// Request DTOs
// ═══════════════════════════════════════════

public record ReviewRequest
{
    public string? Notes { get; init; }
}

public record RejectRequest
{
    public string Reason { get; init; } = string.Empty;
    public string? Notes { get; init; }
}