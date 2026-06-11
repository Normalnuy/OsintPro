using System;
using System.Security.Cryptography;
using System.Text;

namespace OsintPro.UI.Services
{
    public static class DpapiProtector
    {
        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return "";

            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] protectedBytes = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
                return "";

            byte[] protectedBytes = Convert.FromBase64String(protectedText);
            byte[] data = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }

        public static bool TryUnprotect(string protectedText, out string plainText)
        {
            try
            {
                plainText = Unprotect(protectedText);
                return true;
            }
            catch
            {
                plainText = protectedText;
                return false;
            }
        }
    }
}