namespace GnnSimulation.Data.Entities;

public record PollutantInfo(string Name, string Unit, string Description);

public record MarkerSymbolInfo(string Name, string Icon);

public static class PollutantCatalog
{
    public static readonly IReadOnlyDictionary<string, PollutantInfo> Pollutants =
        new Dictionary<string, PollutantInfo>
        {
            ["PM2.5"] = new("PM2.5", "g/s", "细颗粒物"),
            ["PM10"] = new("PM10", "g/s", "可吸入颗粒物"),
            ["TSP"] = new("TSP", "g/s", "总悬浮颗粒物"),
            ["VOCs"] = new("VOCs", "g/s", "挥发性有机物"),
            ["NOx"] = new("NOx", "g/s", "氮氧化物"),
            ["O3"] = new("O3", "g/s", "臭氧"),
        };

    public static readonly IReadOnlyDictionary<string, MarkerSymbolInfo> MarkerSymbols =
        new Dictionary<string, MarkerSymbolInfo>
        {
            ["factory"] = new("工厂", "🏭"),
            ["industry"] = new("工业", "⚙️"),
            ["power"] = new("电厂", "⚡"),
            ["chemical"] = new("化工厂", "🧪"),
            ["circle"] = new("圆形", "●"),
            ["square"] = new("方形", "■"),
            ["triangle"] = new("三角形", "▲"),
            ["diamond"] = new("菱形", "◆"),
            ["star"] = new("星形", "★"),
            ["hexagon"] = new("六边形", "⬡"),
            ["pentagon"] = new("五边形", "⬠"),
            ["cross"] = new("十字", "✚"),
        };
}
