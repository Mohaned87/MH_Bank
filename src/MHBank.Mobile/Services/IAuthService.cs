using MHBank.Mobile.Models;

namespace MHBank.Mobile.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string username, string password, string? otp = null);
    Task<bool> IsAuthenticatedAsync();
    Task LogoutAsync();
    Task<User?> GetCurrentUserAsync();
}