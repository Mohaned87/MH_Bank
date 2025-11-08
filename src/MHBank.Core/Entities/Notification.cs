namespace MHBank.Core.Entities;

/// <summary>
/// الإشعارات
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    // المستخدم
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    // محتوى الإشعار
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }

    // البيانات الإضافية (JSON)
    public string? Data { get; set; }

    // الحالة
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// أنواع الإشعارات
/// </summary>
public enum NotificationType
{
    Transaction = 1,      // معاملة مالية
    Login = 2,            // تسجيل دخول
    Security = 3,         // أمان
    KYC = 4,              // التحقق من الهوية
    Card = 5,             // بطاقة
    Account = 6,          // حساب
    General = 7           // عام
}