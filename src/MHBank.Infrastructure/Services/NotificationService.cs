using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MHBank.Infrastructure.Services;

/// <summary>
/// خدمة إدارة الإشعارات
/// </summary>
public class NotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// إنشاء إشعار جديد
    /// </summary>
    public async Task<Notification> CreateNotificationAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        object? data = null)
    {
        try
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Data = data != null ? JsonSerializer.Serialize(data) : null,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("📬 تم إنشاء إشعار: {Title} للمستخدم: {UserId}", title, userId);

            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إنشاء إشعار");
            throw;
        }
    }

    /// <summary>
    /// إشعار بتحويل مالي
    /// </summary>
    public async Task NotifyTransactionAsync(Guid userId, Transaction transaction, bool isReceiver)
    {
        var title = isReceiver ? "تم استلام تحويل" : "تم إجراء تحويل";
        var message = isReceiver
            ? $"تم استلام {transaction.Amount:N2} {transaction.Currency}"
            : $"تم تحويل {transaction.Amount:N2} {transaction.Currency}";

        await CreateNotificationAsync(
            userId,
            title,
            message,
            NotificationType.Transaction,
            new { TransactionId = transaction.Id, Amount = transaction.Amount }
        );
    }

    /// <summary>
    /// إشعار بتسجيل دخول
    /// </summary>
    public async Task NotifyLoginAsync(Guid userId, string ipAddress, string? deviceInfo = null)
    {
        await CreateNotificationAsync(
            userId,
            "تسجيل دخول جديد",
            $"تم تسجيل الدخول إلى حسابك من {ipAddress}",
            NotificationType.Login,
            new { IpAddress = ipAddress, DeviceInfo = deviceInfo }
        );
    }

    /// <summary>
    /// إشعار بتغيير حالة KYC
    /// </summary>
    public async Task NotifyKycStatusAsync(Guid userId, KycStatus status, string? reason = null)
    {
        var (title, message) = status switch
        {
            KycStatus.Approved => ("تم التحقق من هويتك", "تم قبول طلب التحقق من الهوية بنجاح"),
            KycStatus.Rejected => ("تم رفض طلب التحقق", $"تم رفض طلب التحقق: {reason}"),
            KycStatus.UnderReview => ("طلبك قيد المراجعة", "سيتم مراجعة مستنداتك قريباً"),
            _ => ("تحديث حالة KYC", "تم تحديث حالة التحقق من الهوية")
        };

        await CreateNotificationAsync(userId, title, message, NotificationType.KYC);
    }

    /// <summary>
    /// إشعار بإصدار بطاقة
    /// </summary>
    public async Task NotifyCardIssuedAsync(Guid userId, string cardNumber)
    {
        await CreateNotificationAsync(
            userId,
            "تم إصدار بطاقة جديدة",
            $"تم إصدار بطاقة جديدة: {MaskCardNumber(cardNumber)}",
            NotificationType.Card
        );
    }

    /// <summary>
    /// إشعار بإنشاء حساب
    /// </summary>
    public async Task NotifyAccountCreatedAsync(Guid userId, string accountNumber)
    {
        await CreateNotificationAsync(
            userId,
            "تم إنشاء حساب جديد",
            $"تم إنشاء حسابك المصرفي: {accountNumber}",
            NotificationType.Account
        );
    }

    /// <summary>
    /// إشعار أمني
    /// </summary>
    public async Task NotifySecurityEventAsync(Guid userId, string message)
    {
        await CreateNotificationAsync(
            userId,
            "تنبيه أمني",
            message,
            NotificationType.Security
        );
    }

    private string MaskCardNumber(string cardNumber)
    {
        if (cardNumber.Length < 16)
            return cardNumber;

        return $"{cardNumber.Substring(0, 4)} **** **** {cardNumber.Substring(12)}";
    }
}