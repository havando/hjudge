using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Server
{
    /// <summary>
    ///     Interaction logic for SendMessaging.xaml
    /// </summary>
    public partial class SendMessaging : Window
    {
        private readonly ObservableCollection<UserInfo> _myClientInfo = Connection.GetUsersBelongs(1);

        public SendMessaging()
        {
            InitializeComponent();
        }

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
                Connection.SendMsg($"{Msg.Text}",
                    UserHelper.CurrentUser.UserId == 0 ? 1 : UserHelper.CurrentUser.UserId, i.UserId, null);
            Msg.Clear();
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clickedColumn = (e.OriginalSource as GridViewColumnHeader)?.Column;
                if (clickedColumn == null) return;
                var bindingProperty = (clickedColumn.DisplayMemberBinding as Binding)?.Path.Path;
                var sdc = ListView.Items.SortDescriptions;
                var sortDirection = ListSortDirection.Ascending;
                if (sdc.Count > 0)
                {
                    var sd = sdc[0];
                    sortDirection = (ListSortDirection) (((int) sd.Direction + 1) % 2);
                    sdc.Clear();
                }
                if (bindingProperty != null) sdc.Add(new SortDescription(bindingProperty, sortDirection));
            }
            catch
            {
                //ignored
            }
        }

        private void CheckBox_OnClick(object sender, RoutedEventArgs e)
        {
            var p = _myClientInfo.Count(i => i.IsChecked);
            if (p == _myClientInfo.Count)
            {
                foreach (var i in _myClientInfo)
                    i.IsChecked = false;
                CheckBox.IsChecked = false;
            }
            else
            {
                foreach (var i in _myClientInfo)
                    i.IsChecked = true;
                CheckBox.IsChecked = true;
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var p = _myClientInfo.Count(i => i.IsChecked);
            CheckBox.IsChecked = p == _myClientInfo.Count;
        }
    }
}