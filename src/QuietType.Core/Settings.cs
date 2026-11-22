using System.Text.Json;
namespace QuietType.Core;

public sealed record Settings
{
    public bool Enabled { get; init; } = true;
    public bool UseAi { get; init; } = true;
    public bool CorrectRealWords { get; init; } = false;
    public bool StartWithWindows { get; init; } = true;
    public int IdleDelayMs { get; init; } = 60;
    public string[] ExcludedApps { get; init; } = ["1password", "bitwarden", "keepass", "keepassxc", "credentialuibroker", "consent", "mstsc", "windowsterminal", "cmd", "powershell", "pwsh", "conhost", "code", "devenv", "rider64", "idea64", "pycharm64", "steam", "parsecd", "anydesk"];
    public string[] PersonalWords { get; init; } = [];
    public Settings Normalize() => this with
    {
        IdleDelayMs = Math.Clamp(IdleDelayMs, 50, 1500),
        ExcludedApps = Clean(ExcludedApps, true),
        PersonalWords = Clean(PersonalWords, false)
    };
    private static string[] Clean(string[]? values, bool apps) => (values ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant())
        .Select(x => apps && x.EndsWith(".exe", StringComparison.Ordinal) ? x[..^4] : x)
        .Where(x => x.Length <= 80).Distinct(StringComparer.OrdinalIgnoreCase).Take(5000).ToArray();
    public static Settings Load(string path)
    {
        try { return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new()).Normalize(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Normalize(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
