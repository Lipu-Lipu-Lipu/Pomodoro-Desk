using System.Text.Json;

namespace PomodoroTimer;

internal sealed record AppSettings(int WorkMinutes, int WorkSeconds, int RestMinutes, int RestSeconds)
{
    public static AppSettings Default { get; } = new(25, 0, 5, 0);
}

/// <summary>设置持久化：读写 %APPDATA%\PomodoroTimer\settings.json，下次启动自动恢复。</summary>
internal static class SettingsStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PomodoroTimer",
        "settings.json");

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static AppSettings Load()
    {
        try
        {
            return Load(FilePath);
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public static AppSettings Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return AppSettings.Default;
        }
        AppSettings? parsed = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), Options);
        return parsed is null ? AppSettings.Default : Normalize(parsed);
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Save(FilePath, settings);
        }
        catch
        {
            // 保存失败不影响本次使用
        }
    }

    public static void Save(string filePath, AppSettings settings)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, JsonSerializer.Serialize(Normalize(settings), Options));
    }

    /// <summary>按控件输入范围钳制，防止手改配置文件产生非法值。</summary>
    internal static AppSettings Normalize(AppSettings s)
    {
        return new AppSettings(
            Math.Clamp(s.WorkMinutes, 0, 120),
            Math.Clamp(s.WorkSeconds, 0, 59),
            Math.Clamp(s.RestMinutes, 0, 60),
            Math.Clamp(s.RestSeconds, 0, 59));
    }
}
