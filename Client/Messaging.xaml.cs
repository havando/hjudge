using System;
using System.Windows;

namespace Client
{
    /// <summary>
    ///     Interaction logic for Messaging.xaml
    /// </summary>
    public partial class Messaging : Window
    {
        public Messaging()
        {
            InitializeComponent();
        }

        public void SetMessge(string msg, string sendDate, string sendUser)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Contents.Text = msg;
                SendInfo.Content = $"（用户名：{sendUser}，发送时间：{sendDate}）";
            }));
        }
    }
}