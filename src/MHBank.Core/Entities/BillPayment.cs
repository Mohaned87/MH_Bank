namespace MHBank.Core.Entities;

/// <summary>
/// دفع الفواتير
/// </summary>
public class BillPayment
{
    public Guid Id { get; set; }

    // الحساب الدافع
    public Guid AccountId { get; set; }
    public virtual BankAccount Account { get; set; } = null!;

    // معلومات الفاتورة
    public BillType BillType { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public string ServiceProvider { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";

    // معلومات الدفع
    public string ReferenceNumber { get; set; } = string.Empty;
    public BillPaymentStatus Status { get; set; }

    // التواريخ
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    // ملاحظات
    public string? Notes { get; set; }
}

/// <summary>
/// أنواع الفواتير
/// </summary>
public enum BillType
{
    Electricity = 1,      // كهرباء
    Water = 2,            // ماء
    Internet = 3,         // إنترنت
    Phone = 4,            // هاتف
    Gas = 5,              // غاز
    Government = 6,       // حكومية
    Other = 7             // أخرى
}

/// <summary>
/// حالة دفع الفاتورة
/// </summary>
public enum BillPaymentStatus
{
    Pending = 1,          // قيد الانتظار
    Completed = 2,        // مكتمل
    Failed = 3,           // فشل
    Refunded = 4          // مُسترجع
}