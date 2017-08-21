using System.Windows;

namespace Server
{
    /// <summary>
    ///     App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            foreach (var i in e.Args)
                if (i == "-silent")
                    Configuration.IsHidden = true;
        }
    }
}