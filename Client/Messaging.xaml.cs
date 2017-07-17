using System;
using System.Windows;

namespace Client
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

        public void SetMessge(string msg, string sendDate)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Contents.Text = msg;
                SendDate.Content = $"（发送时间：{sendDate}）";
            }));
        }
    }

}
