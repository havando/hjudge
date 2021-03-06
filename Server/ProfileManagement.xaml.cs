﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Server
{
    /// <summary>
    ///     Interaction logic for ProfileManagement.xaml
    /// </summary>
    public partial class ProfileManagement : Window
    {
        private UserInfo _myInfo;

        public ProfileManagement()
        {
            InitializeComponent();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            var scratchHeightDaV = new DoubleAnimation(194.618, 270.255, new Duration(TimeSpan.FromSeconds(0.25)));
            BeginAnimation(HeightProperty, scratchHeightDaV);
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            var scratchHeightDaV = new DoubleAnimation(270.255, 194.618, new Duration(TimeSpan.FromSeconds(0.25)));
            BeginAnimation(HeightProperty, scratchHeightDaV);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _myInfo = new UserInfo
            {
                UserName = UserHelper.CurrentUser.UserName,
                Achievement = UserHelper.CurrentUser.Achievement,
                UserId = UserHelper.CurrentUser.UserId,
                Icon = UserHelper.CurrentUser.Icon,
                Password = UserHelper.CurrentUser.Password,
                RegisterDate = UserHelper.CurrentUser.RegisterDate,
                Type = UserHelper.CurrentUser.Type
            };
            UserName.Text = _myInfo.UserName;
            if (!string.IsNullOrEmpty(_myInfo.Icon))
                UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(_myInfo.Icon));
            Id.Content = _myInfo.UserId;
            switch (_myInfo.Type)
            {
                case 1:
                    Identity.Content = "BOSS";
                    break;
                case 2:
                    Identity.Content = "管理员";
                    break;
                case 3:
                    Identity.Content = "教师";
                    break;
                case 4:
                    Identity.Content = "选手";
                    break;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(NewPassword.Password))
                if (NewPassword.Password == ConfirmPassword.Password)
                {
                    SHA256 s = new SHA256CryptoServiceProvider();
                    var retVal = s.ComputeHash(Encoding.Unicode.GetBytes(NewPassword.Password));
                    var sb = new StringBuilder();
                    foreach (var t in retVal)
                        sb.Append(t.ToString("x2"));
                    _myInfo.Password = sb.ToString();
                }
                else
                {
                    MessageBox.Show("两次输入的密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            _myInfo.UserName = UserName.Text;
            Connection.UpdateUserInfo(_myInfo);
            UserHelper.SetCurrentUser(_myInfo.UserId, _myInfo.UserName, _myInfo.RegisterDate, _myInfo.Password,
                _myInfo.Type, _myInfo.Icon, _myInfo.Achievement);
            Close();
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
                    _myInfo.Icon = ByteImageConverter.ImageToByte(fs);
                    UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(_myInfo.Icon));
                }
        }
    }
}