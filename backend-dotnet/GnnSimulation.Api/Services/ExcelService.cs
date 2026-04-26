using ClosedXML.Excel;
using GnnSimulation.Data.Entities;

namespace GnnSimulation.Api.Services;

public record ExcelImportResult(int ImportedCount, IReadOnlyList<string> Errors);

// 基于 ClosedXML 的 Excel IO。所有表头用中文，保持和 Python 原版一致。
public static class ExcelService
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#007AFF");
    private static readonly XLColor PollutantFill = XLColor.FromHtml("#34C759");
    private static readonly string[] PollutantTypes = new[] { "PM2.5", "PM10", "TSP", "VOCs", "NOx", "O3" };

    // ========== Receptors ==========

    public static readonly string[] ReceptorHeaders =
        { "名称", "纬度", "经度", "高度", "标记符号", "标记颜色" };

    public static byte[] BuildReceptorTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("受体点");

        WriteHeaderRow(ws, ReceptorHeaders, HeaderFill);
        WriteRow(ws, 2, new object?[] { "示例受体点", 39.9, 116.4, 0, "monitor", "#2196F3" });
        AdjustColumns(ws, ReceptorHeaders.Length);

        return ToBytes(wb);
    }

    public static byte[] ExportReceptors(IReadOnlyList<Receptor> receptors)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("受体点");

        WriteHeaderRow(ws, ReceptorHeaders, HeaderFill);
        for (var i = 0; i < receptors.Count; i++)
        {
            var r = receptors[i];
            WriteRow(ws, i + 2, new object?[] { r.Name, r.Latitude, r.Longitude, r.Height, r.MarkerSymbol, r.MarkerColor });
        }
        AdjustColumns(ws, ReceptorHeaders.Length);

        return ToBytes(wb);
    }

    public static (List<Receptor> Items, List<string> Errors) ParseReceptors(Stream stream)
    {
        var items = new List<Receptor>();
        var errors = new List<string>();

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (var row = 2; row <= lastRow; row++)
        {
            var name = ws.Cell(row, 1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            try
            {
                items.Add(new Receptor
                {
                    Name = name,
                    Latitude = ws.Cell(row, 2).GetDouble(),
                    Longitude = ws.Cell(row, 3).GetDouble(),
                    Height = TryDouble(ws.Cell(row, 4)) ?? 0,
                    MarkerSymbol = OrDefault(ws.Cell(row, 5).GetString(), "monitor"),
                    MarkerColor = OrDefault(ws.Cell(row, 6).GetString(), "#2196F3"),
                    IsActive = true,
                });
            }
            catch (Exception ex)
            {
                errors.Add($"第{row}行: {ex.Message}");
            }
        }
        return (items, errors);
    }

    // ========== Emission Sources ==========

    private static readonly Dictionary<string, string[]> SourceTypeHeaders = new()
    {
        ["point"] = new[] { "名称", "纬度", "经度", "高度", "烟气温度(K)", "烟气速度", "烟囱直径" },
        ["area"] = new[] { "名称", "纬度", "经度", "面源长度", "面源宽度", "面源高度", "烟气温度(K)" },
        ["equivalent_area"] = new[] { "名称", "纬度", "经度", "面源长度", "面源宽度", "面源高度", "烟气温度(K)" },
        ["line"] = new[] { "名称", "起点纬度", "起点经度", "终点纬度", "终点经度", "线源宽度", "线源高度", "烟气温度(K)" },
    };

    private static readonly Dictionary<string, object?[]> SourceExampleRows = new()
    {
        ["point"] = new object?[] { "示例点源", 39.9, 116.4, 50, 400, 15, 2 },
        ["area"] = new object?[] { "示例面源", 39.9, 116.4, 100, 100, 10, 300 },
        ["equivalent_area"] = new object?[] { "示例等效面源", 39.9, 116.4, 100, 100, 10, 300 },
        ["line"] = new object?[] { "示例线源", 39.9, 116.4, 39.91, 116.41, 10, 5, 300 },
    };

    public static bool IsValidSourceType(string type) => SourceTypeHeaders.ContainsKey(type);

    public static byte[] BuildSourceTemplate(string sourceType)
    {
        if (!SourceTypeHeaders.TryGetValue(sourceType, out var base_))
            throw new ArgumentException($"无效的排放源类型: {sourceType}");

        var headers = base_.Concat(PollutantTypes).Concat(new[] { "标记符号", "标记颜色" }).ToArray();
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sourceType);

        // 表头
        for (var col = 1; col <= headers.Length; col++)
        {
            var cell = ws.Cell(1, col);
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Fill.BackgroundColor = PollutantTypes.Contains(headers[col - 1]) ? PollutantFill : HeaderFill;
        }

        // 示例数据
        var baseVals = SourceExampleRows[sourceType];
        var polluantExample = Enumerable.Repeat<object?>("", PollutantTypes.Length).ToArray();
        polluantExample[0] = sourceType == "equivalent_area" ? 100 : 10.5;
        var row = baseVals.Concat(polluantExample).Concat(new object?[] { "factory", "#FF5722" }).ToArray();
        WriteRow(ws, 2, row);

        AdjustColumns(ws, headers.Length);
        return ToBytes(wb);
    }

    public static (List<EmissionSource> Items, List<string> Errors) ParseSources(Stream stream, string sourceType)
    {
        if (!SourceTypeHeaders.ContainsKey(sourceType))
            throw new ArgumentException($"无效的排放源类型: {sourceType}");

        var items = new List<EmissionSource>();
        var errors = new List<string>();

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        var headerRow = ws.Row(1);
        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        // 找污染物列
        var pollutantCols = new Dictionary<string, int>();
        for (var col = 1; col <= lastCol; col++)
        {
            var name = ws.Cell(1, col).GetString().Trim();
            if (PollutantTypes.Contains(name))
                pollutantCols[name] = col;
        }

        var markerSymbolCol = lastCol - 1;
        var markerColorCol = lastCol;

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (var row = 2; row <= lastRow; row++)
        {
            var name = ws.Cell(row, 1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            try
            {
                var source = BuildSourceFromRow(ws, row, sourceType, markerSymbolCol, markerColorCol);

                // 污染物：正值才记录；等效面源当浓度使用，其他当排放速率
                foreach (var kv in pollutantCols)
                {
                    var value = TryDouble(ws.Cell(row, kv.Value));
                    if (value is not > 0) continue;

                    source.Pollutants.Add(new PollutantEmission
                    {
                        PollutantType = kv.Key,
                        EmissionRate = sourceType == "equivalent_area" ? 0 : value.Value,
                        Concentration = sourceType == "equivalent_area" ? value : null,
                    });
                }

                items.Add(source);
            }
            catch (Exception ex)
            {
                errors.Add($"第{row}行: {ex.Message}");
            }
        }
        return (items, errors);
    }

    private static EmissionSource BuildSourceFromRow(
        IXLWorksheet ws, int row, string sourceType, int markerSymbolCol, int markerColorCol)
    {
        var markerSymbol = OrDefault(ws.Cell(row, markerSymbolCol).GetString(), "factory");
        var markerColor = OrDefault(ws.Cell(row, markerColorCol).GetString(), "#FF5722");

        return sourceType switch
        {
            "point" => new EmissionSource
            {
                Name = ws.Cell(row, 1).GetString().Trim(),
                SourceType = "point",
                Latitude = TryDouble(ws.Cell(row, 2)) ?? 0,
                Longitude = TryDouble(ws.Cell(row, 3)) ?? 0,
                Height = TryDouble(ws.Cell(row, 4)) ?? 0,
                Temperature = TryDouble(ws.Cell(row, 5)) ?? 400,
                Velocity = TryDouble(ws.Cell(row, 6)) ?? 15,
                Diameter = TryDouble(ws.Cell(row, 7)) ?? 2,
                MarkerSymbol = markerSymbol,
                MarkerColor = markerColor,
                IsActive = true,
            },
            "area" or "equivalent_area" => new EmissionSource
            {
                Name = ws.Cell(row, 1).GetString().Trim(),
                SourceType = sourceType,
                Latitude = TryDouble(ws.Cell(row, 2)) ?? 0,
                Longitude = TryDouble(ws.Cell(row, 3)) ?? 0,
                AreaLength = TryDouble(ws.Cell(row, 4)) ?? 100,
                AreaWidth = TryDouble(ws.Cell(row, 5)) ?? 100,
                AreaHeight = TryDouble(ws.Cell(row, 6)) ?? 0,
                AreaTemperature = TryDouble(ws.Cell(row, 7)) ?? 300,
                MarkerSymbol = markerSymbol,
                MarkerColor = markerColor,
                IsActive = true,
            },
            "line" => new EmissionSource
            {
                Name = ws.Cell(row, 1).GetString().Trim(),
                SourceType = "line",
                Latitude = TryDouble(ws.Cell(row, 2)) ?? 0,
                Longitude = TryDouble(ws.Cell(row, 3)) ?? 0,
                StartLat = TryDouble(ws.Cell(row, 2)) ?? 0,
                StartLon = TryDouble(ws.Cell(row, 3)) ?? 0,
                EndLat = TryDouble(ws.Cell(row, 4)) ?? 0,
                EndLon = TryDouble(ws.Cell(row, 5)) ?? 0,
                LineWidth = TryDouble(ws.Cell(row, 6)) ?? 10,
                LineHeight = TryDouble(ws.Cell(row, 7)) ?? 0,
                LineTemperature = TryDouble(ws.Cell(row, 8)) ?? 300,
                MarkerSymbol = markerSymbol,
                MarkerColor = markerColor,
                IsActive = true,
            },
            _ => throw new ArgumentException($"无效的排放源类型: {sourceType}"),
        };
    }

    // ========== 共享辅助 ==========

    private static void WriteHeaderRow(IXLWorksheet ws, string[] headers, XLColor fill)
    {
        for (var col = 1; col <= headers.Length; col++)
        {
            var cell = ws.Cell(1, col);
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Fill.BackgroundColor = fill;
        }
    }

    private static void WriteRow(IXLWorksheet ws, int row, object?[] values)
    {
        for (var col = 1; col <= values.Length; col++)
        {
            var cell = ws.Cell(row, col);
            cell.Value = values[col - 1] switch
            {
                null => XLCellValue.FromObject(string.Empty),
                string s => s,
                double d => d,
                int i => i,
                bool b => b,
                _ => XLCellValue.FromObject(values[col - 1]),
            };
        }
    }

    private static void AdjustColumns(IXLWorksheet ws, int columnCount)
    {
        for (var col = 1; col <= columnCount; col++)
            ws.Column(col).Width = 15;
    }

    private static byte[] ToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static double? TryDouble(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        return cell.TryGetValue<double>(out var v) ? v : null;
    }

    private static string OrDefault(string? value, string defaultValue) =>
        string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
}
