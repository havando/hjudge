using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Windows.Media.Animation;

namespace Client
{
    /// <summary>
    /// Interaction logic for JudgingLogsPage.xaml
    /// </summary>
    public partial class JudgingLogsPage : Page
    {
        private ObservableCollection<JudgeInfo> _curJudgeInfo = new ObservableCollection<JudgeInfo>();

        public JudgingLogsPage()
        {
            InitializeComponent();
        }


        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MessageBox.Show("你确定要清空数据吗？清空后不可恢复！", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                MessageBoxResult.Yes)
            {
                Connection.SendData("ClearJudgingLogs", string.Empty);
                _curJudgeInfo.Clear();
                Code.Text = JudgeDetails.Text = string.Empty;
                CheckBox.IsChecked = false;
            }
        }

        private void Label_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            _curJudgeInfo = Connection.QueryJudgeLog();
            ListView.ItemsSource = _curJudgeInfo;
            ListView.Items.Refresh();
            CheckBox.IsChecked = false;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ListView.SelectedItem is JudgeInfo a)) return;
            Code.Text = "代码：\r\n" + a.Code;
            var details = "详情：\r\n";
            if (a.Result != null)
                for (var i = 0; i < a.Result.Length; i++)
                    details +=
                        $"#{i + 1} 时间：{a.Timeused[i]}ms，内存：{a.Memoryused[i]}kb，退出代码：{a.Exitcode[i]}，结果：{a.Result[i]}，分数：{a.Score[i]}\r\n";
            JudgeDetails.Text = details;
        }


        private void Export_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var a = (from c in _curJudgeInfo where c.IsChecked select c).ToList();
            if (a.Any(i => i.ResultSummery == "Judging..."))
            {
                MessageBox.Show("你选择的项目中部分仍在评测，请等待评测完毕再导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var sfg = new SaveFileDialog
            {
                Title = "保存导出数据：",
                Filter = "Excel 文件|*.xlsx"
            };
            if (!(sfg.ShowDialog() ?? false)) return;
            if (a.Any())
            {
                var dt = new DataTable("结果");
                dt.Columns.Add("姓名");
                dt.Columns.Add("题目名称");
                dt.Columns.Add("评测时间");
                dt.Columns.Add("最长时间 (ms)");
                dt.Columns.Add("最大内存 (kb)");
                dt.Columns.Add("结果");
                dt.Columns.Add("分数");
                dt.Columns.Add("代码 (Base64)");
                dt.Columns.Add("代码类型");
                foreach (var i in a)
                {
                    var dr = dt.NewRow();
                    dr[0] = i?.UserName ?? string.Empty;
                    dr[1] = i?.ProblemName ?? string.Empty;
                    dr[2] = i?.JudgeDate ?? string.Empty;
                    dr[3] = i?.Timeused.Max() ?? 0;
                    dr[4] = i?.Memoryused.Max() ?? 0;
                    dr[5] = i?.ResultSummery ?? string.Empty;
                    dr[6] = i?.FullScore ?? 0;
                    try
                    {
                        var bytes = Encoding.Default.GetBytes(i?.Code ?? string.Empty);
                        dr[7] = Convert.ToBase64String(bytes);
                    }
                    catch
                    {
                        dr[7] = string.Empty;
                    }
                    dr[8] = i?.Type ?? string.Empty;
                    dt.Rows.Add(dr);
                }
                try
                {
                    ExcelUtility.CreateExcel(sfg.FileName, new[] {dt}, new[] {"结果"});
                    MessageBox.Show("导出成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败，因为 {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("没有要导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var p = _curJudgeInfo.Count(i => i.IsChecked);
            if (p == _curJudgeInfo.Count)
            {
                foreach (var i in _curJudgeInfo)
                    i.IsChecked = false;
                CheckBox.IsChecked = false;
            }
            else
            {
                foreach (var i in _curJudgeInfo)
                    i.IsChecked = true;
                CheckBox.IsChecked = true;
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var p = _curJudgeInfo.Count(i => i.IsChecked);
            CheckBox.IsChecked = p == _curJudgeInfo.Count;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var rtf = new RotateTransform
            {
                CenterX = Dealing.ActualWidth * 0.5,
                CenterY = Dealing.ActualHeight * 0.5
            };
            var daV = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            Dealing.RenderTransform = rtf;
            rtf.BeginAnimation(RotateTransform.AngleProperty, daV);
            Dealing.Visibility = Visibility.Visible;
            _curJudgeInfo.Clear();
            ListView.ItemsSource = _curJudgeInfo;
            Task.Run(() =>
            {
                foreach (var judgeInfo in Connection.QueryJudgeLog())
                {
                    Dispatcher.Invoke(() => _curJudgeInfo.Add(judgeInfo));
                }
                Dispatcher.Invoke(() => Dealing.Visibility = Visibility.Hidden);
            });
        }
    }

    public static partial class Connection
    {
        private static bool _queryJudgeLogResultState;
        private static ObservableCollection<JudgeInfo> _queryJudgeLogResult;
        public static ObservableCollection<JudgeInfo> QueryJudgeLog()
        {
            _queryJudgeLogResultState = false;
            _queryProblemsResult = new ObservableCollection<Problem>();
            SendData("QueryJudgeLogs", string.Empty);
            while (!_queryJudgeLogResultState)
            {
                Thread.Sleep(10);
            }
            return _queryJudgeLogResult;
        }
    }
}