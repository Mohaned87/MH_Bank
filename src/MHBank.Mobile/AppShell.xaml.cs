using MHBank.Mobile.Views;

namespace MHBank.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // تسجيل الـ Routes
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
        Routing.RegisterRoute(nameof(TransferPage), typeof(TransferPage)); 
        Routing.RegisterRoute(nameof(TransactionsHistoryPage), typeof(TransactionsHistoryPage));
        Routing.RegisterRoute(nameof(AccountDetailsPage), typeof(AccountDetailsPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));

    }
}