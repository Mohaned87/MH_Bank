namespace MHBank.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
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

        try
        {
            // TODO: استدعاء API للدخول

            // الانتقال للصفحة الرئيسية
            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", "حدث خطأ أثناء تسجيل الدخول", "حسناً");
        }
    }

    private async void OnRegisterTapped(object sender, EventArgs e)
    {
        await DisplayAlert("قريباً", "صفحة التسجيل قيد التطوير", "حسناً");
    }
}

