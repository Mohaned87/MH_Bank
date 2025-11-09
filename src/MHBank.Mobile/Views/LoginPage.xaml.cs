using MHBank.Mobile.Services;

namespace MHBank.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _authService;
    private string? _pendingOtp;

    public LoginPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text;
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("خطأ", "يرجى إدخال رقم الهاتف وكلمة المرور", "حسناً");
            return;
        }

        // تعطيل الزر أثناء التحميل
        LoginButton.IsEnabled = false;
        LoginButton.Text = "جاري تسجيل الدخول...";

        try
        {
            var response = await _authService.LoginAsync(username, password);

            if (response == null)
            {
                await DisplayAlert("خطأ", "فشل الاتصال بالخادم", "حسناً");
                return;
            }

            // حالة 2FA
            if (response.RequiresTwoFactor)
            {
                _pendingOtp = response.Otp;

                var otpResult = await DisplayPromptAsync(
                    "المصادقة الثنائية",
                    $"تم إرسال رمز التحقق إلى هاتفك\nللتجربة: {response.Otp}",
                    "تحقق",
                    "إلغاء",
                    keyboard: Keyboard.Numeric,
                    maxLength: 6
                );

                if (!string.IsNullOrEmpty(otpResult))
                {
                    LoginButton.Text = "جاري التحقق...";

                    var otpResponse = await _authService.LoginAsync(username, password, otpResult);

                    if (otpResponse?.Success == true)
                    {
                        await Shell.Current.GoToAsync("//home");
                    }
                    else
                    {
                        await DisplayAlert("خطأ", otpResponse?.Message ?? "رمز التحقق غير صحيح", "حسناً");
                    }
                }

                return;
            }

            // نجح تسجيل الدخول
            if (response.Success)
            {
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                await DisplayAlert("خطأ", response.Message ?? "بيانات الدخول غير صحيحة", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ: {ex.Message}", "حسناً");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "تسجيل الدخول";
        }
    }

    private async void OnRegisterTapped(object sender, EventArgs e)
    {
        await DisplayAlert("قريباً", "صفحة التسجيل قيد التطوير", "حسناً");
    }
}