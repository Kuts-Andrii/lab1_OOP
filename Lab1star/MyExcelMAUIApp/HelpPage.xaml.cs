namespace MyExcelMAUIApp;

public partial class HelpPage : ContentPage
{
    public HelpPage()
    {
        InitializeComponent();
    }

    private async void BackButton_Clicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
