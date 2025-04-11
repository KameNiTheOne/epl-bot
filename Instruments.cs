using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using Newtonsoft.Json;

namespace TelegramBotik.instruments
{
    public static class Instruments
    {
        public static HttpClient httpClient { private get; set; }     
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString().StartsWith("192"))
                        continue;
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        public static string LoremIpsum(int times)
        {
            return string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit.", times));
        }
        public static int CountWords(string text)
        {
            var regex = new Regex(string.Format(@"\b?\b"),
                              RegexOptions.IgnoreCase);
            return regex.Matches(text).Count;
        }
        public static async Task<string> Post<T>(string _http, string _case, T obj)
        {
            using var response = await httpClient.PostAsJsonAsync($"{_http}{_case}", obj).ConfigureAwait(continueOnCapturedContext: false);
            return await response.Content.ReadAsStringAsync();
        }
        public static async Task<G> PostRequestObject<G, T>(string _http, string _case, T obj)
        {
            using var response = await httpClient.PostAsJsonAsync($"{_http}{_case}", obj).ConfigureAwait(continueOnCapturedContext: false);
            var res = JsonConvert.DeserializeObject<G>(await response.Content.ReadAsStringAsync());
            return res;
        }
        public static async Task<string> Get(string _http, string _case)
        {
            using var response = await httpClient.GetAsync($"{_http}{_case}").ConfigureAwait(continueOnCapturedContext: false);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
