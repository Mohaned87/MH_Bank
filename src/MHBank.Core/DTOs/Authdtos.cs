namespace MHBank.Core.DTOs;

/// <summary>
/// طلب تسجيل الدخول - يمكن استخدام رقم الهاتف أو البريد
/// </summary>
public record LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Otp { get; init; }  // للمصادقة الثنائية
}

/// <summary>
/// طلب تسجيل مستخدم جديد
/// </summary>
public record RegisterRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public DateTime DateOfBirth { get; init; }
}

/// <summary>
/// استجابة تسجيل الدخول
/// </summary>
public record LoginResponse
{
    public bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? Message { get; init; }
    public UserDto? User { get; init; }
}