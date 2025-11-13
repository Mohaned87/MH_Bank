using MHBank.Mobile.Services;
using MHBank.Mobile.Models;

namespace MHBank.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    public ProfilePage(IApiService apiService, IAuthService authService)
    {
        InitializeComponent();
        _apiService = apiService;
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadUserDataAsync();
    }

    private async Task LoadUserDataAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("🔵 Loading user profile data...");

            // تحقق من وجود Token أولاً
            var token = await _apiService.GetStoredTokenAsync();
            System.Diagnostics.Debug.WriteLine($"🔑 Stored Token: {(string.IsNullOrEmpty(token) ? "NULL" : "EXISTS - " + token.Substring(0, 30) + "...")}");

            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("❌ No token found - User not logged in");
                await DisplayAlert("خطأ", "يرجى تسجيل الدخول أولاً", "حسناً");
                await Shell.Current.GoToAsync("//login");
                return;
            }

            var user = await _apiService.GetCurrentUserAsync();

            if (user != null)
            {
                System.Diagnostics.Debug.WriteLine($"✅ User loaded: {user.FirstName} {user.LastName}");

                // تحديث الاسم الكامل
                var fullName = $"{user.FirstName} {user.LastName}";
                FullNameLabel.Text = fullName;

                System.Diagnostics.Debug.WriteLine($"📝 Setting FullName: {fullName}");
                System.Diagnostics.Debug.WriteLine($"📝 Setting Email: {user.Email}");

                // تحديث البريد (في الكارت وفي المعلومات)
                EmailLabel.Text = user.Email;
                EmailValueLabel.Text = user.Email;

                // رقم الهاتف
                PhoneLabel.Text = user.PhoneNumber;

                // تاريخ التسجيل
                JoinDateLabel.Text = user.CreatedAt.ToString("dd/MM/yyyy");

                // حالة 2FA
                TwoFactorSwitch.IsToggled = user.TwoFactorEnabled;

                System.Diagnostics.Debug.WriteLine("✅ Profile UI updated successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ GetCurrentUserAsync returned NULL");
                await DisplayAlert("خطأ", "لم يتم العثور على بيانات المستخدم. قد تحتاج لتسجيل الدخول مرة أخرى.", "حسناً");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Profile error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ Stack: {ex.StackTrace}");
            await DisplayAlert("خطأ", $"فشل تحميل البيانات: {ex.Message}", "حسناً");
        }
    }

    private async void OnTwoFactorToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            if (e.Value)
            {
                var confirm = await DisplayAlert(
                    "تفعيل المصادقة الثنائية",
                    "هل تريد تفعيل المصادقة الثنائية (2FA) لحماية إضافية لحسابك؟",
                    "نعم",
                    "لا"
                );

                if (!confirm)
                {
                    TwoFactorSwitch.IsToggled = false;
                    return;
                }

                await DisplayAlert("قريباً", "سيتم إرسال رمز التفعيل إلى بريدك الإلكتروني", "حسناً");
                // TODO: Implement 2FA activation
            }
            else
            {
                var confirm = await DisplayAlert(
                    "تعطيل المصادقة الثنائية",
                    "هل تريد تعطيل المصادقة الثنائية؟ سيقلل هذا من أمان حسابك.",
                    "نعم",
                    "لا"
                );

                if (!confirm)
                {
                    TwoFactorSwitch.IsToggled = true;
                    return;
                }

                // TODO: Implement 2FA deactivation
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", ex.Message, "حسناً");
        }
    }

    private async void OnChangePasswordTapped(object sender, EventArgs e)
    {
        await DisplayAlert("قريباً", "ميزة تغيير كلمة المرور ستكون متاحة قريباً", "حسناً");
        // TODO: Navigate to change password page
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("تسجيل الخروج", "هل أنت متأكد؟", "نعم", "لا");
        if (confirm)
        {
            await _authService.LogoutAsync();
            Application.Current.MainPage = new AppShell();
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}