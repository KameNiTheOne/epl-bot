using Telegram.Bot;
using Telegram.Bot.Types;
using Newtonsoft.Json;
using System.Text;

namespace TelegramBotik.instruments
{
    public static class ConfigRetriever
    {
        // Это клиент для работы с Telegram Bot API, который позволяет отправлять сообщения, управлять ботом, подписываться на обновления и многое другое.
        static ITelegramBotClient _botClient;

        public static async void Initialize()
        {
            _botClient = new TelegramBotClient(Configuration.MainConfig.BotTokens["config"]);

            Console.WriteLine($"Инициализация телеграм-бота - получателя конфига завершена!");

            await Task.Delay(-1);
        }
        public async static Task<Config> DownloadConfig()
        {
            //Get last pinned message from config group
            ChatFullInfo chatInfo = _botClient.GetChat(Configuration.MainConfig.ConfigChatID).Result;
            Message msg = chatInfo.PinnedMessage;

            //Download config file from telegram
            var tgFile = await _botClient.GetFile(msg.Document.FileId);
            await using var stream = new MemoryStream();
            await _botClient.DownloadFile(tgFile.FilePath, stream);
            stream.Position = 0;

            //Read data from config file
            Config config;
            using (stream)
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    config = JsonConvert.DeserializeObject<Config>(reader.ReadToEnd());
                }
            }
            return config;
        }
        public async static Task SendConfig(string path)
        {
            await using var stream = File.OpenRead(path);
            Message message = await _botClient.SendDocument(Configuration.MainConfig.ConfigChatID, stream, "Последний конфиг");
            await _botClient.PinChatMessage(Configuration.MainConfig.ConfigChatID, message.Id);
        }
    }
}
