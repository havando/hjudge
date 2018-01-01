using System;
using System.IO;
using System.Reflection;
using System.Text;
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
        private string _curAddress = null;
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
                         Properties.Resources.MarkdownStyleTail; var curDir = AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/");
            if (curDir.EndsWith("/")) curDir = curDir.Substring(0, curDir.Length - 1);
            result = result.Replace("${ExtensionsDir}", "file://" + curDir + "/Extensions");
            curDir = Environment.GetEnvironmentVariable("temp");
            if (curDir.EndsWith("\\")) curDir = curDir.Substring(0, curDir.Length - 1);
            if (!string.IsNullOrEmpty(_curAddress))
                try
                {
                    File.Delete(_curAddress);
                }
                catch
                {
                    //ignored
                }
            _curAddress = curDir + "\\" + Guid.NewGuid().ToString() + ".html";
            File.WriteAllText(_curAddress, result, Encoding.Unicode);
            Description.Navigate(new Uri(_curAddress));
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_curAddress))
                try
                {
                    File.Delete(_curAddress);
                }
                catch
                {
                    //ignored
                }
        }
    }
}