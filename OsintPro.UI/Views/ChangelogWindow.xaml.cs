using System;
using System.Windows;
using System.Windows.Media.Animation;
using OsintPro.UI.Services;

namespace OsintPro.UI.Views
{
    public partial class ChangelogWindow : Window
    {
        public ChangelogWindow(string newVersion, string changelog)
        {
            InitializeComponent();

            VersionTitle.Text = $"JUSTIN OSINT v{newVersion.TrimStart('v', 'V', '.')}";
            ChangelogDocument.Document = ChangelogMarkdownRenderer.ToFlowDocument(changelog);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.25)),
                FillBehavior = FillBehavior.Stop
            };

            fadeOutAnimation.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeOutAnimation);
        }
    }
}