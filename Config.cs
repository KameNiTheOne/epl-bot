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
    public static class Configuration
    {
        public static Dictionary<string, GPTPrompts> Prompts;
        public static string HostIP;
        public static Dictionary<string, string> GPTHosts;
        public static void Initialize()
        {
            if (File.Exists(Program.pathToConfig))
            {
                using (var stream = File.Open(Program.pathToConfig, FileMode.Open))
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        Config convert = JsonConvert.DeserializeObject<Config>(reader.ReadToEnd());
                        Prompts = convert.Prompts;
                        HostIP = convert.HostIP;
                        GPTHosts = convert.GPTHosts;
                    }
                }
            }
            else
            {
                throw new Exception("Pull config from github first!");
            }
        }
        public static void SaveConfigfile()
        {
            using (var stream = File.Open(Program.pathToConfig, FileMode.Truncate))
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    string convert = JsonConvert.SerializeObject(toConfigClass());
                    writer.WriteLine(convert);
                }
            }
        }
        static Config toConfigClass()
        {
            return new Config() { HostIP = HostIP, GPTHosts = GPTHosts, Prompts = Prompts };
        }
        private class Config
        {
            [JsonProperty("prompts")]
            public Dictionary<string, GPTPrompts> Prompts;
            [JsonProperty("hostip")]
            public string? HostIP;
            [JsonProperty("gpthosts")]
            public Dictionary<string, string> GPTHosts;
        }
    }
}
