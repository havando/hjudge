using Markdig;
using System.Windows;
using System.Windows.Controls;

namespace Client
{
    /// <summary>
    /// Interaction logic for ProblemDescription.xaml
    /// </summary>
    public partial class ProblemDescription : Window
    {
        public ProblemDescription()
        {
            InitializeComponent();
            SuppressScriptErrors(Description, true);
        }

        static void SuppressScriptErrors(WebBrowser webBrowser, bool hide)
        {
            webBrowser.Navigating += (s, e) =>
            {
                var fiComWebBrowser = typeof(WebBrowser).GetField("_axIWebBrowser2", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fiComWebBrowser == null)
                    return;

                object objComWebBrowser = fiComWebBrowser.GetValue(webBrowser);
                if (objComWebBrowser == null)
                    return;

                objComWebBrowser.GetType().InvokeMember("Silent", System.Reflection.BindingFlags.SetProperty, null, objComWebBrowser, new object[] { hide });
            };
        }

        public void SetProblemDescription(string description)
        {
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Properties.Resources.MarkdownStyleHead + "\n" + Markdown.ToHtml(description, pipeline) + "\n" + Properties.Resources.MarkdownStyleTail;
            Description.NavigateToString(result);
        }
    }
}
