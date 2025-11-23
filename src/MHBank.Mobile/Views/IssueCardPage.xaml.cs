using MHBank.Mobile.Services;
using MHBank.Mobile.Models;
using System.Text.Json;

namespace MHBank.Mobile.Views;

public partial class IssueCardPage : ContentPage
{
    private readonly IApiService _apiService;
    private List<BankAccount> _accounts = new();
    private BankAccount? _selectedAccount;
    private string _selectedBrand = "Visa";

    public IssueCardPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAccountsAsync();
        UpdateBrandSelection();
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("🔵 Loading accounts for card issuance...");

            var response = await _apiService.GetAccountsAsync();

            if (response?.Success == true && response.Accounts?.Count > 0)
            {
                _accounts = response.Accounts;
                System.Diagnostics.Debug.WriteLine($"✅ Loaded {_accounts.Count} accounts");

                // اختيار أول حساب تلقائياً
                if (_accounts.Count > 0)
                {
                    _selectedAccount = _accounts[0];
                    SelectedAccountLabel.Text = $"{_selectedAccount.AccountNumber} - {_selectedAccount.Balance:N0} IQD";
                    SelectedAccountLabel.TextColor = Colors.Black;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ No accounts found");
                await DisplayAlert("تنبيه", "لا توجد حسابات متاحة", "حسناً");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error loading accounts: {ex.Message}");
            await DisplayAlert("خطأ", $"فشل تحميل الحسابات: {ex.Message}", "حسناً");
        }
    }

    private async void OnSelectAccountTapped(object sender, EventArgs e)
    {
        if (_accounts.Count == 0)
        {
            await DisplayAlert("خطأ", "لا توجد حسابات متاحة", "حسناً");
            return;
        }

        var accountNames = _accounts.Select(a =>
            $"{(a.AccountType == "Checking" ? "جاري" : "توفير")} - {a.AccountNumber} ({a.Balance:N0} IQD)"
        ).ToArray();

        var selected = await DisplayActionSheet("اختر الحساب", "إلغاء", null, accountNames);

        if (selected != null && selected != "إلغاء")
        {
            var selectedIndex = Array.IndexOf(accountNames, selected);
            if (selectedIndex >= 0)
            {
                _selectedAccount = _accounts[selectedIndex];
                SelectedAccountLabel.Text = $"{_selectedAccount.AccountNumber} - {_selectedAccount.Balance:N0} IQD";
                SelectedAccountLabel.TextColor = Colors.Black;
            }
        }
    }

    private void OnVisaTapped(object sender, EventArgs e)
    {
        _selectedBrand = "Visa";
        UpdateBrandSelection();
    }

    private void OnMastercardTapped(object sender, EventArgs e)
    {
        _selectedBrand = "Mastercard";
        UpdateBrandSelection();
    }

    private void UpdateBrandSelection()
    {
        if (_selectedBrand == "Visa")
        {
            VisaBorder.Stroke = Color.FromArgb("#87CEEB");
            VisaBorder.StrokeThickness = 3;
            MastercardBorder.Stroke = Colors.LightGray;
            MastercardBorder.StrokeThickness = 2;
        }
        else
        {
            MastercardBorder.Stroke = Color.FromArgb("#87CEEB");
            MastercardBorder.StrokeThickness = 3;
            VisaBorder.Stroke = Colors.LightGray;
            VisaBorder.StrokeThickness = 2;
        }
    }

    private async void OnIssueCardClicked(object sender, EventArgs e)
    {
        // التحقق من اختيار الحساب
        if (_selectedAccount == null)
        {
            await DisplayAlert("خطأ", "يرجى اختيار الحساب أولاً", "حسناً");
            return;
        }

        // التحقق من اختيار النوع
        if (string.IsNullOrEmpty(_selectedBrand))
        {
            await DisplayAlert("خطأ", "يرجى اختيار نوع البطاقة (Visa أو Mastercard)", "حسناً");
            return;
        }

        var confirm = await DisplayAlert(
            "تأكيد إصدار البطاقة",
            $"سيتم إصدار بطاقة {_selectedBrand}\n" +
            $"للحساب: {_selectedAccount.AccountNumber}\n" +
            $"الرصيد: {_selectedAccount.Balance:N0} IQD\n\n" +
            $"هل تريد المتابعة؟",
            "نعم، أصدر البطاقة",
            "إلغاء"
        );

        if (!confirm) return;

        IssueButton.IsEnabled = false;
        IssueButton.Text = "جاري الإصدار...";

        try
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("🔵 OnIssueCardClicked START");
            Console.WriteLine("🔵 OnIssueCardClicked START");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            System.Diagnostics.Debug.WriteLine("🔵 Step 1: Getting token...");
            Console.WriteLine("🔵 Step 1: Getting token...");

            var token = await SecureStorage.GetAsync("access_token");

            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("❌ Token is empty!");
                await DisplayAlert("خطأ", "يرجى تسجيل الدخول مرة أخرى", "حسناً");
                IssueButton.IsEnabled = true;
                IssueButton.Text = "إصدار البطاقة 💳";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Token OK: {token.Substring(0, Math.Min(20, token.Length))}...");
            Console.WriteLine($"✅ Token OK");

            System.Diagnostics.Debug.WriteLine("🔵 Step 2: Creating HttpClient...");
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            System.Diagnostics.Debug.WriteLine("✅ HttpClient created");

            System.Diagnostics.Debug.WriteLine("🔵 Step 3: Building request...");
            var request = new
            {
                AccountId = _selectedAccount.Id,
                CardType = "Debit",
                Brand = _selectedBrand == "Visa" ? 1 : 2
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            System.Diagnostics.Debug.WriteLine($"✅ Request built: {json}");
            Console.WriteLine($"✅ Request: AccountId={_selectedAccount.Id}, Brand={request.Brand}");

            System.Diagnostics.Debug.WriteLine("🔵 Step 4: Getting base URL...");
            var baseUrl = Preferences.Get("api_base_url", "http://192.168.1.104:5185");

            // إصلاح: إذا كان URL فارغ أو "غير محدد"
            if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("غير محدد") || baseUrl == "null")
            {
                baseUrl = "http://192.168.1.104:5185";
                Preferences.Set("api_base_url", baseUrl);
                System.Diagnostics.Debug.WriteLine($"⚠️ Fixed empty baseUrl");
            }

            System.Diagnostics.Debug.WriteLine($"✅ Base URL: {baseUrl}");
            System.Diagnostics.Debug.WriteLine($"🔵 Full URL: {baseUrl}/api/Cards");
            Console.WriteLine($"🔵 POST to: {baseUrl}/api/Cards");

            System.Diagnostics.Debug.WriteLine("🔵 Step 5: Sending POST request...");
            Console.WriteLine("🔵 Step 5: Sending POST request...");

            HttpResponseMessage? response = null;
            string responseText = "";
            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    response = await httpClient.PostAsync($"{baseUrl}/api/Cards", content);
                    responseText = await response.Content.ReadAsStringAsync();
                    break; // نجح!
                }
                catch (TaskCanceledException) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    System.Diagnostics.Debug.WriteLine($"⚠️ Timeout, retry {retryCount}/{maxRetries}...");
                    await Task.Delay(1000); // انتظر ثانية
                    continue;
                }
                catch (TaskCanceledException)
                {
                    throw; // آخر محاولة فشلت
                }
            }

            if (response == null)
            {
                await DisplayAlert("خطأ", "فشل الاتصال بعد 3 محاولات", "حسناً");
                IssueButton.IsEnabled = true;
                IssueButton.Text = "إصدار البطاقة 💳";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔵 Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"🔵 Response: {responseText}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<IssueCardResponse>(responseText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                await DisplayAlert("نجح ✅",
                    $"تم إصدار بطاقة {_selectedBrand} بنجاح!\n\n" +
                    $"رقم البطاقة: {result?.Card?.MaskedCardNumber ?? "****"}\n" +
                    $"CVV: {result?.Card?.CVV ?? "***"}\n" +
                    $"PIN: {result?.Card?.DefaultPIN ?? "****"}\n\n" +
                    $"⚠️ احتفظ بهذه المعلومات في مكان آمن!\n" +
                    $"لن يتم عرضها مرة أخرى.",
                    "فهمت");

                await Shell.Current.GoToAsync("..");
            }
            else
            {
                // عرض الـ Response الكامل للتشخيص
                System.Diagnostics.Debug.WriteLine($"❌ Error Response: {responseText}");

                try
                {
                    var errorResult = JsonSerializer.Deserialize<Dictionary<string, object>>(responseText,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var message = errorResult?.ContainsKey("message") == true
                        ? errorResult["message"].ToString()
                        : $"فشل إصدار البطاقة: {response.StatusCode}";

                    await DisplayAlert("خطأ", message, "حسناً");
                }
                catch
                {
                    // إذا فشل الـ JSON parsing، اعرض الـ response كما هو
                    await DisplayAlert("خطأ",
                        $"فشل إصدار البطاقة ({response.StatusCode})\n\n" +
                        $"التفاصيل:\n{responseText.Substring(0, Math.Min(200, responseText.Length))}",
                        "حسناً");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("❌ HttpRequestException!");
            System.Diagnostics.Debug.WriteLine($"❌ Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ InnerException: {ex.InnerException?.Message}");
            Console.WriteLine($"❌ HttpRequestException: {ex.Message}");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            await DisplayAlert("خطأ في الاتصال",
                $"فشل الاتصال بالخادم\n\n" +
                $"التفاصيل: {ex.Message}\n" +
                $"Inner: {ex.InnerException?.Message}\n\n" +
                $"العنوان: {Preferences.Get("api_base_url", "غير محدد")}",
                "حسناً");
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("❌ TaskCanceledException (Timeout)!");
            Console.WriteLine("❌ Timeout!");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            await DisplayAlert("خطأ",
                "انتهت مهلة الاتصال (120 ثانية)\n\n" +
                "تأكد من:\n" +
                "✅ API يعمل\n" +
                "✅ عنوان الخادم صحيح\n\n" +
                $"العنوان الحالي: {Preferences.Get("api_base_url", "غير محدد")}",
                "حسناً");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine($"❌ Exception: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"❌ Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
            Console.WriteLine($"❌ Exception: {ex.GetType().Name} - {ex.Message}");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            await DisplayAlert("خطأ",
                $"حدث خطأ:\n\n" +
                $"النوع: {ex.GetType().Name}\n" +
                $"الرسالة: {ex.Message}",
                "حسناً");
        }
        finally
        {
            IssueButton.IsEnabled = true;
            IssueButton.Text = "إصدار البطاقة 💳";
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class IssueCardResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CardDetails? Card { get; set; }
}

public class CardDetails
{
    public string Id { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string MaskedCardNumber { get; set; } = string.Empty;
    public string CVV { get; set; } = string.Empty;
    public string DefaultPIN { get; set; } = string.Empty;
}