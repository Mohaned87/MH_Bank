using Microsoft.Extensions.Logging;
using MHBank.Mobile.Services;
using MHBank.Mobile.Views;
using CommunityToolkit.Maui;

namespace MHBank.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<IApiService, ApiService>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IStorageService, StorageService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();

        // Views
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<TransferPage>();
        builder.Services.AddTransient<TransactionsHistoryPage>();
        builder.Services.AddTransient<AccountDetailsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<BillPaymentPage>();
        builder.Services.AddTransient<KYCPage>();
        builder.Services.AddTransient<ChangePasswordPage>();
        builder.Services.AddTransient<CardsPage>();
        builder.Services.AddTransient<IssueCardPage>();
        builder.Services.AddTransient<SettingsPage>();



#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}