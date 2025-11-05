using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace MHBank.Core.Entities;

/// <summary>
/// المعاملة المالية
/// </summary>
public class Transaction
{
    public Guid Id { get; set; }

    // معلومات المعاملة
    public string ReferenceNumber { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }

    // المبلغ
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = string.Empty;

    // الحساب
    public Guid AccountId { get; set; }
    public virtual BankAccount Account { get; set; } = null!;

    // التوقيت
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
/// <summary>
/// أنواع المعاملات
/// </summary>
public enum TransactionType
{
    Deposit = 1,     // إيداع
    Withdrawal = 2,  // سحب
    Transfer = 3     // تحويل
}

/// <summary>
/// حالات المعاملة
/// </summary>
public enum TransactionStatus
{
    Pending = 1,     // قيد الانتظار
    Completed = 2,   // مكتملة
    Failed = 3       // فشلت
}