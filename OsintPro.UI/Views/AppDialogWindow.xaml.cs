using System.Windows;

namespace OsintPro.UI.Views
{
    public partial class AppDialogWindow : Window
    {
        public bool? DialogResultValue { get; private set; }

        public AppDialogWindow(string title, string message, string icon, string primaryText, string secondaryText = null)
        {
            InitializeComponent();
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogIcon.Text = icon;
            PrimaryButton.Content = primaryText;

            if (!string.IsNullOrWhiteSpace(secondaryText))
            {
                SecondaryButton.Content = secondaryText;
                SecondaryButton.Visibility = Visibility.Visible;
            }
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            DialogResultValue = true;
            DialogResult = true;
            Close();
        }

        private void Secondary_Click(object sender, RoutedEventArgs e)
        {
            DialogResultValue = false;
            DialogResult = false;
            Close();
        }
    }
}