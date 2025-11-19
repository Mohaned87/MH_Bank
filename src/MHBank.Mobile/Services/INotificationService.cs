namespace MHBank.Mobile.Services;

public interface INotificationService
{
    Task AddNotificationAsync(string title, string message, string type);
    Task<List<Notification>> GetUnreadNotificationsAsync();
    Task MarkAsReadAsync(int notificationId);
    Task<int> GetUnreadCountAsync();
}

public class NotificationService : INotificationService
{
    private readonly List<Notification> _notifications = new();
    private int _nextId = 1;

    public Task AddNotificationAsync(string title, string message, string type)
    {
        // تجنب التكرار - تحقق إذا كان الإشعار موجود بالفعل
        var exists = _notifications.Any(n =>
            n.Title == title &&
            n.Message == message &&
            n.Type == type &&
            (DateTime.Now - n.CreatedAt).TotalMinutes < 5 // خلال آخر 5 دقائق
        );

        if (!exists)
        {
            var notification = new Notification
            {
                Id = _nextId++,
                Title = title,
                Message = message,
                Type = type,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _notifications.Insert(0, notification);
        }

        return Task.CompletedTask;
    }

    public Task<List<Notification>> GetUnreadNotificationsAsync()
    {
        var unread = _notifications.Where(n => !n.IsRead).ToList();
        return Task.FromResult(unread);
    }

    public Task MarkAsReadAsync(int notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
        }
        return Task.CompletedTask;
    }

    public Task<int> GetUnreadCountAsync()
    {
        var count = _notifications.Count(n => !n.IsRead);
        return Task.FromResult(count);
    }
}

public class Notification
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Transfer, Deposit, BillPayment
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}