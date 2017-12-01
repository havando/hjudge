using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Server
{
    /// <summary>
    ///     Interaction logic for MessageList.xaml
    /// </summary>
    public partial class MessageList : Window
    {
        private readonly ObservableCollection<Message> _messages = new ObservableCollection<Message>();

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
                var t = Connection.QueryMsg(1, true);
                t.Reverse();
                foreach (var i in t)
                    Dispatcher.Invoke(() => _messages.Add(i));
            });
        }

        private void MessagesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            while (originalSource != null && !(originalSource is ListViewItem))
                originalSource = VisualTreeHelper.GetParent(originalSource);
            if (originalSource == null) return;
            if (!(sender is ListView)) return;
            if (!(MessagesList.SelectedItem is Message t)) return;
            var y = new Messaging();
            y.SetMessage(t);
            y.Show();
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