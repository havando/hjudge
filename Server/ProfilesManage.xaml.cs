using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Server
{
    /// <summary>
    /// Interaction logic for ProfilesManage.xaml
    /// </summary>
    public partial class ProfilesManage : Window
    {
        public ProfilesManage()
        {
            InitializeComponent();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            DoubleAnimation scratchHeightDaV = new DoubleAnimation(194.618, 270.255, new Duration(TimeSpan.FromSeconds(0.25)));
            BeginAnimation(HeightProperty, scratchHeightDaV);
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            DoubleAnimation scratchHeightDaV = new DoubleAnimation(270.255, 194.618, new Duration(TimeSpan.FromSeconds(0.25)));
            BeginAnimation(HeightProperty, scratchHeightDaV);
        }

        private UserInfo _myInfo;
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
            if (!String.IsNullOrEmpty(_myInfo.Icon)) { UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(_myInfo.Icon)); }
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
            if (!String.IsNullOrEmpty(NewPassword.Password))
            {
                if (NewPassword.Password == ConfirmPassword.Password)
                {
                    SHA256 s = new SHA256CryptoServiceProvider();
                    byte[] retVal = s.ComputeHash(Encoding.Unicode.GetBytes(NewPassword.Password));
                    StringBuilder sb = new StringBuilder();
                    foreach (var t in retVal)
                    {
                        sb.Append(t.ToString("x2"));
                    }
                    _myInfo.Password = sb.ToString();
                }
                else
                {
                    MessageBox.Show("两次输入的密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            _myInfo.UserName = UserName.Text;
            if (Connection.UpdateUserInfo(_myInfo))
            {
                UserHelper.SetCurrentUser(_myInfo.UserId, _myInfo.UserName, _myInfo.RegisterDate, _myInfo.Password,
                    _myInfo.Type, _myInfo.Icon, _myInfo.Achievement);
                Close();
            }
            else
            {
                MessageBox.Show("修改失败", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UserIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog ofg = new OpenFileDialog
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
                    FileStream fs = new FileStream(ofg.FileName, FileMode.Open, FileAccess.Read);
                    _myInfo.Icon = ByteImageConverter.ImageToByte(fs);
                    UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(_myInfo.Icon));
                }
            }
        }
    }
}
