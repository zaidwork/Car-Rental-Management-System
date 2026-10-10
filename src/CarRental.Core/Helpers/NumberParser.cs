using System;
using System.Globalization;
using System.Text;

namespace CarRental.Core.Helpers;

public static class NumberParser
{
    /// <summary>
    /// تحويل أي أرقام هندية/مشرقية في النص إلى أرقام إنجليزية مع توحيد الفواصل
    /// </summary>
    public static string NormalizeDigits(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            switch (ch)
            {
                case '٠': sb.Append('0'); break;
                case '١': sb.Append('1'); break;
                case '٢': sb.Append('2'); break;
                case '٣': sb.Append('3'); break;
                case '٤': sb.Append('4'); break;
                case '٥': sb.Append('5'); break;
                case '٦': sb.Append('6'); break;
                case '٧': sb.Append('7'); break;
                case '٨': sb.Append('8'); break;
                case '٩': sb.Append('9'); break;
                case '٫': sb.Append('.'); break; // فاصلة عشرية عربية (\u066B)
                case '،': // فاصلة عربية (\u060C)
                case '٬': // فاصلة آلاف عربية (\u066C)
                    sb.Append(','); 
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// استخراج الأرقام فقط (مفيد لرقم الهوية ورقم الجوال ورقم الشاسيه)
    /// </summary>
    public static string ExtractDigitsOnly(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = NormalizeDigits(input);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// تحويل النص إلى رقم عشري بأمان دون إطلاق استثناءات
    /// </summary>
    public static decimal ParseDecimal(string? input, decimal defaultValue = 0m)
    {
        if (string.IsNullOrWhiteSpace(input)) return defaultValue;

        var clean = NormalizeDigits(input).Replace(" ", "").Replace(",", "");
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    /// تحويل النص إلى رقم صحيح بأمان دون إطلاق استثناءات
    /// </summary>
    public static int ParseInt(string? input, int defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(input)) return defaultValue;

        var clean = ExtractDigitsOnly(input);
        if (int.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return defaultValue;
    }
}
