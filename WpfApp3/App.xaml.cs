using System.Configuration;
using System.Data;
using System.Windows;

namespace WpfApp3
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            WpfApp3.Services.ThemeService.Current.Initialize();
            base.OnStartup(e);
        }
    }

}
