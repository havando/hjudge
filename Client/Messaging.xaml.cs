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

        public void SetMessge(Message msg)
        {
            _userName = msg.User;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClientMsg.Text = msg.Content;
                SendDate.Content = $"发送时间：{msg.DisplayDateTime}";
                SendUser.Content = $"发送用户：{msg.User}";
            }));
            if (msg.State == 0)
                Connection.SendData("SetMsgState", msg.MsgId + Connection.Divpar + "1");
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
            Connection.SendMsg($"回复消息：\n{ClientMsg.Text}\n消息内容：\n{MyMsg.Text}", _userName);
            Close();
        }
    }
}