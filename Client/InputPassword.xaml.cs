using System;
using System.Windows;
using System.Windows.Input;

namespace Client
{
    /// <summary>
    ///     Interaction logic for InputPassword.xaml
    /// </summary>
    public partial class InputPassword : Window
    {
        private bool _hasNotify;

        private Action<string> _recall;

        public InputPassword()
        {
            InitializeComponent();
        }

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