using System.Windows;

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
            MyJudgeInfo.Content =
                $"#{jInfo.JudgeId}，评测时间：{jInfo.JudgeDate}，题目：{jInfo.ProblemName}，结果：{jInfo.ResultSummery}，得分：{jInfo.FullScore}";
            CodeBox.Text = "代码：\n" + jInfo.Code;
            var details = string.Empty;
            if (jInfo.Result != null)
                for (var i = 0; i < jInfo.Result.Length; i++)
                    details +=
                        $"#{i + 1} 时间：{jInfo.Timeused[i]}ms，内存：{jInfo.Memoryused[i]}kb，退出代码：{jInfo.Exitcode[i]}，结果：{jInfo.Result[i]}，分数：{jInfo.Score[i]}\n";
            details += "\n其他信息：\n" + jInfo.AdditionInfo;
            DetailsBox.Text = "详情：\n" + details;
        }
    }
}