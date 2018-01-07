using System;
using System.IO;
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

            try
            {
                if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\AppData"))
                    Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\AppData");
                if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Files"))
                    Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\Files");
                if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Data"))
                    Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\Data");
                if (Environment.Is64BitProcess)
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\x64\\HPSocket4C_U.dll",
                        AppDomain.CurrentDomain.BaseDirectory + "\\HPSocket4C_U.dll", true);
                else
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\x86\\HPSocket4C_U.dll",
                        AppDomain.CurrentDomain.BaseDirectory + "\\HPSocket4C_U.dll", true);
            }
            catch
            {
                MessageBox.Show("程序初始化失败", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            JudgeHelper.Init();
        }
    }
}