using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Server
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            Height = 228; Width = 473;
            LoginGrid.Margin = new Thickness(10, 10, 0, 0);
            ContentGrid.Margin = new Thickness(10, 10, 0, 0);
            ContentGrid.Opacity = 0;

            if (!Directory.Exists(Environment.CurrentDirectory + "\\AppData"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\AppData");
            }
            if (!Directory.Exists(Environment.CurrentDirectory + "\\Problems"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\Problems");
            }
            if (!Directory.Exists(Environment.CurrentDirectory + "\\Data"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\Data");
            }
            Connection.Init();
        }

        private async void LoginButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = false;
            int res = await Connection.Login(UserName.Text, Password.Password);
            switch (res)
            {
                case 1:
                    {
                        MessageBox.Show("用户名或密码错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }
                default:
                    {
                        if (res == 0)
                        {
                            MessageBox.Show("未知错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;
                    }
            }
            LoginButton.IsEnabled = true;
            if (res != 0)
            {
                UserName.Text = "";
                Password.Password = "";
                DoubleAnimation hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(1)));
                DoubleAnimation showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(1)));
                DoubleAnimation scratchWidthDaV = new DoubleAnimation(473, 673, new Duration(TimeSpan.FromSeconds(0.5)));
                DoubleAnimation scratchHeightDaV = new DoubleAnimation(228, 328, new Duration(TimeSpan.FromSeconds(0.5)));
                LoginGrid.BeginAnimation(OpacityProperty, hiddenDaV);
                BeginAnimation(WidthProperty, scratchWidthDaV);
                await Task.Run(() => { Thread.Sleep(500); });
                BeginAnimation(HeightProperty, scratchHeightDaV);
                ContentGrid.BeginAnimation(OpacityProperty, showDaV);
                Operations.Items.Add(new Button() { Height = 32, Width = 80, Content = "编辑信息" });
                Operations.Items.Add(new Button() { Height = 32, Width = 80, Content = "题目管理" });
                Operations.Items.Add(new Button() { Height = 32, Width = 80, Content = "评测日志" });
                Operations.Items.Add(new Button() { Height = 32, Width = 80, Content = "发送消息" });
                Operations.Items.Add(new Button() { Height = 32, Width = 80, Content = "选手管理" });
                Operations.Items.Add(new Button() { Height = 32, Width = 80, Content = "系统设置" });
                Operations.Items.Add(new Button() { Height = 32, Width = 80, Content = "退出程序" });
            }
        }
    }
}
