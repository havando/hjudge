using System.Windows;

namespace Client
{
    /// <summary>
    /// JudgeDetails.xaml 的交互逻辑
    /// </summary>
    public partial class JudgeDetails : Window
    {
        public JudgeDetails()
        {
            InitializeComponent();
        }

        public void SetContent(string code, string details)
        {
            CodeBox.Text = code;
            DetailsBox.Text = details;
        }
    }
}
