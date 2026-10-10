using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace CarRental.App.Helpers;

public static class ExportHelper
{
    /// <summary>
    /// تصدير مصفوفة بيانات إلى ملف Excel CSV مع ترميز UTF-8 BOM الداعم للغة العربية
    /// </summary>
    public static bool ExportToCsv(string defaultFileName, IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "تصدير البيانات إلى ملف Excel / CSV",
                Filter = "ملف جدول بيانات إكسل (*.csv)|*.csv",
                FileName = defaultFileName
            };

            if (saveDialog.ShowDialog() != true)
            {
                return false;
            }

            var sb = new StringBuilder();
            
            // إضافة علامة UTF-8 BOM لضمان قراءة الإكسل للنصوص العربية بشكل فوري وسليم
            sb.Append('\uFEFF');

            // كتابة العناوين
            var headerList = new List<string>();
            foreach (var h in headers)
            {
                headerList.Add(EscapeCsvCell(h));
            }
            sb.AppendLine(string.Join(",", headerList));

            // كتابة الأسطر
            foreach (var row in rows)
            {
                var rowCells = new List<string>();
                foreach (var cell in row)
                {
                    rowCells.Add(EscapeCsvCell(cell?.ToString() ?? string.Empty));
                }
                sb.AppendLine(string.Join(",", rowCells));
            }

            File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

            var result = MessageBox.Show(
                $"تم تصدير البيانات بنجاح إلى:\n{saveDialog.FileName}\n\nهل ترغب بفتح الملف فوراً في برنامج Excel؟",
                "اكتمل التصدير بنجاح",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = saveDialog.FileName,
                    UseShellExecute = true
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"حدث خطأ أثناء تصدير الملف: {ex.Message}",
                "خطأ في التصدير",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private static string EscapeCsvCell(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        
        bool needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        
        if (needsQuotes)
        {
            return $"\"{escaped}\"";
        }
        return $"\"{escaped}\"";
    }
}
