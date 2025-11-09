using MHBank.Mobile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHBank.Mobile.Services
{
    public interface IApiService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<AccountsResponse?> GetAccountsAsync();
        Task<TransactionsResponse?> GetRecentTransactionsAsync();
        Task<User?> GetCurrentUserAsync();
    }
}
