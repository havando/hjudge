using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Server
{
    /// <summary>
    /// Interaction logic for SendMessaging.xaml
    /// </summary>
    public partial class SendMessaging : Window
    {
        public SendMessaging()
        {
            InitializeComponent();
        }

        private readonly List<ClientInfo> _myClientInfo = Connection.GetAllConnectedClient();
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ListView.ItemsSource = _myClientInfo;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var x = from c in _myClientInfo where c.IsChecked select c;
            Task.Run(() =>
            {
                foreach (var i in x)
                {
                    Connection.SendMsg($"发送时间：{DateTime.Now}\r\n内容：\r\n{Msg.Text}", i.ConnId);
                }
            });
        }
    }
}
