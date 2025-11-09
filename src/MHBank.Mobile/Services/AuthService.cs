using MHBank.Mobile.Models;

namespace MHBank.Mobile.Services;

public class AuthService : IAuthService
{
    private readonly IApiService _apiService;
    private readonly IStorageService _storageService;

    public AuthService(IApiService apiService, IStorageService storageService)
    {
        _apiService = apiService;
        _storageService = storageService;
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password, string? otp = null)
    {
        var request = new LoginRequest
        {
            Username = username,
            Password = password,
            Otp = otp
        };

        var response = await _apiService.LoginAsync(request);
        return response;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _storageService.GetAsync("access_token");
        return !string.IsNullOrEmpty(token);
    }

    public async Task LogoutAsync()
    {
        await _storageService.RemoveAsync("access_token");
        await _storageService.RemoveAsync("refresh_token");
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        return await _apiService.GetCurrentUserAsync();
    }
}