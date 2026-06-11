using System.Windows;
using OsintPro.UI.Models;

namespace OsintPro.UI.Views
{
    public partial class CompareDossiersWindow : Window
    {
        public CompareDossiersWindow(string compareText)
        {
            InitializeComponent();
            CompareText.Text = compareText ?? "";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}