using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

namespace hjudge_server
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Login_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void UserName_LostFocus(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                UserInfo a = Connection.Query_User(UserName.Text);
                if (a.UserId != 0)
                {

                }
            });
        }
    }

    public class UserInfo
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public int UserId { get; set; }
        public int Type { get; set; }
    }
    public static class Connection
    {
        public static UserInfo Query_User(string userName)
        {
            UserInfo a = new UserInfo()
            {
                UserName = userName,
                Password = "",
                UserId = 0
            };
            return a;
        }
    }
}
