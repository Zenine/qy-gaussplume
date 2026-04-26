using System.Net.Http.Json;
using System.Text.Json;

namespace GnnSimulation.Tests.Infrastructure;

internal static class HttpClientExtensions
{
    // ASP.NET Core 默认使用 camelCase JSON，测试读取时要匹配
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response, CancellationToken ct = default)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T payload)
        => client.PostAsJsonAsync(url, payload, JsonOptions);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T payload)
        => client.PutAsJsonAsync(url, payload, JsonOptions);
}
