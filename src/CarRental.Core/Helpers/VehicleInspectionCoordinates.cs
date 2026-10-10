using System;
using CarRental.Core.Entities;

namespace CarRental.Core.Helpers;

public static class VehicleInspectionCoordinates
{
    // car_all_clean.png has dimensions: 1590 x 696
    public const double AllWidth = 1590.0;
    public const double AllHeight = 696.0;

    // Sub-view crop coordinates inside car_all_clean.png
    public const double FrontOrigX = 6.0;
    public const double FrontOrigY = 21.0;
    public const double FrontWidth = 402.0;
    public const double FrontHeight = 306.0;

    public const double SideOrigX = 417.0;
    public const double SideOrigY = 36.0;
    public const double SideWidth = 768.0;
    public const double SideHeight = 291.0;

    public const double RearOrigX = 1188.0;
    public const double RearOrigY = 3.0;
    public const double RearWidth = 396.0;
    public const double RearHeight = 324.0;

    public const double PerspFrontOrigX = 57.0;
    public const double PerspFrontOrigY = 330.0;
    public const double PerspFrontWidth = 735.0;
    public const double PerspFrontHeight = 363.0;

    public const double PerspRearOrigX = 807.0;
    public const double PerspRearOrigY = 321.0;
    public const double PerspRearWidth = 723.0;
    public const double PerspRearHeight = 369.0;

    public static (double X, double Y) ToUniversalCoordinates(VehicleDamageMark mark)
    {
        return ToUniversalCoordinates(mark.ViewType, mark.XPercent, mark.YPercent);
    }

    public static (double X, double Y) ToUniversalCoordinates(string? viewType, double x, double y)
    {
        var vt = (viewType ?? "all").ToLower().Trim();
        switch (vt)
        {
            case "front":
                return (
                    Math.Clamp((FrontOrigX + (x * FrontWidth / 100.0)) / AllWidth * 100.0, 0.5, 99.5),
                    Math.Clamp((FrontOrigY + (y * FrontHeight / 100.0)) / AllHeight * 100.0, 0.5, 99.5)
                );

            case "side":
                return (
                    Math.Clamp((SideOrigX + (x * SideWidth / 100.0)) / AllWidth * 100.0, 0.5, 99.5),
                    Math.Clamp((SideOrigY + (y * SideHeight / 100.0)) / AllHeight * 100.0, 0.5, 99.5)
                );

            case "rear":
                return (
                    Math.Clamp((RearOrigX + (x * RearWidth / 100.0)) / AllWidth * 100.0, 0.5, 99.5),
                    Math.Clamp((RearOrigY + (y * RearHeight / 100.0)) / AllHeight * 100.0, 0.5, 99.5)
                );

            case "persp_front":
                return (
                    Math.Clamp((PerspFrontOrigX + (x * PerspFrontWidth / 100.0)) / AllWidth * 100.0, 0.5, 99.5),
                    Math.Clamp((PerspFrontOrigY + (y * PerspFrontHeight / 100.0)) / AllHeight * 100.0, 0.5, 99.5)
                );

            case "persp_rear":
                return (
                    Math.Clamp((PerspRearOrigX + (x * PerspRearWidth / 100.0)) / AllWidth * 100.0, 0.5, 99.5),
                    Math.Clamp((PerspRearOrigY + (y * PerspRearHeight / 100.0)) / AllHeight * 100.0, 0.5, 99.5)
                );

            case "all":
            default:
                return (Math.Clamp(x, 0.5, 99.5), Math.Clamp(y, 0.5, 99.5));
        }
    }

    public static (double X, double Y)? ToSubViewCoordinates(VehicleDamageMark mark, string targetView)
    {
        var target = (targetView ?? "all").ToLower().Trim();
        if (target == "all") return ToUniversalCoordinates(mark);

        var vt = (mark.ViewType ?? "all").ToLower().Trim();
        if (vt == target)
        {
            return (Math.Clamp(mark.XPercent, 0.5, 99.5), Math.Clamp(mark.YPercent, 0.5, 99.5));
        }

        // Convert to universal coordinates first
        var uni = ToUniversalCoordinates(mark);
        double uniPixelX = uni.X * AllWidth / 100.0;
        double uniPixelY = uni.Y * AllHeight / 100.0;

        double origX, origY, width, height;
        switch (target)
        {
            case "front":
                origX = FrontOrigX; origY = FrontOrigY; width = FrontWidth; height = FrontHeight;
                break;
            case "side":
                origX = SideOrigX; origY = SideOrigY; width = SideWidth; height = SideHeight;
                break;
            case "rear":
                origX = RearOrigX; origY = RearOrigY; width = RearWidth; height = RearHeight;
                break;
            case "persp_front":
                origX = PerspFrontOrigX; origY = PerspFrontOrigY; width = PerspFrontWidth; height = PerspFrontHeight;
                break;
            case "persp_rear":
                origX = PerspRearOrigX; origY = PerspRearOrigY; width = PerspRearWidth; height = PerspRearHeight;
                break;
            default:
                return null;
        }

        // Check if universal point falls within target sub-view box
        if (uniPixelX >= origX && uniPixelX <= (origX + width) &&
            uniPixelY >= origY && uniPixelY <= (origY + height))
        {
            double subX = ((uniPixelX - origX) / width) * 100.0;
            double subY = ((uniPixelY - origY) / height) * 100.0;
            return (Math.Clamp(subX, 0.5, 99.5), Math.Clamp(subY, 0.5, 99.5));
        }

        return null;
    }

    public static string DetectAngleSection(double universalX, double universalY)
    {
        double px = universalX * AllWidth / 100.0;
        double py = universalY * AllHeight / 100.0;

        if (py < 330.0)
        {
            if (px < 417.0) return "front";
            if (px < 1188.0) return "side";
            return "rear";
        }
        else
        {
            if (px < 807.0) return "persp_front";
            return "persp_rear";
        }
    }

    public static string GetViewArabicName(string? view) => (view ?? "all").ToLower().Trim() switch
    {
        "front" => "أمامي",
        "side" => "جانبي",
        "rear" => "خلفي",
        "persp_front" => "أمامي مجسم",
        "persp_rear" => "خلفي مجسم",
        _ => "المخطط الشامل"
    };
}
