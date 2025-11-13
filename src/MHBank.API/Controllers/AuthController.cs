using MHBank.API.DTOs_;
using MHBank.Core.DTOs;
using MHBank.Core.Entities;
using MHBank.Core.Interfaces;
using MHBank.Infrastructure.Data;
using MHBank.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly OtpService _otpService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        IJwtService jwtService,
        OtpService otpService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _otpService = otpService;
        _logger = logger;
    }

    /// <summary>
    /// تسجيل مستخدم جديد
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        try
        {
            _logger.LogInformation("🔵 Register attempt: {Email}", dto.Email);

            // التحقق من البيانات
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { Success = false, Message = "البريد وكلمة المرور مطلوبان" });
            }

            // التحقق من وجود المستخدم
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email || u.PhoneNumber == dto.PhoneNumber);

            if (existingUser != null)
            {
                return BadRequest(new { Success = false, Message = "المستخدم موجود بالفعل" });
            }

            // إنشاء المستخدم
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TwoFactorEnabled = false
            };

            _context.Users.Add(user);

            // حفظ المستخدم أولاً
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ User saved: {UserId}", userId);

            // توليد Account Number و IBAN
            var random = new Random();
            var accountNumber = random.Next(1000000000, int.MaxValue).ToString();
            var iban = $"IQ{random.Next(10, 99)}MHBK{accountNumber}"; // IBAN عراقي

            // إنشاء الحساب البنكي
            var account = new BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountNumber = accountNumber,
                IBAN = iban, // مهم جداً!
                AccountType = AccountType.Checking,
                Balance = 0,
                Currency = "IQD",
                IsActive = true,
                OpenedAt = DateTime.UtcNow,
                DailyTransferLimit = 10000000,
                MonthlyTransferLimit = 50000000,
                CurrentDailyTransferred = 0,
                CurrentMonthlyTransferred = 0
            };

            _context.BankAccounts.Add(account);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Account saved: {AccountNumber}, IBAN: {IBAN}", accountNumber, iban);
            _logger.LogInformation("✅ Registration complete: {Email}", dto.Email);

            return Ok(new
            {
                Success = true,
                Message = "تم إنشاء الحساب بنجاح",
                User = new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.PhoneNumber
                }
            });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "❌ Database error");
            _logger.LogError("❌ Inner Exception: {Inner}", dbEx.InnerException?.Message);

            return StatusCode(500, new
            {
                Success = false,
                Message = "خطأ في قاعدة البيانات",
                Details = dbEx.InnerException?.Message ?? dbEx.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تسجيل المستخدم");

            return StatusCode(500, new
            {
                Success = false,
                Message = "حدث خطأ أثناء التسجيل",
                Details = ex.InnerException?.Message ?? ex.Message
            });
        }
    }
    private string GenerateAccountNumber()
    {
        var random = new Random();
        return random.Next(1000000000, int.MaxValue).ToString();
    }


    /// <summary>
    /// تسجيل الدخول - يمكن استخدام رقم الهاتف أو البريد
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            // البحث عن المستخدم (رقم الهاتف أو البريد)
            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.PhoneNumber == request.Username ||
                    u.Email == request.Username);

            if (user == null)
            {
                return Unauthorized(new { Message = "بيانات الدخول غير صحيحة" });
            }

            // التحقق من كلمة المرور
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { Message = "بيانات الدخول غير صحيحة" });
            }

            // التحقق من 2FA
            if (user.TwoFactorEnabled)
            {
                // إذا لم يتم إرسال OTP بعد
                if (string.IsNullOrEmpty(request.Otp))
                {
                    // إرسال OTP
                    var (success, otp) = await _otpService.GenerateAndSendOtpAsync(user);

                    _logger.LogInformation("📱 تم إرسال OTP للمستخدم: {Phone}", user.PhoneNumber);

                    return Ok(new
                    {
                        RequiresTwoFactor = true,
                        Message = "تم إرسال رمز التحقق إلى هاتفك",
                        Otp = otp, // ⚠️ للتجربة فقط!
                        ExpiresInMinutes = 5
                    });
                }

                // التحقق من OTP
                var isOtpValid = await _otpService.VerifyOtpAsync(user, request.Otp);
                if (!isOtpValid)
                {
                    return Unauthorized(new { Message = "رمز التحقق غير صحيح أو منتهي الصلاحية" });
                }

                _logger.LogInformation("✅ تم التحقق من OTP بنجاح");
            }

            // تحديث آخر تسجيل دخول
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // إنشاء JWT Token
            _logger.LogInformation("🔑 محاولة إنشاء JWT Token...");
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();
            _logger.LogInformation("✅ تم إنشاء Token: {TokenLength} حرف", accessToken?.Length ?? 0);

            // حفظ Refresh Token
            var refreshTokenEntity = new Core.Entities.RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshToken,
                UserId = user.Id,
                DeviceId = request.Username, // مؤقتاً
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تسجيل دخول ناجح: {Username}", request.Username);

            return Ok(new LoginResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Message = "تم تسجيل الدخول بنجاح",
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    CreatedAt = user.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تسجيل الدخول");
            return StatusCode(500, new { Message = "حدث خطأ أثناء تسجيل الدخول" });
        }
    }

    /// <summary>
    /// اختبار الاتصال
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new
        {
            Message = "🏦 MH-Bank API is working!",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// تجديد Access Token باستخدام Refresh Token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            // البحث عن Refresh Token
            var refreshToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (refreshToken == null)
                return Unauthorized(new { Message = "Refresh Token غير صحيح" });

            // التحقق من صلاحية الرمز
            if (!refreshToken.IsActive)
            {
                return Unauthorized(new
                {
                    Message = refreshToken.IsRevoked ? "تم إبطال الرمز" :
                              refreshToken.IsUsed ? "تم استخدام الرمز مسبقاً" :
                              "الرمز منتهي الصلاحية"
                });
            }

            // وضع علامة استخدام على الرمز القديم
            refreshToken.IsUsed = true;
            refreshToken.UsedAt = DateTime.UtcNow;

            // إنشاء رموز جديدة
            var newAccessToken = _jwtService.GenerateAccessToken(refreshToken.User);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            // حفظ Refresh Token الجديد
            var newRefreshTokenEntity = new Core.Entities.RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = newRefreshToken,
                UserId = refreshToken.UserId,
                DeviceId = refreshToken.DeviceId,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(newRefreshTokenEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم تجديد الرموز: {UserId}", refreshToken.UserId);

            return Ok(new
            {
                Success = true,
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                Message = "تم تجديد الرموز بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تجديد الرمز");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إبطال Refresh Token (Logout)
    /// </summary>
    [HttpPost("revoke")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (refreshToken == null)
                return NotFound(new { Message = "الرمز غير موجود" });

            // التحقق من الملكية
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (refreshToken.UserId.ToString() != userId)
                return Forbid();

            // إبطال الرمز
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم إبطال Refresh Token: {UserId}", userId);

            return Ok(new
            {
                Success = true,
                Message = "تم تسجيل الخروج بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إبطال الرمز");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على معلومات المستخدم الحالي (محمي بـ JWT)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation("🔵 /me called. UserId: {UserId}", userId);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("❌ No userId in token");
                return Unauthorized();
            }

            var user = await _context.Users
                .Where(u => u.Id == Guid.Parse(userId))
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("❌ User not found: {UserId}", userId);
                return NotFound(new { Success = false, Message = "المستخدم غير موجود" });
            }

            _logger.LogInformation("✅ User found: {Email}", user.Email);

            // المهم: يجب أن يكون الـ response بهذا الشكل
            return Ok(new
            {
                Success = true,  // ← مهم!
                Data = new      // ← مهم!
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    FullName = user.FirstName + " " + user.LastName,
                    user.PhoneNumber,
                    user.CreatedAt,
                    user.TwoFactorEnabled,
                    user.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in /me");
            return StatusCode(500, new { Success = false, Message = "خطأ" });
        }
    }
}

// ═══════════════════════════════════════════════
// Request/Response Models
// ═══════════════════════════════════════════════
public record RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}