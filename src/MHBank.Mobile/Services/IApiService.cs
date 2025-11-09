using MHBank.Mobile.Models;

namespace MHBank.Mobile.Services;

public interface IApiService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<RegisterResponse?> RegisterAsync(RegisterRequest request);
    Task<AccountsResponse?> GetAccountsAsync();
    Task<TransactionsResponse?> GetRecentTransactionsAsync();
    Task<User?> GetCurrentUserAsync();
}
