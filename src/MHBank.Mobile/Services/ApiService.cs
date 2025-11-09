using MHBank.Mobile.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MHBank.Mobile.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly IStorageService _storageService;

    private const string BaseUrl = "http://192.168.1.106:5185";

    public ApiService(IStorageService storageService)
    {
        _storageService = storageService;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        System.Diagnostics.Debug.WriteLine($"✅ ApiService: {BaseUrl}");
    }

    private async Task SetAuthorizationHeaderAsync()
    {
        var token = await _storageService.GetAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var url = $"{BaseUrl}/api/Auth/login";
            System.Diagnostics.Debug.WriteLine($"🔵 POST {url}");
            System.Diagnostics.Debug.WriteLine($"🔵 Username: {request.Username}");

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"🔵 Status: {(int)response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"🔵 Response: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success == true && !string.IsNullOrEmpty(result.AccessToken))
                {
                    await _storageService.SetAsync("access_token", result.AccessToken);
                    if (!string.IsNullOrEmpty(result.RefreshToken))
                        await _storageService.SetAsync("refresh_token", result.RefreshToken);

                    System.Diagnostics.Debug.WriteLine("✅ Token saved!");
                }

                return result;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error: {responseContent}");
                return new LoginResponse { Success = false, Message = responseContent };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Exception: {ex.Message}");
            return new LoginResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<AccountsResponse?> GetAccountsAsync()
    {
        try
        {
            await SetAuthorizationHeaderAsync();
            var url = $"{BaseUrl}/api/Accounts";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AccountsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ GetAccounts: {ex.Message}");
            return null;
        }
    }

    public async Task<TransactionsResponse?> GetRecentTransactionsAsync()
    {
        try
        {
            await SetAuthorizationHeaderAsync();
            var url = $"{BaseUrl}/api/Transactions/recent";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TransactionsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ GetTransactions: {ex.Message}");
            return null;
        }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            await SetAuthorizationHeaderAsync();
            var url = $"{BaseUrl}/api/Auth/me";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<User>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return result?.Data;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ GetCurrentUser: {ex.Message}");
            return null;
        }
    }
}