using System;
using System.Windows;

namespace Server
{
    /// <summary>
    ///     Interaction logic for Messaging.xaml
    /// </summary>
    public partial class Messaging : Window
    {
        private int _userId;

        public Messaging()
        {
            InitializeComponent();
        }

        public void SetMessage(Message msg)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClientMsg.Text = msg.Content;
                SendDate.Content = $"发送时间：{msg.DisplayDateTime}";
                SendUser.Content = $"相关用户：{msg.User}";
                if (msg.Direction == "发送")
                {
                    MyMsg.IsEnabled = false;
                    MyMsg.Visibility = Visibility.Hidden;
                    Width = 368;
                }
            }));
            _userId = Connection.GetUserId(msg.User);
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
            Connection.SendMsg($"回复消息：\n{ClientMsg.Text}\n消息内容：\n{MyMsg.Text}",
                UserHelper.CurrentUser.UserId == 0 ? 1 : UserHelper.CurrentUser.UserId, _userId);
            Close();
        }
    }
}