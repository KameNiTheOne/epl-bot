using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net.Sockets;
using System.Net;
using Microsoft.AspNetCore.Http;
using TelegramBotik.instruments;

namespace TelegramBotik
{
    public static class GPTServer
    {
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        public static void Initialize()
        {
            var builder = WebApplication.CreateBuilder();
            string localip = GetLocalIPAddress();
            builder.WebHost.UseUrls(@$"http://{localip}:9111");
            WebApplication app = builder.Build();

            app.MapPost("/summarize", async (HttpRequest request) =>
            {
                HttpRetriever.Document doc = await request.ReadFromJsonAsync<HttpRetriever.Document>();

                return await TheGPT.GPTTaskGetter(TheGPT.TaskType.Summarize, doc.Value);
            });
            Console.WriteLine($"Сервер для суммаризации запущен! ip сервера:{localip}");
            app.Run();
        }
    }
}
