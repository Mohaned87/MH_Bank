using MHBank.Mobile.Services;
using MHBank.Mobile.Models;

namespace MHBank.Mobile.Views;

public partial class BillPaymentPage : ContentPage
{
    private readonly IApiService _apiService;
    private readonly INotificationService _notificationService;
    private string _selectedBillType = "";
    private BankAccount? _selectedAccount;
    private List<BankAccount> _accounts = new();

    public BillPaymentPage(IApiService apiService, INotificationService notificationService)
    {
        InitializeComponent();
        _apiService = apiService;
        _notificationService = notificationService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var response = await _apiService.GetAccountsAsync();
            if (response?.Success == true && response.Accounts?.Count > 0)
            {
                _accounts = response.Accounts;
                _selectedAccount = _accounts.First();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل تحميل الحسابات: {ex.Message}", "حسناً");
        }
    }

    private void OnElectricityTapped(object sender, EventArgs e)
    {
        _selectedBillType = "Electricity";
        BillDetailsLayout.IsVisible = true;
        ElectricityBorder.BackgroundColor = Color.FromArgb("#E8F5E9");
        ElectricityBorder.Stroke = Color.FromArgb("#4CAF50");
    }

    private async void OnWaterTapped(object sender, EventArgs e)
    {
        _selectedBillType = "Water";
        BillDetailsLayout.IsVisible = true;
        await DisplayAlert("قريباً", "دفع فواتير الماء قريباً", "حسناً");
    }

    private async void OnInternetTapped(object sender, EventArgs e)
    {
        _selectedBillType = "Internet";
        BillDetailsLayout.IsVisible = true;
        await DisplayAlert("قريباً", "دفع فواتير الإنترنت قريباً", "حسناً");
    }

    private async void OnPayClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SubscriberNumberEntry.Text))
        {
            await DisplayAlert("خطأ", "يرجى إدخال رقم المشترك", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(AmountEntry.Text) || !decimal.TryParse(AmountEntry.Text, out var amount))
        {
            await DisplayAlert("خطأ", "يرجى إدخال المبلغ بشكل صحيح", "حسناً");
            return;
        }

        if (_selectedAccount == null)
        {
            await DisplayAlert("خطأ", "لا توجد حسابات متاحة", "حسناً");
            return;
        }

        // اختيار الحساب إذا كان أكثر من حساب
        if (_accounts.Count > 1)
        {
            var accountNames = _accounts.Select(a =>
                $"{(a.AccountType == "Checking" ? "جاري" : "توفير")} - {a.AccountNumber} ({a.Balance:N0} IQD)"
            ).ToArray();

            var selected = await DisplayActionSheet(
                "اختر الحساب للدفع منه",
                "إلغاء",
                null,
                accountNames
            );

            if (selected == null || selected == "إلغاء")
                return;

            var selectedIndex = Array.IndexOf(accountNames, selected);
            if (selectedIndex >= 0)
            {
                _selectedAccount = _accounts[selectedIndex];
            }
        }

        var confirm = await DisplayAlert(
            "تأكيد الدفع",
            $"دفع {amount:N0} IQD لفاتورة {_selectedBillType}\nرقم المشترك: {SubscriberNumberEntry.Text}\nمن حساب: {_selectedAccount.AccountNumber}",
            "تأكيد",
            "إلغاء"
        );

        if (confirm)
        {
            PayButton.IsEnabled = false;
            PayButton.Text = "جاري الدفع...";

            var request = new BillPaymentRequest
            {
                AccountId = _selectedAccount.Id,
                BillType = _selectedBillType,
                SubscriberNumber = SubscriberNumberEntry.Text,
                Amount = amount
            };

            var result = await _apiService.PayBillAsync(request);

            if (result?.Success == true)
            {
                // إضافة إشعار
                await _notificationService.AddNotificationAsync(
                    "دفع فاتورة ✅",
                    $"تم دفع فاتورة {_selectedBillType} بمبلغ {amount:N0} IQD",
                    "BillPayment"
                );

                await DisplayAlert("نجح", $"تم دفع الفاتورة بنجاح!\nالرصيد المتبقي: {result.NewBalance:N0} IQD", "حسناً");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("خطأ", result?.Message ?? "فشل دفع الفاتورة", "حسناً");
                PayButton.IsEnabled = true;
                PayButton.Text = "دفع الفاتورة 💳";
            }
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
