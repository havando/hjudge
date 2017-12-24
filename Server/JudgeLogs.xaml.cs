using System;
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
using System.Windows.Input;
using Microsoft.Win32;

namespace Server
{
    /// <summary>
    ///     Interaction logic for JudgeLogs.xaml
    /// </summary>
    public partial class JudgeLogs : Window
    {
        private ObservableCollection<JudgeInfo> _curJudgeInfo = new ObservableCollection<JudgeInfo>();
        private readonly ObservableCollection<JudgeInfo> _curJudgeInfoBak = new ObservableCollection<JudgeInfo>();
        private bool _isFilterActivated;
        private readonly ObservableCollection<string> _problemFilter = new ObservableCollection<string>();
        private readonly ObservableCollection<string> _userFilter = new ObservableCollection<string>();

        public JudgeLogs()
        {
            InitializeComponent();
        }

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MessageBox.Show("你确定要清空数据吗？清空后不可恢复！", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                MessageBoxResult.Yes)
            {
                _isFilterActivated = false;
                _curJudgeInfoBak.Clear();
                ProblemFilter.SelectedIndex = UserFilter.SelectedIndex = TimeFilter.SelectedIndex = -1;
                Connection.ClearJudgeLog();
                _curJudgeInfo.Clear();
                Code.Clear();
                JudgeDetails.Clear();
                CheckBox.IsChecked = false;
            }
        }

        private void Label_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            _isFilterActivated = false;
            _curJudgeInfoBak.Clear();
            ProblemFilter.SelectedIndex = UserFilter.SelectedIndex = TimeFilter.SelectedIndex = -1;
            _curJudgeInfo = Connection.QueryJudgeLog(true);
            ListView.ItemsSource = _curJudgeInfo;
            ListView.Items.Refresh();
            CheckBox.IsChecked = false;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ListView.SelectedItem is JudgeInfo a)) return;
            Code.Text = "代码：\n" + a.Code;
            var details = "详情：\n";
            if (a.Result != null)
                for (var i = 0; i < a.Result.Length; i++)
                    details +=
                        $"#{i + 1} 时间：{a.Timeused[i]}ms，内存：{a.Memoryused[i]}kb，退出代码：{a.Exitcode[i]}，结果：{a.Result[i]}，分数：{a.Score[i]}\n";
            details += "\n其他信息：\n" + a.AdditionInfo;
            JudgeDetails.Text = details;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (UserHelper.CurrentUser.Type >= 3) ClearLabel.Visibility = Visibility.Hidden;
            ListView.ItemsSource = _curJudgeInfo;

            _curJudgeInfo.Clear();
            _problemFilter.Clear();
            _userFilter.Clear();
            ProblemFilter.ItemsSource = _problemFilter;
            UserFilter.ItemsSource = _userFilter;
            Task.Run(() =>
            {
                var t = Connection.QueryJudgeLog(true).Reverse();
                var problemList = t.Select(i => i.ProblemName).Distinct();
                var userList = t.Select(i => i.UserName).Distinct();
                foreach (var judgeInfo in t)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _curJudgeInfo.Add(judgeInfo);
                    });
                    Thread.Sleep(1);
                }
                foreach(var problemName in problemList)
                {
                    Dispatcher.Invoke(() => _problemFilter.Add(problemName));
                    Thread.Sleep(1);
                }
                foreach(var userName in userList)
                {
                    Dispatcher.Invoke(() => _userFilter.Add(userName));
                    Thread.Sleep(1);
                }
            });
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                ListView.Height = ActualHeight - 82.149;
                ListView.Width = (ActualWidth - 24) * 0.4;
                JudgeDetails.Width = Code.Width = FilterDock.Width = (ActualWidth - 48) * 0.6;
                JudgeDetails.Height = (ActualHeight - 48) * 0.4 - 28;
                Code.Height = (ActualHeight - 72) * 0.6;
                FilterDock.Margin = new Thickness(ListView.Width + 15, 10, 0, 0);
                JudgeDetails.Margin = new Thickness(ListView.Width + 15, 38, 0, 0);
                Code.Margin = new Thickness(ListView.Width + 15, JudgeDetails.Height + 43, 0, 0);
                Refresh.Margin = new Thickness(ListView.Width - 20, ListView.Height + 10, 0, 0);
                ClearLabel.Margin = new Thickness(ListView.Width - 20 - 39, ListView.Height + 10, 0, 0);
                ExportLabel.Margin = new Thickness(10, ListView.Height + 10, 0, 0);
                TimeFilter.Width = UserFilter.Width = ProblemFilter.Width = 128 + ((ActualWidth - 48) * 0.6 - 644) / 3;
            }
            catch
            {
                Width = e.PreviousSize.Width;
                Height = e.PreviousSize.Height;
            }
        }

        private void Export_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var a = (from c in _curJudgeInfo where c.IsChecked select c).ToList();
            if (a.Any(i => i.ResultSummary == "Judging..."))
            {
                MessageBox.Show("你选择的项目中部分仍在评测，请等待评测完毕再导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (a.Any())
            {
                var sfg = new SaveFileDialog
                {
                    Title = "保存导出数据：",
                    Filter = "Excel 文件|*.xlsx"
                };
                if (!(sfg.ShowDialog() ?? false)) return;
                var dt = new DataTable("结果");
                dt.Columns.Add("姓名");
                dt.Columns.Add("题目名称");
                dt.Columns.Add("评测描述");
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
                    dr[2] = i?.Description ?? string.Empty;
                    dr[3] = i?.JudgeDate ?? string.Empty;
                    dr[4] = i?.Timeused?.Max() ?? 0;
                    dr[5] = i?.Memoryused?.Max() ?? 0;
                    dr[6] = i?.ResultSummary ?? string.Empty;
                    dr[7] = i?.FullScore ?? 0;
                    try
                    {
                        var bytes = Encoding.Default.GetBytes(i?.Code ?? string.Empty);
                        dr[8] = Convert.ToBase64String(bytes);
                    }
                    catch
                    {
                        dr[8] = string.Empty;
                    }
                    dr[9] = i?.Type ?? string.Empty;
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

        private bool Filter(JudgeInfo p)
        {
            var now = DateTime.Now;
            string pf = null, uf = null;
            var tf = -1;
            Dispatcher.Invoke(() =>
            {
                pf = ProblemFilter.SelectedItem as string;
                uf = UserFilter.SelectedItem as string;
                tf = TimeFilter.SelectedIndex;
            });
            if (pf != null) if (p.ProblemName != pf) return false;
            if (uf != null) if (p.UserName != uf) return false;
            if (tf != -1)
            {
                var ti = Convert.ToDateTime(p.JudgeDate);
                switch (tf)
                {
                    case 0:
                    {
                        if (ti.Year != now.Year || ti.Month != now.Month || ti.Day != now.Day) return false;
                        break;
                    }
                    case 1:
                    {
                        if ((now - ti).TotalDays > 3) return false;
                        break;
                    }
                    case 2:
                    {
                        if ((now - ti).TotalDays > 7) return false;
                        break;
                    }
                    case 3:
                    {
                        if ((now - ti).TotalDays > 30) return false;
                        break;
                    }
                    case 4:
                    {
                        if ((now - ti).TotalDays > 91) return false;
                        break;
                    }
                    case 5:
                    {
                        if ((now - ti).TotalDays > 182) return false;
                        break;
                    }
                    case 6:
                    {
                        if ((now - ti).TotalDays > 365) return false;
                        break;
                    }
                }
            }
            return true;
        }

        private void ResetFilterButton_Click(object sender, RoutedEventArgs e)
        {
            CheckBox.IsChecked = false;
            if (_isFilterActivated)
            {
                _curJudgeInfo.Clear();
                foreach (var p in _curJudgeInfoBak)
                    Dispatcher.Invoke(() => { _curJudgeInfo.Add(p); });
            }
            _isFilterActivated = false;
            ProblemFilter.SelectedIndex = UserFilter.SelectedIndex = TimeFilter.SelectedIndex = -1;
            ListView.ItemsSource = _curJudgeInfo;
            ListView.Items.Refresh();
        }

        private void DoFilterButton_Click(object sender, RoutedEventArgs e)
        {
            CheckBox.IsChecked = false;
            if (_isFilterActivated)
            {
                _curJudgeInfo.Clear();
                foreach (var p in _curJudgeInfoBak)
                    Dispatcher.Invoke(() => { _curJudgeInfo.Add(p); });
            }
            _isFilterActivated = true;
            Task.Run(() =>
            {
                Dispatcher.Invoke(() => _curJudgeInfoBak.Clear());
                foreach (var p in _curJudgeInfo)
                    Dispatcher.Invoke(() => _curJudgeInfoBak.Add(p));
                Dispatcher.Invoke(() => _curJudgeInfo.Clear());
                foreach (var p in _curJudgeInfoBak.Where(Filter))
                    Dispatcher.Invoke(() => _curJudgeInfo.Add(p));
            });
        }
    }
}