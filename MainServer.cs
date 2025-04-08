using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO.Pipelines;
using TelegramBotik.instruments;

namespace TelegramBotik
{
    public enum Priority
    {
        High,
        Normal
    }
    public class PriorityIp
    {
        public Priority Priority { get; set; }
        public string Ip { get; set; }
    }
    public static class MainServer
    {
        static Queue<JSONMessage> messages = new();
        static List<Task<PriorityIp>> currentlyHandling = new();
        static PriorityQueue<PriorityIp, int> ips = new();
        static int mainPort = 1337;
        public static void Initialize(CancellationToken ct)
        {
            var builder = WebApplication.CreateBuilder();
            string localip = Instruments.GetLocalIPAddress();
            builder.WebHost.UseUrls(@$"http://{localip}:{mainPort}");
            WebApplication app = builder.Build();

            app.MapPost("/register", async (HttpRequest request) =>
            {
                PriorityIp pIp = await request.ReadFromJsonAsync<PriorityIp>();
                ips.Enqueue(pIp, (int)pIp.Priority);
                return "1";
            });
            app.MapGet("/ping", () => { return "1"; } );
            Console.WriteLine($"Данная сессия выбрана Главной.\nip сервера:{localip}");

            AsyncHandleMessages(ct);
            app.RunAsync();
        }
        public static void DistributeMessage(JSONMessage msg)
        {
            Console.WriteLine(msg.Text);
            messages.Enqueue(msg);
            Console.WriteLine(messages.Count);
        }
        public static async Task Register(PriorityIp pIp)
        {
            await Instruments.Post($@"http://{Configuration.MainConfig.MainIP}:{mainPort}", "/register", pIp);
        }
        static async Task AsyncHandleMessages(CancellationToken ct)
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                if (ips.Count != 0 && messages.TryDequeue(out JSONMessage msg))
                {
                    PriorityIp pIp = ips.Dequeue();
                    currentlyHandling.Add(Instruments.PostRequestObject<PriorityIp, JSONMessage>($@"http://{pIp.Ip}:9111", "/handleMessage", msg));
                }
                if (currentlyHandling.Count != 0)
                {
                    Console.WriteLine("AAAAAAAAAAAAAAAAAAAAAA");
                    Task<PriorityIp> finishedTask = await Task.WhenAny(currentlyHandling);

                    PriorityIp pIp = await finishedTask;
                    Console.WriteLine($"{pIp.Ip} Finished executing");

                    currentlyHandling.Remove(finishedTask);
                    ips.Enqueue(pIp, (int)pIp.Priority);
                    Console.WriteLine(ips.Count);
                }
                await Task.Delay(200);
            }
        }
        public static async Task<bool> isAlreadyActive()
        {
            string response;
            try
            {
                response = await Instruments.Get($"http://{Configuration.MainConfig.MainIP}:{mainPort}", @"/ping");
            }
            catch
            {
                Console.WriteLine($"Главный сервер на {Configuration.MainConfig.MainIP} недоступен.");
                return false;
            }
            Console.WriteLine("Ответ от Главного сервера получен!");
            return true;
        }
    }
}