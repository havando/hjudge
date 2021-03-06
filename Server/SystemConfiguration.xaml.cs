﻿using System;
using System.ComponentModel;
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
            AllowMessagingCheckBox.IsChecked = Configuration.Configurations.AllowCompetitorMessaging;
            MutiThreading.Text = Configuration.Configurations.MutiThreading.ToString();
            Address.Text = Configuration.Configurations.IpAddress;
            if (Configuration.Configurations.RegisterMode == 0)
            {
                BanRegister.IsChecked = true;
                InquiryRegister.IsChecked = false;
                AllowRegister.IsChecked = false;
            }
            if (Configuration.Configurations.RegisterMode == 1)
            {
                BanRegister.IsChecked = false;
                InquiryRegister.IsChecked = true;
                AllowRegister.IsChecked = false;
            }
            if (Configuration.Configurations.RegisterMode == 2)
            {
                BanRegister.IsChecked = false;
                InquiryRegister.IsChecked = false;
                AllowRegister.IsChecked = true;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            new CompilerConfiguration().ShowDialog();
            CurCompCnt.Content = $"当前数量：{Configuration.Configurations.Compiler.Count}";
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Configuration.Configurations.EnvironmentValues = EnvironmentValue.Text;
            Configuration.Configurations.AllowRequestDataSet = AllowCheckBox.IsChecked ?? false;
            Configuration.Configurations.AllowCompetitorMessaging = AllowMessagingCheckBox.IsChecked ?? false;
            try
            {
                if (Configuration.Configurations.IpAddress != Address.Text)
                    MessageBox.Show("检测到您更改了主机地址信息，需要重新启动本程序才能生效", "提示", MessageBoxButton.OK,
                        MessageBoxImage.Information);
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
            if (BanRegister.IsChecked ?? false)
                Configuration.Configurations.RegisterMode = 0;
            if (InquiryRegister.IsChecked ?? false)
                Configuration.Configurations.RegisterMode = 1;
            if (AllowRegister.IsChecked ?? false)
                Configuration.Configurations.RegisterMode = 2;
            Configuration.Save();
        }
    }
}