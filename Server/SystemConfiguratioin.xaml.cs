using System;
using System.Windows;

namespace Server
{
    /// <summary>
    ///     Interaction logic for SystemConfiguratioin.xaml
    /// </summary>
    public partial class SystemConfiguratioin : Window
    {
        public SystemConfiguratioin()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Compiler.Text = Configuration.Configurations.Compiler;
            EnvironmentValue.Text = Configuration.Configurations.EnvironmentValues;
            AllowCheckBox.IsChecked = Configuration.Configurations.AllowRequestDataSet;
            MutiThreading.Text = Configuration.Configurations.MutiThreading.ToString();
            Address.Text = Configuration.Configurations.IpAddress;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Configuration.Configurations.Compiler = Compiler.Text;
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
            Close();
        }
    }
}