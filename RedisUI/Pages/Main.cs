using System.Text.Encodings.Web;
using System.Text.Json;
using RedisUI.Models;

namespace RedisUI.Pages;

public static class Main
{
    public static string Build(List<KeyModel> keys, long next, int nextPageOffset, RedisUISettings settings)
    {
        var encoder = HtmlEncoder.Default;
        var rows = string.Join("", keys.Select((key, index) =>
            $"<tr class=\"redis-row\" data-index=\"{index}\"><td>{encoder.Encode(key.KeyType)}</td><td><button class=\"key-select\" data-index=\"{index}\">{encoder.Encode(key.Name)}</button></td><td>{key.ValueSizeBytes}</td><td>{(key.TTLSeconds.HasValue ? key.TTLSeconds + "s" : "∞")}</td><td><button class=\"delete-key danger\" data-index=\"{index}\">Delete</button></td></tr>"));
        var data = JsonSerializer.Serialize(new { keys = keys.Select(key => new { key.Name, key.KeyType, key.ValueSizeBytes, key.TTLSeconds }),
            nextCursor = next.ToString(System.Globalization.CultureInfo.InvariantCulture), nextPageOffset }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Resources.Read("main.html").Replace("<!--ROWS-->", rows) + InsertModal.Build() +
            $"<script id=\"key-data\" type=\"application/json\">{data}</script>";
    }
}
