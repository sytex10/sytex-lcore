using System.Text.Json.Serialization;

namespace SytexLCore.Models;

public class GameProfile
{
    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("sourceLanguage")]
    public string SourceLanguage { get; set; } = "auto";

    [JsonPropertyName("targetLanguage")]
    public string TargetLanguage { get; set; } = "tr";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "GoogleGTX";

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("aiModel")]
    public string AiModel { get; set; } = "gemini-1.5-flash";

    [JsonPropertyName("ocrRegion")]
    public OcrRegion? OcrRegion { get; set; } = null; // null = tüm ekran

    [JsonPropertyName("exclusionZones")]
    public List<OcrRegion> ExclusionZones { get; set; } = new();

    [JsonPropertyName("fontSizeOverride")]
    public double FontSizeOverride { get; set; } = 15; // 0 veya varsayılan = 15

    [JsonPropertyName("backgroundOpacity")]
    public double BackgroundOpacity { get; set; } = 0.85;

    // ── PREMIUM NEON & CYBERPUNK AYARLARI ───────────────────────────────────
    [JsonPropertyName("bubbleBgColor")]
    public string BubbleBgColor { get; set; } = "#0A0D17"; // Obsidyen Gece

    [JsonPropertyName("bubbleTextColor")]
    public string BubbleTextColor { get; set; } = "#FFFFFF"; // Beyaz

    [JsonPropertyName("bubbleBorderColor")]
    public string BubbleBorderColor { get; set; } = "#00F5FF"; // Neon Siyan

    [JsonPropertyName("manuelHotkey")]
    public uint ManuelHotkey { get; set; } = 0x78; // F9

    [JsonPropertyName("autoHotkey")]
    public uint AutoHotkey { get; set; } = 0x79; // F10

    [JsonPropertyName("settingsHotkey")]
    public uint SettingsHotkey { get; set; } = 0x7A; // F11

    [JsonPropertyName("selectedOcrEngine")]
    public string SelectedOcrEngine { get; set; } = "WindowsOCR"; // WindowsOCR veya PaddleOCR

    [JsonPropertyName("minimizeOnClose")]
    public bool MinimizeOnClose { get; set; } = false; // Varsayılan kapat tuşunda kapansın (kullanıcı isterse tray'e küçültür)

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OcrRegion
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }

    public System.Windows.Rect ToRect() => new(X, Y, Width, Height);
}
