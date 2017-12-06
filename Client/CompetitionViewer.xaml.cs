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
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using Microsoft.Win32;
using System.Windows.Media.Animation;

namespace Client
{
    /// <summary>
    ///     Interaction logic for CompetitionViewer.xaml
    /// </summary>
    public partial class CompetitionViewer : Window
    {
        private readonly DateTime[] _baseTime = new DateTime[2];
        private readonly ObservableCollection<Problem> _problems = new ObservableCollection<Problem>();
        private Competition _competition;

        private readonly ObservableCollection<CompetitionUserInfo> _competitionInfo =
            new ObservableCollection<CompetitionUserInfo>();

        private readonly ObservableCollection<JudgeInfo> _curJudgeInfo = new ObservableCollection<JudgeInfo>();
        private readonly ObservableCollection<JudgeInfo> _curJudgeInfoBak = new ObservableCollection<JudgeInfo>();
        private bool _hasRefreshWhenFinished;
        private bool _isFilterActivated;
        private readonly ObservableCollection<string> _problemFilter = new ObservableCollection<string>();
        private readonly ObservableCollection<string> _userFilter = new ObservableCollection<string>();
        private readonly object _loadLock = new object();

        public CompetitionViewer()
        {
            InitializeComponent();
        }

        private DateTime GetNowDateTime()
        {
            var diff = DateTime.Now - _baseTime[1];
            return _baseTime[0] + diff;
        }

        public void SetCompetition(Competition competition)
        {
            _competition = competition;
        }

        private void Refresh()
        {
            while (true)
            {
                Dispatcher.Invoke(() =>
                {
                    if (GetNowDateTime() >= _competition.EndTime)
                    {
                        var t = _competition.EndTime - _competition.StartTime;
                        ComTimeC.Text = $"{t.Days * 24 + t.Hours}:{t.Minutes}:{t.Seconds}";
                        Submit.IsEnabled = false;
                        ComTimeR.Text = "0:0:0";
                        ComState.Text = "已结束";
                        if (!_hasRefreshWhenFinished)
                        {
                            RefreshButton_Click(null, null);
                            _hasRefreshWhenFinished = true;
                        }
                    }
                    else if (GetNowDateTime() < _competition.StartTime)
                    {
                        var t = _competition.EndTime - _competition.StartTime;
                        ComTimeC.Text = "0:0:0";
                        Submit.IsEnabled = false;
                        ComTimeR.Text = $"{t.Days * 24 + t.Hours}:{t.Minutes}:{t.Seconds}";
                        ComState.Text = "未开始";
                    }
                    else
                    {
                        var st = GetNowDateTime() - _competition.StartTime;
                        var et = _competition.EndTime - GetNowDateTime();
                        Submit.IsEnabled = true;
                        ComTimeC.Text = $"{st.Days * 24 + st.Hours}:{st.Minutes}:{st.Seconds}";
                        ComTimeR.Text = $"{et.Days * 24 + et.Hours}:{et.Minutes}:{et.Seconds}";
                        ComState.Text =
                            $"进行中 ({Math.Round(st.TotalSeconds * 100 / (Math.Abs((_competition.EndTime - _competition.StartTime).TotalSeconds) < 0.00001 ? st.TotalSeconds : (_competition.EndTime - _competition.StartTime).TotalSeconds), 2, MidpointRounding.AwayFromZero)} %)";
                    }
                });
                Thread.Sleep(1000);
            }
        }

        private void VerifyPassword(string password)
        {
            if (password == null)
            {
                Close();
                return;
            }
            if (password != _competition.Password)
            {
                MessageBox.Show("密码错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_competition == null) Close();
            _baseTime[0] = Connection.GetCurrentDateTime();
            _baseTime[1] = DateTime.Now;
            if (GetNowDateTime() < _competition.StartTime)
            {
                MessageBox.Show("比赛未开始", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            if (!string.IsNullOrEmpty(_competition.Password))
            {
                var pass = new InputPassword();
                pass.SetRecallFun(VerifyPassword);
                pass.ShowDialog();
            }
            ComName.Content = $"({_competition.CompetitionId}) {_competition.CompetitionName}";
            if ((_competition.Option & 1) != 0)
                ComMode.Text =
                    $"限制提交赛：{(_competition.SubmitLimit == 0 ? "无限" : _competition.SubmitLimit.ToString())} 次";
            if ((_competition.Option & 2) != 0) ComMode.Text = "最后提交赛";
            if ((_competition.Option & 4) != 0) ComMode.Text = "罚时计时赛";
            ListView.ItemsSource = _curJudgeInfo;
            MyProblemList.ItemsSource = _problems;
            ProblemFilter.ItemsSource = _problemFilter;
            UserFilter.ItemsSource = _userFilter;
            Description.Text = _competition.Description;
            var rtf = new RotateTransform
            {
                CenterX = Loading.ActualWidth * 0.5,
                CenterY = Loading.ActualHeight * 0.5
            };
            var daV = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            Loading.RenderTransform = rtf;
            rtf.BeginAnimation(RotateTransform.AngleProperty, daV);
            new Thread(Refresh).Start();
            _hasRefreshWhenFinished = true;
            new Thread(Load).Start();
        }

        private void Load()
        {
            lock (_loadLock)
            {
                Dispatcher.Invoke(() =>
                {
                    Loading.Visibility = Visibility.Visible;
                    RefreshButton.IsEnabled = false;
                });
                var now = Connection.GetCurrentDateTime();
                var problems = Connection.QueryProblemsForCompetition(_competition.CompetitionId);
                Dispatcher.Invoke(() => _problems.Clear());
                foreach (var i in problems) Dispatcher.Invoke(() => _problems.Add(i));
                var languages = Connection.QueryLanguagesForCompetition();
                Dispatcher.Invoke(() => LangBox.Items.Clear());
                foreach (var m in languages)
                    Dispatcher.Invoke(() => LangBox.Items.Add(new RadioButton { Content = m.DisplayName }));
                Dispatcher.Invoke(() =>
                {
                    while (CompetitionStateColumn.Columns.Count > 4)
                        CompetitionStateColumn.Columns.RemoveAt(CompetitionStateColumn.Columns.Count - 1);
                });
                for (var i = 0; i < (_competition.ProblemSet?.Length ?? 0); i++)
                {
                    var t = Properties.Resources.CompetitionDetailsProblemInfoControl.Replace("${index}",
                        $"{i}").Replace("${ProblemName}",
                        problems.Where(j => j.ProblemId == _competition.ProblemSet[i]).Select(j => j.ProblemIndex)
                            .FirstOrDefault() ?? string.Empty);
                    var strreader = new StringReader(t);
                    var xmlreader = new XmlTextReader(strreader);
                    Dispatcher.Invoke(() =>
                    {
                        var obj = XamlReader.Load(xmlreader);
                        CompetitionStateColumn.Columns.Add(obj as GridViewColumn);
                    });
                }
                Dispatcher.Invoke(() => CompetitionState.ItemsSource = _competitionInfo);
                var x = Connection.QueryJudgeLogBelongsToCompetition(_competition.CompetitionId);
                Dispatcher.Invoke(() =>
                {
                    _curJudgeInfo.Clear();
                    _competitionInfo.Clear();
                });
                if ((_competition.Option & 8) != 0 || now > _competition.EndTime)
                    for (var i = x.Count - 1; i >= 0; i--)
                        Dispatcher.Invoke(() => _curJudgeInfo.Add(x[i]));
                if (!((_competition.Option & 8) == 0 && GetNowDateTime() < _competition.EndTime))
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var i in CompetitionStateColumn.Columns)
                            if (i.Header is StackPanel j)
                                foreach (var k in j.Children)
                                    if (k is TextBlock l && l.Name.Contains("ProblemColumn"))
                                    {
                                        var m = Convert.ToInt32(l.Name.Substring(13));
                                        l.Text =
                                            $"{x.Count(p => p.ProblemId == _competition.ProblemSet[m] && p.ResultSummery == "Accepted")}/{x.Count(p => p.ProblemId == _competition.ProblemSet[m])}";
                                    }
                    });
                var tmpList = new List<CompetitionUserInfo>();
                Dispatcher.Invoke(() => _competitionInfo.Clear());
                var user = x.Select(p => p.UserName).Distinct();
                foreach (var i in user)
                {
                    if (_competition.ProblemSet == null) continue;
                    var tmp = new CompetitionUserInfo
                    {
                        UserName = i,
                        ProblemInfo = new CompetitionProblemInfo[_competition.ProblemSet.Length]
                    };
                    float score = 0;
                    var totTime = new TimeSpan(0);
                    for (var j = 0; j < _competition.ProblemSet.Length; j++)
                    {
                        tmp.ProblemInfo[j] = new CompetitionProblemInfo();
                        if ((_competition.Option & 8) == 0 && GetNowDateTime() < _competition.EndTime) continue;
                        var ac = x.Count(p => p.UserName == i && p.ResultSummery == "Accepted" &&
                                              p.ProblemId == _competition.ProblemSet[j]);
                        var all = x.Count(p => p.UserName == i && p.ProblemId == _competition.ProblemSet[j]);
                        tmp.ProblemInfo[j].Color = ac != 0 ? Brushes.LightGreen : Brushes.LightPink;
                        var time = new TimeSpan(0);
                        var cnt = 0;
                        var tmpScoreBase = x.Where(p => p.UserName == i && p.ProblemId == _competition.ProblemSet[j])
                            .ToList();
                        if ((_competition.Option & 2) == 0)
                        {
                            if (tmpScoreBase.Any()) score += tmpScoreBase.Max(p => p.FullScore);
                            tmp.ProblemInfo[j].State = $"{ac}/{all}";
                            foreach (var k in x.Where(p =>
                                p.UserName == i && p.ProblemId == _competition.ProblemSet[j]))
                            {
                                var tmpTime = Convert.ToDateTime(k.JudgeDate) - _competition.StartTime;
                                time += tmpTime;
                                totTime += tmpTime;
                                if (k.ResultSummery == "Accepted")
                                {
                                    break;
                                }
                                if ((_competition.Option & 4) != 0)
                                {
                                    time += new TimeSpan(0, 20, 0);
                                    totTime += new TimeSpan(0, 20, 0);
                                    cnt++;
                                }
                            }
                        }
                        else
                        {
                            if (tmpScoreBase.Any()) score += tmpScoreBase.LastOrDefault()?.FullScore ?? 0;
                            var y = x.LastOrDefault(p => p.UserName == i && p.ProblemId == _competition.ProblemSet[j]);
                            if (y != null && y.ResultSummery == "Accepted")
                            {
                                tmp.ProblemInfo[j].State = "Solved";
                            }
                            else
                            {
                                tmp.ProblemInfo[j].Color = Brushes.LightPink;
                                tmp.ProblemInfo[j].State = "Unsolved";
                            }
                            if (y != null)
                            {
                                var tmpTime = Convert.ToDateTime(y.JudgeDate) - _competition.StartTime;
                                time += tmpTime;
                                totTime += tmpTime;
                            }
                        }
                        if ((_competition.Option & 4) != 0 && cnt != 0) tmp.ProblemInfo[j].State += $" (-{cnt})";
                        tmp.ProblemInfo[j].Time = $"{time.Days * 24 + time.Hours}:{time.Minutes}:{time.Seconds}";
                    }
                    tmp.Score = score;
                    tmp.TotTime = totTime;
                    tmpList.Add(tmp);
                }
                tmpList.Sort((x1, x2) =>
                {
                    if (Math.Abs(x1.Score - x2.Score) > 0.00001) return x2.Score.CompareTo(x1.Score);
                    return x1.TotTime.CompareTo(x2.TotTime);
                });
                for (var i = 0; i < tmpList.Count; i++)
                {
                    tmpList[i].Rank = i + 1;
                    Dispatcher.Invoke(() => _competitionInfo.Add(tmpList[i]));
                }
                Dispatcher.Invoke(() =>
                {
                    _problemFilter.Clear();
                    _userFilter.Clear();
                });
                foreach (var judgeInfo in x)
                    Dispatcher.Invoke(() =>
                    {
                        if (_problemFilter.All(i => i != judgeInfo.ProblemName))
                            _problemFilter.Add(judgeInfo.ProblemName);
                        if (_userFilter.All(i => i != judgeInfo.UserName)) _userFilter.Add(judgeInfo.UserName);
                    });
                if (GetNowDateTime() < _competition.EndTime)
                    _hasRefreshWhenFinished = false;
                Dispatcher.Invoke(() =>
                {
                    Loading.Visibility = Visibility.Hidden;
                    RefreshButton.IsEnabled = true;
                });
            }
        }

        private void Export_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var a = (from c in _curJudgeInfo where c.IsChecked select c).ToList();
            if (a.Any(i => i.ResultSummery == "Judging..."))
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
                    dr[6] = i?.ResultSummery ?? string.Empty;
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
                    ExcelUtility.CreateExcel(sfg.FileName, new[] { dt }, new[] { "结果" });
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
                if (!(sender is ListView listView)) return;
                var clickedColumn = (e.OriginalSource as GridViewColumnHeader)?.Column;
                if (clickedColumn == null) return;
                var bindingProperty = (clickedColumn.DisplayMemberBinding as Binding)?.Path.Path;
                var sdc = listView.Items.SortDescriptions;
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
            var now = GetNowDateTime();
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

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ListView.SelectedItem is JudgeInfo a)) return;
            if (!string.IsNullOrEmpty(a.Code))
            {
                Code.Text = "代码：\n" + a.Code;
                var details = "详情：\n";
                if (a.Result != null)
                    for (var i = 0; i < a.Result.Length; i++)
                        details +=
                            $"#{i + 1} 时间：{a.Timeused[i]}ms，内存：{a.Memoryused[i]}kb，退出代码：{a.Exitcode[i]}，结果：{a.Result[i]}，分数：{a.Score[i]}\n";
                details += "\n其他信息：\n" + a.AdditionInfo;
                JudgeDetails.Text = details;
            }
            else
            {
                Code.Text = "不允许查看其他用户的代码";
                JudgeDetails.Text = "不允许查看其他用户的评测详情";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _isFilterActivated = false;
            _curJudgeInfoBak.Clear();
            ProblemFilter.SelectedIndex = UserFilter.SelectedIndex = TimeFilter.SelectedIndex = -1;
            new Thread(Load).Start();
        }

        private void ProblemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var x = MyProblemList.SelectedItem as Problem;
            ProblemInfomationList.Items.Clear();
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目 ID：{x?.ProblemId}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目名称：{x?.ProblemName}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目难度：{x?.Level}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"数据组数：{x?.DataSets.Length}" });
            ProblemInfomationList.Items.Add(new TextBlock { Text = $"题目总分：{x?.DataSets.Sum(i => i.Score)}" });
        }

        private void Button_Submit(object sender, RoutedEventArgs e)
        {
            if (!(MyProblemList.SelectedItem is Problem x))
            {
                MessageBox.Show("请选择题目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var type = string.Empty;
            foreach (var i in LangBox.Items)
                if (i is RadioButton t)
                    if (t.IsChecked ?? false)
                        type = t.Content.ToString();
            if (!string.IsNullOrEmpty(CodeBox.Text) && !string.IsNullOrEmpty(type))
            {
                Connection.UpdateMainPage.Invoke($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 提交代码，题目：{x.ProblemName}");
                Connection.SendData("SubmitCodeForCompetition",
                    x.ProblemId + Connection.Divpar + _competition.CompetitionId + Connection.Divpar + type +
                    Connection.Divpar + CodeBox.Text);
                CodeBox.Clear();
            }
            else
            {
                if (string.IsNullOrEmpty(type))
                {
                    MessageBox.Show("请选择语言", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ProblemDescription_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(MyProblemList.SelectedItem is Problem x))
            {
                MessageBox.Show("请选择题目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var d = new ProblemDescription();
            d.SetProblemDescription(
                string.IsNullOrEmpty(x.Description) ? Connection.GetProblemDescription(x.ProblemId) : x.Description,
                x.ProblemIndex);
            d.Show();
        }
    }

    public static partial class Connection
    {
        private static readonly List<Problem> QueryProblemsForCompetitionResult = new List<Problem>();
        private static bool _queryProblemsForCompetitionState;
        private static readonly List<JudgeInfo> QueryJudgeLogBelongsToCompetitionResult = new List<JudgeInfo>();
        private static bool _queryJudgeLogBelongsToCompetitionState;
        private static List<Compiler> _queryLanguagesForCompetitionResult = new List<Compiler>();
        private static bool _queryLanguagesForCompetitionState;
        private static DateTime _getCurrentDateTimeResult;
        private static bool _getCurrentDateTimeState;


        public static List<JudgeInfo> QueryJudgeLogBelongsToCompetition(int competitionId)
        {
            _queryJudgeLogBelongsToCompetitionState = false;
            QueryJudgeLogBelongsToCompetitionResult.Clear();
            SendData("QueryJudgeLogBelongsToCompetition", competitionId.ToString());
            while (!_queryJudgeLogBelongsToCompetitionState)
                Thread.Sleep(1);
            return QueryJudgeLogBelongsToCompetitionResult;
        }

        public static List<Problem> QueryProblemsForCompetition(int competitionId)
        {
            _queryProblemsForCompetitionState = false;
            QueryProblemsForCompetitionResult.Clear();
            SendData("QueryProblemsForCompetition", competitionId.ToString());
            while (!_queryProblemsForCompetitionState)
                Thread.Sleep(1);
            return QueryProblemsForCompetitionResult;
        }

        public static List<Compiler> QueryLanguagesForCompetition()
        {
            _queryLanguagesForCompetitionState = false;
            _queryLanguagesForCompetitionResult.Clear();
            SendData("QueryLanguagesForCompetition", string.Empty);
            while (!_queryLanguagesForCompetitionState)
                Thread.Sleep(1);
            return _queryLanguagesForCompetitionResult;
        }

        public static DateTime GetCurrentDateTime()
        {
            _getCurrentDateTimeState = false;
            SendData("GetCurrentDateTime", string.Empty);
            while (!_getCurrentDateTimeState)
                Thread.Sleep(1);
            return _getCurrentDateTimeResult;
        }
    }
}