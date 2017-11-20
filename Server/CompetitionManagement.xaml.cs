using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Server
{
    /// <summary>
    ///     Interaction logic for CompetitionManagement.xaml
    /// </summary>
    public partial class CompetitionManagement : Window
    {
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

        private void ListView_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}