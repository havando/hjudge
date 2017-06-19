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
    /// Interaction logic for Messaging.xaml
    /// </summary>
    public partial class Messaging : Window
    {
        public Messaging()
        {
            InitializeComponent();
        }

        private string _msg = "";
        private IntPtr _id = IntPtr.Zero;
        public void SetMessage(string msg, IntPtr id)
        {
            _msg = msg;
            _id = id;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ClientMsg.Text = _msg;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Connection.SendMsg($"回复时间：{DateTime.Now}\r\n回复消息：\r\n{_msg}\r\n消息内容：\r\n{MyMsg.Text}", _id);
        }
    }
}
