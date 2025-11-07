using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHBank.Core.Entities;

/// <summary>
/// رمز التجديد (Refresh Token)
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    // الرمز
    public string Token { get; set; } = string.Empty;

    // صاحب الرمز
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    // معلومات الجهاز
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }

    // الصلاحية
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    // Helper
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsUsed && !IsExpired;
}