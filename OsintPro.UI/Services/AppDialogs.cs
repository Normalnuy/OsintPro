using System.Windows;
using OsintPro.UI.Views;

namespace OsintPro.UI.Services
{
    public static class AppDialogs
    {
        public static void Info(Window owner, string title, string message) =>
            Show(owner, title, message, "ℹ️", "OK");

        public static void Success(Window owner, string title, string message) =>
            Show(owner, title, message, "✅", "OK");

        public static void Warning(Window owner, string title, string message) =>
            Show(owner, title, message, "⚠️", "OK");

        public static bool Confirm(Window owner, string title, string message) =>
            Show(owner, title, message, "❓", "Так", "Ні") == true;

        private static bool? Show(Window owner, string title, string message, string icon, string primary, string secondary = null)
        {
            var dlg = new AppDialogWindow(title, message, icon, primary, secondary)
            {
                Owner = owner
            };
            dlg.ShowDialog();
            return dlg.DialogResultValue;
        }
    }
}