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
    public string PhoneNumber { get; set; } = string.Empty;// 07xxxxxxxxx

    // المعلومات الشخصية
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }

    // الحالة
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    // KYC (التحقق من الهوية)
    public KycStatus KycStatus { get; set; } = KycStatus.Pending;
    public string? IdDocumentPath { get; set; }
    public string? SelfiePath { get; set; }
    public DateTime? KycSubmittedAt { get; set; }
    public DateTime? KycVerifiedAt { get; set; }
    public string? KycRejectionReason { get; set; }

    // العلاقات
    public virtual ICollection<BankAccount> Accounts { get; set; } = new List<BankAccount>();

    // Helper
    public string FullName => $"{FirstName} {LastName}";
}
/// <summary>
/// حالات التحقق من الهوية
/// </summary>
public enum KycStatus
{
    Pending = 1,      // قيد الانتظار
    UnderReview = 2,  // قيد المراجعة
    Approved = 3,     // موافق عليه
    Rejected = 4      // مرفوض
}