using MHBank.Mobile.Services;
using System.Text.Json;

namespace MHBank.Mobile.Views;

public partial class ChangePasswordPage : ContentPage
{
    private readonly IApiService _apiService;

    public ChangePasswordPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    private async void OnChangePasswordClicked(object sender, EventArgs e)
    {
        // التحقق من المدخلات
        if (string.IsNullOrWhiteSpace(CurrentPasswordEntry.Text))
        {
            await DisplayAlert("خطأ", "يرجى إدخال كلمة المرور الحالية", "حسناً");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPasswordEntry.Text))
        {
            await DisplayAlert("خطأ", "يرجى إدخال كلمة المرور الجديدة", "حسناً");
            return;
        }

        if (NewPasswordEntry.Text.Length < 6)
        {
            await DisplayAlert("خطأ", "كلمة المرور يجب أن تكون 6 أحرف على الأقل", "حسناً");
            return;
        }

        if (NewPasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            await DisplayAlert("خطأ", "كلمتا المرور غير متطابقتين", "حسناً");
            return;
        }

        if (CurrentPasswordEntry.Text == NewPasswordEntry.Text)
        {
            await DisplayAlert("خطأ", "كلمة المرور الجديدة يجب أن تختلف عن الحالية", "حسناً");
            return;
        }

        ChangePasswordButton.IsEnabled = false;
        ChangePasswordButton.Text = "جاري التغيير...";

        try
        {
            System.Diagnostics.Debug.WriteLine("🔵 Starting password change...");

            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            var token = await SecureStorage.GetAsync("access_token");

            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("❌ No token found");
                await DisplayAlert("خطأ", "يرجى تسجيل الدخول مرة أخرى", "حسناً");
                ChangePasswordButton.IsEnabled = true;
                ChangePasswordButton.Text = "تغيير كلمة المرور 🔒";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔵 Token: {token.Substring(0, Math.Min(20, token.Length))}...");

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                CurrentPassword = CurrentPasswordEntry.Text,
                NewPassword = NewPasswordEntry.Text
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/api/Auth/change-password";

            System.Diagnostics.Debug.WriteLine($"🔵 POST {url}");
            System.Diagnostics.Debug.WriteLine($"🔵 Request: {json}");

            HttpResponseMessage? response = null;
            string responseText = "";
            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    response = await httpClient.PostAsync(url, content);
                    responseText = await response.Content.ReadAsStringAsync();
                    break; // نجح!
                }
                catch (TaskCanceledException) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    System.Diagnostics.Debug.WriteLine($"⚠️ Timeout, retry {retryCount}/{maxRetries}...");
                    await Task.Delay(1000);
                    continue;
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
            }

            if (response == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ All retries failed");
                await DisplayAlert("خطأ", "فشل الاتصال بعد 3 محاولات", "حسناً");
                ChangePasswordButton.IsEnabled = true;
                ChangePasswordButton.Text = "تغيير كلمة المرور 🔒";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔵 Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"🔵 Response: {responseText}");

            if (response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine("✅ Password changed successfully");
                await DisplayAlert("نجح ✅", "تم تغيير كلمة المرور بنجاح", "حسناً");

                // مسح الحقول
                CurrentPasswordEntry.Text = "";
                NewPasswordEntry.Text = "";
                ConfirmPasswordEntry.Text = "";

                await Shell.Current.GoToAsync("..");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed: {response.StatusCode}");

                try
                {
                    var errorResult = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        responseText,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    var message = errorResult?.ContainsKey("message") == true
                        ? errorResult["message"].ToString()
                        : $"فشل تغيير كلمة المرور: {response.StatusCode}";

                    await DisplayAlert("خطأ", message, "حسناً");
                }
                catch
                {
                    await DisplayAlert("خطأ",
                        $"فشل تغيير كلمة المرور ({response.StatusCode})\n\n{responseText.Substring(0, Math.Min(200, responseText.Length))}",
                        "حسناً");
                }
            }
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("❌ Timeout");
            await DisplayAlert("خطأ",
                "انتهت مهلة الاتصال 😔\n\n" +
                "تأكد من:\n" +
                "✅ API يعمل\n" +
                "✅ عنوان الخادم صحيح\n" +
                "✅ الاتصال مستقر",
                "حسناً");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Exception: {ex.Message}\n{ex.StackTrace}");
            await DisplayAlert("خطأ", $"حدث خطأ: {ex.Message}", "حسناً");
        }
        finally
        {
            ChangePasswordButton.IsEnabled = true;
            ChangePasswordButton.Text = "تغيير كلمة المرور 🔒";
        }
    }

    private async Task<string> GetBaseUrlAsync()
    {
        // قراءة BaseUrl من Preferences أو استخدام الافتراضي
        var baseUrl = Preferences.Get("api_base_url", "http://192.168.1.104:5185");
        return baseUrl;
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}