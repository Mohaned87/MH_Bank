using MHBank.Mobile.Models;
using MHBank.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MHBank.Mobile.Views;

public partial class TransactionsHistoryPage : ContentPage
{
    private readonly IApiService _apiService;
    private List<Transaction> _allTransactions = new();
    private string _currentFilter = "All";

    public TransactionsHistoryPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTransactionsAsync();
    }

    private async Task LoadTransactionsAsync()
    {
        try
        {
            LoadingLabel.IsVisible = true;
            ContentLayout.IsVisible = false;

            var response = await _apiService.GetAllTransactionsAsync();

            if (response?.Success == true && response.Transactions != null)
            {
                _allTransactions = response.Transactions;
                ApplyFilter(_currentFilter);
            }

            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل تحميل المعاملات: {ex.Message}", "حسناً");
        }
    }

    private void ApplyFilter(string filter)
    {
        _currentFilter = filter;

        // Update button colors
        ResetFilterButtons();

        switch (filter)
        {
            case "All":
                AllFilterButton.BackgroundColor = Color.FromArgb("#87CEEB");
                ((Label)AllFilterButton.Content).TextColor = Colors.White;
                ((Label)AllFilterButton.Content).FontAttributes = FontAttributes.Bold;
                break;
            case "Transfer":
                TransferFilterButton.BackgroundColor = Color.FromArgb("#87CEEB");
                ((Label)TransferFilterButton.Content).TextColor = Colors.White;
                ((Label)TransferFilterButton.Content).FontAttributes = FontAttributes.Bold;
                break;
            case "Deposit":
                DepositFilterButton.BackgroundColor = Color.FromArgb("#87CEEB");
                ((Label)DepositFilterButton.Content).TextColor = Colors.White;
                ((Label)DepositFilterButton.Content).FontAttributes = FontAttributes.Bold;
                break;
        }

        // Filter transactions
        var filteredTransactions = filter switch
        {
            "All" => _allTransactions,
            "Transfer" => _allTransactions.Where(t => t.Type == "Transfer").ToList(),
            "Deposit" => _allTransactions.Where(t => t.Type == "Deposit").ToList(),
            _ => _allTransactions
        };

        // Display transactions
        TransactionsLayout.Children.Clear();

        if (filteredTransactions.Count == 0)
        {
            TransactionsLayout.Children.Add(new Label
            {
                Text = "لا توجد معاملات",
                FontSize = 16,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        foreach (var transaction in filteredTransactions)
        {
            TransactionsLayout.Children.Add(CreateTransactionCard(transaction));
        }
    }

    private void ResetFilterButtons()
    {
        AllFilterButton.BackgroundColor = Color.FromArgb("#E8E8E8");
        ((Label)AllFilterButton.Content).TextColor = Colors.Gray;
        ((Label)AllFilterButton.Content).FontAttributes = FontAttributes.None;

        TransferFilterButton.BackgroundColor = Color.FromArgb("#E8E8E8");
        ((Label)TransferFilterButton.Content).TextColor = Colors.Gray;
        ((Label)TransferFilterButton.Content).FontAttributes = FontAttributes.None;

        DepositFilterButton.BackgroundColor = Color.FromArgb("#E8E8E8");
        ((Label)DepositFilterButton.Content).TextColor = Colors.Gray;
        ((Label)DepositFilterButton.Content).FontAttributes = FontAttributes.None;
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
            Content = new Label
            {
                Text = icon,
                FontSize = 24,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        var info = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center
        };

        info.Children.Add(new Label
        {
            Text = GetTransactionTypeArabic(transaction.Type),
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        });

        if (!string.IsNullOrEmpty(transaction.Description))
        {
            info.Children.Add(new Label
            {
                Text = transaction.Description,
                FontSize = 13,
                TextColor = Colors.Gray
            });
        }

        info.Children.Add(new Label
        {
            Text = transaction.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            FontSize = 12,
            TextColor = Colors.Gray
        });

        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

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

    private void OnAllFilterTapped(object sender, EventArgs e)
    {
        ApplyFilter("All");
    }

    private void OnTransferFilterTapped(object sender, EventArgs e)
    {
        ApplyFilter("Transfer");
    }

    private void OnDepositFilterTapped(object sender, EventArgs e)
    {
        ApplyFilter("Deposit");
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
