using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;

namespace Server
{
    /// <summary>
    ///     Interaction logic for CompilerConfiguration.xaml
    /// </summary>
    public partial class CompilerConfiguration : Window
    {
        public CompilerConfiguration()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var i in Configuration.Configurations.Compiler)
            {
                var strreader =
                    new StringReader(
                        Properties.Resources.CompilerSetControl.Replace("${index}",
                            (CompilerBox.Items.Count + 1).ToString()));
                var xmlreader = new XmlTextReader(strreader);
                var obj = XamlReader.Load(xmlreader);
                CompilerBox.Items.Add((UIElement)obj);
                if ((CompilerBox.Items[CompilerBox.Items.Count - 1] as GroupBox)?.Content is StackPanel tmp)
                    foreach (var j in tmp.Children)
                        if (j is DockPanel tmp2)
                            foreach (var k in tmp2.Children)
                            {
                                if (k is TextBox t)
                                {
                                    if (t.Name.Contains("Name"))
                                        t.Text = i.DisplayName;
                                    if (t.Name.Contains("Compiler"))
                                        t.Text = i.CompilerExec;
                                    if (t.Name.Contains("ComArgs"))
                                        t.Text = i.CompilerArgs;
                                    if (t.Name.Contains("Ext"))
                                        t.Text = i.ExtName;
                                    if (t.Name.Contains("Static"))
                                        t.Text = i.StaticCheck;
                                    if (t.Name.Contains("StaArgs"))
                                        t.Text = i.StaticArgs;
                                    if (t.Name.Contains("RunExec"))
                                        t.Text = i.RunExec;
                                    if (t.Name.Contains("RunArgs"))
                                        t.Text = i.RunArgs;
                                }
                                else if (k is CheckBox p)
                                {
                                    if (p.Name.Contains("WSLComExec"))
                                        p.IsChecked = i.LinuxComExec;
                                    if (p.Name.Contains("WSLComArgs"))
                                        p.IsChecked = i.LinuxComArgs;
                                    if (p.Name.Contains("WSLStaExec"))
                                        p.IsChecked = i.LinuxStaExec;
                                    if (p.Name.Contains("WSLStaArgs"))
                                        p.IsChecked = i.LinuxStaArgs;
                                    if (p.Name.Contains("WSLRunExec"))
                                        p.IsChecked = i.LinuxRunExec;
                                    if (p.Name.Contains("WSLRunArgs"))
                                        p.IsChecked = i.LinuxRunArgs;
                                }
                            }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var strreader =
                new StringReader(
                    Properties.Resources.CompilerSetControl.Replace("${index}",
                        (CompilerBox.Items.Count + 1).ToString()));
            var xmlreader = new XmlTextReader(strreader);
            var obj = XamlReader.Load(xmlreader);
            CompilerBox.Items.Add((UIElement)obj);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Configuration.Configurations.Compiler.Clear();
            foreach (var i in CompilerBox.Items)
            {
                var cTmp = new Compiler();
                if (i is GroupBox gpBox)
                    if (gpBox.Content is StackPanel tmp)
                        foreach (var j in tmp.Children)
                            if (j is DockPanel tmp2)
                                foreach (var k in tmp2.Children)
                                {
                                    if (k is TextBox t)
                                    {
                                        if (t.Name.Contains("Name"))
                                            cTmp.DisplayName = t.Text;
                                        if (t.Name.Contains("Compiler"))
                                            cTmp.CompilerExec = t.Text;
                                        if (t.Name.Contains("ComArgs"))
                                            cTmp.CompilerArgs = t.Text;
                                        if (t.Name.Contains("Ext"))
                                            cTmp.ExtName = t.Text;
                                        if (t.Name.Contains("Static"))
                                            cTmp.StaticCheck = t.Text;
                                        if (t.Name.Contains("StaArgs"))
                                            cTmp.StaticArgs = t.Text;
                                        if (t.Name.Contains("RunExec"))
                                            cTmp.RunExec = t.Text;
                                        if (t.Name.Contains("RunArgs"))
                                            cTmp.RunArgs = t.Text;
                                    }
                                    else if (k is CheckBox p)
                                    {
                                        if (p.Name.Contains("WSLComExec"))
                                            cTmp.LinuxComExec = p.IsChecked ?? false;
                                        if (p.Name.Contains("WSLComArgs"))
                                            cTmp.LinuxComArgs = p.IsChecked ?? false;
                                        if (p.Name.Contains("WSLStaExec"))
                                            cTmp.LinuxStaExec = p.IsChecked ?? false;
                                        if (p.Name.Contains("WSLStaArgs"))
                                            cTmp.LinuxStaArgs = p.IsChecked ?? false;
                                        if (p.Name.Contains("WSLRunExec"))
                                            cTmp.LinuxRunExec = p.IsChecked ?? false;
                                        if (p.Name.Contains("WSLRunArgs"))
                                            cTmp.LinuxRunArgs = p.IsChecked ?? false;
                                    }
                                }
                if (!string.IsNullOrEmpty(cTmp.DisplayName))
                {
                    if (Configuration.Configurations.Compiler.Any(j => j.DisplayName == cTmp.DisplayName))
                    {
                        MessageBox.Show("显示名称不允许重复", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Cancel = true;
                    }
                    Configuration.Configurations.Compiler.Add(cTmp);
                }
            }
        }
    }
}