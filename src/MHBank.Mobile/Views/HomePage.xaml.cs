using MHBank.Mobile.Models;
using MHBank.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Text.Json;

namespace MHBank.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;
    private readonly INotificationService _notificationService;
    private List<BankAccount> _allAccounts = new();

    // Default API URL
    private const string DEFAULT_API_URL = "http://192.168.1.105:5185";

    public HomePage(IApiService apiService, IAuthService authService, INotificationService notificationService)
    {
        InitializeComponent();
        _apiService = apiService;
        _authService = authService;
        _notificationService = notificationService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
        await UpdateNotificationBadgeAsync();
        await CheckKYCNotificationsAsync();
    }

    private async Task CheckKYCNotificationsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("🔵 Checking KYC notifications...");

            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var token = await SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("⚠️ No token for KYC notifications");
                return;
            }

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = Preferences.Get("api_base_url", DEFAULT_API_URL);
            if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("غير محدد"))
            {
                baseUrl = DEFAULT_API_URL;
            }
            var url = $"{baseUrl}/api/KYC/notifications";

            System.Diagnostics.Debug.WriteLine($"🔵 GET {url}");

            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"🔵 Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"🔵 Response: {content}");

            if (response.IsSuccessStatusCode)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<KYCNotificationsResponse>(
                    content,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (result?.Notifications != null && result.Notifications.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Found {result.Notifications.Count} KYC notifications");

                    foreach (var notif in result.Notifications)
                    {
                        System.Diagnostics.Debug.WriteLine($"  📬 {notif.Title}: {notif.Message}");

                        await _notificationService.AddNotificationAsync(
                            notif.Title ?? "إشعار",
                            notif.Message ?? "",
                            notif.Type ?? "KYC"
                        );
                    }

                    await UpdateNotificationBadgeAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No KYC notifications");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to get KYC notifications: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error checking KYC notifications: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async Task UpdateNotificationBadgeAsync()
    {
        var unreadCount = await _notificationService.GetUnreadCountAsync();

        if (unreadCount > 0)
        {
            NotificationBadge.IsVisible = true;
            NotificationCount.Text = unreadCount > 9 ? "9+" : unreadCount.ToString();
        }
        else
        {
            NotificationBadge.IsVisible = false;
        }
    }

    private async Task LoadDataAsync()
    {
        LoadingLabel.IsVisible = true;
        ContentLayout.IsVisible = false;

        try
        {
            System.Diagnostics.Debug.WriteLine("🔵 Loading user data...");

            // تحميل معلومات المستخدم
            try
            {
                var user = await _apiService.GetCurrentUserAsync();
                if (user != null)
                {
                    WelcomeLabel.Text = $"مرحباً، {user.FirstName} 👋";
                    System.Diagnostics.Debug.WriteLine($"✅ User: {user.FirstName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No user data");
                    WelcomeLabel.Text = "مرحباً 👋";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetUser failed: {ex.Message}");
                WelcomeLabel.Text = "مرحباً 👋";
            }

            // تحميل الحسابات
            try
            {
                System.Diagnostics.Debug.WriteLine("🔵 Loading accounts...");

                var accountsResponse = await _apiService.GetAccountsAsync();
                if (accountsResponse?.Success == true && accountsResponse.Accounts?.Count > 0)
                {
                    _allAccounts = accountsResponse.Accounts;

                    // ملء Picker
                    AccountPicker.Items.Clear();
                    AccountPicker.Items.Add("كل الحسابات");
                    foreach (var acc in _allAccounts)
                    {
                        var type = acc.AccountType == "Checking" ? "جاري" : "توفير";
                        AccountPicker.Items.Add($"{type} - {acc.AccountNumber}");
                    }
                    AccountPicker.SelectedIndex = 0;

                    TotalBalanceLabel.Text = $"{accountsResponse.TotalBalance:N0} IQD";
                    System.Diagnostics.Debug.WriteLine($"✅ Total Balance: {accountsResponse.TotalBalance:N0}");

                    AccountsLayout.Children.Clear();
                    foreach (var account in accountsResponse.Accounts)
                    {
                        AccountsLayout.Children.Add(CreateAccountCard(account));
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No accounts found");
                    TotalBalanceLabel.Text = "0 IQD";
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("❌ GetAccounts Timeout");
                TotalBalanceLabel.Text = "فشل التحميل";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetAccounts failed: {ex.Message}");
                TotalBalanceLabel.Text = "0 IQD";
            }

            // تحميل المعاملات
            try
            {
                var transactionsResponse = await _apiService.GetRecentTransactionsAsync();
                if (transactionsResponse?.Success == true && transactionsResponse.Transactions?.Count > 0)
                {
                    TransactionsLayout.Children.Clear();
                    foreach (var transaction in transactionsResponse.Transactions.Take(5))
                    {
                        TransactionsLayout.Children.Add(CreateTransactionCard(transaction));
                    }
                    System.Diagnostics.Debug.WriteLine($"✅ Loaded {transactionsResponse.Transactions.Count} transactions");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No transactions");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetTransactions failed: {ex.Message}");
            }

            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ LoadDataAsync Error: {ex.Message}\n{ex.StackTrace}");
            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
            TotalBalanceLabel.Text = "خطأ";
        }
    }

    private Border CreateAccountCard(BankAccount account)
    {
        var accountTypeText = account.AccountType == "Checking" ? "حساب جاري" : "حساب توفير";
        var color = account.AccountType == "Checking" ? "#4A90E2" : "#4CAF50";

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var accountInfo = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = accountTypeText, FontSize = 16, TextColor = Colors.Gray },
                new Label { Text = $"{account.Balance:N0} IQD", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color) },
                new Label { Text = account.AccountNumber, FontSize = 12, TextColor = Colors.Gray }
            }
        };
        Grid.SetColumn(accountInfo, 0);
        grid.Children.Add(accountInfo);

        var arrow = new Label { Text = "→", FontSize = 28, TextColor = Colors.Gray, VerticalOptions = LayoutOptions.Center };
        Grid.SetColumn(arrow, 1);
        grid.Children.Add(arrow);

        return new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = 20,
            Margin = new Thickness(0, 0, 0, 10),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = grid
        };
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
            Content = new Label { Text = icon, FontSize = 24, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        var transactionInfo = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = GetTransactionTypeArabic(transaction.Type), FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black },
                new Label { Text = transaction.CreatedAt.ToString("dd/MM/yyyy"), FontSize = 13, TextColor = Colors.Gray }
            }
        };
        Grid.SetColumn(transactionInfo, 1);
        grid.Children.Add(transactionInfo);

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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("تسجيل الخروج", "هل أنت متأكد؟", "نعم", "لا");
        if (confirm)
        {
            await _authService.LogoutAsync();
            await Shell.Current.GoToAsync("//login");
        }
    }

    private async void OnTransferTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(TransferPage));
    }

    private async void OnViewAllTransactionsTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(TransactionsHistoryPage));
    }

    private async void OnProfileTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ProfilePage));
    }

    private async void OnBillPaymentTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(BillPaymentPage));
    }

    private async void OnKYCTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(KYCPage));
    }

    private async void OnDepositTapped(object sender, EventArgs e)
    {
        await DisplayAlert("قريباً", "ميزة الإيداع ستكون متاحة قريباً", "حسناً");
        // TODO: يمكن إضافة صفحة DepositPage لاحقاً
    }

    private async void OnCardsTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CardsPage));
    }

    private async void OnNotificationsTapped(object sender, EventArgs e)
    {
        var notifications = await _notificationService.GetUnreadNotificationsAsync();

        if (notifications.Count == 0)
        {
            await DisplayAlert("الإشعارات", "لا توجد إشعارات جديدة", "حسناً");
            return;
        }

        var messages = string.Join("\n\n", notifications.Select(n =>
            $"• {n.Title}\n  {n.Message}\n  {n.CreatedAt:dd/MM/yyyy HH:mm}"
        ));

        await DisplayAlert("الإشعارات", messages, "حسناً");

        // وضع علامة مقروء على جميع الإشعارات
        foreach (var notification in notifications)
        {
            await _notificationService.MarkAsReadAsync(notification.Id);
        }

        // تحديث Badge
        await UpdateNotificationBadgeAsync();
    }

    private void OnAccountSelected(object sender, EventArgs e)
    {
        if (AccountPicker.SelectedIndex == 0)
        {
            // كل الحسابات
            var total = _allAccounts.Sum(a => a.Balance);
            TotalBalanceLabel.Text = $"{total:N0} IQD";
        }
        else if (AccountPicker.SelectedIndex > 0 && AccountPicker.SelectedIndex - 1 < _allAccounts.Count)
        {
            // حساب محدد
            var selectedAccount = _allAccounts[AccountPicker.SelectedIndex - 1];
            TotalBalanceLabel.Text = $"{selectedAccount.Balance:N0} IQD";
        }
    }

    private async void OnAddButtonTapped(object sender, EventArgs e)
    {
        var action = await DisplayActionSheet(
            "اختر العملية",
            "إلغاء",
            null,
            "💳 إصدار بطاقة لأول مرة",
            "🏦 إضافة حساب موجود"
        );

        if (action == "💳 إصدار بطاقة لأول مرة")
        {
            await OnIssueFirstCardAsync();
        }
        else if (action == "🏦 إضافة حساب موجود")
        {
            await OnAddExistingAccountAsync();
        }
    }

    private async Task OnIssueFirstCardAsync()
    {
        try
        {
            // اختيار نوع البطاقة
            var brand = await DisplayActionSheet(
                "اختر نوع البطاقة",
                "إلغاء",
                null,
                "💳 Visa",
                "💳 Mastercard"
            );

            if (brand == null || brand == "إلغاء")
                return;

            var selectedBrand = brand.Contains("Visa") ? "Visa" : "Mastercard";
            var brandInt = brand.Contains("Visa") ? 1 : 2;

            var confirm = await DisplayAlert(
                "تأكيد",
                $"سيتم إنشاء حساب جديد وإصدار بطاقة {selectedBrand}\n\n" +
                $"البطاقة ستكون غير مفعلة، ستحتاج لتفعيلها بكلمة سر التطبيق.\n\n" +
                $"هل تريد المتابعة؟",
                "نعم، أصدر",
                "إلغاء"
            );

            if (!confirm)
                return;

            LoadingLabel.IsVisible = true;
            LoadingLabel.Text = "جاري إصدار البطاقة...";
            ContentLayout.IsVisible = false;

            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("🔵 OnIssueFirstCardAsync START");
            Console.WriteLine("🔵 OnIssueFirstCardAsync START");
            System.Diagnostics.Debug.WriteLine($"🔵 Brand: {selectedBrand} (Int: {brandInt})");
            Console.WriteLine($"🔵 Brand: {brandInt}");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            System.Diagnostics.Debug.WriteLine("🔵 Step 1: Getting token...");
            var token = await SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("❌ Token is empty!");
                await DisplayAlert("خطأ", "يرجى تسجيل الدخول مرة أخرى", "حسناً");
                LoadingLabel.IsVisible = false;
                ContentLayout.IsVisible = true;
                return;
            }
            System.Diagnostics.Debug.WriteLine($"✅ Token OK: {token.Substring(0, Math.Min(20, token.Length))}...");
            Console.WriteLine("✅ Token OK");

            System.Diagnostics.Debug.WriteLine("🔵 Step 2: Creating HttpClient...");
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            System.Diagnostics.Debug.WriteLine("✅ HttpClient created");

            System.Diagnostics.Debug.WriteLine("🔵 Step 3: Getting base URL...");

            // استخدام DEFAULT_API_URL مباشرة
            string baseUrl = DEFAULT_API_URL;

            // محاولة القراءة من Preferences
            try
            {
                var savedUrl = Preferences.Get("api_base_url", "");
                if (!string.IsNullOrEmpty(savedUrl) &&
                    !savedUrl.Contains("غير محدد") &&
                    savedUrl != "null")
                {
                    baseUrl = savedUrl;
                    System.Diagnostics.Debug.WriteLine($"✅ Using saved URL: {baseUrl}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Using default URL: {baseUrl}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Preferences failed: {ex.Message}. Using default.");
                baseUrl = DEFAULT_API_URL;
            }

            Console.WriteLine($"✅ Final URL: {baseUrl}");
            System.Diagnostics.Debug.WriteLine($"✅ Base URL: {baseUrl}");

            System.Diagnostics.Debug.WriteLine("🔵 Step 4: Building request...");
            var request = new { Brand = brandInt };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            System.Diagnostics.Debug.WriteLine($"✅ Request: {json}");
            Console.WriteLine($"✅ Request: Brand={brandInt}");

            System.Diagnostics.Debug.WriteLine($"🔵 Step 5: Sending POST to {baseUrl}/api/Cards/issue-with-new-account");
            Console.WriteLine($"🔵 Step 5: POST /api/Cards/issue-with-new-account");

            var response = await httpClient.PostAsync($"{baseUrl}/api/Cards/issue-with-new-account", content);
            System.Diagnostics.Debug.WriteLine($"✅ Response received: {response.StatusCode}");
            Console.WriteLine($"✅ Response: {response.StatusCode}");

            var responseText = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"✅ Response text length: {responseText.Length}");
            System.Diagnostics.Debug.WriteLine($"🔵 Response content: {responseText}");
            Console.WriteLine($"🔵 Response preview: {responseText.Substring(0, Math.Min(200, responseText.Length))}");

            if (response.IsSuccessStatusCode)
            {
                // التحقق من أن Response ليس فارغ
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    System.Diagnostics.Debug.WriteLine("❌ Response is empty!");
                    await DisplayAlert("خطأ", "استجابة فارغة من الخادم", "حسناً");
                    LoadingLabel.IsVisible = false;
                    ContentLayout.IsVisible = true;
                    return;
                }

                try
                {
                    var result = JsonSerializer.Deserialize<IssueCardWithAccountResponse>(
                        responseText,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (result?.Card != null)
                    {
                        await DisplayAlert(
                            "نجح ✅",
                            $"تم إنشاء الحساب وإصدار البطاقة بنجاح!\n\n" +
                            $"رقم الحساب: {result.Account?.AccountNumber}\n" +
                            $"رقم البطاقة: {result.Card.MaskedCardNumber}\n" +
                            $"CVV: {result.Card.CVV}\n" +
                            $"PIN: {result.Card.DefaultPIN}\n\n" +
                            $"⚠️ البطاقة غير مفعلة. انتقل إلى 'بطاقاتي' لتفعيلها.",
                            "حسناً"
                        );

                        // إعادة تحميل البيانات
                        await LoadDataAsync();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("❌ Result or Card is null");
                        await DisplayAlert("خطأ", "فشل في معالجة الاستجابة", "حسناً");
                    }
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ JSON Error: {jsonEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"❌ Response was: {responseText}");
                    await DisplayAlert("خطأ في JSON",
                        $"فشل في تحليل الاستجابة\n\n" +
                        $"الخطأ: {jsonEx.Message}\n\n" +
                        $"الاستجابة: {responseText.Substring(0, Math.Min(300, responseText.Length))}",
                        "حسناً");
                }

                LoadingLabel.IsVisible = false;
                ContentLayout.IsVisible = true;
            }
            else
            {
                var errorResult = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    responseText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                var message = errorResult?.ContainsKey("message") == true
                    ? errorResult["message"].ToString()
                    : $"فشل إصدار البطاقة: {response.StatusCode}";

                await DisplayAlert("خطأ", message, "حسناً");
            }

            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
        catch (TaskCanceledException)
        {
            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
            await DisplayAlert("خطأ", "انتهت مهلة الاتصال (120 ثانية)", "حسناً");
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ HttpRequestException: {ex.Message}");
            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;

            // استخدام URL من المتغير المحلي
            var currentUrl = Preferences.Get("api_base_url", "http://192.168.1.105:5185");

            await DisplayAlert("خطأ في الاتصال",
                $"فشل الاتصال بالخادم\n\n" +
                $"العنوان المستخدم: {currentUrl}\n\n" +
                $"التفاصيل: {ex.Message}",
                "حسناً");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
            await DisplayAlert("خطأ",
                $"حدث خطأ:\n\n" +
                $"النوع: {ex.GetType().Name}\n" +
                $"الرسالة: {ex.Message}",
                "حسناً");
        }
    }

    private async Task OnAddExistingAccountAsync()
    {
        // الخيار الثاني: إضافة حساب موجود
        var accountNumber = await DisplayPromptAsync(
            "إضافة حساب",
            "أدخل رقم الحساب:",
            placeholder: "مثال: 1001234567890",
            keyboard: Keyboard.Numeric,
            maxLength: 13
        );

        if (string.IsNullOrWhiteSpace(accountNumber))
            return;

        // TODO: Implement add existing account
        await DisplayAlert("قريباً", "هذه الميزة قيد التطوير", "حسناً");
    }
}

public class KYCNotificationsResponse
{
    public bool Success { get; set; }
    public List<KYCNotificationItem>? Notifications { get; set; }
}

public class KYCNotificationItem
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public string? Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}

public class IssueCardWithAccountResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public IssueAccountInfo? Account { get; set; }
    public CardInfo? Card { get; set; }
}

public class IssueAccountInfo
{
    public string? Id { get; set; }
    public string? AccountNumber { get; set; }
    public string? IBAN { get; set; }
    public string? AccountType { get; set; }
    public decimal Balance { get; set; }
    public string? Currency { get; set; }
}