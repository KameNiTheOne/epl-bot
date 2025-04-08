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
        public bool EqualsTo(GPTPrompts prompts)
        {
            return User == prompts.User && Assistant == prompts.Assistant && System == prompts.System;
        }
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
        [JsonProperty("mainip")]
        public string? MainIP;
        [JsonProperty("gpthosts")]
        public Dictionary<string, string> GPTHosts;
        [JsonProperty("bottokens")]
        public Dictionary<string, string> BotTokens;
        [JsonProperty("configchatid")]
        public string? ConfigChatID;
        public static Config DeepCopy(Config input)
        {
            string serialized = JsonConvert.SerializeObject(input);
            return JsonConvert.DeserializeObject<Config>(serialized);
        }
        public bool Equals(Config other)
        {
            bool promptsComparison = true;
            foreach (var key in Prompts.Keys)
            {
                if (!Prompts[key].EqualsTo(other.Prompts[key]))
                {
                    Console.WriteLine($"{Prompts[key]}\n{other.Prompts[key]}");
                    promptsComparison = false;
                }
            }
            Console.WriteLine($"Are Prompts equal? {promptsComparison}");
            Console.WriteLine($"Is RAGHost equal? {HostIP == other.HostIP}");
            Console.WriteLine($"Are GPTHosts equal? {GPTHosts.SequenceEqual(other.GPTHosts)}");
            Console.WriteLine($"Is MainHost equal? {MainIP == other.MainIP}");
            return promptsComparison && HostIP==other.HostIP&&GPTHosts.SequenceEqual(other.GPTHosts)&&MainIP==other.MainIP;
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
                if (!Program.useLocalConfig) ConfigRetriever.Initialize();
            }
            downloadedConfig = !Program.useLocalConfig ? await ConfigRetriever.DownloadConfig() : MainConfig;
            MainConfig = Config.DeepCopy(downloadedConfig);
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
        public static async void IsCurrentConfigDifferent()
        {
            Console.WriteLine("Comparing current config to config on Telegram...");
            if (!MainConfig.Equals(downloadedConfig))
            {
                Console.WriteLine($"Current config does not match main config! Saving changes to {pathToConfig}. Sending config to chat.");
                SaveConfig();
                if (!Program.useLocalConfig) await ConfigRetriever.SendConfig(pathToConfig);
                return;
            }
            Console.WriteLine("Config of current application matches main config.");
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
    }
}
