using System;
using System.IO;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;

namespace Client
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string Divpar = "<h~|~j>";
        public MainWindow()
        {
            var mutex = new Mutex(
                true,
                "hjudge_client",
                out bool isSucceed);
            if (!isSucceed)
            {
                MessageBox.Show("本程序已在运行，请勿重复运行", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            try
            {
                if (!Directory.Exists(Environment.CurrentDirectory + "\\AppData"))
                {
                    Directory.CreateDirectory(Environment.CurrentDirectory + "\\AppData");
                }
                if (Environment.Is64BitProcess)
                {
                    File.Copy(Environment.CurrentDirectory + "\\x64\\HPSocket4C_U.dll",
                        Environment.CurrentDirectory + "\\HPSocket4C_U.dll", true);
                }
                else
                {
                    File.Copy(Environment.CurrentDirectory + "\\x86\\HPSocket4C_U.dll",
                        Environment.CurrentDirectory + "\\HPSocket4C_U.dll", true);
                }
            }
            catch
            {
                MessageBox.Show("程序初始化失败", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            InitializeComponent();
            Configuration.Init();
            if (string.IsNullOrEmpty(Configuration.Configurations.Ip))
            {
                MessageBox.Show("尚未配置服务端地址", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            else
            {
                if (Connection.Init(Configuration.Configurations.Ip, Configuration.Configurations.Port,
                    UpdateMainPage)) return;
                MessageBox.Show("程序初始化失败，请检查网络", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoginGrid.Margin = new Thickness(61, 32, 0, 0);
            CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility = JudgeResult.Visibility = GetFiles.Visibility = ContentGrid.Visibility = Visibility.Hidden;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Connection.SendData("Login", UserName.Text + Divpar + Password.Password);
        }

        private void UpdateMainPage(string s)
        {
            try
            {
                var info = s.Split(new[] { Divpar }, StringSplitOptions.None);
                var header = info[0];
                var content = "";
                for (var i = 1; i < info.Length; i++)
                {
                    if (i != info.Length - 1)
                    {
                        content += info[i] + Divpar;
                    }
                    else
                    {
                        content += info[i];
                    }
                }
                switch (header)
                {
                    case "Login":
                        {
                            if (content == "Succeed")
                            {
                                Dispatcher.BeginInvoke((Action)(() =>
                               {
                                   Password.Password = "";
                                   CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility =
                                       JudgeResult.Visibility =
                                           GetFiles.Visibility = ContentGrid.Visibility = Visibility.Visible;
                                   LoginGrid.Visibility = Visibility.Hidden;
                               }));
                            }
                            else
                            {
                                MessageBox.Show("登录失败", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            break;
                        }
                    case "Logout":
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility = JudgeResult.Visibility = GetFiles.Visibility = ContentGrid.Visibility = Visibility.Hidden;
                                LoginGrid.Visibility = Visibility.Visible;
                            }));
                            break;
                        }
                    case "Messaging":
                        {
                            var x = new Messaging();
                            x.SetMessge(content);
                            x.Show();
                            break;
                        }
                    case "FileList":
                        {
                            break;
                        }
                    case "JudgeResult":
                        {
                            break;
                        }
                    case "ProblemList":
                        {
                            break;
                        }
                    case "Profile":
                        {
                            var x = (UserInfo)JsonConvert.DeserializeObject(content);
                            break;
                        }
                    case "UpdateProfile":
                        {
                            break;
                        }
                    case "ChangePassword":
                        {
                            switch (content)
                            {
                                case "Succeed":
                                    MessageBox.Show("密码修改成功", "提示", MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                    break;
                                case "Failed":
                                    MessageBox.Show("密码修改失败", "提示", MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                    break;
                            }
                            break;
                        }
                }

            }
            catch
            {
                //ignored
            }
        }
    }
}
