using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Interaction logic for MembersManagementPage.xaml
    /// </summary>
    public partial class MembersManagementPage : Page
    {
        public MembersManagementPage()
        {
            InitializeComponent();
        }
        private readonly List<UserInfo> _toDelete = new List<UserInfo>();
        private UserInfo _curItem = new UserInfo();
        private ObservableCollection<UserInfo> _curUserList = new ObservableCollection<UserInfo>();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var t = Connection.GetUserBelongings();
            _curItem = new UserInfo();
            _toDelete?.Clear();
            UserIdentity.Items.Clear();
            UserEdit.Visibility = Visibility.Hidden;
            switch (t.type)
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
            }
            UserIdentity.SelectedIndex = 0;
            _curUserList.Clear();
            foreach (var i in t.list)
                _curUserList.Add(i);
            ListView.ItemsSource = _curUserList;
            ListView.Items.Refresh();
        }

        private void NewUser_Click(object sender, RoutedEventArgs e)
        {
            UserEdit.Visibility = Visibility.Visible;
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = false;
            PasswordReset.Visibility = Visibility.Hidden;
            _curItem.UserId = -1;
            UserName.IsEnabled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UserName.Text = string.Empty;
            UserEdit.Visibility = Visibility.Hidden;
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = true;
        }

        private void PasswordReset_Click(object sender, RoutedEventArgs e)
        {
            _curItem.Password =
                    "ec278a38901287b2771a13739520384d43e4b078f78affe702def108774cce24";
            MessageBox.Show("密码已重置为初始密码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (!(ListView.SelectedItem is UserInfo userInfo)) return;
            _curItem = userInfo;
            UserIdentity.SelectedItem = userInfo.Type2;
            UserName.Text = userInfo.UserName;
            UserName.IsEnabled = false;
            UserEdit.Visibility = Visibility.Visible;
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = false;
            PasswordReset.Visibility = Visibility.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (_curItem.UserId == -1)
                _curUserList.Add(new UserInfo
                {
                    UserId = 0,
                    UserName = UserName.Text,
                    Type2 = UserIdentity.Text,
                    Password = "ec278a38901287b2771a13739520384d43e4b078f78affe702def108774cce24",
                    RegisterDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                });
            else
            {
                _curItem.Type2 = UserIdentity.Text;
                _curItem.IsChanged = true;
            }
            UserEdit.Visibility = Visibility.Hidden;
            UserName.Text = string.Empty;
            NewUser.IsEnabled = EditUser.IsEnabled =
                DeleteUser.IsEnabled = OkButton.IsEnabled = true;
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (!(ListView.SelectedItem is UserInfo userInfo)) return;
            if (userInfo.UserId != 0)
                _toDelete.Add(userInfo);
            _curUserList.Remove(userInfo);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Connection.UpdateUserBelongings(_toDelete, _curUserList.ToList());
            Page_Loaded(null, null);
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
            if (bindingProperty != null) sdc.Add(new SortDescription(bindingProperty, sortDirection));
        }
    }
    public static partial class Connection
    {
        private static bool _getUserBelongingsState = false;
        private static int _getUserBelongingsType;
        private static List<UserInfo> _getUserBelongingsResult;
        private static bool _updateUserBelongingsState = false;

        public static (int type, List<UserInfo> list) GetUserBelongings()
        {
            _getUserBelongingsState = false;
            _getUserBelongingsResult?.Clear();
            SendData("GetUserBelongings", string.Empty);
            while (!_getUserBelongingsState)
            {
                Thread.Sleep(1);
            }
            return (_getUserBelongingsType, _getUserBelongingsResult);
        }
        public static void UpdateUserBelongings(List<UserInfo> toDelete, List<UserInfo> toUpdate)
        {
            _updateUserBelongingsState = false;
            var t = new List<List<UserInfo>>
            {
                toDelete,
                toUpdate
            };
            SendData("UpdateUserBelongings", JsonConvert.SerializeObject(t));
            while (!_updateUserBelongingsState)
            {
                Thread.Sleep(1);
            }
            return;
        }
    }
}
