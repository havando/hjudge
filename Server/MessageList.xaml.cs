using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Interaction logic for MessageList.xaml
    /// </summary>
    public partial class MessageList : Window
    {
        public ObservableCollection<Message> _messages = new ObservableCollection<Message>();

        public MessageList()
        {
            InitializeComponent();
            MessagesList.ItemsSource = _messages;
            LoadMsg();
        }

        private void LoadMsg()
        {
            _messages.Clear();
            Task.Run(() =>
            {
                foreach (var i in Connection.QueryMsg(1, false))
                {
                    Dispatcher.Invoke(() => _messages.Add(i));
                }
            });
        }

        private void MessagesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MessagesList.SelectedItem is Message t)
            {
                if (t.Content.Length == 33)
                {
                    t.Content = Connection.GetMsg(t.MsgId).Content;
                }
                var y = new Messaging();
                y.SetMessage(t);
                y.Show();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMsg();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            LoadMsg();
        }
    }
}
