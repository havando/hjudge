using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Server
{
    /// <summary>
    ///     Interaction logic for CompetitionManagement.xaml
    /// </summary>
    public partial class CompetitionManagement : Window
    {
        private static ObservableCollection<Competition> _competitions = new ObservableCollection<Competition>();

        public CompetitionManagement()
        {
            InitializeComponent();
        }

        private void Hour_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox t)
            {
                try
                {
                    var value = Convert.ToInt32(t.Text);
                    if (t.Text != value.ToString() || value < 0 || value >= 24)
                    {
                        t.Text = "0";
                    }
                }
                catch
                {
                    t.Text = "0";
                }
            }
        }

        private void NonHour_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox t)
            {
                try
                {
                    var value = Convert.ToInt32(t.Text);
                    if (t.Text != value.ToString() || value < 0 || value >= 60)
                    {
                        t.Text = "0";
                    }
                }
                catch
                {
                    t.Text = "0";
                }
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView.SelectedItem is Competition t)
            {
                ComName.Text = t.CompetitionName;
                StartDate.SelectedDate = t.StartTime;
                EndDate.SelectedDate = t.EndTime;
                StartHour.Text = t.StartTime.Hour.ToString();
                StartMinute.Text = t.StartTime.Minute.ToString();
                StartSecond.Text = t.StartTime.Second.ToString();
                EndHour.Text = t.EndTime.Hour.ToString();
                EndMinute.Text = t.EndTime.Minute.ToString();
                EndSecond.Text = t.EndTime.Second.ToString();
                var tmp = string.Empty;
                foreach (var i in t.ProblemSet) tmp += $"{i} ";
                ComProblems.Text = tmp;
                if ((t.Option & 1) != 0) LimitedSubmit.IsChecked = true;
                if ((t.Option & 2) != 0) LastSubmit.IsChecked = true;
                if ((t.Option & 4) != 0) SimpleTimeCount.IsChecked = true;
                if ((t.Option & 8) != 0) TimeCount.IsChecked = true;
                if ((t.Option & 16) != 0) IntimeNotify.IsChecked = true;
                else DelayNotify.IsChecked = true;
                if ((t.Option & 32) != 0) ShowRank.IsChecked = true;
                else HideRank.IsChecked = true;
                LimitedSubmitTime.Text = t.SubmitLimit.ToString();
                ComPassword.Text = t.Password;
                ComNote.Text = t.Description;
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
            if (bindingProperty != null) sdc.Add(new SortDescription(bindingProperty, sortDirection));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _competitions.Clear();
            ListView.ItemsSource = _competitions;
            foreach (var i in Connection.QueryCompetition())
            {
                _competitions.Add(i);
            }
        }

        private void LimitedSubmitTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox t)
            {
                try
                {
                    var value = Convert.ToInt32(t.Text);
                    if (t.Text != value.ToString() || value < 0)
                    {
                        t.Text = "0";
                    }
                }
                catch
                {
                    t.Text = "0";
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var t = Connection.GetCompetition(Connection.NewCompetition());
            _competitions.Add(t);
            ListView.SelectedItem = t;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (ListView.SelectedItem is Competition t)
            {
                Connection.DeleteCompetition(t.CompetitionId);
                _competitions.Remove(t);
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (!(ListView.SelectedItem is Competition t)) return;
            t.CompetitionName = ComName.Text;
            var tmpSdate = StartDate.SelectedDate ?? DateTime.Now;
            var tmpEdate = EndDate.SelectedDate ?? DateTime.Now;
            t.StartTime = Convert.ToDateTime($"{tmpSdate:yyyy/MM/dd} {(StartHour.Text.Length == 1 ? $"0{StartHour.Text}" : StartHour.Text)}:{(StartMinute.Text.Length == 1 ? $"0{StartMinute.Text}" : StartMinute.Text)}:{(StartSecond.Text.Length == 1 ? $"0{StartSecond.Text}" : StartSecond.Text)}");
            t.EndTime = Convert.ToDateTime($"{tmpEdate:yyyy/MM/dd} {(EndHour.Text.Length == 1 ? $"0{EndHour.Text}" : EndHour.Text)}:{(EndMinute.Text.Length == 1 ? $"0{EndMinute.Text}" : EndMinute.Text)}:{(EndSecond.Text.Length == 1 ? $"0{EndSecond.Text}" : EndSecond.Text)}");
            t.Option = 0;
            if (LimitedSubmit.IsChecked ?? false) t.Option |= 1;
            if (LastSubmit.IsChecked ?? false) t.Option |= 2;
            if (SimpleTimeCount.IsChecked ?? false) t.Option |= 4;
            if (TimeCount.IsChecked ?? false) t.Option |= 8;
            if (IntimeNotify.IsChecked ?? false) t.Option |= 16;
            if (ShowRank.IsChecked ?? false) t.Option |= 32;
            t.Password = ComPassword.Text;
            t.SubmitLimit = Convert.ToInt32(LimitedSubmitTime.Text);
            var tmpProId = new List<int>();
            foreach (var i in ComProblems.Text.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var x = Convert.ToInt32(i);
                    if (Connection.GetProblem(x).ProblemId == 0)
                    {
                        MessageBox.Show("输入的题目 ID 不合法，无法保存", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    tmpProId.Add(x);
                }
                catch
                {
                    MessageBox.Show("输入的题目 ID 不合法，无法保存", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            t.ProblemSet = tmpProId.ToArray();
            t.Description = ComNote.Text;
            Connection.UpdateCompetition(t);
        }

        private void ListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && !(originalSource is ListViewItem)) originalSource = VisualTreeHelper.GetParent(originalSource);
            if (originalSource == null) return;
            if (!(sender is ListView)) return;
            if (!(ListView.SelectedItem is Competition t)) return;
            var x = new CompetitionViewer();
            x.SetCompetition(t);
            x.Show();
        }
    }
}