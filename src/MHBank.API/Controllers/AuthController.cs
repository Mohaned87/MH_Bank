using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.DTOs;
using MHBank.Core.Entities;
using MHBank.Core.Interfaces;
using MHBank.Infrastructure.Data;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ApplicationDbContext context, IJwtService jwtService, ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// تسجيل مستخدم جديد
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // التحقق من عدم وجود المستخدم
            var exists = await _context.Users.AnyAsync(u =>
                u.Email == request.Email ||
                u.PhoneNumber == request.PhoneNumber);

            if (exists)
            {
                return BadRequest(new { Message = "البريد الإلكتروني أو رقم الهاتف مسجل مسبقاً" });
            }

            // التحقق من صحة رقم الهاتف (07xxxxxxxxx)
            if (!request.PhoneNumber.StartsWith("07") ||
                (request.PhoneNumber.Length != 11 && request.PhoneNumber.Length != 12))
            {
                return BadRequest(new { Message = "رقم الهاتف يجب أن يبدأ بـ 07 ويحتوي على 11 أو 12 رقم" });
            }

            // إنشاء المستخدم
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                DateOfBirth = request.DateOfBirth,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم تسجيل مستخدم جديد: {Phone}", user.PhoneNumber);

            return Ok(new
            {
                Success = true,
                Message = "تم التسجيل بنجاح!",
                UserId = user.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في التسجيل");
            return StatusCode(500, new { Message = "حدث خطأ أثناء التسجيل" });
        }
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

            // تحديث آخر تسجيل دخول
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();


            // إنشاء JWT Token
            _logger.LogInformation("🔑 محاولة إنشاء JWT Token...");
            var accessToken = _jwtService.GenerateAccessToken(user);
            _logger.LogInformation("✅ تم إنشاء Token: {TokenLength} حرف", accessToken?.Length ?? 0);

            _logger.LogInformation("✅ تسجيل دخول ناجح: {Username}", request.Username);

            return Ok(new LoginResponse
            {
                Success = true,
                AccessToken = accessToken,
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
}