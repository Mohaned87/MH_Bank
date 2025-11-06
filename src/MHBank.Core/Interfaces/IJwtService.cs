using MHBank.Core.Entities;



namespace MHBank.Core.Interfaces;

public interface IJwtService
{
    /// <summary>
    /// إنشاء Access Token
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// التحقق من صحة الرمز
    /// </summary>
    bool ValidateToken(string token);

    /// <summary>
    /// الحصول على UserId من الرمز
    /// </summary>
    string? GetUserIdFromToken(string token);
}
