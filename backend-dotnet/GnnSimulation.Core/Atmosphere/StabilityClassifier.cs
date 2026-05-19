namespace GnnSimulation.Core.Atmosphere;

// Pasquill 稳定度分类器：根据风速 + 辐照/云量 + 昼夜确定 A-F 等级
public static class StabilityClassifier
{
    public static string Classify(
        double windSpeed,
        double? solarRadiation = null,
        double? cloudCover = null,
        bool isDaytime = true)
    {
        if (solarRadiation.HasValue)
        {
            var insolation = solarRadiation.Value > 700 ? Insolation.Strong
                : solarRadiation.Value > 350 ? Insolation.Moderate
                : Insolation.Slight;

            return ClassifyByInsolation(windSpeed, insolation);
        }

        if (cloudCover.HasValue)
        {
            if (!isDaytime)
            {
                if (cloudCover.Value < 0.3) return "F";
                if (cloudCover.Value < 0.7) return "E";
                return "D";
            }

            var insolation = cloudCover.Value < 0.3 ? Insolation.Strong
                : cloudCover.Value < 0.7 ? Insolation.Moderate
                : Insolation.Slight;
            return ClassifyByInsolation(windSpeed, insolation);
        }

        return "D";
    }

    private enum Insolation { Strong, Moderate, Slight }

    private static string ClassifyByInsolation(double windSpeed, Insolation insolation)
    {
        if (windSpeed < 2)
            return insolation switch { Insolation.Strong => "A", Insolation.Moderate => "A-B", _ => "B" };
        if (windSpeed < 3)
            return insolation switch { Insolation.Strong => "A-B", Insolation.Moderate => "B", _ => "C" };
        if (windSpeed < 5)
            return insolation switch { Insolation.Strong => "B", Insolation.Moderate => "B-C", _ => "C" };
        if (windSpeed < 6)
            return insolation == Insolation.Strong ? "C" : "C-D";
        return "D";
    }
}
