using MHBank.Mobile.Services;

namespace MHBank.Mobile.Views;

public partial class KYCPage : ContentPage
{
    private readonly IApiService _apiService;
    private bool _idFrontUploaded = false;
    private bool _idBackUploaded = false;
    private bool _selfieUploaded = false;

    public KYCPage(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    private async void OnUploadIDFrontTapped(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "اختر صورة البطاقة (الوجه الأمامي)"
            });

            if (result != null)
            {
                _idFrontUploaded = true;
                IDFrontStatusLabel.Text = "✅ تم الرفع";
                IDFrontStatusLabel.TextColor = Colors.Green;
                CheckAllUploaded();

                await DisplayAlert("نجح", "تم رفع الصورة بنجاح", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل رفع الصورة: {ex.Message}", "حسناً");
        }
    }

    private async void OnUploadIDBackTapped(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "اختر صورة البطاقة (الوجه الخلفي)"
            });

            if (result != null)
            {
                _idBackUploaded = true;
                IDBackStatusLabel.Text = "✅ تم الرفع";
                IDBackStatusLabel.TextColor = Colors.Green;
                CheckAllUploaded();

                await DisplayAlert("نجح", "تم رفع الصورة بنجاح", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل رفع الصورة: {ex.Message}", "حسناً");
        }
    }

    private async void OnUploadSelfieTapped(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "التقط صورة شخصية"
            });

            if (result == null)
            {
                // Try picking from gallery if camera fails
                result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "اختر صورة شخصية"
                });
            }

            if (result != null)
            {
                _selfieUploaded = true;
                SelfieStatusLabel.Text = "✅ تم الرفع";
                SelfieStatusLabel.TextColor = Colors.Green;
                CheckAllUploaded();

                await DisplayAlert("نجح", "تم رفع الصورة بنجاح", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("خطأ", $"فشل رفع الصورة: {ex.Message}", "حسناً");
        }
    }

    private void CheckAllUploaded()
    {
        if (_idFrontUploaded && _idBackUploaded && _selfieUploaded)
        {
            SubmitButton.IsEnabled = true;
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "تأكيد الإرسال",
            "هل أنت متأكد من إرسال المستندات للمراجعة؟",
            "نعم",
            "لا"
        );

        if (confirm)
        {
            SubmitButton.IsEnabled = false;
            SubmitButton.Text = "جاري الإرسال...";

            // Simulate API call
            await Task.Delay(2000);

            StatusLabel.Text = "قيد المراجعة";
            StatusLabel.TextColor = Color.FromArgb("#FF9800");
            StatusIcon.Text = "⏳";

            await DisplayAlert(
                "تم الإرسال",
                "تم إرسال مستنداتك للمراجعة. سيتم إعلامك بالنتيجة خلال 24-48 ساعة.",
                "حسناً"
            );

            await Shell.Current.GoToAsync("..");
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
