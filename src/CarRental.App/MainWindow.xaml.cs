using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CarRental.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        blazorWebView.BlazorWebViewInitializing += (s, e) =>
        {
            try
            {
                e.UserDataFolder = CarRental.Infrastructure.Helpers.AppPaths.WebView2DataDirectory;
            }
            catch { }
        };

        blazorWebView.BlazorWebViewInitialized += async (s, e) =>
        {
            try
            {
                blazorWebView.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(246, 243, 235);
                if (blazorWebView.WebView?.CoreWebView2 != null)
                {
                    await blazorWebView.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                        Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.DiskCache);
                }
            }
            catch { }
        };

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (blazorWebView?.WebView != null)
            {
                blazorWebView.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(246, 243, 235);
            }
        }
        catch { }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        try
        {
            blazorWebView?.WebView?.Dispose();
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            blazorWebView?.WebView?.Dispose();
        }
        catch { }

        try
        {
            Application.Current?.Shutdown();
        }
        catch { }
    }
}
