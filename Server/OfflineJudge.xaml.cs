using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
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
                ExportButton.IsEnabled = false;
                JudgingLog.Items.Clear();
                JudgeDetails.Items.Clear();
                JudgingProcess.Value = 0;
                CurrentState.Content = "开始评测";
                CurrentState.Content = "评测中...";
                _results.Clear();
                JudgeButton.Content = "停止";
                StartJudge(members, JudgeDir.Text);
            }
            else
            {
                _stop = true;
                JudgeButton.IsEnabled = false;
            }
        }

        private void StartJudge(IReadOnlyCollection<string> members, string dirPath)
        {
            Task.Run(() =>
            {
                var all = members.Count * _problems.Count(t => t.IsChecked);
                int[] cur = { -1 };
                var cnt = 0;
                var myJudgeTask = new List<Task>();
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
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            JudgingLog.Items.Add(new Label
                            {
                                Content = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 开始评测，题目：{m.ProblemName}，评测选手：{t}"
                            });
                        }));
                        string code;
                        try
                        {
                            if (RadioButton1.IsChecked ?? false)
                                code = File.ReadAllText(dirPath + "\\" + t + "\\" +
                                                        Judge.GetEngName(m.ProblemName) + ".cpp");
                            else
                                code = File.ReadAllText(dirPath + "\\" + t + "\\" +
                                                        Judge.GetEngName(m.ProblemName) + "\\" +
                                                        Judge.GetEngName(m.ProblemName) + ".cpp");
                        }
                        catch
                        {
                            continue;
                        }
                        cnt++;
                        myJudgeTask.Add(Task.Run(() =>
                            {
                                var j = new Judge(m.ProblemId, 1, code);
                                p.Result.Add(j.JudgeResult);
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    cur[0]++;
                                    JudgingProcess.Value = (double)cur[0] * 100 / all;
                                    JudgingLog.Items.Add(new Label
                                    {
                                        Content =
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕，题目：{m.ProblemName}，评测选手：{t}，结果：{j.JudgeResult.ResultSummery}"
                                    });
                                }));
                            })
                        );
                        if (cnt % (Configuration.Configurations.MutiThreading == 0
                                ? 5
                                : Configuration.Configurations.MutiThreading) != 0) continue;
                        foreach (var task in myJudgeTask)
                            task?.Wait();
                    }
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _results.Add(p);
                        JudgeResult.Items.Refresh();
                    }));
                }
                foreach (var task in myJudgeTask)
                    task?.Wait();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CurrentState.Content = "评测完毕";
                    JudgingProcess.Value = 100;
                    JudgeResult.Items.Refresh();
                    JudgeButton.IsEnabled = true;
                    JudgeButton.Content = "开始";
                    ExportButton.IsEnabled = true;
                }));
            });
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JudgeResult.Items.Refresh();
        }

        private void JudgeResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var t = JudgeResult.SelectedItem as JudgeResult;
            if (t == null)
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
                    Content = "代码",
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
            sdc.Add(new SortDescription(bindingProperty, sortDirection));
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
                    var dt = new List<DataTable>();
                    var dtname = new List<string>();
                    var dt1 = new DataTable("结果");
                    dt1.Columns.Add("姓名");
                    dt1.Columns.Add("总分");
                    foreach (var i in _results[0].Result)
                        dt1.Columns.Add(i.ProblemName, typeof(string));
                    foreach (var i in _results)
                    {
                        var dr1 = dt1.NewRow();
                        dr1[0] = i.MemberName;
                        dr1[1] = i.FullScore;
                        var k = 2;
                        foreach (var j in i.Result)
                            dr1[k++] = j.FullScore;
                        dt1.Rows.Add(dr1);
                    }
                    dt.Add(dt1);
                    dtname.Add("结果");
                    var cnt = 0;
                    foreach (var i in _results[0].Result)
                    {
                        var dti = new DataTable(i.ProblemName);
                        dtname.Add(i.ProblemName);
                        dti.Columns.Add("姓名");
                        dti.Columns.Add("最长时间 (ms)");
                        dti.Columns.Add("最大内存 (kb)");
                        dti.Columns.Add("结果");
                        dti.Columns.Add("分数");
                        dti.Columns.Add("代码");
                        foreach (var t in _results)
                        {
                            var dr = dti.NewRow();
                            dr[0] = t.MemberName;
                            dr[1] = t.Result[cnt].Timeused.Max();
                            dr[2] = t.Result[cnt].Memoryused.Max();
                            dr[3] = t.Result[cnt].ResultSummery;
                            dr[4] = t.Result[cnt].FullScore;
                            dr[5] = t.Result[cnt].Code;
                            dti.Rows.Add(dr);
                        }
                        dt.Add(dti);
                        cnt++;
                    }
                    ExcelUtility.CreateExcel(sfg.FileName, dt, dtname.ToArray());
                    MessageBox.Show("导出成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                sdc.Add(new SortDescription(bindingProperty, sortDirection));
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