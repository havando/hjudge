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

        public void SetContent(string code, string details)
        {
            CodeBox.Text = "代码：\r\n" + code;
            DetailsBox.Text = "详情：\r\n" + details;
        }
    }
}