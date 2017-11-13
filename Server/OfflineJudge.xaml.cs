using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace Server
{
    /// <summary>
    ///     Interaction logic for OfflineJudge.xaml
    /// </summary>
    public partial class OfflineJudge : Window
    {
        private readonly ObservableCollection<JudgeResult> _results = new ObservableCollection<JudgeResult>();
        private ObservableCollection<Problem> _problems;

        private bool _stop;
        private bool _isFinished;

        public OfflineJudge()
        {
            InitializeComponent();
        }

        private string[] GetAllMembers()
        {
            var k = Directory.GetDirectories(JudgeDir.Text);
            for (var i = 0; i < k.Length; i++)
                k[i] = Path.GetFileName(k[i]);
            return k;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _problems = Connection.QueryProblems();
            ListView.ItemsSource = _problems;
            JudgeResult.ItemsSource = _results;
        }

        private void JudgeButton_Click(object sender, RoutedEventArgs e)
        {
            if (JudgeButton.Content.ToString() == "开始")
            {
                if (!Directory.Exists(JudgeDir.Text))
                    return;
                string[] members;
                try
                {
                    members = GetAllMembers();
                }
                catch
                {
                    return;
                }
                if (members == null)
                    return;
                _stop = false;
                _isFinished = false;
                ExportButton.IsEnabled = false;
                JudgingLog.Items.Clear();
                _results.Clear();
                JudgingProcess.Value = 0;
                CurrentState.Content = "开始评测";
                CurrentState.Content = "评测中...";
                JudgeButton.Content = "停止";
                StartJudge(members, JudgeDir.Text, RadioButton1.IsChecked ?? false, StdInOut.IsChecked ?? false);
            }
            else
            {
                _stop = true;
                JudgeButton.IsEnabled = false;
            }
        }

        private void StartJudge(IReadOnlyCollection<string> members, string dirPath, bool dirPlan, bool isStdInOut)
        {
            Task.Run(() =>
            {
                var start = DateTime.Now;
                while (!_isFinished)
                {
                    var dTime = DateTime.Now - start;
                    Dispatcher.Invoke(() =>
                    {
                        TimeConsume.Content = $"{dTime.Hours}:{dTime.Minutes}:{dTime.Seconds}";
                        JudgingSpeed.Content =
                            $"{Math.Round(Convert.ToInt32(JudgingLog.Items.Count / 2) / (Math.Abs(dTime.TotalMinutes) < 1 ? 1 : dTime.TotalMinutes), MidpointRounding.AwayFromZero)} 题/分钟";
                    });
                    Thread.Sleep(1000);
                }
            });
            Task.Run(() =>
            {
                var all = members.Count * _problems.Count(t => t.IsChecked);
                var cur = -1;
                var myJudgeTask = new List<Task>();
                var cntLock = new object();
                foreach (var t in members)
                {
                    if (_stop) break;
                    var p = new JudgeResult
                    {
                        MemberName = t,
                        Result = new List<JudgeInfo>()
                    };
                    foreach (var m in _problems)
                    {
                        if (_stop) break;
                        if (!m.IsChecked)
                            continue;
                        string code;
                        string type;
                        try
                        {
                            if (dirPlan)
                            {
                                var codeFile = Directory.GetFiles(dirPath + "\\" + t).FirstOrDefault(f =>
                                                   Path.GetFileNameWithoutExtension(f) ==
                                                   Judge.GetEngName(m.ProblemName)) ??
                                               throw new InvalidOperationException();
                                code = File.ReadAllText(codeFile, Encoding.Default);
                                type = Configuration.Configurations.Compiler.FirstOrDefault(c =>
                                               c.ExtName.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Any(d => string.Equals(d, Path.GetExtension(codeFile), StringComparison.CurrentCultureIgnoreCase)))
                                           ?.DisplayName ??
                                       throw new InvalidOperationException();
                            }
                            else
                            {
                                var codeFile =
                                    Directory.GetFiles(dirPath + "\\" + t + "\\" + Judge.GetEngName(m.ProblemName))
                                        .FirstOrDefault(f =>
                                            Path.GetFileNameWithoutExtension(f) ==
                                            Judge.GetEngName(m.ProblemName)) ??
                                    throw new InvalidOperationException();
                                code = File.ReadAllText(codeFile, Encoding.Default);
                                type = Configuration.Configurations.Compiler.FirstOrDefault(c =>
                                               c.ExtName.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Any(d => string.Equals(d, Path.GetExtension(codeFile), StringComparison.CurrentCultureIgnoreCase)))
                                           ?.DisplayName ??
                                       throw new InvalidOperationException();
                            }
                        }
                        catch
                        {
                            continue;
                        }
                        Connection.CanPostJudgTask = false;
                        myJudgeTask.Add(Task.Run(() =>
                        {
                            if (_stop) return;
                            Dispatcher.Invoke(() =>
                            {
                                JudgingLog.Items.Add(new TextBlock
                                {
                                    Text = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 开始评测，题目：{m.ProblemName}，评测选手：{t}"
                                });
                                JudgingLog.ScrollIntoView(JudgingLog.Items[JudgingLog.Items.Count - 1]);
                            });
                            var j = new Judge(m.ProblemId, 1, code, type, isStdInOut);
                            p.Result.Add(j.JudgeResult);
                            Dispatcher.Invoke(() =>
                            {
                                lock (cntLock)
                                {
                                    cur++;
                                    JudgingProcess.Value = (double)cur * 100 / all;
                                }
                                JudgingLog.Items.Add(new TextBlock
                                {
                                    Text =
                                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕，题目：{m.ProblemName}，评测选手：{t}，结果：{j.JudgeResult.ResultSummery}"
                                });
                                JudgingLog.ScrollIntoView(JudgingLog.Items[JudgingLog.Items.Count - 1]);
                            });
                        }));
                        while (true)
                        {
                            if (Connection.CurJudgingCnt < (Configuration.Configurations.MutiThreading == 0
                                    ? Configuration.ProcessorCount
                                    : Configuration.Configurations.MutiThreading)&& Connection.CanPostJudgTask) break;
                            Thread.Sleep(100);
                        }
                    }
                    Dispatcher.Invoke(() =>
                    {
                        _results.Add(p);
                        JudgeResult.Items.Refresh();
                    });
                }
                foreach (var task in myJudgeTask)
                    task?.Wait();
                _isFinished = true;
                Dispatcher.Invoke(() =>
                {
                    CurrentState.Content = "评测完毕";
                    JudgingProcess.Value = 100;
                    JudgeResult.Items.Refresh();
                    JudgeButton.IsEnabled = true;
                    JudgeButton.Content = "开始";
                    ExportButton.IsEnabled = true;
                });
            });
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JudgeResult.Items.Refresh();
        }

        private void JudgeResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(JudgeResult.SelectedItem is JudgeResult t))
                return;
            var f = new ObservableCollection<ResultTree>();
            for (var i = 0; i < t.Result.Count; i++)
            {
                f.Add(new ResultTree
                {
                    Content = $"{t.Result[i].ProblemName}：{t.Result[i].ResultSummery}，{t.Result[i].FullScore}",
                    Children = new ObservableCollection<ResultTree>()
                });
                for (var j = 0; j < t.Result[i].Result.Length; j++)
                {
                    f[i].Children.Add(new ResultTree
                    {
                        Content = $"#{j + 1}：{t.Result[i].Result[j]}，{t.Result[i].Score[j]}",
                        Children = new ObservableCollection<ResultTree>()
                    });
                    f[i].Children[j].Children.Add(new ResultTree { Content = $"时间：{t.Result[i].Timeused[j]}ms" });
                    f[i].Children[j].Children.Add(new ResultTree { Content = $"内存：{t.Result[i].Memoryused[j]}kb" });
                    f[i].Children[j].Children.Add(new ResultTree { Content = $"退出代码：{t.Result[i].Exitcode[j]}" });
                }
                f[i].Children.Add(new ResultTree
                {
                    Content = $"代码（{t.Result[i].Type}）",
                    Children = new ObservableCollection<ResultTree>()
                });
                f[i].Children[f[i].Children.Count - 1].Children.Add(new ResultTree
                {
                    Content = t.Result[i].Code
                });
            }
            JudgeDetails.ItemsSource = f;
            JudgeDetails.Items.Refresh();
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var clickedColumn = (e.OriginalSource as GridViewColumnHeader)?.Column;
            if (clickedColumn == null) return;
            var bindingProperty = (clickedColumn.DisplayMemberBinding as Binding)?.Path.Path;
            var sdc = JudgeResult.Items.SortDescriptions;
            var sortDirection = ListSortDirection.Ascending;
            if (sdc.Count > 0)
            {
                var sd = sdc[0];
                sortDirection = (ListSortDirection)(((int)sd.Direction + 1) % 2);
                sdc.Clear();
            }
            if (bindingProperty != null) sdc.Add(new SortDescription(bindingProperty, sortDirection));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            JudgeButton.IsEnabled = false;
            var sfg = new SaveFileDialog
            {
                Title = "保存导出数据：",
                Filter = "Excel 文件|*.xlsx"
            };
            if (sfg.ShowDialog() ?? false)
                if (_results.Any())
                {
                    var problems = (from c in _problems where c.IsChecked select c).ToList();
                    var dt = new List<DataTable>();
                    var dtname = new List<string>();
                    var dt1 = new DataTable("结果");
                    dt1.Columns.Add("姓名");
                    dt1.Columns.Add("总分");
                    foreach (var i in problems)
                        dt1.Columns.Add(i.ProblemName, typeof(string));
                    foreach (var i in _results)
                    {
                        var dr1 = dt1.NewRow();
                        dr1[0] = i.MemberName;
                        dr1[1] = i.FullScore;
                        var k = 2;
                        foreach (var j in problems)
                        {
                            var temp = (from c in i.Result where c.ProblemName == j.ProblemName select c)
                                .FirstOrDefault();
                            dr1[k++] = temp?.FullScore ?? 0;
                        }
                        dt1.Rows.Add(dr1);
                    }
                    dt.Add(dt1);
                    dtname.Add("结果");
                    foreach (var i in problems)
                    {
                        var dti = new DataTable(i.ProblemName);
                        dtname.Add(i.ProblemName);
                        dti.Columns.Add("姓名");
                        dti.Columns.Add("最长时间 (ms)");
                        dti.Columns.Add("最大内存 (kb)");
                        dti.Columns.Add("结果");
                        dti.Columns.Add("分数");
                        dti.Columns.Add("代码 (Base64)");
                        dti.Columns.Add("评测 ID");
                        dti.Columns.Add("代码类型");
                        foreach (var t in _results)
                        {
                            var dr = dti.NewRow();
                            dr[0] = t.MemberName;
                            var temp =
                                (from c in t.Result where c.ProblemName == i.ProblemName select c).FirstOrDefault();
                            dr[1] = temp?.Timeused.Max() ?? 0;
                            dr[2] = temp?.Memoryused.Max() ?? 0;
                            dr[3] = temp?.ResultSummery ?? string.Empty;
                            dr[4] = temp?.FullScore ?? 0;
                            try
                            {
                                var bytes = Encoding.Default.GetBytes(temp?.Code ?? string.Empty);
                                dr[5] = Convert.ToBase64String(bytes);
                            }
                            catch
                            {
                                dr[5] = string.Empty;
                            }
                            dr[6] = temp?.JudgeId ?? 0;
                            dr[7] = temp?.Type ?? string.Empty;
                            dti.Rows.Add(dr);
                        }
                        dt.Add(dti);
                    }
                    try
                    {
                        ExcelUtility.CreateExcel(sfg.FileName, dt, dtname.ToArray());
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
            JudgeButton.IsEnabled = true;
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
                    sortDirection = (ListSortDirection)(((int)sd.Direction + 1) % 2);
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
            var p = _problems.Count(i => i.IsChecked);
            if (p == _problems.Count)
            {
                foreach (var i in _problems)
                    i.IsChecked = false;
                CheckBox.IsChecked = false;
            }
            else
            {
                foreach (var i in _problems)
                    i.IsChecked = true;
                CheckBox.IsChecked = true;
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var p = _problems.Count(i => i.IsChecked);
            CheckBox.IsChecked = p == _problems.Count;
        }
    }
}