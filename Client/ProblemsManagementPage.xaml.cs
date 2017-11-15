using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Xml;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Client
{
    /// <summary>
    /// Interaction logic for ProblemsManagementPage.xaml
    /// </summary>
    public partial class ProblemsManagementPage : Page
    {
        private ObservableCollection<Problem> _problems = new ObservableCollection<Problem>();

        public ProblemsManagementPage()
        {
            InitializeComponent();
        }

        private void Level_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LevelShow != null)
                LevelShow.Content = Level.Value;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var a = Convert.ToInt32(DataSetsNumber.Text);
                while (ListBox.Items.Count > a)
                {
                    (ListBox.FindName($"Data{ListBox.Items.Count}") as Grid)?.Children.Clear();
                    ListBox.Items.RemoveAt(ListBox.Items.Count - 1);
                }
                while (ListBox.Items.Count < a)
                {
                    var strreader =
                        new StringReader(
                            Properties.Resources.DataSetControl.Replace("${index}",
                                (ListBox.Items.Count + 1).ToString()));
                    var xmlreader = new XmlTextReader(strreader);
                    var obj = XamlReader.Load(xmlreader);
                    ListBox.Items.Add((UIElement)obj);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var m = ComboBox.Text;
            for (var i = 1; i <= ListBox.Items.Count; i++)
            {
                var t = m.Split('|');
                if (t.Length != 5)
                {
                    MessageBox.Show("套用模板格式错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var c = (ListBox.Items[i - 1] as Grid)?.FindName($"Input{i}") as TextBox;
                if (c != null)
                    c.Text = t[0] == "*" ? c.Text : t[0];
                c = (ListBox.Items[i - 1] as Grid)?.FindName($"Output{i}") as TextBox;
                if (c != null)
                    c.Text = t[1] == "*" ? c.Text : t[1];
                c = (ListBox.Items[i - 1] as Grid)?.FindName($"Time{i}") as TextBox;
                if (c != null)
                    c.Text = t[2] == "*" ? c.Text : t[2];
                c = (ListBox.Items[i - 1] as Grid)?.FindName($"Memory{i}") as TextBox;
                if (c != null)
                    c.Text = t[3] == "*" ? c.Text : t[3];
                c = (ListBox.Items[i - 1] as Grid)?.FindName($"Score{i}") as TextBox;
                if (c != null)
                    c.Text = t[4] == "*" ? c.Text : t[4];
            }
        }

        private string StringArrCastToString(IReadOnlyList<string> p)
        {
            if (p == null) return string.Empty;
            var m = string.Empty;
            for (var i = 0; i < p.Count; i++)
                if (i != p.Count - 1)
                    m += p[i] + "|";
                else
                    m += p[i];
            return m;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ListView.SelectedItem is Problem problem)) return;
            ProblemName.Text = problem.ProblemName;
            SpecialJudge.Text = problem.SpecialJudge;
            ExtraFiles.Text = StringArrCastToString(problem.ExtraFiles);
            InputFileName.Text = problem.InputFileName;
            OutputFileName.Text = problem.OutputFileName;
            CompileCommand.Text = problem.CompileCommand;
            AddDate.Content = problem.AddDate;
            DataSetsNumber.Text = problem.DataSets?.Length.ToString() ?? "0";
            Level.Value = Convert.ToInt32(problem.Level);
            LevelShow.Content = Level.Value;
            var a = problem.DataSets?.Length ?? 0;
            DataSetsNumber.Text = a.ToString();
            while (ListBox.Items.Count > a)
            {
                foreach (var t in ListBox.Items)
                    if ((t as Grid)?.Name == $"Data{ListBox.Items.Count}")
                        (t as Grid).Children.Clear();
                ListBox.Items.RemoveAt(ListBox.Items.Count - 1);
            }
            while (ListBox.Items.Count < a)
            {
                var strreader =
                    new StringReader(
                        Properties.Resources.DataSetControl.Replace("${index}", (ListBox.Items.Count + 1).ToString()));
                var xmlreader = new XmlTextReader(strreader);
                var obj = XamlReader.Load(xmlreader);
                ListBox.Items.Add((UIElement)obj);
            }
            for (var i = 0; i < ListBox.Items.Count; i++)
                foreach (var t in ListBox.Items)
                {
                    if ((t as Grid)?.Name != $"Data{i + 1}") continue;
                    var b = (t as Grid).FindName($"Input{i + 1}") as TextBox;
                    if (b != null) b.Text = problem.DataSets?[i]?.InputFile ?? string.Empty;
                    b = (t as Grid).FindName($"Output{i + 1}") as TextBox;
                    if (b != null) b.Text = problem.DataSets?[i]?.OutputFile ?? string.Empty;
                    b = (t as Grid).FindName($"Time{i + 1}") as TextBox;
                    if (b != null) b.Text = problem.DataSets?[i]?.TimeLimit.ToString() ?? string.Empty;
                    b = (t as Grid).FindName($"Memory{i + 1}") as TextBox;
                    if (b != null) b.Text = problem.DataSets?[i]?.MemoryLimit.ToString() ?? string.Empty;
                    b = (t as Grid).FindName($"Score{i + 1}") as TextBox;
                    if (b != null)
                        b.Text = problem.DataSets?[i]?.Score.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
                }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            _problems.Add(new Problem
            {
                ProblemId = Connection.AddProblem()
            });
            ListView.SelectedIndex = ListView.Items.Count - 1;
            InputFileName.Text = "${name}.in";
            OutputFileName.Text = "${name}.out";
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (!(ListView.SelectedItem is Problem problem)) return;
            foreach (var t in _problems)
            {
                if (t.ProblemId != problem.ProblemId) continue;
                Connection.DeleteProblem(t.ProblemId);
                _problems.Remove(t);
                break;
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (!(ListView.SelectedItem is Problem problem)) return;
            problem.Type = 1;
            problem.CompileCommand = CompileCommand.Text;
            problem.ProblemName = ProblemName.Text;
            problem.ExtraFiles = ExtraFiles.Text.Split('|');
            problem.InputFileName = InputFileName.Text;
            problem.OutputFileName = OutputFileName.Text;
            problem.SpecialJudge = SpecialJudge.Text;
            problem.Level = Convert.ToInt32(Level.Value);
            problem.DataSets = new Data[ListBox.Items.Count];
            for (var i = 0; i < ListBox.Items.Count; i++)
            {
                problem.DataSets[i] = new Data();
                foreach (var t in ListBox.Items)
                {
                    if ((t as Grid)?.Name != $"Data{i + 1}") continue;
                    problem.DataSets[i].InputFile =
                        ((t as Grid).FindName($"Input{i + 1}") as TextBox
                        )
                        ?.Text;
                    problem.DataSets[i].OutputFile =
                        ((t as Grid).FindName($"Output{i + 1}") as
                            TextBox)
                        ?.Text;
                    try
                    {
                        problem.DataSets[i].TimeLimit = Convert.ToInt64(
                            ((t as Grid).FindName($"Time{i + 1}") as TextBox)
                            ?.Text);
                    }
                    catch
                    {
                        problem.DataSets[i].TimeLimit = 0;
                    }
                    try
                    {
                        problem.DataSets[i].MemoryLimit = Convert.ToInt64(
                            ((t as Grid).FindName($"Memory{i + 1}") as
                                TextBox)
                            ?.Text);
                    }
                    catch
                    {
                        problem.DataSets[i].MemoryLimit = 0;
                    }
                    try
                    {
                        problem.DataSets[i].Score = Convert.ToSingle(
                            ((t as Grid).FindName($"Score{i + 1}") as TextBox
                            )
                            ?.Text);
                    }
                    catch
                    {
                        problem.DataSets[i].Score = 0;
                    }
                }
            }
            Connection.UpdateProblem(problem);
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
            if (bindingProperty != null) sdc.Add(new SortDescription(bindingProperty, sortDirection));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ListView.ItemsSource = _problems;
            _problems.Clear();
            Task.Run(() =>
            {
                Dispatcher.Invoke(() => Dealing.Visibility = Visibility.Visible);
                foreach (var queryProblem in Connection.QueryProblems())
                {
                    Dispatcher.Invoke(() => _problems.Add(queryProblem));
                }
                Dispatcher.Invoke(() => Dealing.Visibility = Visibility.Hidden);
            });
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
        }

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var ofg = new OpenFileDialog
            {
                Title = "选择数据包：",
                Filter = "数据包文件 (.zip)|*.zip",
                Multiselect = false
            };
            if (ofg.ShowDialog() == true)
            {
                if (Path.GetExtension(ofg.FileName).ToLower() != ".zip")
                {
                    MessageBox.Show("不是标准的数据包文件", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!string.IsNullOrEmpty(ofg.FileName))
                {
                    var fi = new FileInfo(ofg.FileName);
                    if (fi.Length > 512 * 1048576)
                    {
                        MessageBox.Show("数据包大小不能超过 512 MB", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Connection.UploadFileResult = false;
                    IsEnabled = false;
                    Task.Run(() =>
                    {
                        Connection.CanSwitch = false;
                        Dispatcher.Invoke(() => Dealing.Visibility = Visibility.Visible);
                        Connection.SendFile(ofg.FileName, "DataFile");
                        while (!Connection.UploadFileResult)
                        {
                            Thread.Sleep(1);
                        }
                        Dispatcher.Invoke(() =>
                        {
                            Dealing.Visibility = Visibility.Hidden;
                            IsEnabled = true;
                        });
                        Connection.CanSwitch = true;
                    });
                }
            }
        }

        private void Label_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            var ofg = new OpenFileDialog
            {
                Title = "选择共享文件包（自动解压）：",
                Filter = "共享文件包 (.zip)|*.zip",
                Multiselect = false
            };
            if (ofg.ShowDialog() == true)
            {
                if (Path.GetExtension(ofg.FileName).ToLower() != ".zip")
                {
                    MessageBox.Show("不是标准的共享文件包文件", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!string.IsNullOrEmpty(ofg.FileName))
                {
                    var fi = new FileInfo(ofg.FileName);
                    if (fi.Length > 512 * 1048576)
                    {
                        MessageBox.Show("共享文件包大小不能超过 512 MB", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Connection.UploadFileResult = false;
                    IsEnabled = false;
                    Task.Run(() =>
                    {
                        Connection.CanSwitch = false;
                        Dispatcher.Invoke(() => Dealing.Visibility = Visibility.Visible);
                        Connection.SendFile(ofg.FileName, "PublicFile");
                        while (!Connection.UploadFileResult)
                        {
                            Thread.Sleep(1);
                        }
                        Dispatcher.Invoke(() =>
                        {
                            Dealing.Visibility = Visibility.Hidden;
                            IsEnabled = true;
                        });
                        Connection.CanSwitch = true;
                    });
                }
            }
        }

        private void Label_MouseDown_2(object sender, MouseButtonEventArgs e)
        {
            if (!(ListView.SelectedItem is Problem problem)) return;
            if (MessageBox.Show("删除后不可恢复，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Connection.SendData("ClearData", problem.ProblemId.ToString());
                if (problem.ExtraFiles.Length > 0&& MessageBox.Show("是否删除额外文件？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    Connection.SendData("DeleteExtra", problem.ProblemId.ToString());
                if (!string.IsNullOrEmpty(problem.SpecialJudge)&& MessageBox.Show("是否删除比较程序？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    Connection.SendData("DeleteJudge", problem.ProblemId.ToString());
            }
        }
    }

    public static partial class Connection
    {
        private static int _addProblemResult;
        private static bool _deleteProblemResult;
        private static bool _updateProblemResult;
        private static bool _queryProblemsResultState;
        private static ObservableCollection<Problem> _queryProblemsResult;
        public static bool UploadFileResult;

        public static IEnumerable<Problem> QueryProblems()
        {
            _queryProblemsResultState = false;
            _queryProblemsResult = new ObservableCollection<Problem>();
            SendData("QueryProblems", string.Empty);
            while (!_queryProblemsResultState)
            {
                Thread.Sleep(1);
            }
            return _queryProblemsResult;
        }

        public static void DeleteProblem(int problemId)
        {
            _deleteProblemResult = false;
            SendData("DeleteProblem", problemId.ToString());
            while (!_deleteProblemResult)
            {
                Thread.Sleep(1);
            }
        }

        public static int AddProblem()
        {
            _addProblemResult = -1;
            SendData("AddProblem", string.Empty);
            while (_addProblemResult <= 0)
            {
                Thread.Sleep(1);
            }
            return _addProblemResult;
        }

        public static void UpdateProblem(Problem problem)
        {
            _updateProblemResult = false;
            SendData("UpdateProblem", JsonConvert.SerializeObject(problem));
            while (!_updateProblemResult)
            {
                Thread.Sleep(1);
            }
        }
    }

}