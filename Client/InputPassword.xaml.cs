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

namespace Client
{
    /// <summary>
    /// Interaction logic for InputPassword.xaml
    /// </summary>
    public partial class InputPassword : Window
    {
        public InputPassword()
        {
            InitializeComponent();
        }

        private Action<string> _recall;
        private bool _hasNotify = false;

        public void SetRecallFun(Action<string> fun)
        {
            _recall = fun;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _hasNotify = true;
            _recall.Invoke(Password.Password);
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (!_hasNotify)
                _recall.Invoke(null);
        }

        private void Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _hasNotify = true;
                _recall.Invoke(Password.Password);
                Close();
            }
        }
    }
}
