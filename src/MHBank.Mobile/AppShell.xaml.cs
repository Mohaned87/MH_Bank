using MHBank.Mobile.Views;

namespace MHBank.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // تسجيل الـ Routes
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(TransferPage), typeof(TransferPage));
        Routing.RegisterRoute(nameof(TransactionsHistoryPage), typeof(TransactionsHistoryPage));
        Routing.RegisterRoute(nameof(AccountDetailsPage), typeof(AccountDetailsPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(BillPaymentPage), typeof(BillPaymentPage));
        Routing.RegisterRoute(nameof(KYCPage), typeof(KYCPage));
        Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
        Routing.RegisterRoute(nameof(CardsPage), typeof(CardsPage));
        Routing.RegisterRoute(nameof(IssueCardPage), typeof(IssueCardPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}