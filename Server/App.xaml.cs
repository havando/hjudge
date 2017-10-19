using System;
using System.Threading;
using System.Windows;

namespace Server
{
    /// <summary>
    ///     App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex;

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            _mutex = new Mutex(
                true,
                "hjudge_server",
                out var isSucceed);
            if (!isSucceed)
            {
                MessageBox.Show("本程序已在运行，请勿重复运行", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            foreach (var i in e.Args)
                if (i == "-silent")
                    Configuration.IsHidden = true;
        }
    }
}