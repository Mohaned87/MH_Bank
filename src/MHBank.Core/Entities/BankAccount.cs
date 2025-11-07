namespace MHBank.Core.Entities;

/// <summary>
/// الحساب البنكي
/// </summary>
public class BankAccount
{
    public Guid Id { get; set; }

    // معلومات الحساب
    public string AccountNumber { get; set; } = string.Empty;
    public string IBAN { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }

    // الرصيد
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";

    // حدود التحويل
    public decimal DailyTransferLimit { get; set; } = 10000;
    public decimal MonthlyTransferLimit { get; set; } = 100000;
    public decimal CurrentDailyTransferred { get; set; }
    public decimal CurrentMonthlyTransferred { get; set; }

    // الحالة
    public bool IsActive { get; set; } = true;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTransactionAt { get; set; }

    // صاحب الحساب
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    // العلاقات
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public virtual ICollection<Card> Cards { get; set; } = new List<Card>();
}

/// <summary>
/// أنواع الحسابات
/// </summary>
public enum AccountType
{
    Checking = 1,    // جاري
    Savings = 2      // توفير
}