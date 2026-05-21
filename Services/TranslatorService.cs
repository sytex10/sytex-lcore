using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace SytexLCore.Services;

public class TranslatorService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    /// <summary>
    /// Metni otomatik algılanan dilden Türkçeye çevirir.
    /// Ücretsiz Google Translate endpoint kullanır — API key gerekmez.
    /// </summary>
    public static async Task<string> TranslateToTurkishAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var encoded = Uri.EscapeDataString(text.Trim());
        var url = $"https://translate.googleapis.com/translate_a/single" +
                  $"?client=gtx&sl=auto&tl=tr&dt=t&q={encoded}";

        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        // Yanıt: [[["çeviri","orijinal",null,null,1],...],...]
        var sb = new System.Text.StringBuilder();
        foreach (var chunk in doc.RootElement[0].EnumerateArray())
        {
            var part = chunk[0].GetString();
            if (!string.IsNullOrEmpty(part)) sb.Append(part);
        }
        return sb.ToString();
    }
}
