using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using TelegramBotik.instruments;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Newtonsoft.Json;
using System.Text;

namespace TelegramBotik
{
    public static class ConfigRetriever
    {
        // Это клиент для работы с Telegram Bot API, который позволяет отправлять сообщения, управлять ботом, подписываться на обновления и многое другое.
        static ITelegramBotClient _botClient;

        // Это объект с настройками работы бота. Здесь мы будем указывать, какие типы Update мы будем получать, Timeout бота и так далее.
        static ReceiverOptions _receiverOptions;
        public static async void Initialize()
        {
            _botClient = new TelegramBotClient(Configuration.MainConfig.BotTokens["config"]);
            _receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                UpdateType.Message,
            },
                DropPendingUpdates = true,
            };

            using var cts = new CancellationTokenSource();

            _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

            var me = await _botClient.GetMe();

            Console.WriteLine($"{me.FirstName} инициализирован.");

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
        static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) { return; }
        static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
        {
            var ErrorMessage = error switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => error.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }
    }
}
