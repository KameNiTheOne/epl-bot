using Newtonsoft.Json;
using System.Text;
using System.Text.Json;

namespace TelegramBotik.instruments
{
    public static class HttpRetriever
    {
        private class Documents
        {
            [JsonProperty("texts")]
            public List<string> Texts { get; set; }
            [JsonProperty("urls")]
            public List<string> Urls { get; set; }
            public Documents()
            {
                Texts = new List<string>();
                Urls = new List<string>();
            }
            public List<RetrieverDocument> GetDocuments()
            {
                using var texts = Texts.GetEnumerator();
                using var urls = Urls.GetEnumerator();
                List<RetrieverDocument> result = new();
                while (texts.MoveNext() && urls.MoveNext())
                {
                    result.Add(new RetrieverDocument(texts.Current, urls.Current));
                }
                return result;
            }
        }
        public class Document
        {
            string _value = string.Empty;
            public string Value
            {
                get { return _value; }
                set
                {
                    _value = Encoding.UTF8.GetString(Encoding.Default.GetBytes(value));
                }
            }
            public Document(string _value)
            {
                Value = _value;
            }
        }
        public class RetrieverDocument : Document
        {
            public string Url { get; set; } = string.Empty;
            public RetrieverDocument(string _value, string _url) : base(_value)
            {
                Url = _url;
            }
        }
        private class Text
        {
            public string? Value { get; set; }
        }
        private static HttpClient? client;
        private static string? http;
        public static void Initialize(string _http)
        {
            client = new HttpClient();
            http = _http;
        }
        public static async Task<string> Post<T>(string _http, string _case, T obj)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(obj));
            using var response = await client.PostAsync($"{_http}{_case}", content);
            return await response.Content.ReadAsStringAsync();
        }
        public static async Task<List<RetrieverDocument>> PostQuery(string text)
        {
            string response = await Post(http, @"/query", new Text { Value = text });
            Documents convert = JsonConvert.DeserializeObject<Documents>(response);
            return convert.GetDocuments();
        }
    }
}