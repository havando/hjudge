using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Server
{
    /// <summary>
    /// Interaction logic for JudgeLogs.xaml
    /// </summary>
    public partial class JudgeLogs : Window
    {
        public JudgeLogs()
        {
            InitializeComponent();
        }

        private ObservableCollection<JudgeInfo> _curJudgeInfo;

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Connection.ClearJudgeLog();
            ListView.Items.Clear();
        }

        private void Label_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            _curJudgeInfo = Connection.QueryJudgeLog();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var a = ListView.SelectedItem as JudgeInfo;
            if (a != null)
            {
                Code.Text = "代码：\r\n" + a.Code;
                var details = "评测详情：\r\n";
                for (var i = 0; i < a.Result.Length; i++)
                {
                    details +=
                        $"#{i + 1} ———— 时间：{a.Timeused[i]}，内存：{a.Memoryused[i]}，退出代码：{a.Exitcode[i]}，结果：{a.Result[i]}，分数： {a.Score[i]}\r\n";
                }
                JudgeDetails.Text = details;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _curJudgeInfo = Connection.QueryJudgeLog();
            ListView.ItemsSource = _curJudgeInfo;
            if (UserHelper.CurrentUser.Type >= 3) { ClearLabel.Visibility = Visibility.Hidden; }
        }
    }
}
