using System;
using System.Linq;

namespace CarRental.Infrastructure.Helpers;

public static class WhatsAppHelper
{
    /// <summary>
    /// تجهيز رقم الهاتف للواتساب مع دعم خياري المقدمة الفلسطينية (+970) أو الإسرائيلية (+972)
    /// </summary>
    public static string BuildWhatsAppNumber(string rawPhone, string prefix = "970")
    {
        if (string.IsNullOrWhiteSpace(rawPhone)) return string.Empty;

        var digits = new string(rawPhone.Where(char.IsDigit).ToArray());
        var cleanPrefix = prefix.Replace("+", "").Trim();

        if (digits.StartsWith("00970")) return digits.Substring(2);
        if (digits.StartsWith("00972")) return digits.Substring(2);
        if (digits.StartsWith("970") || digits.StartsWith("972")) return digits;

        if (digits.StartsWith("05"))
        {
            return cleanPrefix + digits.Substring(1);
        }

        if (digits.StartsWith("5"))
        {
            return cleanPrefix + digits;
        }

        return cleanPrefix + digits;
    }

    /// <summary>
    /// فتح محادثة الواتساب مباشرة عبر المتصفح أو تطبيق WhatsApp Desktop
    /// </summary>
    public static bool OpenWhatsApp(string rawPhone, string prefix, string message)
    {
        try
        {
            var fullNumber = BuildWhatsAppNumber(rawPhone, prefix);
            if (string.IsNullOrWhiteSpace(fullNumber)) return false;

            var url = $"https://api.whatsapp.com/send?phone={fullNumber}&text={Uri.EscapeDataString(message)}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
