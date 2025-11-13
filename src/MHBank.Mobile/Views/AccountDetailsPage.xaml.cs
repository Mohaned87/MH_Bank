using MHBank.Mobile.Services;
using MHBank.Mobile.Models;

namespace MHBank.Mobile.Views;

public partial class AccountDetailsPage : ContentPage
{
    private readonly IApiService _apiService;
    private Guid _accountId;

    public AccountDetailsPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    public void SetAccountId(Guid accountId)
    {
        _accountId = accountId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAccountDetailsAsync();
    }

    private async Task LoadAccountDetailsAsync()
    {
        try
        {
            var response = await _apiService.GetAccountsAsync();

            if (response?.Success == true && response.Accounts != null)
            {
                var account = response.Accounts.FirstOrDefault();

                if (account != null)
                {
                    AccountTypeLabel.Text = account.AccountType == "Checking" ? "حساب جاري" : "حساب توفير";
                    BalanceLabel.Text = $"{account.Balance:N0} IQD";
                    AccountNumberLabel.Text = account.AccountNumber;
                    IBANLabel.Text = account.IBAN ?? "غير متوفر";
                    StatusLabel.Text = account.IsActive ? "نشط" : "غير نشط";
                    StatusLabel.TextColor = account.IsActive ? Color.FromArgb("#4CAF50") : Colors.Red;
                    OpenedAtLabel.Text = account.OpenedAt.ToString("dd/MM/yyyy");
                    CurrencyLabel.Text = account.Currency;
                    DailyLimitLabel.Text = $"{account.DailyTransferLimit:N0} IQD";
                    MonthlyLimitLabel.Text = $"{account.MonthlyTransferLimit:N0} IQD";
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل تحميل التفاصيل: {ex.Message}", "حسناً");
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
