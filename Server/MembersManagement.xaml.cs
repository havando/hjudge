using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Server
{
    /// <summary>
    /// Interaction logic for MembersManagement.xaml
    /// </summary>
    public partial class MembersManagement : Window
    {
        private UserInfo _curItem = new UserInfo();
        private readonly List<int> _toDelete = new List<int>();
        public MembersManagement()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UserEdit.Visibility = Visibility.Hidden;
            switch (UserHelper.CurrentUser.Type)
            {
                case 1:
                    Identity.Content = "BOSS";
                    UserIdentity.Items.Add("管理员");
                    UserIdentity.Items.Add("教师");
                    UserIdentity.Items.Add("选手");
                    break;
                case 2:
                    Identity.Content = "管理员";
                    UserIdentity.Items.Add("教师");
                    UserIdentity.Items.Add("选手");
                    break;
                case 3:
                    Identity.Content = "教师";
                    UserIdentity.Items.Add("选手");
                    break;
                default:
                    Close();
                    break;
            }
            UserIdentity.SelectedIndex = 0;
            ListView.ItemsSource = UserHelper.UsersBelongs;
        }

        private void NewUser_Click(object sender, RoutedEventArgs e)
        {
            UserEdit.Visibility = Visibility.Visible;
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = Cancel.IsEnabled = false;
            PasswordReset.Visibility = Visibility.Hidden;
            _curItem.UserId = -1;
            UserName.IsEnabled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UserName.Text = "";
            UserEdit.Visibility = Visibility.Hidden;
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = Cancel.IsEnabled = true;
        }

        private void PasswordReset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in UserHelper.UsersBelongs)
            {
                if (t.UserId == _curItem.UserId)
                {
                    t.Password =
                        "ec278a38901287b2771a13739520384d43e4b078f78affe702def108774cce24";
                    break;
                }
            }
            MessageBox.Show("密码已重置为初始密码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            var userInfo = ListView.SelectedItem as UserInfo;
            if (userInfo == null) return;
            _curItem = userInfo;
            UserIdentity.SelectedItem = userInfo.Type2;
            UserName.Text = userInfo.UserName;
            UserName.IsEnabled = false;
            UserEdit.Visibility = Visibility.Visible;
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = Cancel.IsEnabled = false;
            PasswordReset.Visibility = Visibility.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (_curItem.UserId == -1)
            {
                UserHelper.UsersBelongs.Add(new UserInfo()
                {
                    UserId = 0,
                    UserName = UserName.Text,
                    Type2 = UserIdentity.Text,
                    Password = "ec278a38901287b2771a13739520384d43e4b078f78affe702def108774cce24",
                    RegisterDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                });
            }
            else
            {
                foreach (var t in UserHelper.UsersBelongs)
                {
                    if (t.UserName != _curItem.UserName) continue;
                    t.UserName = UserName.Text;
                    t.Type2 = UserIdentity.Text;
                    t.IsChanged = true;
                    break;
                }
            }
            UserEdit.Visibility = Visibility.Hidden;
            UserName.Text = "";
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = Cancel.IsEnabled = true;
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var userInfo = ListView.SelectedItem as UserInfo;
            if (userInfo != null)
            {
                if (userInfo.UserId != 0)
                {
                    _toDelete.Add(userInfo.UserId);
                }
            }
            foreach (var t in UserHelper.UsersBelongs)
            {
                if (userInfo != null && t.UserName != userInfo.UserName) continue;
                UserHelper.UsersBelongs.Remove(t);
                break;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            UserHelper.GetUserBelongs();
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var failed = Connection.SaveUser(_toDelete);
            var t = "";
            for (var i = 0; i < failed.Count; i++)
            {
                if (i != failed.Count - 1)
                {
                    t += failed[i] + "、";
                }
                else
                {
                    t += failed[i];
                }
            }
            if (failed.Count != 0)
            {
                MessageBox.Show("以下用户未能保存，因为已存在相同用户名的用户：\r\n" + t, "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UserHelper.GetUserBelongs();
            Close();
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var clickedColumn = (e.OriginalSource as GridViewColumnHeader)?.Column;
            if (clickedColumn == null) return;
            var bindingProperty = (clickedColumn.DisplayMemberBinding as Binding)?.Path.Path;
            var sdc = ListView.Items.SortDescriptions;
            var sortDirection = ListSortDirection.Ascending;
            if (sdc.Count > 0)
            {
                var sd = sdc[0];
                sortDirection = (ListSortDirection)(((int)sd.Direction + 1) % 2);
                sdc.Clear();
            }
            sdc.Add(new SortDescription(bindingProperty, sortDirection));
        }
    }
}
