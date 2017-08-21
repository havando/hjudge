using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace UpdateInfoGenerator
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var p = (ComboBoxT.SelectedItem as ComboBoxItem)?.Content as string;
            var x = new NewVersionInfo
            {
                Content = ContentT.Text,
                Date = DateTime.Now.ToString("yyyy/MM/dd"),
                Program = $"{p}.exe",
                Uri = $"http://www.hez2010.vip/hjudge/Update_{p}.zip",
                Version = VersionT.Text
            };
            File.WriteAllText(Environment.CurrentDirectory + $"\\Update_{p}.txt", JsonConvert.SerializeObject(x),
                Encoding.Default);
            MessageBox.Show("已生成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}