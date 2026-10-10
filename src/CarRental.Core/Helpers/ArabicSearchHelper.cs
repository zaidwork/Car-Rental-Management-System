using System;
using System.Text;

namespace CarRental.Core.Helpers;

public static class ArabicSearchHelper
{
    /// <summary>
    /// تطبيع النص العربي لتسهيل البحث السريع وتجاوز أخطاء الإملاء (همزات، تاء مربوطة، تشكيل، أرقام)
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            // 1. إزالة حركات التشكيل والتنوين والشدة
            if (ch >= '\u064B' && ch <= '\u0652') continue;
            if (ch == '\u0670') continue; // ألف خنجرية
            if (ch == '\u0640') continue; // تطويل (كشيدة)

            switch (ch)
            {
                // 2. توحيد الألف بكافة أشكالها إلى ألف عادية
                case 'أ':
                case 'إ':
                case 'آ':
                case 'ٱ':
                    sb.Append('ا');
                    break;

                // 3. توحيد التاء المربوطة مع الهاء
                case 'ة':
                    sb.Append('ه');
                    break;

                // 4. توحيد الألف المقصورة مع الياء
                case 'ى':
                    sb.Append('ي');
                    break;

                // 5. توحيد الأرقام الهندية/المشرقية إلى أرقام قياسية
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

                default:
                    sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// فحص ما إذا كان النص المصدر يحتوي على كلمة البحث بعد تطبيع الطرفين
    /// </summary>
    public static bool ContainsArabic(this string? source, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        if (string.IsNullOrWhiteSpace(source)) return false;

        var normSource = Normalize(source);
        var normQuery = Normalize(query);

        return normSource.Contains(normQuery, StringComparison.OrdinalIgnoreCase);
    }
}
