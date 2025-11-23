using MHBank.Core.Entities;

namespace MHBank.API.Services;

/// <summary>
/// خدمة إرسال الإشعارات الفورية عبر SignalR
/// </summary>
public interface ISignalRNotificationService
{
    /// <summary>
    /// إرسال إشعار لمستخدم محدد
    /// </summary>
    Task SendToUserAsync(Guid userId, string title, string message, NotificationType type);

    /// <summary>
    /// إرسال إشعار لمجموعة من المستخدمين
    /// </summary>
    Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string message, NotificationType type);

    /// <summary>
    /// إرسال إشعار KYC
    /// </summary>
    Task SendKYCNotificationAsync(Guid userId, string title, string message, bool isApproved);
}