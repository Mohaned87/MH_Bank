using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

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

    // الحالة
    public bool IsActive { get; set; } = true;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    // صاحب الحساب
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    // العلاقات
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
/// <summary>
/// أنواع الحسابات
/// </summary>
public enum AccountType
{
    Checking = 1,    // جاري
    Savings = 2      // توفير
}