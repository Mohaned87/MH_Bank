using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Infrastructure.Data;
using MHBank.Infrastructure.Services;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TwoFactorController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OtpService _otpService;
    private readonly ILogger<TwoFactorController> _logger;

    public TwoFactorController(
        ApplicationDbContext context,
        OtpService otpService,
        ILogger<TwoFactorController> logger)
    {
        _context = context;
        _otpService = otpService;
        _logger = logger;
    }

    /// <summary>
    /// الحصول على حالة 2FA
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
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
                TwoFactorEnabled = user.TwoFactorEnabled,
                PhoneNumber = MaskPhoneNumber(user.PhoneNumber)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب حالة 2FA");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تفعيل 2FA - الخطوة 1: إرسال OTP (بدون مصادقة - للمستخدمين الجدد)
    /// </summary>
    [HttpPost("enable/send-otp-public")]
    [AllowAnonymous]
    public async Task<IActionResult> EnableSendOtpPublic([FromBody] PublicOtpRequest request)
    {
        try
        {
            // البحث عن المستخدم
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

            if (user == null)
                return NotFound(new { Message = "المستخدم غير موجود" });

            // التحقق من كلمة المرور
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized(new { Message = "كلمة المرور غير صحيحة" });

            if (user.TwoFactorEnabled)
                return BadRequest(new { Message = "المصادقة الثنائية مفعّلة بالفعل" });

            // إرسال OTP
            var (success, otp) = await _otpService.GenerateAndSendOtpAsync(user);

            if (!success)
                return StatusCode(500, new { Message = "فشل إرسال رمز التحقق" });

            _logger.LogInformation("📱 تم إرسال OTP لتفعيل 2FA: {Phone}", user.PhoneNumber);

            return Ok(new
            {
                Success = true,
                Message = $"تم إرسال رمز التحقق إلى {MaskPhoneNumber(user.PhoneNumber)}",
                Otp = otp,
                UserId = user.Id,
                ExpiresInMinutes = 5
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إرسال OTP");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تفعيل 2FA - الخطوة 2: التحقق من OTP (بدون مصادقة)
    /// </summary>
    [HttpPost("enable/verify-otp-public")]
    [AllowAnonymous]
    public async Task<IActionResult> EnableVerifyOtpPublic([FromBody] PublicVerifyOtpRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
                return NotFound();

            // التحقق من OTP
            var isValid = await _otpService.VerifyOtpAsync(user, request.Otp);

            if (!isValid)
                return BadRequest(new { Message = "رمز التحقق غير صحيح أو منتهي الصلاحية" });

            // تفعيل 2FA
            user.TwoFactorEnabled = true;
            user.TwoFactorSecret = Guid.NewGuid().ToString();
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم تفعيل 2FA: {UserId}", request.UserId);

            return Ok(new
            {
                Success = true,
                Message = "تم تفعيل المصادقة الثنائية بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تفعيل 2FA");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تفعيل 2FA - الخطوة 1: إرسال OTP
    /// </summary>
    [HttpPost("enable/send-otp")]
    public async Task<IActionResult> EnableSendOtp()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            if (user.TwoFactorEnabled)
                return BadRequest(new { Message = "المصادقة الثنائية مفعّلة بالفعل" });

            // إرسال OTP
            var (success, otp) = await _otpService.GenerateAndSendOtpAsync(user);

            if (!success)
                return StatusCode(500, new { Message = "فشل إرسال رمز التحقق" });

            _logger.LogInformation("📱 تم إرسال OTP لتفعيل 2FA: {Phone}", user.PhoneNumber);

            return Ok(new
            {
                Success = true,
                Message = $"تم إرسال رمز التحقق إلى {MaskPhoneNumber(user.PhoneNumber)}",
                Otp = otp, // ⚠️ للتجربة فقط - في الإنتاج لا نرسله!
                ExpiresInMinutes = 5
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إرسال OTP");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تفعيل 2FA - الخطوة 2: التحقق من OTP
    /// </summary>
    [HttpPost("enable/verify-otp")]
    public async Task<IActionResult> EnableVerifyOtp([FromBody] VerifyOtpRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            // التحقق من OTP
            var isValid = await _otpService.VerifyOtpAsync(user, request.Otp);

            if (!isValid)
                return BadRequest(new { Message = "رمز التحقق غير صحيح أو منتهي الصلاحية" });

            // تفعيل 2FA
            user.TwoFactorEnabled = true;
            user.TwoFactorSecret = Guid.NewGuid().ToString(); // يمكن استخدامه لاحقاً
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم تفعيل 2FA: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تم تفعيل المصادقة الثنائية بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تفعيل 2FA");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تعطيل 2FA
    /// </summary>
    [HttpPost("disable")]
    public async Task<IActionResult> Disable([FromBody] VerifyOtpRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            if (!user.TwoFactorEnabled)
                return BadRequest(new { Message = "المصادقة الثنائية غير مفعّلة" });

            // إرسال OTP للتأكيد
            if (string.IsNullOrEmpty(user.CurrentOtp))
            {
                await _otpService.GenerateAndSendOtpAsync(user);
                return Ok(new
                {
                    RequiresOtp = true,
                    Message = "تم إرسال رمز التحقق. يرجى إعادة المحاولة مع الرمز."
                });
            }

            // التحقق من OTP
            var isValid = await _otpService.VerifyOtpAsync(user, request.Otp);

            if (!isValid)
                return BadRequest(new { Message = "رمز التحقق غير صحيح" });

            // تعطيل 2FA
            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("⚠️ تم تعطيل 2FA: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تم تعطيل المصادقة الثنائية"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تعطيل 2FA");
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

    private string MaskPhoneNumber(string phoneNumber)
    {
        if (phoneNumber.Length < 4)
            return phoneNumber;

        return phoneNumber.Substring(0, 4) + "****" + phoneNumber.Substring(phoneNumber.Length - 2);
    }
}

// ═══════════════════════════════════════════════
// Request Models
// ═══════════════════════════════════════════════

public record VerifyOtpRequest
{
    public string Otp { get; init; } = string.Empty;
}

public record PublicOtpRequest
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record PublicVerifyOtpRequest
{
    public Guid UserId { get; init; }
    public string Otp { get; init; } = string.Empty;
}