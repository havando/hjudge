﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;
using Markdig;

namespace Server
{
    /// <summary>
    ///     Interaction logic for ProblemManagement.xaml
    /// </summary>
    public partial class ProblemManagement : Window
    {
        private readonly ObservableCollection<Problem> _problems = new ObservableCollection<Problem>();

        public ProblemManagement()
        {
            InitializeComponent();
        }

        private string _curAddress = null;

        private static void SuppressScriptErrors(WebBrowser webBrowser, bool hide)
        {
            webBrowser.Navigating += (s, e) =>
            {
                var fiComWebBrowser =
                    typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fiComWebBrowser == null)
                    return;

                var objComWebBrowser = fiComWebBrowser.GetValue(webBrowser);
                if (objComWebBrowser == null)
                    return;

                objComWebBrowser.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, objComWebBrowser,
                    new object[] { hide });
            };
        }

        private void Level_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LevelShow != null)
                LevelShow.Content = Level.Value;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SuppressScriptErrors(DescriptionViewer, true);
            foreach (var queryProblem in Connection.QueryProblems(true))
            {
                _problems.Add(queryProblem);
            }
            ListView.ItemsSource = _problems;
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
            Description.Text = problem.Description;
            Public.IsChecked = (problem.Option & 1) != 0;
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Properties.Resources.MarkdownStyleHead + "\n" + Markdown.ToHtml(Description.Text, pipeline) +
                         "\n" + Properties.Resources.MarkdownStyleTail;
            var curDir = AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/");
            if (curDir.EndsWith("/")) curDir = curDir.Substring(0, curDir.Length - 1);
            result = result.Replace("${ExtensionsDir}", "file://" + curDir + "/Extensions");
            curDir = Environment.GetEnvironmentVariable("temp");
            if (curDir.EndsWith("\\")) curDir = curDir.Substring(0, curDir.Length - 1);
            if (!string.IsNullOrEmpty(_curAddress))
                try
                {
                    File.Delete(_curAddress);
                }
                catch
                {
                    //ignored
                }
            _curAddress = curDir + "\\" + Guid.NewGuid().ToString() + ".html";
            File.WriteAllText(_curAddress, result, Encoding.Unicode);
            DescriptionViewer.Navigate(new Uri(_curAddress));
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
            var tid = Connection.NewProblem();
            _problems.Add(Connection.GetProblem(tid));
            ListView.SelectedIndex = ListView.Items.Count - 1;
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
            problem.Option = 0;
            problem.Type = 1;
            problem.CompileCommand = CompileCommand.Text;
            problem.ProblemName = ProblemName.Text;
            problem.ExtraFiles = ExtraFiles.Text.Split('|');
            problem.InputFileName = InputFileName.Text;
            problem.OutputFileName = OutputFileName.Text;
            problem.SpecialJudge = SpecialJudge.Text;
            problem.Level = Convert.ToInt32(Level.Value);
            problem.DataSets = new Data[ListBox.Items.Count];
            problem.Description = Description.Text;
            if (Public.IsChecked ?? false) problem.Option |= 1;
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

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl t)
                if (t.SelectedIndex == 1)
                {
                    var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                    var result = Properties.Resources.MarkdownStyleHead + "\n" +
                                 Markdown.ToHtml(Description.Text, pipeline) + "\n" +
                                 Properties.Resources.MarkdownStyleTail;
                    var curDir = AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/");
                    if (curDir.EndsWith("/")) curDir = curDir.Substring(0, curDir.Length - 1);
                    result = result.Replace("${ExtensionsDir}", "file://" + curDir + "/Extensions");
                    curDir = Environment.GetEnvironmentVariable("temp");
                    if (curDir.EndsWith("\\")) curDir = curDir.Substring(0, curDir.Length - 1);
                    if (!string.IsNullOrEmpty(_curAddress))
                        try
                        {
                            File.Delete(_curAddress);
                        }
                        catch
                        {
                            //ignored
                        }
                    _curAddress = curDir + "\\" + Guid.NewGuid().ToString() + ".html";
                    File.WriteAllText(_curAddress, result, Encoding.Unicode);
                    DescriptionViewer.Navigate(new Uri(_curAddress));
                }
        }
    }
}