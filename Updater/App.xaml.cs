using System;
using System.Windows;

namespace Updater
{
    /// <summary>
    ///     App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length == 4)
            {
                UpdateInfo.Product = e.Args[0];
                UpdateInfo.CurrentVersion = e.Args[1];
                UpdateInfo.ProcessId = Convert.ToInt32(e.Args[2]);
                UpdateInfo.RootDirectory = e.Args[3];
            }
            else
            {
                Environment.Exit(0);
            }
        }
    }
}