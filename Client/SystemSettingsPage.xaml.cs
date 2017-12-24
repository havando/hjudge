using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace Client
{
    /// <summary>
    ///     Interaction logic for SystemSettingsPage.xaml
    /// </summary>
    public partial class SystemSettingsPage : Page
    {
        public SystemSettingsPage()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            var config = Connection.GetServerConfig();
            AllowCheckBox.IsChecked = config.AllowRequestDataSet;
            AllowMessagingCheckBox.IsChecked = config.AllowCompetitorMessaging;
            MutiThreading.Text = config.MutiThreading.ToString();
            if (config.RegisterMode == 0)
            {
                BanRegister.IsChecked = true;
                InquiryRegister.IsChecked = false;
                AllowRegister.IsChecked = false;
            }
            if (config.RegisterMode == 1)
            {
                BanRegister.IsChecked = false;
                InquiryRegister.IsChecked = true;
                AllowRegister.IsChecked = false;
            }
            if (config.RegisterMode == 2)
            {
                BanRegister.IsChecked = false;
                InquiryRegister.IsChecked = false;
                AllowRegister.IsChecked = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(MutiThreading.Text) < 0)
                {
                    MessageBox.Show("输入不合法，无法保存", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch
            {
                MessageBox.Show("输入不合法，无法保存", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var config = new ServerConfig
            {
                AllowCompetitorMessaging = AllowMessagingCheckBox.IsChecked ?? false,
                AllowRequestDataSet = AllowCheckBox.IsChecked ?? false,
                MutiThreading = Convert.ToInt32(MutiThreading.Text),
                RegisterMode = (BanRegister.IsChecked ?? false) ? 0 : (InquiryRegister.IsChecked ?? false) ? 1 : 2
            };
            Connection.UpdateServerConfig(config);
        }
    }

    public static partial class Connection
    {
        private static ServerConfig _getServerConfigResult;
        private static bool _getServerConfigState;
        private static bool _updateServerConfigState;

        public static ServerConfig GetServerConfig()
        {
            _getServerConfigState = false;
            SendData("GetServerConfig", string.Empty);
            while (!_getServerConfigState)
                Thread.Sleep(1);
            return _getServerConfigResult;
        }

        public static void UpdateServerConfig(ServerConfig serverConfig)
        {
            _updateServerConfigState = false;
            SendData("UpdateServerConfig", JsonConvert.SerializeObject(serverConfig));
            while (!_updateServerConfigState)
                Thread.Sleep(1);
        }
    }
}