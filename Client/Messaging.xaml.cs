using System.Windows;

namespace Client
{
    /// <summary>
    /// Interaction logic for Messaging.xaml
    /// </summary>
    public partial class Messaging : Window
    {

        public Messaging()
        {
            InitializeComponent();
        }

        public void SetMessge(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                Contents.Text = msg;
            });
        }
    }

}
