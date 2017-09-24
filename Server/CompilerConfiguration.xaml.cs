using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;

namespace Server
{
    /// <summary>
    /// Interaction logic for CompilerConfiguration.xaml
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
                        Properties.Resources.CompilerSetControl.Replace("${index}", (CompilerBox.Items.Count + 1).ToString()));
                var xmlreader = new XmlTextReader(strreader);
                var obj = XamlReader.Load(xmlreader);
                CompilerBox.Items.Add((UIElement)obj);
                if ((CompilerBox.Items[CompilerBox.Items.Count - 1] as GroupBox)?.Content is Grid tmp)
                {
                    foreach (var j in tmp.Children)
                    {
                        if (j is TextBox t)
                        {
                            if (t.Name.Contains("Name"))
                            {
                                t.Text = i.DisplayName;
                            }
                            if (t.Name.Contains("Compiler"))
                            {
                                t.Text = i.CompilerPath;
                            }
                            if (t.Name.Contains("Args"))
                            {
                                t.Text = i.DefaultArgs;
                            }
                            if (t.Name.Contains("Ext"))
                            {
                                t.Text = i.ExtName;
                            }
                            if (t.Name.Contains("Safe"))
                            {
                                t.Text = i.SafeCheck;
                            }
                        }
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var strreader =
                new StringReader(
                    Properties.Resources.CompilerSetControl.Replace("${index}", (CompilerBox.Items.Count + 1).ToString()));
            var xmlreader = new XmlTextReader(strreader);
            var obj = XamlReader.Load(xmlreader);
            CompilerBox.Items.Add((UIElement)obj);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Configuration.Configurations.Compiler.Clear();
            foreach (var i in CompilerBox.Items)
            {
                var cTmp = new Compiler();
                if (i is GroupBox gpBox)
                {
                    if (gpBox.Content is Grid tmp)
                    {
                        foreach (var j in tmp.Children)
                        {
                            if (j is TextBox t)
                            {
                                if (t.Name.Contains("Name"))
                                {
                                    cTmp.DisplayName = t.Text;
                                }
                                if (t.Name.Contains("Compiler"))
                                {
                                    cTmp.CompilerPath = t.Text;
                                }
                                if (t.Name.Contains("Args"))
                                {
                                    cTmp.DefaultArgs = t.Text;
                                }
                                if (t.Name.Contains("Ext"))
                                {
                                    cTmp.ExtName = t.Text;
                                }
                                if (t.Name.Contains("Safe"))
                                {
                                    cTmp.SafeCheck = t.Text;
                                }
                            }
                        }
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
