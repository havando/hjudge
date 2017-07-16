using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Client
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string Divpar = "<h~|~j>";
        private string _userName;
        private int _coins = 0, _experience = 0;

        public readonly ObservableCollection<Problem> Problems = new ObservableCollection<Problem>();

        public string SelectedString { get; set; }

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
            MyProblemList.ItemsSource = Problems;

            Configuration.Init();
            if (string.IsNullOrEmpty(Configuration.Configurations.Ip))
            {
                var hostIp = Dns.GetHostAddresses(Dns.GetHostName());
                var flag = false;
                foreach (var t in hostIp)
                {
                    if (t.ToString().Contains(":"))
                    {
                        continue;
                    }
                    Configuration.Configurations.Ip = t.ToString();
                    Configuration.Configurations.Port = 23333;
                    flag = true;
                    break;
                }
                if (!flag)
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
            CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility =
                JudgeResult.Visibility = GetFiles.Visibility = ContentGrid.Visibility = Visibility.Hidden;

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
                    case "Connection":
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                LoginButton.IsEnabled = true;
                            }));
                            break;
                        }
                    case "Login":
                        {
                            if (content == "Succeed")
                            {
                                Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    _userName = UserName.Text;
                                    Password.Password = "";
                                    CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility =
                                        JudgeResult.Visibility =
                                            GetFiles.Visibility = ContentGrid.Visibility = Visibility.Visible;
                                    LoginGrid.Visibility = Visibility.Hidden;
                                    Connection.SendData("RequestProfile", _userName);
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
                            _userName = string.Empty;
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility = JudgeResult.Visibility = GetFiles.Visibility = ContentGrid.Visibility = Visibility.Hidden;
                                LoginGrid.Visibility = Visibility.Visible;
                                OldPassword.Password = NewPassword.Password = ConfirmPassword.Password = string.Empty;
                                AchienementBox.Items.Clear();
                                Experience.Content = Coins.Content = "0";
                                Level.Content = "-";
                                WelcomeLabel.Content = "你好，";
                                Identity.Content = "身份：";
                                CodeBox.Text = string.Empty;
                            }));
                            break;
                        }
                    case "Messaging":
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                           {
                               var x = new Messaging();
                               x.SetMessge(content);
                               x.Show();
                           }));
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
                            var x = JsonConvert.DeserializeObject<Problem[]>(content);
                            UpdateProblemList(x);
                            break;
                        }
                    case "Profile":
                        {
                            var x = JsonConvert.DeserializeObject<UserInfo>(content);
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                WelcomeLabel.Content = $"你好，{x.UserName}";
                                Identity.Content = $"身份：{x.Type2}";
                                UserIcon.Source = ByteImageConverter.ByteToImage(!string.IsNullOrEmpty(x.Icon) ? Convert.FromBase64String(x.Icon) : Convert.FromBase64String(Properties.Resources.default_user_icon_string));
                                Coins.Content = _coins = x.Coins;
                                Experience.Content = _experience = x.Experience;
                                foreach (var i in x.Achievement.Split('|'))
                                {
                                    if (!string.IsNullOrEmpty(i))
                                    {
                                        AchienementBox.Items.Add(new Label { Content = i });
                                    }
                                }
                                if (x.Experience >= 1048576)
                                {
                                    Level.Content = "最强王者";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level6));
                                }
                                else if (x.Experience >= 524288)
                                {
                                    Level.Content = "璀璨钻石 Lev.3";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
                                }
                                else if (x.Experience >= 262144)
                                {
                                    Level.Content = "璀璨钻石 Lev.2";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
                                }
                                else if (x.Experience >= 131072)
                                {
                                    Level.Content = "璀璨钻石 Lev.1";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
                                }
                                else if (x.Experience >= 65536)
                                {
                                    Level.Content = "华贵铂金 Lev.3";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
                                }
                                else if (x.Experience >= 32768)
                                {
                                    Level.Content = "华贵铂金 Lev.2";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
                                }
                                else if (x.Experience >= 16384)
                                {
                                    Level.Content = "华贵铂金 Lev.1";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
                                }
                                else if (x.Experience >= 8192)
                                {
                                    Level.Content = "荣耀黄金 Lev.3";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
                                }
                                else if (x.Experience >= 4096)
                                {
                                    Level.Content = "荣耀黄金 Lev.2";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
                                }
                                else if (x.Experience >= 2048)
                                {
                                    Level.Content = "荣耀黄金 Lev.1";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
                                }
                                else if (x.Experience >= 1024)
                                {
                                    Level.Content = "不屈白银 Lev.3";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
                                }
                                else if (x.Experience >= 512)
                                {
                                    Level.Content = "不屈白银 Lev.2";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
                                }
                                else if (x.Experience >= 256)
                                {
                                    Level.Content = "不屈白银 Lev.1";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
                                }
                                else if (x.Experience >= 128)
                                {
                                    Level.Content = "英勇黄铜 Lev.3";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
                                }
                                else if (x.Experience >= 64)
                                {
                                    Level.Content = "英勇黄铜 Lev.2";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
                                }
                                else if (x.Experience >= 32)
                                {
                                    Level.Content = "英勇黄铜 Lev.1";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
                                }
                                else if (x.Experience >= 16)
                                {
                                    Level.Content = "一只辣鸡 Lev.3";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
                                }
                                else if (x.Experience >= 8)
                                {
                                    Level.Content = "一只辣鸡 Lev.2";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
                                }
                                else if (x.Experience >= 4)
                                {
                                    Level.Content = "一只辣鸡 Lev.1";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
                                }
                                else
                                {
                                    Level.Content = "蒟蒻来袭";
                                    LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.nolevel));
                                }
                            }));
                            break;
                        }
                    case "UpdateProfile":
                        {
                            switch (content)
                            {
                                case "Succeed":
                                    MessageBox.Show("修改成功", "提示", MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                    break;
                                case "Failed":
                                    MessageBox.Show("修改失败", "提示", MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                    Connection.SendData("RequestProfile", _userName);
                                    break;
                            }
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

        private void UpdateProblemList(Problem[] x)
        {
            Dispatcher.Invoke(() =>
            {
                Problems.Clear();
                foreach (var i in x)
                {
                    Problems.Add(i);
                }
            });
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Connection.SendData("Logout", string.Empty);
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            BonusGrid.Visibility = Visibility.Hidden;
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            BonusGrid.Visibility = Visibility.Visible;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(_userName))
            {
                Connection.SendData("Logout", string.Empty);
            }
        }

        private void UserIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var ofg = new OpenFileDialog
            {
                Title = "选择一张图片：",
                Filter = "图片文件 (.jpg, .png, .gif, .bmp)|*.jpg;*.png;*.gif;*.bmp",
                Multiselect = false
            };
            if (ofg.ShowDialog() == true)
            {
                if (!string.IsNullOrEmpty(ofg.FileName))
                {
                    ofg.OpenFile();
                    var fs = new FileStream(ofg.FileName, FileMode.Open, FileAccess.Read);
                    var icon = ByteImageConverter.ImageToByte(fs);
                    UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(icon));
                    Connection.SendData("UpdateProfile", _userName + Divpar + icon);
                }
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (NewPassword.Password == ConfirmPassword.Password && !string.IsNullOrEmpty(NewPassword.Password))
            {
                Connection.SendData("ChangePassword",
                    OldPassword.Password + Divpar + NewPassword.Password);
                NewPassword.Password = string.Empty;
                ConfirmPassword.Password = string.Empty;
                OldPassword.Password = string.Empty;
            }
            else
            {
                MessageBox.Show("密码修改失败", "提示", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void ProblemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var x = MyProblemList.SelectedItem as Problem;
            ProblemInfomationList.Items.Clear();
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目 ID：{x?.ProblemId}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目名称：{x?.ProblemName}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目难度：{x?.Level}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"数据组数：{x?.DataSets.Length}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目总分：{x?.DataSets.Sum(i => i.Score)}" });
            InputFileName.Text = x?.InputFileName;
            OutFileName.Text = x?.OutputFileName;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Connection.SendData("RequestProblemList", string.Empty);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.Source is TabControl)) return;
            if ((sender as TabControl)?.SelectedIndex == 1)
            {
                Connection.SendData("RequestProblemList", string.Empty);
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(MessageContent.Text))
            {
                Connection.SendMsg(MessageContent.Text);
                MessageContent.Text = string.Empty;
            }
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            var x = MyProblemList.SelectedItem as Problem;
            if (x == null) return;
            if (!string.IsNullOrEmpty(CodeBox.Text))
            {
                Connection.SendData("SubmitCode", x.ProblemId + Divpar + CodeBox.Text);
                CodeBox.Text = string.Empty;
            }
        }

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_experience <= 233)
            {
                MessageBox.Show("经验不足，达到 233 后再来吧", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (_coins >= 200)
            {
                if (MessageBox.Show("操此作将花费您 200 金币，确定继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    _coins -= 200;
                    Coins.Content = _coins;
                    Connection.SendData("UpdateCoins", "-200");
                    Connection.SendData("RequestProblemDataSet", (MyProblemList.SelectedItem as Problem)?.ProblemId.ToString());
                }
            }
            else
            {
                MessageBox.Show("金币不足，无法购买", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
