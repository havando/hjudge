using System;
using System.Windows;

namespace Server
{
    /// <summary>
    ///     Interaction logic for SystemConfiguration.xaml
    /// </summary>
    public partial class SystemConfiguration : Window
    {
        public SystemConfiguration()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CurCompCnt.Content = $"当前数量：{Configuration.Configurations.Compiler.Count}";
            EnvironmentValue.Text = Configuration.Configurations.EnvironmentValues;
            AllowCheckBox.IsChecked = Configuration.Configurations.AllowRequestDataSet;
            MutiThreading.Text = Configuration.Configurations.MutiThreading.ToString();
            Address.Text = Configuration.Configurations.IpAddress;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            new CompilerConfiguration().ShowDialog();
            CurCompCnt.Content = $"当前数量：{Configuration.Configurations.Compiler.Count}";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Configuration.Configurations.EnvironmentValues = EnvironmentValue.Text;
            Configuration.Configurations.AllowRequestDataSet = AllowCheckBox.IsChecked ?? false;
            try
            {
                if (Configuration.Configurations.IpAddress != Address.Text)
                {
                    MessageBox.Show("检测到您更改了主机地址信息，需要重新启动本程序才能生效", "提示", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch
            {
                //ignored
            }
            Configuration.Configurations.IpAddress = string.IsNullOrEmpty(Address.Text) ? "127.0.0.1" : Address.Text;
            int mt;
            try
            {
                mt = Convert.ToInt32(MutiThreading.Text);
            }
            catch
            {
                mt = Configuration.Configurations.MutiThreading;
            }
            if (mt < 0)
                mt = Configuration.Configurations.MutiThreading;
            Configuration.Configurations.MutiThreading = mt;
            Configuration.Save();
        }
    }
}