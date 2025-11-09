using MHBank.Mobile.Services;
using MHBank.Mobile.Models;

namespace MHBank.Mobile.Views;

public partial class RegisterPage : ContentPage
{
    private readonly IApiService _apiService;

    public RegisterPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        try
        {
            // التحقق من المدخلات
            if (string.IsNullOrWhiteSpace(FirstNameEntry.Text))
            {
                await DisplayAlert("خطأ", "يرجى إدخال الاسم الأول", "حسناً");
                return;
            }

            if (string.IsNullOrWhiteSpace(LastNameEntry.Text))
            {
                await DisplayAlert("خطأ", "يرجى إدخال الاسم الأخير", "حسناً");
                return;
            }

            if (string.IsNullOrWhiteSpace(EmailEntry.Text))
            {
                await DisplayAlert("خطأ", "يرجى إدخال البريد الإلكتروني", "حسناً");
                return;
            }

            if (string.IsNullOrWhiteSpace(PhoneEntry.Text))
            {
                await DisplayAlert("خطأ", "يرجى إدخال رقم الهاتف", "حسناً");
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                await DisplayAlert("خطأ", "يرجى إدخال كلمة المرور", "حسناً");
                return;
            }

            if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
            {
                await DisplayAlert("خطأ", "كلمة المرور وتأكيد كلمة المرور غير متطابقتين", "حسناً");
                return;
            }

            if (!TermsCheckBox.IsChecked)
            {
                await DisplayAlert("خطأ", "يجب الموافقة على الشروط والأحكام", "حسناً");
                return;
            }

            if (PasswordEntry.Text.Length < 6)
            {
                await DisplayAlert("خطأ", "كلمة المرور يجب أن تحتوي على 6 أحرف على الأقل", "حسناً");
                return;
            }

            // تعطيل الزر أثناء التسجيل
            RegisterButton.IsEnabled = false;
            RegisterButton.Text = "جاري إنشاء الحساب...";

            var request = new RegisterRequest
            {
                FirstName = FirstNameEntry.Text.Trim(),
                LastName = LastNameEntry.Text.Trim(),
                Email = EmailEntry.Text.Trim(),
                PhoneNumber = PhoneEntry.Text.Trim(),
                Password = PasswordEntry.Text
            };

            var response = await _apiService.RegisterAsync(request);

            if (response?.Success == true)
            {
                await DisplayAlert("نجح", "تم إنشاء الحساب بنجاح! يمكنك الآن تسجيل الدخول", "حسناً");
                //await Shell.Current.GoToAsync("//login");
                Application.Current.MainPage = new AppShell();
            }
            else
            {
                await DisplayAlert("خطأ", response?.Message ?? "فشل إنشاء الحساب", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ: {ex.Message}", "حسناً");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            RegisterButton.Text = "إنشاء الحساب";
        }
    }

    private async void OnTermsTapped(object sender, EventArgs e)
    {
        await DisplayAlert("الشروط والأحكام",
            "1. يجب أن تكون المعلومات صحيحة\n" +
            "2. لن نشارك معلوماتك مع أطراف ثالثة\n" +
            "3. أنت مسؤول عن حماية حسابك",
            "حسناً");
    }

    private async void OnLoginTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//login");
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//login");
    }
}