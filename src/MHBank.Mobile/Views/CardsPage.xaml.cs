using MHBank.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Text.Json;

namespace MHBank.Mobile.Views;

public partial class CardsPage : ContentPage
{
    private readonly IApiService _apiService;

    public CardsPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    protected override async void OnAppearing()
    {
        System.Diagnostics.Debug.WriteLine("🔵🔵🔵 CardsPage OnAppearing!");
        Console.WriteLine("🔵🔵🔵 CardsPage OnAppearing!");

        base.OnAppearing();

        try
        {
            System.Diagnostics.Debug.WriteLine("🔵 Calling LoadCardsAsync...");
            Console.WriteLine("🔵 Calling LoadCardsAsync...");
            await LoadCardsAsync();
            System.Diagnostics.Debug.WriteLine("✅ LoadCardsAsync completed");
            Console.WriteLine("✅ LoadCardsAsync completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnAppearing Exception: {ex.Message}");
            Console.WriteLine($"❌ OnAppearing Exception: {ex.Message}");
            await DisplayAlert("خطأ في OnAppearing", ex.Message, "OK");
        }
    }

    private async Task LoadCardsAsync()
    {
        // DEBUG: تأكد أن الـ method تُنفّذ
        System.Diagnostics.Debug.WriteLine("🔵🔵🔵 LoadCardsAsync CALLED!");
        Console.WriteLine("🔵🔵🔵 LoadCardsAsync CALLED!");

        try
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("🔵 LoadCardsAsync START");
            Console.WriteLine("🔵 LoadCardsAsync START");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            LoadingLabel.IsVisible = true;
            LoadingLabel.Text = "جاري التحميل... [DEBUG MODE]";
            ContentLayout.IsVisible = false;

            System.Diagnostics.Debug.WriteLine("🔵 Step 1: Getting token...");
            Console.WriteLine("🔵 Step 1: Getting token...");
            var token = await SecureStorage.GetAsync("access_token");

            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("❌ Token is empty!");
                await DisplayAlert("خطأ", "يرجى تسجيل الدخول مرة أخرى", "حسناً");
                LoadingLabel.IsVisible = false;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Token OK: {token.Substring(0, Math.Min(20, token.Length))}...");

            System.Diagnostics.Debug.WriteLine("🔵 Step 2: Creating HttpClient...");
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            System.Diagnostics.Debug.WriteLine("✅ HttpClient created");

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            System.Diagnostics.Debug.WriteLine("✅ Authorization header set");

            System.Diagnostics.Debug.WriteLine("🔵 Step 3: Getting base URL...");
            var baseUrl = Preferences.Get("api_base_url", "http://192.168.1.104:5185");

            // إصلاح: إذا كان URL فارغ أو "غير محدد"
            if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("غير محدد") || baseUrl == "null")
            {
                baseUrl = "http://192.168.1.104:5185";
                Preferences.Set("api_base_url", baseUrl);
                System.Diagnostics.Debug.WriteLine($"⚠️ Fixed empty baseUrl to: {baseUrl}");
            }

            System.Diagnostics.Debug.WriteLine($"✅ Base URL: {baseUrl}");
            System.Diagnostics.Debug.WriteLine($"🔵 Full URL: {baseUrl}/api/Cards");

            System.Diagnostics.Debug.WriteLine("🔵 Step 4: Sending GET request...");
            var response = await httpClient.GetAsync($"{baseUrl}/api/Cards");
            System.Diagnostics.Debug.WriteLine($"✅ Response received!");
            System.Diagnostics.Debug.WriteLine($"🔵 Status Code: {response.StatusCode}");

            System.Diagnostics.Debug.WriteLine("🔵 Step 5: Reading content...");
            var content = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"✅ Content length: {content.Length} chars");
            System.Diagnostics.Debug.WriteLine($"🔵 Content preview: {content.Substring(0, Math.Min(200, content.Length))}...");

            System.Diagnostics.Debug.WriteLine($"🔵 Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"🔵 Body: {content}");

            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("خطأ", $"فشل تحميل البطاقات: {response.StatusCode}\n{content}", "حسناً");
                LoadingLabel.IsVisible = false;
                ContentLayout.IsVisible = true;
                return;
            }

            var result = JsonSerializer.Deserialize<CardsResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            System.Diagnostics.Debug.WriteLine($"🔵 Deserialized: Success={result?.Success}, Cards={result?.Cards?.Count ?? 0}");

            CardsLayout.Children.Clear();

            if (result?.Cards != null && result.Cards.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Found {result.Cards.Count} card(s)");

                foreach (var card in result.Cards)
                {
                    System.Diagnostics.Debug.WriteLine($"  💳 {card.Brand} - {card.MaskedCardNumber} - Active: {card.IsActive}");
                    CardsLayout.Children.Add(CreateCardView(card));
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ لا توجد بطاقات");

                CardsLayout.Children.Add(new Label
                {
                    Text = "لا توجد بطاقات.\nاضغط على 'إصدار بطاقة جديدة' للبدء.",
                    FontSize = 14,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40)
                });
            }

            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("❌ HttpRequestException caught!");
            System.Diagnostics.Debug.WriteLine($"❌ Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ InnerException: {ex.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            await DisplayAlert("خطأ في الاتصال",
                $"فشل الاتصال بالخادم\n\n" +
                $"تأكد من:\n" +
                $"✅ API يعمل\n" +
                $"✅ عنوان الخادم صحيح: {Preferences.Get("api_base_url", "غير محدد")}\n\n" +
                $"التفاصيل: {ex.Message}\n" +
                $"Inner: {ex.InnerException?.Message}",
                "حسناً");
            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("❌ TaskCanceledException (Timeout)");
            System.Diagnostics.Debug.WriteLine($"❌ Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            await DisplayAlert("خطأ", "انتهت مهلة الاتصال (120 ثانية). تأكد من تشغيل API", "حسناً");
            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine($"❌ Exception: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"❌ Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ InnerException: {ex.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════");

            await DisplayAlert("خطأ",
                $"حدث خطأ غير متوقع:\n\n" +
                $"النوع: {ex.GetType().Name}\n" +
                $"الرسالة: {ex.Message}\n" +
                $"Inner: {ex.InnerException?.Message}\n\n" +
                $"يرجى التحقق من Logcat للتفاصيل",
                "حسناً");
            LoadingLabel.IsVisible = false;
            ContentLayout.IsVisible = true;
        }
    }

    private View CreateCardView(CardInfo card)
    {
        var brandColor = card.Brand == "Visa" ? "#1A1F71" : "#EB001B";
        var brandLogo = card.Brand == "Visa" ? "💳 VISA" : "💳 Mastercard";

        var cardBorder = new Border
        {
            BackgroundColor = Color.FromArgb(brandColor),
            StrokeThickness = 0,
            Padding = 20,
            Margin = new Thickness(0, 10),
            HeightRequest = 200,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }
        };

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        // Brand Logo
        var brandLabel = new Label
        {
            Text = brandLogo,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End
        };
        Grid.SetRow(brandLabel, 0);
        grid.Children.Add(brandLabel);

        // Card Number
        var cardNumberLabel = new Label
        {
            Text = card.MaskedCardNumber ?? card.CardNumber,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetRow(cardNumberLabel, 1);
        grid.Children.Add(cardNumberLabel);

        // Bottom Info
        var bottomStack = new HorizontalStackLayout
        {
            Spacing = 30,
            HorizontalOptions = LayoutOptions.Start
        };

        var nameStack = new VerticalStackLayout { Spacing = 2 };
        nameStack.Children.Add(new Label
        {
            Text = "اسم الحامل",
            FontSize = 10,
            TextColor = Color.FromArgb("#CCCCCC")
        });
        nameStack.Children.Add(new Label
        {
            Text = card.CardHolderName,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        bottomStack.Children.Add(nameStack);

        var expiryStack = new VerticalStackLayout { Spacing = 2 };
        expiryStack.Children.Add(new Label
        {
            Text = "تاريخ الانتهاء",
            FontSize = 10,
            TextColor = Color.FromArgb("#CCCCCC")
        });
        expiryStack.Children.Add(new Label
        {
            Text = $"{card.ExpiryMonth}/{card.ExpiryYear}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        bottomStack.Children.Add(expiryStack);

        Grid.SetRow(bottomStack, 2);
        grid.Children.Add(bottomStack);

        cardBorder.Content = grid;

        // Add Toggle and Delete Buttons
        var mainStack = new VerticalStackLayout { Spacing = 10 };
        mainStack.Children.Add(cardBorder);

        var buttonsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 10
        };

        var toggleButton = new Button
        {
            Text = card.IsActive ? "تعطيل 🔒" : "تفعيل ✅",
            BackgroundColor = card.IsActive ? Color.FromArgb("#F44336") : Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 12
        };
        toggleButton.Clicked += async (s, e) => await OnToggleCardAsync(card.Id, card.IsActive);
        Grid.SetColumn(toggleButton, 0);
        buttonsGrid.Children.Add(toggleButton);

        var deleteButton = new Button
        {
            Text = "حذف 🗑️",
            BackgroundColor = Color.FromArgb("#FF5722"),
            TextColor = Colors.White,
            CornerRadius = 12
        };
        deleteButton.Clicked += async (s, e) => await OnDeleteCardAsync(card.Id, card.MaskedCardNumber);
        Grid.SetColumn(deleteButton, 1);
        buttonsGrid.Children.Add(deleteButton);

        mainStack.Children.Add(buttonsGrid);

        return mainStack;
    }

    private async Task OnToggleCardAsync(string cardId, bool currentStatus)
    {
        try
        {
            var confirm = await DisplayAlert(
                "تأكيد",
                currentStatus ? "هل تريد تعطيل هذه البطاقة؟" : "هل تريد تفعيل هذه البطاقة؟",
                "نعم",
                "لا"
            );

            if (!confirm) return;

            var httpClient = new HttpClient();
            var token = await SecureStorage.GetAsync("access_token");
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = Preferences.Get("api_base_url", "http://192.168.1.104:5185");
            var response = await httpClient.PostAsync($"{baseUrl}/api/Cards/{cardId}/toggle", null);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("نجح", currentStatus ? "تم تعطيل البطاقة" : "تم تفعيل البطاقة", "حسناً");
                await LoadCardsAsync();
            }
            else
            {
                await DisplayAlert("خطأ", "فشلت العملية", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ: {ex.Message}", "حسناً");
        }
    }

    private async Task OnDeleteCardAsync(string cardId, string maskedNumber)
    {
        try
        {
            var confirm = await DisplayAlert(
                "تأكيد الحذف",
                $"هل أنت متأكد من حذف البطاقة {maskedNumber}؟\n\n" +
                $"⚠️ هذا الإجراء لا يمكن التراجع عنه!",
                "نعم، احذف",
                "إلغاء"
            );

            if (!confirm) return;

            var httpClient = new HttpClient();
            var token = await SecureStorage.GetAsync("access_token");
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = Preferences.Get("api_base_url", "http://192.168.1.104:5185");
            var response = await httpClient.DeleteAsync($"{baseUrl}/api/Cards/{cardId}");

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("نجح ✅", "تم حذف البطاقة بنجاح", "حسناً");
                await LoadCardsAsync();
            }
            else
            {
                await DisplayAlert("خطأ", "فشل حذف البطاقة", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"حدث خطأ: {ex.Message}", "حسناً");
        }
    }

    private async void OnIssueCardTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(IssueCardPage));
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnRefreshTapped(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("🔄 Refreshing cards...");
        await LoadCardsAsync();
    }
}

public class CardsResponse
{
    public bool Success { get; set; }
    public int TotalCards { get; set; }
    public List<CardInfo> Cards { get; set; } = new();
}

public class CardInfo
{
    public string Id { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string MaskedCardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string AccountNumber { get; set; } = string.Empty;

    // حقول إضافية لـ IssueCardWithNewAccount
    public string? CVV { get; set; }
    public string? DefaultPIN { get; set; }
    public string? Message { get; set; }
}