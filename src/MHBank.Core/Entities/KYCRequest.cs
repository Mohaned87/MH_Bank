using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHBank.Core.Entities
{
    public class KYCRequest
    {
        public Guid Id { get; set; }

        // الحساب المراد توثيقه
        public Guid AccountId { get; set; }
        public virtual BankAccount Account { get; set; } = null!;

        // المستخدم
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        // الحالة
        public KYCStatus Status { get; set; } = KYCStatus.NotStarted;

        // المستندات
        public virtual ICollection<KYCDocument> Documents { get; set; } = new List<KYCDocument>();

        // التواريخ
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        // المراجعة
        public string? ReviewedByAdminId { get; set; }
        public string? ReviewNotes { get; set; }
        public string? RejectionReason { get; set; }

        // النسخ التلقائي
        public Guid? CopiedFromRequestId { get; set; }
        public bool IsAutoVerified { get; set; } // إذا تم النسخ من حساب موثق
    }
    /// <summary>
    /// حالة التوثيق KYC
    /// </summary>
    public enum KYCStatus
    {
        NotStarted = 0,      // لم يبدأ
        DocumentsNeeded = 1, // ينتظر المستندات
        Pending = 2,         // قيد المراجعة
        Approved = 3,        // موافق عليه
        Rejected = 4         // مرفوض
    }
    /// <summary>
    /// نوع المستند
    /// </summary>
    public enum DocumentType
    {
        NationalID = 1,      // هوية وطنية
        Passport = 2,        // جواز سفر
        Selfie = 3          // صورة شخصية
    }

    /// <summary>
    /// المستند المرفق
    /// </summary>
    public class KYCDocument
    {
        public Guid Id { get; set; }

        // الطلب
        public Guid KYCRequestId { get; set; }
        public virtual KYCRequest KYCRequest { get; set; } = null!;

        // نوع المستند
        public DocumentType Type { get; set; }

        // البيانات (Base64)
        public string Base64Data { get; set; } = string.Empty;

        // معلومات إضافية
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public long FileSize { get; set; }

        // التحقق
        public bool IsVerified { get; set; }
        public string? VerificationNotes { get; set; }

        // التاريخ
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
