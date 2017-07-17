using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Server
{
    /// <summary>
    /// Interaction logic for OfflineJudge.xaml
    /// </summary>
    public partial class OfflineJudge : Window
    {
        private ObservableCollection<Problem> _problems;
        private readonly ObservableCollection<JudgeResult> _results = new ObservableCollection<JudgeResult>();

        private string[] GetAllMembers()
        {
            var k = Directory.GetDirectories(JudgeDir.Text);
            for (var i = 0; i < k.Length; i++)
            {
                k[i] = Path.GetFileName(k[i]);
            }
            return k;
        }

        public OfflineJudge()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _problems = Connection.QueryProblems();
            ListView.ItemsSource = _problems;
            JudgeResult.ItemsSource = _results;
        }

        private void JudgeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(JudgeDir.Text))
            {
                return;
            }
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
            {
                return;
            }
            ExportButton.IsEnabled = false;
            JudgingLog.Items.Clear();
            JudgeDetails.Items.Clear();
            JudgingProcess.Value = 0;
            CurrentState.Content = "开始评测";
            JudgeButton.IsEnabled = false;
            _results.Clear();
            StartJudge(members, JudgeDir.Text);
        }

        private void StartJudge(IReadOnlyCollection<string> members, string dirPath)
        {
            Task.Run(() =>
            {
                var all = members.Count * _problems.Count(t => t.IsChecked);
                int[] cur = { -1 };
                foreach (var t in members)
                {
                    var p = new JudgeResult
                    {
                        MemberName = t,
                        Result = new List<JudgeInfo>()
                    };
                    foreach (var m in _problems)
                    {
                        if (!m.IsChecked)
                        {
                            continue;
                        }
                        cur[0]++;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            JudgingProcess.Value = (double)cur[0] * 100 / all;
                            CurrentMember.Content = t;
                            CurrentProblem.Content = m.ProblemName;
                            CurrentState.Content = $"评测中... {cur[0] + 1}/{all}";

                            JudgingLog.Items.Add(new Label { Content = $"{DateTime.Now} 评测题目：{m.ProblemName}，评测选手：{t}" });
                        }));
                        string code;
                        try
                        {
                            code = File.ReadAllText(dirPath + "\\" + t + "\\" +
                                                    Judge.GetEngName(m.ProblemName) +
                                                    ".cpp");
                        }
                        catch
                        {
                            continue;
                        }
                        var j = new Judge(m.ProblemId, 1, code);
                        p.Result.Add(j.JudgeResult);
                    }
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _results.Add(p);
                        JudgeResult.Items.Refresh();
                    }));
                }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CurrentState.Content = "评测完毕";
                    JudgingProcess.Value = 100;
                    JudgeResult.Items.Refresh();
                    JudgeButton.IsEnabled = true;
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
            {
                return;
            }
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
            var a = JsonConvert.SerializeObject(_results);
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
            JudgeButton.IsEnabled = true;
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
