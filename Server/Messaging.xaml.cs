using System;
using System.Windows;

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

        private IntPtr _id = IntPtr.Zero;
        public void SetMessage(string msg, IntPtr id, string userName)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClientMsg.Text = msg;
                SendDate.Content = $"发送时间：{DateTime.Now:yyyy/MM/dd HH:mm:ss:ffff}";
                SendUser.Content = $"发送用户：{userName}";
            }));
            _id = id;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Connection.SendMsg($"回复消息：\r\n{ClientMsg.Text}\r\n消息内容：\r\n{MyMsg.Text}", _id);
            Close();
        }
    }
}
