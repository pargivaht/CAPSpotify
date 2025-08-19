using Newtonsoft.Json;
using System;
using System.IO;
using System.Text.Json;

public class Config
{
    public bool EnableAPI { get; set; } = true;
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "http://localhost:5555/callback";
}

public class ConfigManager
{
    private static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     ".pargivaht", "CAPSpotify", "config.json");

    public static Config Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            var defaultConfig = new Config();
            Save(defaultConfig);
            return defaultConfig;
        }

        string json = File.ReadAllText(ConfigPath);
        return System.Text.Json.JsonSerializer.Deserialize<Config>(json) ?? new Config();
    }

    public static void Save(Config config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        string json = System.Text.Json.JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
