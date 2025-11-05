using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHBank.Core.Entities;
/// <summary>
/// المستخدم - العميل في البنك
/// </summary>

public class User
{
    public Guid Id { get; set; }

    // معلومات تسجيل الدخول
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    // المعلومات الشخصية
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }

    // الحالة
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // العلاقات
    public virtual ICollection<BankAccount> Accounts { get; set; } = new List<BankAccount>();

    // Helper
    public string FullName => $"{FirstName} {LastName}";
}
