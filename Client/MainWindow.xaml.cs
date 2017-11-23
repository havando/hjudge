using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Client
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string Divpar = "<h~|~j>";
        private readonly Random _random;

        private readonly ObservableCollection<FileInfomation> _fileInfomations =
            new ObservableCollection<FileInfomation>();
        private readonly ObservableCollection<Competition> _competitionsCollection = new ObservableCollection<Client.Competition>();
        private readonly ObservableCollection<JudgeInfo> _judgeInfos = new ObservableCollection<JudgeInfo>();
        private readonly ObservableCollection<Message> _messagesCollection = new ObservableCollection<Message>();

        private readonly ObservableCollection<Problem> _problems = new ObservableCollection<Problem>();
        private int _coins, _experience, _currentGetJudgeRecordIndex;
        private int _curId;
        private string _userName;

        private string _requestMsgListId;
        private string _requestMsgTargetUserId;
        private string _requestProblemListId;
        private string _requestCompetitionListId;

        public MainWindow()
        {
            var tick = DateTime.Now.Ticks;
            _random = new Random((int)(tick & 0xffffffffL) | (int)(tick >> 32));
            try
            {
                if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\AppData"))
                    Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\AppData");
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
            InitializeComponent();

            MyProblemList.ItemsSource = _problems;
            JudgeList.ItemsSource = _judgeInfos;
            MessageList.ItemsSource = _messagesCollection;
            FileList.ItemsSource = _fileInfomations;
            CompetitionList.ItemsSource = _competitionsCollection;

            Configuration.Init();

            if (string.IsNullOrEmpty(Configuration.Configurations.Ip))
            {
                MessageBox.Show("尚未配置服务端地址，将自动连接至 ::1", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Configuration.Configurations.Ip = "::1";
                Configuration.Configurations.Port = 23333;
                if (Connection.Init(Configuration.Configurations.Ip, Configuration.Configurations.Port,
                    UpdateMainPage)) return;
                MessageBox.Show("程序初始化失败，请检查网络", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
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
            CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility =
                JudgeResult.Visibility = GetFiles.Visibility =
                    ContentGrid.Visibility = Competitions.Visibility = AdminConsole.Visibility = Visibility.Hidden;

            var rtf = new RotateTransform
            {
                CenterX = Loading1.ActualWidth * 0.5,
                CenterY = Loading1.ActualHeight * 0.5
            };
            var daV = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            Loading0.RenderTransform = rtf;
            Loading1.RenderTransform = rtf;
            Loading2.RenderTransform = rtf;
            Loading3.RenderTransform = rtf;
            Loading4.RenderTransform = rtf;
            Loading5.RenderTransform = rtf;
            ReceivingFile.RenderTransform = rtf;
            rtf.BeginAnimation(RotateTransform.AngleProperty, daV);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Connection.SendData("Login", UserName.Text + Divpar + Password.Password);
            Loading0.Visibility = Visibility.Visible;
        }

        private void UpdateMainPage(string s)
        {
            try
            {
                var info = s.Split(new[] { Divpar }, StringSplitOptions.None);
                var header = info[0];
                var content = string.Empty;
                var id = string.Empty;
                var contentWithoutId = string.Empty;
                for (var i = 1; i < info.Length; i++)
                    if (i != info.Length - 1)
                        content += info[i] + Divpar;
                    else
                        content += info[i];
                try
                {
                    id = info[1];
                    for (var i = 2; i < info.Length; i++)
                        if (i != info.Length - 1)
                            contentWithoutId += info[i] + Divpar;
                        else
                            contentWithoutId += info[i];
                }
                catch
                {
                    id = string.Empty;
                    contentWithoutId = string.Empty;
                }
                switch (header)
                {
                    case "Connection":
                        {
                            switch (content)
                            {
                                case "Connected":
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            LoginButton.IsEnabled = Register.IsEnabled = true;
                                        });
                                        break;
                                    }
                                case "Break":
                                    {
                                        _userName = string.Empty;
                                        _curId = 0;
                                        Dispatcher.Invoke(() =>
                                        {
                                            CodeSubmit.Visibility = Messaging.Visibility =
                                                Messages.Visibility = JudgeResult.Visibility =
                                                    GetFiles.Visibility = ContentGrid.Visibility =
                                                        Competitions.Visibility = AdminConsole.Visibility = Visibility.Hidden;
                                            LoginGrid.Visibility = Visibility.Visible;
                                            Loading1.Visibility = Visibility.Hidden;
                                            InitManagementTools(0);
                                            OldPassword.Password = NewPassword.Password = ConfirmPassword.Password = string.Empty;
                                            ActiveBox.Items.Clear();
                                            _judgeInfos.Clear();
                                            _messagesCollection.Clear();
                                            Experience.Content = Coins.Content = "0";
                                            Level.Content = "-";
                                            WelcomeLabel.Content = "你好，";
                                            Identity.Content = "身份：";
                                            CodeBox.Text = string.Empty;
                                            _coins = _experience = _currentGetJudgeRecordIndex = 0;
                                            TabControl.SelectedIndex = 0;
                                            FileList.IsEnabled = true;
                                            ReceivingFile.Visibility = Visibility.Hidden;
                                            ReceivingProcess.Visibility = Visibility.Hidden;
                                            Loading1.Visibility = Visibility.Hidden;
                                            Loading2.Visibility = Visibility.Hidden;
                                            Loading3.Visibility = Visibility.Hidden;
                                        });
                                        break;
                                    }
                            }
                            break;
                        }
                    case "Login":
                        {
                            Dispatcher.Invoke(() => { Loading0.Visibility = Visibility.Hidden; });
                            if (content == "Succeed")
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    _userName = UserName.Text;
                                    Password.Password = string.Empty;
                                    CodeSubmit.Visibility = Messaging.Visibility = Messages.Visibility =
                                        JudgeResult.Visibility = Competitions.Visibility =
                                            GetFiles.Visibility = ContentGrid.Visibility = Visibility.Visible;
                                    LoginGrid.Visibility = Visibility.Hidden;
                                    Loading1.Visibility = Visibility.Visible;
                                    _messagesCollection.Clear();
                                    ActiveBox.Items.Add(new TextBlock
                                    {
                                        Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {_userName} 登录"
                                    });
                                });
                                _requestMsgListId = Guid.NewGuid().ToString();
                                Connection.SendData("RequestProfile", _userName);
                                Connection.SendData("RequestJudgeRecord", $"0{Divpar}20");
                                Connection.SendData("RequestFileList", string.Empty);
                                Connection.SendData("RequestMsgList" + Divpar + _requestMsgListId, string.Empty);

                                _currentGetJudgeRecordIndex = 20;
                                _coins = _experience = _currentGetJudgeRecordIndex = 0;
                            }
                            else if (content == "NeedReview")
                            {
                                MessageBox.Show("注册还未通过审核，请耐心等待", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else MessageBox.Show("用户名或密码错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                    case "Logout":
                        {
                            _userName = string.Empty;
                            _curId = 0;
                            Dispatcher.Invoke(() =>
                            {
                                CodeSubmit.Visibility = Messaging.Visibility =
                                    Messages.Visibility = JudgeResult.Visibility =
                                        GetFiles.Visibility = ContentGrid.Visibility =
                                            Competitions.Visibility = AdminConsole.Visibility = Visibility.Hidden;
                                LoginGrid.Visibility = Visibility.Visible;
                                Loading1.Visibility = Visibility.Hidden;
                                InitManagementTools(0);
                                OldPassword.Password = NewPassword.Password = ConfirmPassword.Password = string.Empty;
                                ActiveBox.Items.Clear();
                                _judgeInfos.Clear();
                                _messagesCollection.Clear();
                                Experience.Content = Coins.Content = "0";
                                Level.Content = "-";
                                WelcomeLabel.Content = "你好，";
                                Identity.Content = "身份：";
                                CodeBox.Text = string.Empty;
                                _coins = _experience = _currentGetJudgeRecordIndex = 0;
                                TabControl.SelectedIndex = 0;
                                FileList.IsEnabled = true;
                                ReceivingFile.Visibility = Visibility.Hidden;
                                ReceivingProcess.Visibility = Visibility.Hidden;
                                Loading1.Visibility = Visibility.Hidden;
                                Loading2.Visibility = Visibility.Hidden;
                                Loading3.Visibility = Visibility.Hidden;
                                ChangePasswordExpander.IsExpanded = false;
                            });
                            break;
                        }
                    case "Register":
                        {
                            Dispatcher.Invoke(() => { Loading0.Visibility = Visibility.Hidden; });
                            if (content == "Succeeded")
                            {
                                MessageBox.Show("注册成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else if (content == "NeedReview")
                            {
                                MessageBox.Show("注册请求已提交，等待管理员审核通过后方可登陆", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else if (content == "Duplicate")
                            {
                                MessageBox.Show("该用户名已被注册", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                            {
                                MessageBox.Show("注册失败", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            break;
                        }
                    case "Messaging":
                        {
                            var t = JsonConvert.DeserializeObject<Message>(content);
                            Dispatcher.Invoke(() =>
                            {
                                ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 收到消息" });
                                _messagesCollection.Insert(0, new Message
                                {
                                    Content = t.Content,
                                    Direction = "接收",
                                    MessageTime = t.MessageTime,
                                    User = t.User
                                });
                                var x = new Messaging();
                                x.SetMessge(t);
                                x.Show();
                            });
                            break;
                        }
                    case "FileList":
                        {
                            var final = content.Split(new[] { Divpar }, StringSplitOptions.None);
                            if (final.Length < 2) break;
                            Dispatcher.Invoke(() =>
                            {
                                _fileInfomations.Clear();
                                CurrentLocation.Text = final[0];
                                var flag = true;
                                for (var i = 1; i < final.Length; i++)
                                {
                                    if (final[i] == "|")
                                    {
                                        flag = !flag;
                                        continue;
                                    }
                                    _fileInfomations.Add(new FileInfomation
                                    {
                                        Type = flag ? "文件夹" : "文件",
                                        Name = final[i]
                                    });
                                }
                                ReceivingFile.Visibility = Visibility.Hidden;
                                ReceivingProcess.Visibility = Visibility.Hidden;
                            });
                            break;
                        }
                    case "JudgeResult":
                        {
                            var p = JsonConvert.DeserializeObject<JudgeInfo>(content);
                            Dispatcher.Invoke(() =>
                            {
                                ActiveBox.Items.Add(new TextBlock
                                {
                                    Text =
                                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 收到题目 {p.ProblemName} 的评测结果：{p.ResultSummery}"
                                });
                                if (p.ResultSummery == "Accepted")
                                {
                                    var delta = 4 + _random.Next() % 32;
                                    var delta2 = 16 + _random.Next() % 12;
                                    Connection.SendData("UpdateExperience", delta.ToString());
                                    _experience += delta;
                                    Connection.SendData("UpdateCoins", delta2.ToString());
                                    _coins += delta2;
                                    ActiveBox.Items.Add(new TextBlock
                                    {
                                        Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +{delta2}，经验 +{delta}"
                                    });
                                }
                                else if (p.ResultSummery.Contains("Exceeded"))
                                {
                                    var delta = 2 + _random.Next() % 16;
                                    var delta2 = 8 + _random.Next() % 4;
                                    Connection.SendData("UpdateCoins", delta.ToString());
                                    _coins += delta;
                                    Connection.SendData("UpdateExperience", delta2.ToString());
                                    _experience += delta2;
                                    ActiveBox.Items.Add(new TextBlock
                                    {
                                        Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +{delta}，经验 +{delta2}"
                                    });
                                }
                                else
                                {
                                    var delta = 1 + _random.Next() % 4;
                                    Connection.SendData("UpdateExperience", delta.ToString());
                                    _experience += delta;
                                    ActiveBox.Items.Add(new TextBlock
                                    {
                                        Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 经验 +{delta}"
                                    });
                                }
                                _judgeInfos.Insert(0, p);
                                Coins.Content = _coins;
                                Experience.Content = _experience;
                                SetLevel(_experience);
                                ShowJudgeDetails(p);
                            });
                            break;
                        }
                    case "JudgeResultForCompetition":
                        {
                            var p = JsonConvert.DeserializeObject<JudgeInfo>(content);
                            if (p.ResultSummery == "Judging...")
                            {
                                MessageBox.Show("提交次数超出限制", "题目", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                                Dispatcher.Invoke(() =>
                                {
                                    ActiveBox.Items.Add(new TextBlock
                                    {
                                        Text =
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 收到题目 {p.ProblemName} 的评测结果：{p.ResultSummery}"
                                    });
                                    if (p.ResultSummery == "Accepted")
                                    {
                                        var delta = 4 + _random.Next() % 32;
                                        var delta2 = 16 + _random.Next() % 12;
                                        Connection.SendData("UpdateExperience", delta.ToString());
                                        _experience += delta;
                                        Connection.SendData("UpdateCoins", delta2.ToString());
                                        _coins += delta2;
                                        ActiveBox.Items.Add(new TextBlock
                                        {
                                            Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +{delta2}，经验 +{delta}"
                                        });
                                    }
                                    else if (p.ResultSummery.Contains("Exceeded"))
                                    {
                                        var delta = 2 + _random.Next() % 16;
                                        var delta2 = 8 + _random.Next() % 4;
                                        Connection.SendData("UpdateCoins", delta.ToString());
                                        _coins += delta;
                                        Connection.SendData("UpdateExperience", delta2.ToString());
                                        _experience += delta2;
                                        ActiveBox.Items.Add(new TextBlock
                                        {
                                            Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +{delta}，经验 +{delta2}"
                                        });
                                    }
                                    else
                                    {
                                        var delta = 1 + _random.Next() % 4;
                                        Connection.SendData("UpdateExperience", delta.ToString());
                                        _experience += delta;
                                        ActiveBox.Items.Add(new TextBlock
                                        {
                                            Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 经验 +{delta}"
                                        });
                                    }
                                    Coins.Content = _coins;
                                    Experience.Content = _experience;
                                    SetLevel(_experience);
                                    ShowJudgeDetails(p);
                                });
                            break;
                        }
                    case "ProblemList":
                        {
                            if (id == _requestProblemListId)
                            {
                                var x = JsonConvert.DeserializeObject<Problem>(contentWithoutId);
                                Dispatcher.Invoke(() =>
                                {
                                    _problems.Add(x);
                                });
                            }
                            break;
                        }
                    case "Profile":
                        {
                            var x = JsonConvert.DeserializeObject<UserInfo>(content);
                            _curId = x.Type;
                            Dispatcher.Invoke(() =>
                            {
                                WelcomeLabel.Content = $"你好，{x.UserName}";
                                Identity.Content = $"身份：{x.Type2}";
                                UserIcon.Source = ByteImageConverter.ByteToImage(!string.IsNullOrEmpty(x.Icon)
                                    ? Convert.FromBase64String(x.Icon)
                                    : Convert.FromBase64String(Properties.Resources.default_user_icon_string));
                                Coins.Content = _coins = x.Coins;
                                Experience.Content = _experience = x.Experience;
                                SetLevel(x.Experience);
                                Loading1.Visibility = Visibility.Hidden;
                                InitManagementTools(x.Type);
                                if (x.Type >= 1 && x.Type <= 3)
                                {
                                    AdminConsole.Visibility = Visibility.Visible;
                                }
                            });
                            break;
                        }
                    case "UpdateProfile":
                        {
                            Dispatcher.Invoke(() => { Loading1.Visibility = Visibility.Hidden; });
                            switch (content)
                            {
                                case "Succeed":
                                    MessageBox.Show("修改成功", "提示", MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                    ActiveBox.Items.Add(
                                        new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 修改个人信息" });
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
                            Dispatcher.Invoke(() => { Loading1.Visibility = Visibility.Hidden; });
                            switch (content)
                            {
                                case "Succeed":
                                    MessageBox.Show("密码修改成功", "提示", MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                    ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 修改密码" });
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
                            Dispatcher.Invoke(() => { Loading3.Visibility = Visibility.Hidden; });
                            var final = content.Split(new[] { Divpar }, StringSplitOptions.None);
                            if (final.Length < 3) break;
                            _currentGetJudgeRecordIndex = Convert.ToInt32(final[0]) + Convert.ToInt32(final[1]);
                            if (Convert.ToInt32(final[1]) != 20)
                                _currentGetJudgeRecordIndex = -1;
                            Dispatcher.Invoke(() =>
                            {
                                foreach (var i in JsonConvert.DeserializeObject<JudgeInfo[]>(final[2]))
                                    _judgeInfos.Add(i);
                            });
                            break;
                        }
                    case "JudgeCode":
                        {
                            Dispatcher.Invoke(() => { Loading3.Visibility = Visibility.Hidden; });
                            var jc = JsonConvert.DeserializeObject<JudgeInfo>(content);
                            var j = (from c in _judgeInfos where c.JudgeId == jc.JudgeId select c)
                                .FirstOrDefault();
                            if (j == null) break;
                            j.Code = jc.Code;
                            Dispatcher.Invoke(() => { ShowJudgeDetails(j); });
                            break;
                        }
                    case "FileReceived":
                        {
                            Dispatcher.Invoke(() =>
                            {
                                FileList.IsEnabled = true;
                                ReceivingFile.Visibility = Visibility.Hidden;
                                ReceivingProcess.Visibility = Visibility.Hidden;
                            });
                            if (content == "Error")
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    Connection.SendData("RequestFileList", string.Empty);
                                    ReceivingFile.Visibility = Visibility.Visible;
                                    ReceivingProcess.Visibility = Visibility.Visible;
                                });
                                MessageBox.Show("文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            break;
                        }
                    case "FileReceiving":
                        {
                            Dispatcher.Invoke(() => { ReceivingProcess.Content = content; });
                            break;
                        }
                    case "ProblemDataSet":
                        {
                            Dispatcher.Invoke(() => { Loading2.Visibility = Visibility.Hidden; });
                            if (content != "Denied") break;
                            Dispatcher.Invoke(() =>
                            {
                                _coins += 500;
                                Coins.Content = _coins;
                                ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 +500" });
                                MessageBox.Show("抱歉，系统设定不允许获取题目数据，请联系管理员。金币已为您加回。", "提示", MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });

                            Connection.SendData("UpdateCoins", "500");
                            break;
                        }
                    case "Compiler":
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LangBox.Items.Clear();
                                var l = JsonConvert.DeserializeObject<List<Compiler>>(content);
                                foreach (var m in l)
                                    LangBox.Items.Add(new RadioButton { Content = m.DisplayName });
                            });
                            break;
                        }
                    case "Version":
                        {
                            if (content != Assembly.GetExecutingAssembly().GetName().Version.ToString())
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show($"此版本的 hjudge - Client 无法正常工作，请更新至 {content} 版本", "提示",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                                Environment.Exit(0);
                            }
                            break;
                        }
                    case "RequestMsgTargetUser":
                        {
                            if (id == _requestMsgTargetUserId)
                            {
                                var t = JsonConvert.DeserializeObject<string>(contentWithoutId);
                                Dispatcher.Invoke(() =>
                                {
                                    SendingTarget.Items.Add(new CheckBox { Content = t });
                                });
                            }
                            break;
                        }
                    case "RequestMsgList":
                        {
                            if (_requestMsgListId == id)
                            {
                                var t = JsonConvert.DeserializeObject<Message>(contentWithoutId);
                                Dispatcher.Invoke(() =>
                                {
                                    _messagesCollection.Add(t);
                                    if (t.State == 0 && t.Direction == "接收")
                                    {
                                        if (t.Content.Length == 33)
                                        {
                                            Connection.SendData("RequestMsg", t.MsgId.ToString());
                                        }
                                        else
                                        {
                                            var x = new Messaging();
                                            x.SetMessge(t);
                                            x.Show();
                                        }
                                    }
                                });
                            }
                            break;
                        }
                    case "RequestMsg":
                        {
                            var t = JsonConvert.DeserializeObject<Message>(content);
                            Dispatcher.Invoke(() =>
                            {
                                Loading5.Visibility = Visibility.Hidden;
                                _messagesCollection.Where(i => i.MsgId == t.MsgId).FirstOrDefault().Content = t.Content;
                                MessageList.Items.Refresh();
                                var x = new Messaging();
                                x.SetMessge(t);
                                x.Show();
                            });
                            break;
                        }
                    case "RequestCompetitionList":
                        {
                            if (_requestCompetitionListId == id)
                            {
                                var t = JsonConvert.DeserializeObject<Competition>(contentWithoutId);
                                Dispatcher.Invoke(() =>
                                {
                                    _competitionsCollection.Add(t);
                                });
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

        private void InitManagementTools(int type)
        {
            ManagementToolsPage.Navigate(new ManagementWelcomePage());
            Connection.CanSwitch = true;
            switch (type)
            {
                case 1:
                case 2:
                    {
                        Button[] operationsButton =
                        {
                            new Button {Height = 32, Width = 80, Content = "题目管理"},
                            new Button {Height = 32, Width = 80, Content = "比赛管理"},
                            new Button {Height = 32, Width = 80, Content = "成员管理"},
                            new Button {Height = 32, Width = 80, Content = "评测日志"},
                            new Button {Height = 32, Width = 80, Content = "系统设置"}
                        };
                        operationsButton[0].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch) ManagementToolsPage.Navigate(new ProblemsManagementPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        operationsButton[1].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new CompetitionsManagementPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        operationsButton[2].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new MembersManagementPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        operationsButton[3].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new JudgingLogsPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        operationsButton[4].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new SystemSettingsPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        foreach (var t in operationsButton)
                            ManagementToolsList.Items.Add(t);
                        break;
                    }
                case 3:
                    {
                        Button[] operationsButton =
                        {
                            new Button {Height = 32, Width = 80, Content = "题目管理"},
                            new Button {Height = 32, Width = 80, Content = "比赛管理"},
                            new Button {Height = 32, Width = 80, Content = "成员管理"},
                            new Button {Height = 32, Width = 80, Content = "评测日志"}
                        };
                        operationsButton[0].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new ProblemsManagementPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        operationsButton[1].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new CompetitionsManagementPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        operationsButton[2].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new MembersManagementPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        operationsButton[3].Click += (sender, args) =>
                        {
                            if (Connection.CanSwitch)
                                ManagementToolsPage.Navigate(new JudgingLogsPage());
                            else MessageBox.Show("目前无法切换，请等待当前操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        };
                        foreach (var t in operationsButton)
                            ManagementToolsList.Items.Add(t);
                        break;
                    }
                default:
                    {
                        ManagementToolsList.Items.Clear();
                        break;
                    }
            }
        }

        private void SetLevel(int experience)
        {
            if (experience >= 1048576)
            {
                Level.Content = "最强王者";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level6));
            }
            else if (experience >= 524288)
            {
                Level.Content = "璀璨钻石 Lev.3";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
            }
            else if (experience >= 262144)
            {
                Level.Content = "璀璨钻石 Lev.2";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
            }
            else if (experience >= 131072)
            {
                Level.Content = "璀璨钻石 Lev.1";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level5));
            }
            else if (experience >= 65536)
            {
                Level.Content = "华贵铂金 Lev.3";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
            }
            else if (experience >= 32768)
            {
                Level.Content = "华贵铂金 Lev.2";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
            }
            else if (experience >= 16384)
            {
                Level.Content = "华贵铂金 Lev.1";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level4));
            }
            else if (experience >= 8192)
            {
                Level.Content = "荣耀黄金 Lev.3";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
            }
            else if (experience >= 4096)
            {
                Level.Content = "荣耀黄金 Lev.2";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
            }
            else if (experience >= 2048)
            {
                Level.Content = "荣耀黄金 Lev.1";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level3));
            }
            else if (experience >= 1024)
            {
                Level.Content = "不屈白银 Lev.3";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
            }
            else if (experience >= 512)
            {
                Level.Content = "不屈白银 Lev.2";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
            }
            else if (experience >= 256)
            {
                Level.Content = "不屈白银 Lev.1";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level2));
            }
            else if (experience >= 128)
            {
                Level.Content = "英勇黄铜 Lev.3";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
            }
            else if (experience >= 64)
            {
                Level.Content = "英勇黄铜 Lev.2";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
            }
            else if (experience >= 32)
            {
                Level.Content = "英勇黄铜 Lev.1";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level1));
            }
            else if (experience >= 16)
            {
                Level.Content = "一只辣鸡 Lev.3";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
            }
            else if (experience >= 8)
            {
                Level.Content = "一只辣鸡 Lev.2";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
            }
            else if (experience >= 4)
            {
                Level.Content = "一只辣鸡 Lev.1";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.level0));
            }
            else
            {
                Level.Content = "蒟蒻来袭";
                LevelImage.Source =
                    ByteImageConverter.ByteToImage(Convert.FromBase64String(Properties.Resources.nolevel));
            }
        }

        private void ShowJudgeDetails(JudgeInfo jInfo)
        {
            var x = new JudgeDetails();
            x.SetContent(jInfo);
            x.Show();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Loading1.Visibility = Visibility.Visible;
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
                Connection.SendData("Logout", string.Empty);
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
                if (!string.IsNullOrEmpty(ofg.FileName))
                {
                    var fi = new FileInfo(ofg.FileName);
                    if (fi.Length > 1048576)
                    {
                        MessageBox.Show("图片大小不能超过 1 MB", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    ofg.OpenFile();
                    var fs = new FileStream(ofg.FileName, FileMode.Open, FileAccess.Read,
                        FileShare.Read);
                    var icon = ByteImageConverter.ImageToByte(fs);
                    UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(icon));
                    Connection.SendData("UpdateProfile", _userName + Divpar + icon);
                    Loading1.Visibility = Visibility.Visible;
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
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            _requestProblemListId = Guid.NewGuid().ToString();
            _problems.Clear();
            Connection.SendData("RequestProblemList", _requestProblemListId);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.Source is TabControl)) return;
            switch ((sender as TabControl)?.SelectedIndex ?? 0)
            {
                case 1:
                    _requestProblemListId = Guid.NewGuid().ToString();
                    _problems.Clear();
                    Connection.SendData("RequestProblemList", _requestProblemListId);
                    Connection.SendData("RequestCompiler", string.Empty);
                    break;
                case 2:
                    _requestMsgTargetUserId = Guid.NewGuid().ToString();
                    SendingTarget.Items.Clear();
                    Connection.SendData("RequestMsgTargetUser", _requestMsgTargetUserId);
                    break;
                case 6:
                    _requestCompetitionListId = Guid.NewGuid().ToString();
                    _competitionsCollection.Clear();
                    Connection.SendData("RequestCompetitionList", _requestCompetitionListId);
                    break;
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MessageContent.Text)) return;
            var target = new List<string>();
            foreach (var i in SendingTarget.Items)
            {
                if (i is CheckBox t)
                {
                    if (t.IsChecked ?? false)
                    {
                        target.Add(t.Content.ToString());
                    }
                }
            }
            if (target.Count == 0) return;
            if (MessageContent.Text.Length > 1048576)
            {
                MessageBox.Show("消息过长，无法发送", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (_curId == 0 || _curId == 4)
            {
                if (_coins < 10 * target.Count)
                {
                    MessageBox.Show("金币不足，无法发送", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (MessageBox.Show($"操此作将花费您 {10 * target.Count} 金币，确定继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
                    MessageBoxResult.Yes) return;
                _coins -= 10 * target.Count;
                Coins.Content = _coins;
                Connection.SendData("UpdateCoins", $"-{10 * target.Count}");
                ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -{10 * target.Count}" });
            }
            ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 发送消息" });

            foreach (var t in target)
            {
                _messagesCollection.Insert(0, new Message
                {
                    Content = MessageContent.Text,
                    Direction = "发送",
                    MessageTime = DateTime.Now,
                    User = t
                });
                Connection.SendMsg(MessageContent.Text, t);
            }
            MessageContent.Text = string.Empty;
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            if (!(MyProblemList.SelectedItem is Problem x)) return;
            var type = string.Empty;
            foreach (var i in LangBox.Items)
                if (i is RadioButton t)
                    if (t.IsChecked ?? false)
                        type = t.Content.ToString();
            if (!string.IsNullOrEmpty(CodeBox.Text) && !string.IsNullOrEmpty(type))
            {
                ActiveBox.Items.Add(
                    new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 提交代码，题目：{x.ProblemName}" });
                Connection.SendData("SubmitCode", x.ProblemId + Divpar + type + Divpar + CodeBox.Text);
                CodeBox.Text = string.Empty;
            }
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            Connection.SendData("RequestFileList", string.Empty);
            ReceivingFile.Visibility = Visibility.Visible;
            ReceivingProcess.Visibility = Visibility.Visible;
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            var filePath = CurrentLocation.Text;
            CurrentLocation.Text = filePath.Contains("\\")
                ? filePath.Substring(0, filePath.LastIndexOf("\\", StringComparison.Ordinal))
                : string.Empty;
            Connection.SendData("RequestFileList", CurrentLocation.Text);
            ReceivingFile.Visibility = Visibility.Visible;
            ReceivingProcess.Visibility = Visibility.Visible;
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            Connection.SendData("RequestFileList", CurrentLocation.Text);
            ReceivingFile.Visibility = Visibility.Visible;
            ReceivingProcess.Visibility = Visibility.Visible;
        }

        private void Label_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            if (_currentGetJudgeRecordIndex == -1)
            {
                MessageBox.Show("已经全部加载完了", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_curId == 0 || _curId == 4)
            {
                if (_experience <= 128)
                {
                    MessageBox.Show("经验不足，达到 128 后再来吧", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (_coins >= 100)
                {
                    if (MessageBox.Show("操此作将花费您 100 金币，确定继续？", "提示", MessageBoxButton.YesNo,
                            MessageBoxImage.Question) ==
                        MessageBoxResult.Yes)
                    {
                        _coins -= 100;
                        Coins.Content = _coins;
                        Connection.SendData("UpdateCoins", "-100");
                        Connection.SendData("RequestJudgeRecord", $"{_currentGetJudgeRecordIndex}{Divpar}20");
                        ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -100" });
                        Loading3.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    MessageBox.Show("金币不足，无法购买", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Connection.SendData("RequestJudgeRecord", $"{_currentGetJudgeRecordIndex}{Divpar}20");
                Loading3.Visibility = Visibility.Visible;
            }
        }

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(MyProblemList.SelectedItem is Problem)) return;
            if (_curId == 0 || _curId == 4)
            {
                if (_experience <= 2333)
                {
                    MessageBox.Show("经验不足，达到 2333 后再来吧", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (_coins >= 500)
                {
                    if (MessageBox.Show("操此作将花费您 500 金币，确定继续？", "提示", MessageBoxButton.YesNo,
                            MessageBoxImage.Question) ==
                        MessageBoxResult.Yes)
                    {
                        _coins -= 500;
                        Coins.Content = _coins;
                        Connection.SendData("UpdateCoins", "-500");
                        Connection.SendData("RequestProblemDataSet",
                            ((Problem)MyProblemList.SelectedItem)?.ProblemId.ToString());
                        ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -500" });
                        Loading2.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    MessageBox.Show("金币不足，无法购买", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Connection.SendData("RequestProblemDataSet",
                    ((Problem)MyProblemList.SelectedItem)?.ProblemId.ToString());
                Loading2.Visibility = Visibility.Visible;
            }
        }

        private void MessageList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && !(originalSource is ListViewItem)) originalSource = VisualTreeHelper.GetParent(originalSource);
            if (originalSource == null) return;
            if (!(sender is ListView)) return;
            if (!(MessageList.SelectedItem is Message si)) return;
            if (si.Content.Length == 33)
            {
                Loading5.Visibility = Visibility.Visible;
                Connection.SendData("RequestMsg", si.MsgId.ToString());
            }
            else
            {
                var x = new Messaging();
                x.SetMessge(si);
                x.Show();
            }
        }

        private void JudgeList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && !(originalSource is ListViewItem)) originalSource = VisualTreeHelper.GetParent(originalSource);
            if (originalSource == null) return;
            if (!(sender is ListView)) return;
            if (!(JudgeList.SelectedItem is JudgeInfo si)) return;
            if (si.Code == "-|/|\\|-")
                if (_curId == 0 || _curId == 4)
                {
                    if (_coins < 20)
                    {
                        MessageBox.Show("金币不足，无法查看", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (MessageBox.Show("操此作将花费您 20 金币，确定继续？", "提示", MessageBoxButton.YesNo,
                            MessageBoxImage.Question) ==
                        MessageBoxResult.Yes)
                    {
                        Connection.SendData("RequestJudgeCode", si.JudgeId.ToString());
                        _coins -= 20;
                        Coins.Content = _coins;
                        Connection.SendData("UpdateCoins", "-20");
                        ActiveBox.Items.Add(new TextBlock { Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 金币 -20" });
                        Loading3.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    Connection.SendData("RequestJudgeCode", si.JudgeId.ToString());
                    Loading3.Visibility = Visibility.Visible;
                }
            else
                ShowJudgeDetails(si);
        }

        private void FileList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && !(originalSource is ListViewItem)) originalSource = VisualTreeHelper.GetParent(originalSource);
            if (originalSource == null) return;
            if (!(sender is ListView)) return;
            if (!(FileList.SelectedItem is FileInfomation si)) return;
            Connection.SendData(si.Type == "文件" ? "RequestFile" : "RequestFileList",
                CurrentLocation.Text + "\\" + si.Name);
            if (si.Type == "文件")
            {
                ActiveBox.Items.Add(new TextBlock
                {
                    Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 请求文件 {si.Name}"
                });
                FileList.IsEnabled = false;
            }
            ReceivingFile.Visibility = Visibility.Visible;
            ReceivingProcess.Visibility = Visibility.Visible;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Task.Run(() => { Environment.Exit(0); });
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            if (!Register.IsEnabled) return;
            if (!string.IsNullOrWhiteSpace(UserName.Text) && !string.IsNullOrEmpty(Password.Password))
            {
                if (MessageBox.Show("你输入的密码是：" + Password.Password + "，确认无误？", "提示", MessageBoxButton.OKCancel,
                        MessageBoxImage.Question) == MessageBoxResult.OK)
                {
                    Loading0.Visibility = Visibility.Visible;
                    Connection.SendData("Register", UserName.Text + Divpar + Password.Password);
                }
            }
            else
            {
                MessageBox.Show("请填写完整的用户名和密码", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Label_MouseDown_2(object sender, MouseButtonEventArgs e)
        {
            foreach (var i in SendingTarget.Items)
            {
                if (i is CheckBox t)
                {
                    t.IsChecked = true;
                }
            }
        }

        private void Label_MouseDown_3(object sender, MouseButtonEventArgs e)
        {
            foreach (var i in SendingTarget.Items)
            {
                if (i is CheckBox t)
                {
                    t.IsChecked = !(t.IsChecked ?? false);
                }
            }
        }

        private void Label_MouseDown_4(object sender, MouseButtonEventArgs e)
        {
            if (!(MyProblemList.SelectedItem is Problem x)) return;
            var d = new ProblemDescription();
            d.SetProblemDescription(string.IsNullOrEmpty(x.Description) ? Connection.GetProblemDescription(x.ProblemId) : x.Description, x.ProblemIndex);
            d.Show();
        }

        private void CompetitionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && !(originalSource is ListViewItem)) originalSource = VisualTreeHelper.GetParent(originalSource);
            if (originalSource == null) return;
            if (!(sender is ListView)) return;
            if (!(CompetitionList.SelectedItem is Competition t)) return;
            var x = new CompetitionViewer();
            x.SetCompetition(t);
            x.ShowDialog();
        }

        private void UserName_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!LoginButton.IsEnabled) return;
            if (e.Key == Key.Enter)
                Button_Click(null, null);
        }

        private void Password_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!LoginButton.IsEnabled) return;
            if (e.Key == Key.Enter)
                Button_Click(null, null);
        }
    }
}