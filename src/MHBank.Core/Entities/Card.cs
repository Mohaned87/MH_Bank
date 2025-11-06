namespace MHBank.Core.Entities;

/// <summary>
/// البطاقة المصرفية
/// </summary>
public class Card
{
    public Guid Id { get; set; }

    // معلومات البطاقة
    public string CardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string CVV { get; set; } = string.Empty;

    // نوع البطاقة
    public CardType CardType { get; set; }
    public CardBrand Brand { get; set; }

    // الحساب المربوط
    public Guid AccountId { get; set; }
    public virtual BankAccount Account { get; set; } = null!;

    // حدود البطاقة
    public decimal DailyLimit { get; set; } = 5000;
    public decimal MonthlyLimit { get; set; } = 50000;
    public decimal CurrentDailySpent { get; set; }
    public decimal CurrentMonthlySpent { get; set; }

    // إعدادات الأمان
    public bool IsActive { get; set; } = true;
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public bool ContactlessEnabled { get; set; } = true;
    public bool OnlinePaymentsEnabled { get; set; } = true;
    public bool InternationalPaymentsEnabled { get; set; }

    // PIN
    public string PinHash { get; set; } = string.Empty;
    public int FailedPinAttempts { get; set; }

    // التواريخ
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? BlockedAt { get; set; }
}

/// <summary>
/// أنواع البطاقات
/// </summary>
public enum CardType
{
    Debit = 1,
    Credit = 2
}

/// <summary>
/// العلامات التجارية
/// </summary>
public enum CardBrand
{
    Visa = 1,
    Mastercard = 2
}