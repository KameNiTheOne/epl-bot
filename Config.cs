using Newtonsoft.Json;
using System.Text;

namespace TelegramBotik.instruments
{
    public class GPTPrompts
    {
        [JsonProperty("user")]
        public string? User { get; set; }
        [JsonProperty("assistant")]
        public string? Assistant { get; set; }
        [JsonProperty("system")]
        public string? System { get; set; }
        public override string ToString()
        {
            return $"{User}, {Assistant}, {System}";
        }
    }
    public class Config
    {
        [JsonProperty("prompts")]
        public Dictionary<string, GPTPrompts> Prompts;
        [JsonProperty("hostip")]
        public string? HostIP;
        [JsonProperty("gpthosts")]
        public Dictionary<string, string> GPTHosts;
        [JsonProperty("bottokens")]
        public Dictionary<string, string> BotTokens;
        [JsonProperty("configchatid")]
        public string? ConfigChatID;
        public bool Equals(Config other)
        {
            return Prompts.SequenceEqual(other.Prompts)&&HostIP==other.HostIP&&GPTHosts.SequenceEqual(other.GPTHosts);
        }
    }
    public static class Configuration
    {
        static string pathToConfig;
        public static Config MainConfig;
        static Config downloadedConfig;
        public static async Task Load()
        {
            SetPathToConfig();
            if (MainConfig == null)
            {
                try
                {
                    using (var stream = File.Open(pathToConfig, FileMode.Open))
                    {
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            MainConfig = JsonConvert.DeserializeObject<Config>(reader.ReadToEnd());
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"Config file not found at {pathToConfig}! Please download latest config from group chat.");
                    Environment.Exit(2);
                }
                ConfigRetriever.Initialize();
            }
            downloadedConfig = await ConfigRetriever.DownloadConfig();
            MainConfig = downloadedConfig;
        }
        static void SetPathToConfig()
        {
            string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(baseFolder, @"BitaSatoshi(C#)\");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            pathToConfig = folderPath + "config.json";
        }
        static void SaveConfig()
        {
            using (var stream = File.Open(pathToConfig, FileMode.Create))
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    string convert = JsonConvert.SerializeObject(MainConfig);
                    writer.WriteLine(convert);
                }
            }
        }
        public static void IsCurrentConfigDifferent()
        {
            if (!MainConfig.Equals(downloadedConfig))
            {
                Console.WriteLine($"Current config does not match main config! Saving changes to {pathToConfig}. Consider pinning new config in group chat.");
                SaveConfig();
                return;
            }
            Console.WriteLine("Config of current application matches main config.");
        }
    }
}
