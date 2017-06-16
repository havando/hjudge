using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Interaction logic for SystemConfiguratioin.xaml
    /// </summary>
    public partial class SystemConfiguratioin : Window
    {
        public SystemConfiguratioin()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Compiler.Text = Configuration.Configurations.Compiler;
            EnvironmentValue.Text = Configuration.Configurations.EnvironmentValues;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Configuration.Configurations.Compiler = Compiler.Text;
            Configuration.Configurations.EnvironmentValues = EnvironmentValue.Text;
            Configuration.Save();
            Close();
        }
    }
}
