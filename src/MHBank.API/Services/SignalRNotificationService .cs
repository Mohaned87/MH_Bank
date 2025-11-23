using MHBank.API.Hubs;
using MHBank.Core.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MHBank.API.Services;

/// <summary>
/// خدمة إرسال الإشعارات الفورية عبر SignalR
/// </summary>
public class SignalRNotificationService : ISignalRNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// إرسال إشعار لمستخدم محدد
    /// </summary>
    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type)
    {
        try
        {
            var notification = new
            {
                Title = title,
                Message = message,
                Type = type.ToString(),
                Timestamp = DateTime.UtcNow
            };

            // إرسال للمجموعة الخاصة بالمستخدم
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("📨 Notification sent to user {UserId}: {Title}",
                userId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error sending notification to user {UserId}", userId);
        }
    }

    /// <summary>
    /// إرسال إشعار لمجموعة من المستخدمين
    /// </summary>
    public async Task SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string title,
        string message,
        NotificationType type)
    {
        try
        {
            var notification = new
            {
                Title = title,
                Message = message,
                Type = type.ToString(),
                Timestamp = DateTime.UtcNow
            };

            foreach (var userId in userIds)
            {
                await _hubContext.Clients
                    .Group($"user_{userId}")
                    .SendAsync("ReceiveNotification", notification);
            }

            _logger.LogInformation("📨 Notification sent to {Count} users: {Title}",
                userIds.Count(), title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error sending notification to multiple users");
        }
    }

    /// <summary>
    /// إرسال إشعار KYC
    /// </summary>
    public async Task SendKYCNotificationAsync(
        Guid userId,
        string title,
        string message,
        bool isApproved)
    {
        try
        {
            var notification = new
            {
                Title = title,
                Message = message,
                Type = isApproved ? "KYC_Approved" : "KYC_Rejected",
                IsApproved = isApproved,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveKYCNotification", notification);

            _logger.LogInformation("📨 KYC notification sent to user {UserId}: Approved={IsApproved}",
                userId, isApproved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error sending KYC notification to user {UserId}", userId);
        }
    }
}