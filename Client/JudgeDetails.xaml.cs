using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace Client
{
    /// <summary>
    ///     JudgeDetails.xaml 的交互逻辑
    /// </summary>
    public partial class JudgeDetails : Window
    {
        public JudgeDetails()
        {
            InitializeComponent();
        }

        public void SetContent(JudgeInfo jInfo)
        {
            Title = $"评测详情 - #{jInfo.JudgeId}";
            ResultSummary.Content = jInfo.ResultSummary;
            if (jInfo.ResultSummary == "Accepted") ResultSummary.Foreground = Brushes.Green;
            else if (!(jInfo.ResultSummary == "Judging..."))
            {
                if (jInfo.ResultSummary.Contains("Exceeded"))
                    ResultSummary.Foreground = Brushes.DarkOrange;
                else
                    ResultSummary.Foreground = Brushes.Red;
            }
            JudgeInfoSummary.Content =
                $"#{jInfo.JudgeId}，评测时间：{jInfo.JudgeDate}，题目：{jInfo.ProblemName}，得分：{jInfo.FullScore}";
            CodeBox.Text = "代码：\n" + jInfo.Code;
            var details = new ObservableCollection<JudgeInfoDetails>();
            JudgeDetailsTree.ItemsSource = details;
            if (jInfo.Result != null)
                for (var i = 0; i < jInfo.Result.Length; i++)
                {
                    var tmp = new JudgeInfoDetails { Title = $"#{i + 1}：{jInfo.Result[i]}，{jInfo.Score[i]}" };
                    tmp.Children = new ObservableCollection<JudgeInfoDetails>
                    {
                        new JudgeInfoDetails { Title = $"时间：{jInfo.Timeused[i]}ms" },
                        new JudgeInfoDetails { Title = $"内存：{jInfo.Memoryused[i]}kb" },
                        new JudgeInfoDetails { Title = $"退出代码：{jInfo.Exitcode[i]}" }
                    };
                    details.Add(tmp);
                }

            DetailsBox.Text = "其他信息：\n" + jInfo.AdditionInfo;
        }
    }
    public class JudgeInfoDetails
    {
        public string Title { get; set; }
        public ObservableCollection<JudgeInfoDetails> Children { get; set; }
    }
}