namespace MHBank.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var currentUrl = Preferences.Get("api_base_url", "http://10.0.2.2:5185");
        CurrentUrlLabel.Text = currentUrl;
        ApiUrlEntry.Text = currentUrl;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var url = ApiUrlEntry.Text?.Trim();

        if (string.IsNullOrEmpty(url))
        {
            await DisplayAlert("خطأ", "يرجى إدخال عنوان الخادم", "حسناً");
            return;
        }

        // التحقق من صحة العنوان
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            await DisplayAlert("خطأ", "يجب أن يبدأ العنوان بـ http:// أو https://", "حسناً");
            return;
        }

        // حفظ العنوان
        Preferences.Set("api_base_url", url);
        CurrentUrlLabel.Text = url;

        await DisplayAlert("نجح ✅",
            $"تم حفظ العنوان:\n{url}\n\n" +
            $"⚠️ يُنصح بإغلاق التطبيق وإعادة فتحه\n" +
            $"لضمان استخدام العنوان الجديد في كل الصفحات.",
            "حسناً");
    }

    private async void OnTestConnectionClicked(object sender, EventArgs e)
    {
        var url = ApiUrlEntry.Text?.Trim();

        if (string.IsNullOrEmpty(url))
        {
            await DisplayAlert("خطأ", "يرجى إدخال عنوان الخادم أولاً", "حسناً");
            return;
        }

        SaveButton.IsEnabled = false;
        SaveButton.Text = "جاري الاختبار...";

        try
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            System.Diagnostics.Debug.WriteLine($"🔵 Testing connection to: {url}");

            var response = await httpClient.GetAsync($"{url}/swagger/index.html");

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("نجح ✅",
                    $"الاتصال ناجح!\n" +
                    $"الخادم يعمل بشكل صحيح.\n\n" +
                    $"يمكنك الآن استخدام التطبيق.",
                    "رائع!");

                // حفظ العنوان تلقائياً
                Preferences.Set("api_base_url", url);
                CurrentUrlLabel.Text = url;
            }
            else
            {
                await DisplayAlert("تحذير ⚠️",
                    $"الخادم استجاب لكن بحالة: {response.StatusCode}\n" +
                    $"تأكد من أن API يعمل.",
                    "حسناً");
            }
        }
        catch (TaskCanceledException)
        {
            await DisplayAlert("فشل ❌",
                $"انتهت مهلة الاتصال.\n\n" +
                $"تأكد من:\n" +
                $"1. API يعمل على الكمبيوتر\n" +
                $"2. الهاتف والكمبيوتر على نفس الشبكة\n" +
                $"3. Firewall مطفي\n" +
                $"4. العنوان صحيح",
                "حسناً");
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ ❌",
                $"فشل الاتصال:\n{ex.Message}\n\n" +
                $"تأكد من أن العنوان صحيح والخادم يعمل.",
                "حسناً");
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Text = "حفظ العنوان ✅";
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}