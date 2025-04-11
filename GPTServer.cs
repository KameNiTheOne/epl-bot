using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using TelegramBotik.instruments;
using Newtonsoft.Json;

namespace TelegramBotik
{
    public static class GPTServer
    {
        public const int nodePort = 9111;
        public static void Initialize(bool isGPTSummarizer, CancellationToken ct)
        {
            var builder = WebApplication.CreateBuilder();
            string localip = Instruments.GetLocalIPAddress();
            builder.WebHost.UseUrls(@$"http://{localip}:{nodePort}");
            WebApplication app = builder.Build();
            string info;

            if (isGPTSummarizer)
            {
                info = "суммаризации";
                app.MapPost("/summarize", async (HttpRequest request) =>
                {
                    HttpRetriever.Document doc = await request.ReadFromJsonAsync<HttpRetriever.Document>();
                    return await TheGPT.GPTTaskGetter(TheGPT.TaskType.Summarize, doc.Value);
                });
            }
            else
            {
                info = "обработки сообщений";
                app.MapPost("/handleMessage", async (HttpRequest request) =>
                {
                    JSONMessage msg = await request.ReadFromJsonAsync<JSONMessage>();
                    PriorityIp pIp = await Program.HandleMessage(msg);
                    return JsonConvert.SerializeObject(pIp);
                });
            }

            Console.WriteLine($"Запущена ячейка кластера {info}.\nip сервера:{localip}");
            app.StartAsync(ct);
        }
    }
}
