using Microsoft.UI.Xaml.Controls;
using DtshPocWinUI.ViewModels;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DtshPocWinUI;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ViewModel.UpdateWindowHandle();
        ViewModel.RefreshStatusCommand.Execute(null);
    }

    public static bool IsNotBusy(bool isBusy)
    {
        return !isBusy;
    }
}
