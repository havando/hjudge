using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Markdig;

namespace Client
{
    /// <summary>
    ///     Interaction logic for ProblemDescription.xaml
    /// </summary>
    public partial class ProblemDescription : Window
    {
        public ProblemDescription()
        {
            InitializeComponent();
            SuppressScriptErrors(Description, true);
        }

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
                    new object[] {hide});
            };
        }

        public void SetProblemDescription(string description, string problemIndex)
        {
            Title = $"题目描述 - {problemIndex}";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Properties.Resources.MarkdownStyleHead + "\n" + Markdown.ToHtml(description, pipeline) + "\n" +
                         Properties.Resources.MarkdownStyleTail;
            Description.NavigateToString(result);
        }
    }
}