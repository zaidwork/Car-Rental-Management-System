using System;

namespace CarRental.Core.Helpers;

public static class ArabicNumberHelper
{
    private static readonly string[] Ones = {
        "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
        "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر",
        "سبعة عشر", "ثمانية عشر", "تسعة عشر"
    };

    private static readonly string[] Tens = {
        "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"
    };

    private static readonly string[] Hundreds = {
        "", "مائة", "مئتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"
    };

    public static string ToArabicWords(decimal amount, string currency = "شيكل جديد")
    {
        if (amount == 0) return $"صفر {currency} فقط لا غير";

        long wholePart = (long)Math.Truncate(amount);
        int decimalPart = (int)Math.Round((amount - wholePart) * 100);

        string words = ConvertGroup(wholePart);

        string result = words.Trim();
        if (!string.IsNullOrEmpty(currency))
        {
            result += " " + currency;
        }

        if (decimalPart > 0)
        {
            string agorat = ConvertGroup(decimalPart);
            result += $" و{agorat} أغورة";
        }

        return "فقط " + result + " لا غير";
    }

    private static string ConvertGroup(long number)
    {
        if (number == 0) return "";

        if (number < 20) return Ones[number];

        if (number < 100)
        {
            long one = number % 10;
            long ten = number / 10;
            if (one > 0)
                return Ones[one] + " و" + Tens[ten];
            return Tens[ten];
        }

        if (number < 1000)
        {
            long hundred = number / 100;
            long remainder = number % 100;
            string remStr = ConvertGroup(remainder);
            if (!string.IsNullOrEmpty(remStr))
                return Hundreds[hundred] + " و" + remStr;
            return Hundreds[hundred];
        }

        if (number < 1000000)
        {
            long thousands = number / 1000;
            long remainder = number % 1000;

            string thouStr;
            if (thousands == 1)
                thouStr = "ألف";
            else if (thousands == 2)
                thouStr = "ألفان";
            else if (thousands >= 3 && thousands <= 10)
                thouStr = ConvertGroup(thousands) + " آلاف";
            else
                thouStr = ConvertGroup(thousands) + " ألفاً";

            string remStr = ConvertGroup(remainder);
            if (!string.IsNullOrEmpty(remStr))
                return thouStr + " و" + remStr;
            return thouStr;
        }

        if (number < 1000000000)
        {
            long millions = number / 1000000;
            long remainder = number % 1000000;

            string milStr;
            if (millions == 1)
                milStr = "مليون";
            else if (millions == 2)
                milStr = "مليونان";
            else if (millions >= 3 && millions <= 10)
                milStr = ConvertGroup(millions) + " ملايين";
            else
                milStr = ConvertGroup(millions) + " مليوناً";

            string remStr = ConvertGroup(remainder);
            if (!string.IsNullOrEmpty(remStr))
                return milStr + " و" + remStr;
            return milStr;
        }

        return number.ToString("N0");
    }
}
