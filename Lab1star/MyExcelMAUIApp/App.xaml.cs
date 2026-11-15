namespace MyExcelMAUIApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell())
        {
            Title = "MyExcel — Куць Андрій, К-28",
            Width = 1280,
            Height = 800,
            MinimumWidth = 800,
            MinimumHeight = 500,
        };
}
