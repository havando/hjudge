using System;
using System.Windows;

namespace Client
{
    /// <summary>
    ///     Interaction logic for Messaging.xaml
    /// </summary>
    public partial class Messaging : Window
    {
        private string _userName = string.Empty;

        public Messaging()
        {
            InitializeComponent();
        }

        public void SetMessge(string msg, string sendDate, string sendUser)
        {
            _userName = sendUser;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClientMsg.Text = msg;
                SendDate.Content = $"发送时间：{DateTime.Now:yyyy/MM/dd HH:mm:ss}";
                SendUser.Content = $"发送用户：{sendUser}";
            }));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (MyMsg.Text.Length > 1048576)
            {
                MessageBox.Show("消息过长，无法发送", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Connection.SendMsg($"回复消息：\r\n{ClientMsg.Text}\r\n消息内容：\r\n{MyMsg.Text}", _userName);
            Close();
        }
    }
}