using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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

        private readonly ObservableCollection<ClientInfo> _myClientInfo = Connection.GetAllConnectedClient();
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
            foreach (var i in x)
            {
                Connection.SendMsg($"发送时间：{DateTime.Now}\r\n内容：\r\n{Msg.Text}", i.ConnId);
            }
            Msg.Text = string.Empty;
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            var clickedColumn = (e.OriginalSource as GridViewColumnHeader)?.Column;
            if (clickedColumn == null) return;
            var bindingProperty = (clickedColumn.DisplayMemberBinding as Binding)?.Path.Path;
            var sdc = ListView.Items.SortDescriptions;
            var sortDirection = ListSortDirection.Ascending;
            if (sdc.Count > 0)
            {
                var sd = sdc[0];
                sortDirection = (ListSortDirection)(((int)sd.Direction + 1) % 2);
                sdc.Clear();
            }
            sdc.Add(new SortDescription(bindingProperty, sortDirection));
        }
    }
}
