namespace MHBank.Mobile.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Otp { get; set; }
}

public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Message { get; set; }
    public User? User { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public string? Otp { get; set; }
}

public class RegisterResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public User? User { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
}

public class AccountsResponse
{
    public bool Success { get; set; }
    public int TotalAccounts { get; set; }
    public decimal TotalBalance { get; set; }
    public List<BankAccount>? Accounts { get; set; }
}

public class TransactionsResponse
{
    public bool Success { get; set; }
    public int TotalTransactions { get; set; }
    public List<Transaction>? Transactions { get; set; }
}
