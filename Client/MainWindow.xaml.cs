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
        private int _coins, _experience, _currentGetJudgeRecordIndex;

        public readonly ObservableCollection<Problem> Problems = new ObservableCollection<Problem>();
        public readonly ObservableCollection<JudgeInfo> JudgeInfos = new ObservableCollection<JudgeInfo>();
        public readonly ObservableCollection<Message> MessagesCollection = new ObservableCollection<Message>();
        public readonly ObservableCollection<FileInfomation> FileInfomations = new ObservableCollection<FileInfomation>();
        private Random _random;
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
            var tick = DateTime.Now.Ticks;
            _random = new Random((int)(tick & 0xffffffffL) | (int)(tick >> 32));
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
            JudgeList.ItemsSource = JudgeInfos;
            MessageList.ItemsSource = MessagesCollection;
            FileList.ItemsSource = FileInfomations;

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
                            switch (content)
                            {
                                case "Connected":
                                    {
                                        Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            LoginButton.IsEnabled = true;
                                        }));
                                        break;
                                    }
                                case "Break":
                                    {
                                        Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility = JudgeResult.Visibility = GetFiles.Visibility = ContentGrid.Visibility = Visibility.Hidden;
                                            LoginGrid.Visibility = Visibility.Visible;
                                            OldPassword.Password = NewPassword.Password = ConfirmPassword.Password = string.Empty;
                                            ActiveBox.Items.Clear();
                                            JudgeInfos.Clear();
                                            MessagesCollection.Clear();
                                            Experience.Content = Coins.Content = "0";
                                            Level.Content = "-";
                                            WelcomeLabel.Content = "你好，";
                                            Identity.Content = "身份：";
                                            CodeBox.Text = string.Empty;
                                            _coins = _experience = _currentGetJudgeRecordIndex = 0;
                                            LoginButton.IsEnabled = false;
                                            TabControl.SelectedIndex = 0;
                                        }));
                                        break;
                                    }
                            }
                            break;
                        }
                    case "Login":
                        {
                            if (content == "Succeed")
                            {
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    _userName = UserName.Text;
                                    Password.Password = string.Empty;
                                    _coins = _experience = _currentGetJudgeRecordIndex = 0;
                                    CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility =
                                        JudgeResult.Visibility =
                                            GetFiles.Visibility = ContentGrid.Visibility = Visibility.Visible;
                                    LoginGrid.Visibility = Visibility.Hidden;
                                    Connection.SendData("RequestProfile", _userName);
                                    Connection.SendData("RequestJudgeRecord", $"0{Divpar}20");
                                    Connection.SendData("RequestFileList", string.Empty);
                                    _currentGetJudgeRecordIndex = 20;
                                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {_userName} 登录" });
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
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility = JudgeResult.Visibility = GetFiles.Visibility = ContentGrid.Visibility = Visibility.Hidden;
                                LoginGrid.Visibility = Visibility.Visible;
                                OldPassword.Password = NewPassword.Password = ConfirmPassword.Password = string.Empty;
                                ActiveBox.Items.Clear();
                                JudgeInfos.Clear();
                                MessagesCollection.Clear();
                                Experience.Content = Coins.Content = "0";
                                Level.Content = "-";
                                WelcomeLabel.Content = "你好，";
                                Identity.Content = "身份：";
                                CodeBox.Text = string.Empty;
                                _coins = _experience = _currentGetJudgeRecordIndex = 0;
                                TabControl.SelectedIndex = 0;
                            }));
                            break;
                        }
                    case "Messaging":
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 收到消息" });
                                MessagesCollection.Insert(0, new Message
                                {
                                    Content = content,
                                    Direction = "接收",
                                    MessageTime = DateTime.Now
                                });
                                var x = new Messaging();
                                x.SetMessge(content, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff"));
                                x.Show();
                            }));
                            break;
                        }
                    case "FileList":
                        {
                            var final = content.Split(new[] { Divpar }, StringSplitOptions.None);
                            if (final.Length < 2) break;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                FileInfomations.Clear();
                                CurrentLocation.Text = final[0];
                                var flag = true;
                                for (var i = 1; i < final.Length; i++)
                                {
                                    if (final[i] == "|")
                                    {
                                        flag = !flag;
                                        continue;
                                    }
                                    FileInfomations.Add(new FileInfomation
                                    {
                                        Type = flag ? "文件夹" : "文件",
                                        Name = final[i]
                                    });
                                }
                            }));
                            break;
                        }
                    case "JudgeResult":
                        {
                            var p = JsonConvert.DeserializeObject<JudgeInfo>(content);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 收到评测结果：{p.ResultSummery}" });
                                if (p.ResultSummery == "Accept")
                                {
                                    var delta = 4 + _random.Next() % 32;
                                    var delta2 = 16 + _random.Next() % 8;
                                    Connection.SendData("UpdateExperience", delta.ToString());
                                    _experience += delta;
                                    Connection.SendData("UpdateCoins", delta2.ToString());
                                    _coins += delta2;
                                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +{delta2}，经验 +{delta}" });
                                }
                                else if (p.ResultSummery.Contains("Excceed"))
                                {
                                    var delta = 2 + _random.Next() % 16;
                                    var delta2 = 8 + _random.Next() % 4;
                                    Connection.SendData("UpdateCoins", delta.ToString());
                                    _coins += delta;
                                    Connection.SendData("UpdateExperience", delta2.ToString());
                                    _experience += delta2;
                                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +{delta}，经验 +{delta2}" });
                                }
                                else
                                {
                                    var delta = 1 + _random.Next() % 4;
                                    Connection.SendData("UpdateExperience", delta.ToString());
                                    _experience += delta;
                                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 经验 +{delta}" });
                                }
                                JudgeInfos.Insert(0, p);
                                Coins.Content = _coins;
                                Experience.Content = _experience;
                                SetLevel(_experience);
                            }));
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
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                WelcomeLabel.Content = $"你好，{x.UserName}";
                                Identity.Content = $"身份：{x.Type2}";
                                UserIcon.Source = ByteImageConverter.ByteToImage(!string.IsNullOrEmpty(x.Icon) ? Convert.FromBase64String(x.Icon) : Convert.FromBase64String(Properties.Resources.default_user_icon_string));
                                Coins.Content = _coins = x.Coins;
                                Experience.Content = _experience = x.Experience;
                                SetLevel(x.Experience);
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
                    case "JudgeRecord":
                        {
                            var final = content.Split(new[] { Divpar }, StringSplitOptions.None);
                            if (final.Length < 3) break;
                            _currentGetJudgeRecordIndex = Convert.ToInt32(final[0]) + Convert.ToInt32(final[1]);
                            if (Convert.ToInt32(final[1]) != 20)
                            {
                                _currentGetJudgeRecordIndex = -1;
                            }
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                foreach (var i in JsonConvert.DeserializeObject<JudgeInfo[]>(final[2]))
                                {
                                    JudgeInfos.Add(i);
                                }
                            }));
                            break;
                        }
                    case "JudgeCode":
                        {
                            var jc = JsonConvert.DeserializeObject<JudgeInfo>(content);
                            var j = (from c in JudgeInfos where c.JudgeId == jc.JudgeId select c)
                                .FirstOrDefault();
                            if (j == null) break;
                            j.Code = jc.Code;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ShowJudgeDetails(j);
                            }));
                            break;
                        }
                    case "FileReceived":
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                FileList.IsEnabled = true;
                                ReceivingFile.Visibility = Visibility.Hidden;
                            }));
                            break;
                        }
                    case "ProblemDataSet":
                        {
                            if (content != "Denied") break;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                _coins += 500;
                                Coins.Content = _coins;
                                ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +500" });
                                MessageBox.Show("抱歉，系统设定不允许获取题目数据，请联系管理员。金币已为您加回。", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            }));

                            Connection.SendData("UpdateCoins", "500");
                            break;
                        }
                }

            }
            catch
            {
                //ignored
            }
        }

        private void SetLevel(int experience)
        {
            if (experience >= 1048576)
            {
                Level.Content = "最强王者";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level6));
            }
            else if (experience >= 524288)
            {
                Level.Content = "璀璨钻石 Lev.3";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
            }
            else if (experience >= 262144)
            {
                Level.Content = "璀璨钻石 Lev.2";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
            }
            else if (experience >= 131072)
            {
                Level.Content = "璀璨钻石 Lev.1";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
            }
            else if (experience >= 65536)
            {
                Level.Content = "华贵铂金 Lev.3";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
            }
            else if (experience >= 32768)
            {
                Level.Content = "华贵铂金 Lev.2";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
            }
            else if (experience >= 16384)
            {
                Level.Content = "华贵铂金 Lev.1";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
            }
            else if (experience >= 8192)
            {
                Level.Content = "荣耀黄金 Lev.3";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
            }
            else if (experience >= 4096)
            {
                Level.Content = "荣耀黄金 Lev.2";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
            }
            else if (experience >= 2048)
            {
                Level.Content = "荣耀黄金 Lev.1";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
            }
            else if (experience >= 1024)
            {
                Level.Content = "不屈白银 Lev.3";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
            }
            else if (experience >= 512)
            {
                Level.Content = "不屈白银 Lev.2";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
            }
            else if (experience >= 256)
            {
                Level.Content = "不屈白银 Lev.1";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
            }
            else if (experience >= 128)
            {
                Level.Content = "英勇黄铜 Lev.3";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
            }
            else if (experience >= 64)
            {
                Level.Content = "英勇黄铜 Lev.2";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
            }
            else if (experience >= 32)
            {
                Level.Content = "英勇黄铜 Lev.1";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
            }
            else if (experience >= 16)
            {
                Level.Content = "一只辣鸡 Lev.3";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
            }
            else if (experience >= 8)
            {
                Level.Content = "一只辣鸡 Lev.2";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
            }
            else if (experience >= 4)
            {
                Level.Content = "一只辣鸡 Lev.1";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
            }
            else
            {
                Level.Content = "蒟蒻来袭";
                LevelImage.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.nolevel));
            }
        }

        private void ShowJudgeDetails(JudgeInfo jInfo)
        {
            var details = string.Empty;
            if (jInfo.Result != null)
            {
                for (var i = 0; i < jInfo.Result.Length; i++)
                {
                    details +=
                        $"#{i + 1} 时间：{jInfo.Timeused[i]}ms，内存：{jInfo.Memoryused[i]}kb，退出代码：{jInfo.Exitcode[i]}，结果：{jInfo.Result[i]}，分数：{jInfo.Score[i]}\r\n";
                }
            }
            var x = new JudgeDetails();
            x.SetContent(jInfo.Code, details);
            x.Show();
        }

        private void UpdateProblemList(Problem[] x)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Problems.Clear();
                foreach (var i in x)
                {
                    Problems.Add(i);
                }
            }));
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
            Connection.IsExited = true;
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
            if (_coins < 10)
            {
                MessageBox.Show("金币不足，无法发送", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (MessageBox.Show("操此作将花费您 10 金币，确定继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
                MessageBoxResult.Yes) return;
            _coins -= 10;
            Coins.Content = _coins;
            Connection.SendData("UpdateCoins", "-10");
            ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -10" });
            if (!string.IsNullOrEmpty(MessageContent.Text))
            {
                MessagesCollection.Insert(0, new Message
                {
                    Content = MessageContent.Text,
                    Direction = "发送",
                    MessageTime = DateTime.Now
                });
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

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            Connection.SendData("RequestFileList", string.Empty);
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            var filePath = CurrentLocation.Text;
            CurrentLocation.Text = filePath.Contains("\\") ? filePath.Substring(0, filePath.LastIndexOf("\\", StringComparison.Ordinal)) : string.Empty;
            Connection.SendData("RequestFileList", CurrentLocation.Text);
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            Connection.SendData("RequestFileList", CurrentLocation.Text);
        }

        private void Label_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            if (_currentGetJudgeRecordIndex == -1)
            {
                MessageBox.Show("已经全部加载完了", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_experience <= 128)
            {
                MessageBox.Show("经验不足，达到 128 后再来吧", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (_coins >= 100)
            {
                if (MessageBox.Show("操此作将花费您 100 金币，确定继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    _coins -= 100;
                    Coins.Content = _coins;
                    Connection.SendData("UpdateCoins", "-100");
                    Connection.SendData("RequestJudgeRecord", $"{_currentGetJudgeRecordIndex}{Divpar}20");
                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -100" });
                }
            }
            else
            {
                MessageBox.Show("金币不足，无法购买", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(MyProblemList.SelectedItem is Problem)) return;
            if (_experience <= 2333)
            {
                MessageBox.Show("经验不足，达到 2333 后再来吧", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (_coins >= 500)
            {
                if (MessageBox.Show("操此作将花费您 500 金币，确定继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    _coins -= 500;
                    Coins.Content = _coins;
                    Connection.SendData("UpdateCoins", "-500");
                    Connection.SendData("RequestProblemDataSet", ((Problem)MyProblemList.SelectedItem)?.ProblemId.ToString());
                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -500" });
                }
            }
            else
            {
                MessageBox.Show("金币不足，无法购买", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MessageList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var si = MessageList.SelectedItem as Message;
            if (si == null) return;
            var x = new Messaging();
            x.SetMessge(si.Content, si.DisplayDateTime);
            x.Show();
        }

        private void JudgeList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var si = JudgeList.SelectedItem as JudgeInfo;
            if (si == null) return;
            if (si.Code == "-|/|\\|-")
            {
                if (_coins < 20)
                {
                    MessageBox.Show("金币不足，无法查看", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (MessageBox.Show("操此作将花费您 20 金币，确定继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    Connection.SendData("RequestJudgeCode", si.JudgeId.ToString());
                    _coins -= 20;
                    Coins.Content = _coins;
                    Connection.SendData("UpdateCoins", "-20");
                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -20" });
                }
            }
            else
            {
                ShowJudgeDetails(si);
            }
        }

        private void FileList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var si = FileList.SelectedItem as FileInfomation;
            if (si == null) return;
            Connection.SendData(si.Type == "文件" ? "RequestFile" : "RequestFileList",
                CurrentLocation.Text + "\\" + si.Name);
            if (si.Type == "文件")
            {
                ReceivingFile.Visibility = Visibility.Visible;
                FileList.IsEnabled = false;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void UserName_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!LoginButton.IsEnabled) return;
            if (e.Key == Key.Enter)
            {
                Button_Click(null, null);
            }
        }

        private void Password_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!LoginButton.IsEnabled) return;
            if (e.Key == Key.Enter)
            {
                Button_Click(null, null);
            }
        }
    }
}
