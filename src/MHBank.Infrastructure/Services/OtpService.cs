using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MHBank.Infrastructure.Services;

/// <summary>
/// خدمة إدارة OTP (One-Time Password)
/// </summary>
public class OtpService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OtpService> _logger;

    private const int OTP_LENGTH = 6;
    private const int OTP_VALIDITY_MINUTES = 5;

    public OtpService(ApplicationDbContext context, ILogger<OtpService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// إنشاء وإرسال OTP
    /// </summary>
    public async Task<(bool Success, string? Otp)> GenerateAndSendOtpAsync(User user)
    {
        try
        {
            // إنشاء OTP عشوائي (6 أرقام)
            var otp = GenerateOtp();

            // حفظ OTP في قاعدة البيانات
            user.CurrentOtp = otp;
            user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(OTP_VALIDITY_MINUTES);
            await _context.SaveChangesAsync();

            // محاكاة إرسال SMS
            await SendOtpViaSmsAsync(user.PhoneNumber, otp);

            _logger.LogInformation("✅ تم إنشاء OTP للمستخدم: {Phone}", user.PhoneNumber);

            // في الواقع، لا نعيد OTP! لكن للتجربة نعيده
            return (true, otp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إنشاء OTP");
            return (false, null);
        }
    }

    /// <summary>
    /// التحقق من OTP
    /// </summary>
    public async Task<bool> VerifyOtpAsync(User user, string otp)
    {
        try
        {
            // التحقق من وجود OTP
            if (string.IsNullOrEmpty(user.CurrentOtp))
            {
                _logger.LogWarning("⚠️ لا يوجد OTP للمستخدم: {Phone}", user.PhoneNumber);
                return false;
            }

            // التحقق من انتهاء الصلاحية
            if (user.OtpExpiresAt == null || user.OtpExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("⚠️ OTP منتهي الصلاحية: {Phone}", user.PhoneNumber);
                user.CurrentOtp = null;
                user.OtpExpiresAt = null;
                await _context.SaveChangesAsync();
                return false;
            }

            // التحقق من تطابق OTP
            if (user.CurrentOtp != otp)
            {
                _logger.LogWarning("⚠️ OTP غير صحيح: {Phone}", user.PhoneNumber);
                return false;
            }

            // OTP صحيح - حذفه
            user.CurrentOtp = null;
            user.OtpExpiresAt = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم التحقق من OTP بنجاح: {Phone}", user.PhoneNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في التحقق من OTP");
            return false;
        }
    }

    /// <summary>
    /// إلغاء OTP
    /// </summary>
    public async Task CancelOtpAsync(User user)
    {
        user.CurrentOtp = null;
        user.OtpExpiresAt = null;
        await _context.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════

    private string GenerateOtp()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    private Task SendOtpViaSmsAsync(string phoneNumber, string otp)
    {
        // محاكاة إرسال SMS
        // في الواقع، هنا نستخدم خدمة SMS مثل Twilio أو AWS SNS

        _logger.LogInformation(
            "📱 [SMS Simulation] إرسال OTP إلى {Phone}: {Otp}",
            phoneNumber,
            otp
        );

        // في الإنتاج:
        // await _smsService.SendAsync(phoneNumber, $"كود التحقق الخاص بك: {otp}");

        return Task.CompletedTask;
    }
}