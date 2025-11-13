using MHBank.Mobile.Services;
using MHBank.Mobile.Models;

namespace MHBank.Mobile.Views;

public partial class TransferPage : ContentPage
{
    private readonly IApiService _apiService;
    private BankAccount? _selectedAccount;
    private List<BankAccount> _accounts = new();

    public TransferPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
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

                UpdateSelectedAccount();
            }
            else
            {
                await DisplayAlert("خطأ", "لا توجد حسابات متاحة", "حسناً");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل تحميل الحسابات: {ex.Message}", "حسناً");
        }
    }

    private void UpdateSelectedAccount()
    {
        if (_selectedAccount != null)
        {
            FromAccountTypeLabel.Text = _selectedAccount.AccountType == "Checking" ? "حساب جاري" : "حساب توفير";
            FromAccountNumberLabel.Text = _selectedAccount.AccountNumber;
            FromAccountBalanceLabel.Text = $"{_selectedAccount.Balance:N0} IQD";
            AvailableBalanceLabel.Text = $"{_selectedAccount.Balance:N0} IQD";
        }
    }

    private async void OnTransferClicked(object sender, EventArgs e)
    {
        try
        {
            // التحقق من المدخلات
            if (_selectedAccount == null)
            {
                await DisplayAlert("خطأ", "لم يتم تحديد حساب", "حسناً");
                return;
            }

            if (string.IsNullOrWhiteSpace(ToAccountEntry.Text))
            {
                await DisplayAlert("خطأ", "يرجى إدخال رقم الحساب المستلم", "حسناً");
                return;
            }

            if (string.IsNullOrWhiteSpace(AmountEntry.Text))
            {
                await DisplayAlert("خطأ", "يرجى إدخال المبلغ", "حسناً");
                return;
            }

            if (!decimal.TryParse(AmountEntry.Text, out decimal amount) || amount <= 0)
            {
                await DisplayAlert("خطأ", "المبلغ غير صحيح", "حسناً");
                return;
            }

            // التحقق من الرصيد
            if (amount > _selectedAccount.Balance)
            {
                await DisplayAlert("خطأ", "الرصيد غير كافي", "حسناً");
                return;
            }

            // التحقق من عدم التحويل لنفس الحساب
            if (ToAccountEntry.Text == _selectedAccount.AccountNumber)
            {
                await DisplayAlert("خطأ", "لا يمكن التحويل لنفس الحساب", "حسناً");
                return;
            }

            // تأكيد التحويل
            var confirm = await DisplayAlert(
                "تأكيد التحويل",
                $"هل تريد تحويل {amount:N0} IQD إلى الحساب {ToAccountEntry.Text}؟",
                "نعم",
                "إلغاء"
            );

            if (!confirm)
                return;

            // تعطيل الزر أثناء المعالجة
            TransferButton.IsEnabled = false;
            TransferButton.Text = "جاري التحويل...";

            // إرسال طلب التحويل
            var transferRequest = new TransferRequest
            {
                FromAccountId = _selectedAccount.Id,
                ToAccountNumber = ToAccountEntry.Text,
                Amount = amount,
                Description = string.IsNullOrWhiteSpace(NotesEditor.Text) ? "تحويل" : NotesEditor.Text
            };

            var response = await _apiService.TransferAsync(transferRequest);

            if (response?.Success == true)
            {
                await DisplayAlert("نجح", $"تم التحويل بنجاح!\nرقم العملية: {response.TransactionId}", "حسناً");

                // العودة للصفحة الرئيسية
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                await DisplayAlert("خطأ", response?.Message ?? "فشل التحويل", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ: {ex.Message}", "حسناً");
        }
        finally
        {
            TransferButton.IsEnabled = true;
            TransferButton.Text = "تحويل الآن 💸";
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
