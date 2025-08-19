using CapsLockRemap;
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

        Console.WriteLine(ConfigPath);


        if (!File.Exists(ConfigPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            var defaultConfig = new Config();
            Save(defaultConfig);
            return defaultConfig;
        }

        string json = File.ReadAllText(ConfigPath);

        Console.WriteLine(json);


        try
        {
            return JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        catch (Exception ex)
        {
            // log the error (to file, since console might be invisible)
            string logPath = Path.Combine(Path.GetDirectoryName(ConfigPath), "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Failed to load config: {ex}{Environment.NewLine}");
            Console.WriteLine(ex.Message);
            return new Config();      
        }
        
    }

    public static void Save(Config config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        string json = System.Text.Json.JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
