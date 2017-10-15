﻿using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Server
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly NotifyIcon _notifyIcon = new NotifyIcon
        {
            Text = @"hjudge - server",
            Icon = Properties.Resources.Server
        };

        private JudgeLogs _judgeLogsForm;

        public MainWindow()
        {
            //if (File.Exists($"{Environment.CurrentDirectory}\\Updater.exe"))
            //{
            //    new Process
            //    {
            //        StartInfo =
            //        {
            //            FileName = $"{Environment.CurrentDirectory}\\Updater.exe",
            //            Arguments =
            //                $"Server {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} {Process.GetCurrentProcess().Id} \"{Environment.CurrentDirectory}\""
            //        }
            //    }.Start();
            //}
            try
            {
                if (!Directory.Exists(Environment.CurrentDirectory + "\\AppData"))
                    Directory.CreateDirectory(Environment.CurrentDirectory + "\\AppData");
                if (!Directory.Exists(Environment.CurrentDirectory + "\\Files"))
                    Directory.CreateDirectory(Environment.CurrentDirectory + "\\Files");
                if (!Directory.Exists(Environment.CurrentDirectory + "\\Data"))
                    Directory.CreateDirectory(Environment.CurrentDirectory + "\\Data");
                if (Environment.Is64BitProcess)
                    File.Copy(Environment.CurrentDirectory + "\\x64\\HPSocket4C_U.dll",
                        Environment.CurrentDirectory + "\\HPSocket4C_U.dll", true);
                else
                    File.Copy(Environment.CurrentDirectory + "\\x86\\HPSocket4C_U.dll",
                        Environment.CurrentDirectory + "\\HPSocket4C_U.dll", true);
            }
            catch
            {
                MessageBox.Show("程序初始化失败", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            InitializeComponent();
            _notifyIcon.MouseClick += (sender, args) =>
            {
                if (args.Button != MouseButtons.Left) return;
                Visibility = Visibility.Visible;
                ShowInTaskbar = true;
                Activate();
            };
            _notifyIcon.Visible = true;

            if (Configuration.IsHidden)
            {
                ShowInTaskbar = false;
                Visibility = Visibility.Hidden;
            }

            Init();
        }

        private void Init()
        {
            Height = 228;
            Width = 473;
            LoginGrid.Margin = new Thickness(10, 10, 0, 0);
            ContentGrid.Margin = new Thickness(10, 10, 0, 0);
            ContentGrid.Opacity = 0;
            LoginGrid.Visibility = Visibility.Visible;
            ContentGrid.Visibility = Visibility.Hidden;

            Configuration.Init();
            Connection.Init(UpdateListBoxContent);
            UserHelper.SetCurrentUser(0, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty);
            UserHelper.CurrentUser.IsChanged = false;
            ShowUserInfo();

            UpdateListBoxContent($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 欢迎使用 hjudge");

            Task.Run(() =>
            {
                while (!Connection.IsExited)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CurrentJudgeList.Content = "当前评测线程数量：" + Connection.CurJudgingCnt;
                    }));
                    Thread.Sleep(1000);
                }
            });
        }

        private void UpdateListBoxContent(string content)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ListBox.Items.Add(new TextBlock
                {
                    Text = content
                });
                ListBox.ScrollIntoView(ListBox.Items[ListBox.Items.Count - 1]);
            }));
        }

        private async void LoginButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = false;
            var res = await Connection.Login(UserName.Text, Password.Password);
            switch (res)
            {
                case 1:
                    {
                        MessageBox.Show("用户名或密码错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }
                default:
                    {
                        if (res != 0)
                            MessageBox.Show("未知错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }
            }
            LoginButton.IsEnabled = true;
            if (res != 0) return;
            UpdateListBoxContent($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {UserHelper.CurrentUser.UserName} 欢迎登录 hjudge 服务端");
            UserName.Text = string.Empty;
            Password.Password = string.Empty;
            LoginGrid.Visibility = Visibility.Hidden;
            ContentGrid.Visibility = Visibility.Visible;
            var hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5)));
            var showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)));
            var scratchWidthDaV = new DoubleAnimation(473, 673, new Duration(TimeSpan.FromSeconds(0.25)));
            var scratchHeightDaV = new DoubleAnimation(228, 328, new Duration(TimeSpan.FromSeconds(0.25)));
            LoginGrid.BeginAnimation(OpacityProperty, hiddenDaV);
            BeginAnimation(WidthProperty, scratchWidthDaV);
            await Task.Run(() => { Thread.Sleep(300); });
            BeginAnimation(HeightProperty, scratchHeightDaV);
            ContentGrid.BeginAnimation(OpacityProperty, showDaV);
            switch (UserHelper.CurrentUser.Type)
            {
                case 1:
                    SetEnvironmentForBoss();
                    break;
                case 2:
                    SetEnvironmentForAdministrator();
                    break;
                case 3:
                    SetEnvironmentForTeacher();
                    break;
                case 4:
                    SetEnvironmentForStudent();
                    break;
            }
        }

        private void SetEnvironmentForBoss()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "题目管理"}, //TODO: Competition Mode
                new Button {Height = 32, Width = 80, Content = "比赛管理"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "发送消息"},
                new Button {Height = 32, Width = 80, Content = "成员管理"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "系统设置"},
                new Button {Height = 32, Width = 80, Content = "注销登录"},
                new Button {Height = 32, Width = 80, Content = "退出程序"}
            };
            operationsButton[0].Click += (o, args) => new ProfileManagement().ShowDialog();
            operationsButton[1].Click += (o, args) => new ProblemManagement().ShowDialog();
            operationsButton[2].Click += (o, args) => new CompetitionManagement().ShowDialog();
            operationsButton[3].Click += (o, args) =>
            {
                if (_judgeLogsForm == null)
                {
                    _judgeLogsForm = new JudgeLogs();
                    _judgeLogsForm.Closed += (sender, eventArgs) => { _judgeLogsForm = null; };
                    _judgeLogsForm.Show();
                }
                else
                {
                    _judgeLogsForm.Activate();
                }
            };
            operationsButton[4].Click += (o, args) => new SendMessaging().ShowDialog();
            operationsButton[5].Click += (o, args) => new MembersManagement().ShowDialog();
            operationsButton[6].Click += (o, args) => new OfflineJudge().Show();
            operationsButton[7].Click += (o, args) => new SystemConfiguration().ShowDialog();
            operationsButton[8].Click += async (o, args) => await Logout();
            operationsButton[9].Click += (o, args) => { Exit(); };
            foreach (var t in operationsButton)
                Operations.Items.Add(t);
        }

        private void SetEnvironmentForAdministrator()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "题目管理"},
                new Button {Height = 32, Width = 80, Content = "比赛管理"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "发送消息"},
                new Button {Height = 32, Width = 80, Content = "成员管理"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "系统设置"},
                new Button {Height = 32, Width = 80, Content = "注销登录"},
                new Button {Height = 32, Width = 80, Content = "退出程序"}
            };
            operationsButton[0].Click += (o, args) => new ProfileManagement().ShowDialog();
            operationsButton[1].Click += (o, args) => new ProblemManagement().ShowDialog();
            operationsButton[2].Click += (o, args) => new CompetitionManagement().ShowDialog();
            operationsButton[3].Click += (o, args) =>
            {
                if (_judgeLogsForm == null)
                {
                    _judgeLogsForm = new JudgeLogs();
                    _judgeLogsForm.Closed += (sender, eventArgs) => { _judgeLogsForm = null; };
                    _judgeLogsForm.Show();
                }
                else
                {
                    _judgeLogsForm.Activate();
                }
            };
            operationsButton[4].Click += (o, args) => new SendMessaging().ShowDialog();
            operationsButton[5].Click += (o, args) => new MembersManagement().ShowDialog();
            operationsButton[6].Click += (o, args) => new OfflineJudge().Show();
            operationsButton[7].Click += (o, args) => new SystemConfiguration().ShowDialog();
            operationsButton[8].Click += async (o, args) => await Logout();
            operationsButton[9].Click += (o, args) => { Exit(); };
            foreach (var t in operationsButton)
                Operations.Items.Add(t);
        }

        private void SetEnvironmentForTeacher()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "题目管理"},
                new Button {Height = 32, Width = 80, Content = "比赛管理"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "发送消息"},
                new Button {Height = 32, Width = 80, Content = "选手管理"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "注销登录"}
            };
            operationsButton[0].Click += (o, args) => new ProfileManagement().ShowDialog();
            operationsButton[1].Click += (o, args) => new ProblemManagement().ShowDialog();
            operationsButton[2].Click += (o, args) => new CompetitionManagement().ShowDialog();
            operationsButton[3].Click += (o, args) =>
            {
                if (_judgeLogsForm == null)
                {
                    _judgeLogsForm = new JudgeLogs();
                    _judgeLogsForm.Closed += (sender, eventArgs) => { _judgeLogsForm = null; };
                    _judgeLogsForm.Show();
                }
                else
                {
                    _judgeLogsForm.Activate();
                }
            };
            operationsButton[4].Click += (o, args) => new SendMessaging().ShowDialog();
            operationsButton[5].Click += (o, args) => new MembersManagement().ShowDialog();
            operationsButton[6].Click += (o, args) => new OfflineJudge().Show();
            operationsButton[7].Click += async (o, args) => await Logout();
            foreach (var t in operationsButton)
                Operations.Items.Add(t);
        }

        private void SetEnvironmentForStudent()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "注销登录"}
            };
            operationsButton[0].Click += (o, args) => new ProfileManagement().ShowDialog();
            operationsButton[1].Click += (o, args) =>
            {
                if (_judgeLogsForm == null)
                {
                    _judgeLogsForm = new JudgeLogs();
                    _judgeLogsForm.Closed += (sender, eventArgs) => { _judgeLogsForm = null; };
                    _judgeLogsForm.Show();
                }
                else
                {
                    _judgeLogsForm.Activate();
                }
            };
            operationsButton[2].Click += (o, args) => new OfflineJudge().Show();
            operationsButton[3].Click += async (o, args) => await Logout();
            foreach (var t in operationsButton)
                Operations.Items.Add(t);
        }

        private void ShowUserInfo()
        {
            Task.Run(() =>
            {
                while (!Connection.IsExited)
                {
                    if (UserHelper.CurrentUser.IsChanged ?? false)
                    {
                        UserHelper.CurrentUser.IsChanged = false;
                        string idnty = null;
                        switch (UserHelper.CurrentUser.Type)
                        {
                            case 1:
                                idnty = "BOSS";
                                break;
                            case 2:
                                idnty = "管理员";
                                break;
                            case 3:
                                idnty = "教师";
                                break;
                            case 4:
                                idnty = "选手";
                                break;
                        }
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Identity.Content = $"{UserHelper.CurrentUser.UserName}，欢迎回来！当前身份：{idnty}";
                        }));
                        if (!string.IsNullOrEmpty(UserHelper.CurrentUser.Icon))
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                UserIcon.Source =
                                    ByteImageConverter.ByteToImage(
                                        Convert.FromBase64String(UserHelper.CurrentUser.Icon));
                            }));
                        else
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(
                                    "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V2WkckhEUgAAACV0RVh0ZGF0ZTpjcmVhdGUAMjAxNi0wOS0xN1QxNToyMjoxNSswODowMCsLl+sAAAAldEVYdGRhdGU6bW9kaWZ5ADIwMTYtMDUtMjZUMDA6NTk6MjErMDg6MDBsVmrEAAAATXRFWHRzb2Z0d2FyZQBJbWFnZU1hZ2ljayA3LjAuMS02IFExNiB4ODZfNjQgMjAxNi0wOS0xNyBodHRwOi8vd3d3LmltYWdlbWFnaWNrLm9yZ93ZpU4AAABjdEVYdHN2Zzpjb21tZW50ACBHZW5lcmF0b3I6IEFkb2JlIElsbHVzdHJhdG9yIDE5LjAuMCwgU1ZHIEV4cG9ydCBQbHVnLUluIC4gU1ZHIFZlcnNpb246IDYuMDAgQnVpbGQgMCkgIM5IkAsAAAAYdEVYdFRodW1iOjpEb2N1bWVudDo6UGFnZXMAMaf/uy8AAAAYdEVYdFRodW1iOjpJbWFnZTo6SGVpZ2h0ADUyOTNy2coAAAAXdEVYdFRodW1iOjpJbWFnZTo6V2lkdGgANTI5oIOJlwAAABl0RVh0VGh1bWI6Ok1pbWV0eXBlAGltYWdlL3BuZz+yVk4AAAAXdEVYdFRodW1iOjpNVGltZQAxNDY0MTk1NTYxyN1ubAAAABJ0RVh0VGh1bWI6OlNpemUAMjIuNktCSPcP1AAAAF90RVh0VGh1bWI6OlVSSQBmaWxlOi8vL2hvbWUvd3d3cm9vdC9zaXRlL3d3dy5lYXN5aWNvbi5uZXQvY2RuLWltZy5lYXN5aWNvbi5jbi9zcmMvMTIwMTQvMTIwMTQwOS5wbmdgVPHMAAAAAElFTkSuQmCC"));
                            }));
                    }
                    Thread.Sleep(1000);
                }
            });
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            ShowInTaskbar = false;
            Visibility = Visibility.Hidden;
            await Logout();
        }

        private async Task Logout()
        {
            var hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5)));
            var showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)));
            var unscratchWidthDaV = new DoubleAnimation(673, 473, new Duration(TimeSpan.FromSeconds(0.25)));
            var unscratchHeightDaV = new DoubleAnimation(328, 228, new Duration(TimeSpan.FromSeconds(0.25)));
            UserName.Text = Connection.Logout();
            UpdateListBoxContent($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {UserName.Text} 在服务端注销");
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                LoginGrid.Visibility = Visibility.Visible;
                ContentGrid.Visibility = Visibility.Hidden;
                Operations.Items.Clear();
            }));
            ContentGrid.BeginAnimation(OpacityProperty, hiddenDaV);
            BeginAnimation(WidthProperty, unscratchWidthDaV);
            await Task.Run(() => { Thread.Sleep(300); });
            BeginAnimation(HeightProperty, unscratchHeightDaV);
            LoginGrid.BeginAnimation(OpacityProperty, showDaV);
        }

        private void UserName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                LoginButton_ClickAsync(null, null);
        }

        private void Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                LoginButton_ClickAsync(null, null);
        }

        private void Exit()
        {
            if (Connection.CurJudgingCnt != 0)
            {
                MessageBox.Show($"仍有 {Connection.CurJudgingCnt} 项评测任务正在进行，不能退出程序", "提示", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            _notifyIcon.Visible = false;
            Connection.IsExited = true;
            Task.Run(() =>
            {
                Connection.LogoutAll();
                Environment.Exit(0);
            });
        }
    }
}