namespace MHBank.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // التحقق من تسجيل الدخول
        MainPage = new AppShell();
    }
}