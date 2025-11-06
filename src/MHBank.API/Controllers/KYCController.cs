using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KYCController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<KYCController> _logger;
    private readonly IWebHostEnvironment _environment;

    public KYCController(
        ApplicationDbContext context,
        ILogger<KYCController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// الحصول على حالة KYC الحالية
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetKYCStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            return Ok(new
            {
                Status = user.KycStatus.ToString(),
                IdDocumentUploaded = !string.IsNullOrEmpty(user.IdDocumentPath),
                SelfieUploaded = !string.IsNullOrEmpty(user.SelfiePath),
                SubmittedAt = user.KycSubmittedAt,
                VerifiedAt = user.KycVerifiedAt,
                RejectionReason = user.KycRejectionReason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب حالة KYC");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// رفع صورة الهوية
    /// </summary>
    [HttpPost("upload-id")]
    public async Task<IActionResult> UploadIdDocument(IFormFile file)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "الملف مطلوب" });

            // التحقق من نوع الملف
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { Message = "نوع الملف غير مدعوم. استخدم: jpg, png, pdf" });

            // التحقق من حجم الملف (5 MB max)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { Message = "حجم الملف كبير جداً. الحد الأقصى: 5 MB" });

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            // حفظ الملف
            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "uploads", "kyc", userId.Value.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"id_document_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // تحديث المستخدم
            user.IdDocumentPath = filePath;

            if (user.KycStatus == KycStatus.Pending)
            {
                user.KycStatus = KycStatus.UnderReview;
                user.KycSubmittedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم رفع صورة الهوية: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تم رفع صورة الهوية بنجاح",
                FileName = fileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في رفع صورة الهوية");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// رفع صورة شخصية (Selfie)
    /// </summary>
    [HttpPost("upload-selfie")]
    public async Task<IActionResult> UploadSelfie(IFormFile file)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "الملف مطلوب" });

            // التحقق من نوع الملف (صور فقط)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { Message = "نوع الملف غير مدعوم. استخدم: jpg, png" });

            // التحقق من حجم الملف (3 MB max)
            if (file.Length > 3 * 1024 * 1024)
                return BadRequest(new { Message = "حجم الملف كبير جداً. الحد الأقصى: 3 MB" });

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            // حفظ الملف
            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "uploads", "kyc", userId.Value.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"selfie_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // تحديث المستخدم
            user.SelfiePath = filePath;

            if (user.KycStatus == KycStatus.Pending)
            {
                user.KycStatus = KycStatus.UnderReview;
                user.KycSubmittedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم رفع الصورة الشخصية: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تم رفع الصورة الشخصية بنجاح",
                FileName = fileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في رفع الصورة الشخصية");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إرسال طلب KYC للمراجعة (بعد رفع جميع المستندات)
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitKYC()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            // التحقق من رفع جميع المستندات
            if (string.IsNullOrEmpty(user.IdDocumentPath))
                return BadRequest(new { Message = "يرجى رفع صورة الهوية أولاً" });

            if (string.IsNullOrEmpty(user.SelfiePath))
                return BadRequest(new { Message = "يرجى رفع صورة شخصية أولاً" });

            // تحديث الحالة
            user.KycStatus = KycStatus.UnderReview;
            user.KycSubmittedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم إرسال طلب KYC للمراجعة: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تم إرسال طلبك للمراجعة. سيتم إشعارك بالنتيجة قريباً.",
                Status = "UnderReview"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إرسال طلب KYC");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    // ═══════════════════════════════════════════════
    // Admin Endpoints (للموظفين فقط)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// الموافقة على KYC (للموظفين فقط)
    /// </summary>
    [HttpPost("{userId}/approve")]
    public async Task<IActionResult> ApproveKYC(Guid userId)
    {
        try
        {
            // TODO: إضافة فحص صلاحيات الموظف هنا

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.KycStatus = KycStatus.Approved;
            user.KycVerifiedAt = DateTime.UtcNow;
            user.KycRejectionReason = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تمت الموافقة على KYC: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تمت الموافقة على الطلب"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في الموافقة على KYC");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// رفض KYC (للموظفين فقط)
    /// </summary>
    [HttpPost("{userId}/reject")]
    public async Task<IActionResult> RejectKYC(Guid userId, [FromBody] RejectKYCRequest request)
    {
        try
        {
            // TODO: إضافة فحص صلاحيات الموظف هنا

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.KycStatus = KycStatus.Rejected;
            user.KycRejectionReason = request.Reason;
            await _context.SaveChangesAsync();

            _logger.LogWarning("⚠️ تم رفض KYC: {UserId} - السبب: {Reason}", userId, request.Reason);

            return Ok(new
            {
                Success = true,
                Message = "تم رفض الطلب"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في رفض KYC");
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

// ═══════════════════════════════════════════════
// Request Models
// ═══════════════════════════════════════════════

public record RejectKYCRequest
{
    public string Reason { get; init; } = string.Empty;
}