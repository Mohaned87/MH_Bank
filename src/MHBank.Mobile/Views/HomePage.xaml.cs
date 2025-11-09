using MHBank.Mobile.Models;
using MHBank.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MHBank.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public HomePage(IApiService apiService, IAuthService authService)
    {
        InitializeComponent();
        _apiService = apiService;
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            LoadingLabel.IsVisible = true;
            ContentLayout.IsVisible = false;

            var user = await _apiService.GetCurrentUserAsync();
            if (user != null)
            {
                WelcomeLabel.Text = $"مرحباً، {user.FirstName} 👋";
            }

            var accountsResponse = await _apiService.GetAccountsAsync();
            if (accountsResponse?.Success == true && accountsResponse.Accounts?.Count > 0)
            {
                TotalBalanceLabel.Text = $"{accountsResponse.TotalBalance:N0} IQD";

                AccountsLayout.Children.Clear();
                foreach (var account in accountsResponse.Accounts)
                {
                    AccountsLayout.Children.Add(CreateAccountCard(account));
                }
            }

            var transactionsResponse = await _apiService.GetRecentTransactionsAsync();
            if (transactionsResponse?.Success == true && transactionsResponse.Transactions?.Count > 0)
            {
                TransactionsLayout.Children.Clear();
                foreach (var transaction in transactionsResponse.Transactions.Take(5))
                {
                    TransactionsLayout.Children.Add(CreateTransactionCard(transaction));
                }
            }

            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل تحميل البيانات: {ex.Message}", "حسناً");
        }
    }

    private Border CreateAccountCard(BankAccount account)
    {
        var accountTypeText = account.AccountType == "Checking" ? "حساب جاري" : "حساب توفير";
        var color = account.AccountType == "Checking" ? "#4A90E2" : "#4CAF50";

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var accountInfo = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = accountTypeText, FontSize = 16, TextColor = Colors.Gray },
                new Label { Text = $"{account.Balance:N0} IQD", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color) },
                new Label { Text = account.AccountNumber, FontSize = 12, TextColor = Colors.Gray }
            }
        };
        Grid.SetColumn(accountInfo, 0);
        grid.Children.Add(accountInfo);

        var arrow = new Label { Text = "→", FontSize = 28, TextColor = Colors.Gray, VerticalOptions = LayoutOptions.Center };
        Grid.SetColumn(arrow, 1);
        grid.Children.Add(arrow);

        return new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = 20,
            Margin = new Thickness(0, 0, 0, 10),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = grid
        };
    }

    private Border CreateTransactionCard(Transaction transaction)
    {
        var isDebit = transaction.Type == "Transfer" || transaction.Type == "Withdrawal";
        var icon = isDebit ? "🔽" : "🔼";
        var bgColor = isDebit ? "#FFEBEE" : "#E8F5E9";
        var amountColor = isDebit ? "#F44336" : "#4CAF50";
        var sign = isDebit ? "-" : "+";

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 15
        };

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb(bgColor),
            StrokeThickness = 0,
            WidthRequest = 48,
            HeightRequest = 48,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label { Text = icon, FontSize = 24, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        var transactionInfo = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = GetTransactionTypeArabic(transaction.Type), FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black },
                new Label { Text = transaction.CreatedAt.ToString("dd/MM/yyyy"), FontSize = 13, TextColor = Colors.Gray }
            }
        };
        Grid.SetColumn(transactionInfo, 1);
        grid.Children.Add(transactionInfo);

        var amount = new Label
        {
            Text = $"{sign}{transaction.Amount:N0}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(amountColor),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(amount, 2);
        grid.Children.Add(amount);

        return new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = 18,
            Margin = new Thickness(0, 0, 0, 10),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = grid
        };
    }

    private string GetTransactionTypeArabic(string type)
    {
        return type switch
        {
            "Deposit" => "إيداع",
            "Withdrawal" => "سحب",
            "Transfer" => "تحويل",
            "BillPayment" => "دفع فاتورة",
            _ => type
        };
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var confirm = await DisplayAlert("تسجيل الخروج", "هل أنت متأكد؟", "نعم", "لا");
            if (confirm)
            {
                await _authService.LogoutAsync();

                // تغيير MainPage بدلاً من Navigation
                Application.Current.MainPage = new AppShell();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", ex.Message, "حسناً");
        }
    }

}