using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Server
{
    /// <summary>
    /// Interaction logic for JudgeLogs.xaml
    /// </summary>
    public partial class JudgeLogs : Window
    {
        public JudgeLogs()
        {
            InitializeComponent();
        }

        private ObservableCollection<JudgeInfo> _curJudgeInfo;

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Connection.ClearJudgeLog();
            _curJudgeInfo.Clear();
            Code.Text = JudgeDetails.Text = string.Empty;
        }

        private void Label_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            _curJudgeInfo = Connection.QueryJudgeLog();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var a = ListView.SelectedItem as JudgeInfo;
            if (a == null) return;
            Code.Text = a.Code;
            var details = string.Empty;
            if (a.Result != null)
            {
                for (var i = 0; i < a.Result.Length; i++)
                {
                    details +=
                        $"#{i + 1} 时间：{a.Timeused[i]}ms，内存：{a.Memoryused[i]}kb，退出代码：{a.Exitcode[i]}，结果：{a.Result[i]}，分数： {a.Score[i]}\r\n";
                }
            }
            JudgeDetails.Text = details;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _curJudgeInfo = Connection.QueryJudgeLog();
            ListView.ItemsSource = _curJudgeInfo;
            if (UserHelper.CurrentUser.Type >= 3) { ClearLabel.Visibility = Visibility.Hidden; }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView.Height = ActualHeight - 82.149;
            ListView.Width = (ActualWidth - 24) * 0.4;
            JudgeDetails.Width = Code.Width = (ActualWidth - 48) * 0.6;
            JudgeDetails.Height = (ActualHeight - 48) * 0.4;
            Code.Height = (ActualHeight - 72) * 0.6;
            JudgeDetails.Margin = new Thickness(ListView.Width + 15, 10, 0, 0);
            Code.Margin = new Thickness(ListView.Width + 15, JudgeDetails.Height + 15, 0, 0);
            Refresh.Margin = new Thickness(ListView.Width - 20, ListView.Height + 10, 0, 0);
            ClearLabel.Margin = new Thickness(ListView.Width - 20 - 39, ListView.Height + 10, 0, 0);
            ExportLabel.Margin = new Thickness(10, ListView.Height + 10, 0, 0);
        }

        private void Export_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var a = JsonConvert.SerializeObject((from c in _curJudgeInfo where c.IsChecked select c).Select(JsonConvert.SerializeObject).ToList());
            var sfg = new SaveFileDialog
            {
                Title = "保存导出数据：",
                Filter = "Json 文件 (.json)|*.json"
            };
            if (sfg.ShowDialog() == true)
            {
                var s = sfg.OpenFile();
                var tmp = Encoding.Unicode.GetBytes(a);
                s.Write(tmp, 0, tmp.Length);
            }
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
