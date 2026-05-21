namespace SytexLCore.Models;

public class TranslationRecord
{
    public string Original { get; set; } = "";
    public string Translated { get; set; } = "";
    public string GameName { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string DisplayTime => Timestamp.ToString("HH:mm:ss");
}
