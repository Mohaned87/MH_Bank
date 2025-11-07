using MHBank.Core.Entities;

namespace MHBank.Core.Interfaces;

/// <summary>
/// خدمة JWT - لإدارة رموز المصادقة
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// إنشاء Access Token
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// إنشاء Refresh Token
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// التحقق من صحة الرمز
    /// </summary>
    bool ValidateToken(string token);

    /// <summary>
    /// الحصول على UserId من الرمز
    /// </summary>
    string? GetUserIdFromToken(string token);
}