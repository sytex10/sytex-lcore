using System.IO;
using System.Text.Json;
using SytexLCore.Models;

namespace SytexLCore.Services;

public sealed class ProfileService
{
    private static readonly string _profileDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SYTEX-LCore", "profiles");

    static ProfileService() => Directory.CreateDirectory(_profileDir);

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public async Task<GameProfile> LoadAsync(string processName)
    {
        var path = GetPath(processName);
        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<GameProfile>(json) ?? DefaultProfile(processName);
            }
            catch { }
        }
        return DefaultProfile(processName);
    }

    public async Task SaveAsync(GameProfile profile)
    {
        var path = GetPath(profile.ProcessName);
        var json = JsonSerializer.Serialize(profile, _opts);
        await File.WriteAllTextAsync(path, json);
    }

    public List<GameProfile> LoadAll()
    {
        var list = new List<GameProfile>();
        foreach (var f in Directory.GetFiles(_profileDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(f);
                var p = JsonSerializer.Deserialize<GameProfile>(json);
                if (p != null) list.Add(p);
            }
            catch { }
        }
        return list;
    }

    private static string GetPath(string name) =>
        Path.Combine(_profileDir, $"{SanitizeName(name)}.json");

    private static string SanitizeName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars()));

    private static GameProfile DefaultProfile(string processName) => new()
    {
        ProcessName = processName,
        DisplayName = processName,
        SourceLanguage = "auto",
        TargetLanguage = "tr"
    };
}
