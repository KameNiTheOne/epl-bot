using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using TelegramBotik.instruments;
using LLama.Native;
using System.Text;

namespace TelegramBotik
{
    public class JSONMessage
    {
        public long Chatid { get; set; }
        public int Id { get; set; }
        public string Text { get; set; }
        public static JSONMessage FromMeassage(Message msg)
        {
            return new JSONMessage { Chatid = msg.Chat.Id, Id = msg.Id, Text = msg.Text };
        }
    }
    class Program
    {
        // Это клиент для работы с Telegram Bot API, который позволяет отправлять сообщения, управлять ботом, подписываться на обновления и многое другое.
        static ITelegramBotClient _botClient;

        static ChatId chatId;
        static int messageId;
        static string gpt_response;
        static string batch;
        static PriorityIp prioritizedIp;
        static CancellationTokenSource cts;

        //Булевые константы для конфигурации главного сервера
        public const bool telegramBotDebug = false; // Режим для разработки фич телеграм бота без GPT функций, чтобы включить, поменять на true
        const bool useSummarizer = false; // Сокращать документы или нет
        public const bool useSummarizerCluster = false; // Использовать кластер для сокращения или нет
        public const bool useLocalConfig = false; // Не загружать конфиг с теоеграма, а использовать локальный

        //Булевые константы для выбора режимов сервера, True на какой-либо из превращает данную сессию в ячейку кластера
        const bool isGPTSummarizer = false; // Режим кластерного сокращения документов

        //Константы для настройки бота и GPT
        public const uint contextSize = 24000; // Кол-во токенов, которые может обработать GPT
        public const int layersToGPU = 10; // Часть GPT, которую обрабатывает видеокарта, см. диспетчер задач, если использующаяся память превышает колв-о выделенной памяти, уменьшай
        const string GPTHostID = "1"; // уникальный id GPT для суммаризации, замените на любое натуральное число
        const int batch_size = 36; // 36

        static async Task Main() // Начало выполнения программы
        {
            cts = new CancellationTokenSource(); // Источник токенов для выхода из программы при Ctrl+C

            Instruments.httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(600) }; // Инициализируем httpClient, который будет принимать и посылать запросы серверам.
                                                                                               // Задержка 10 минут, чтобы запросы не сбрасывались после долгого отсутствия ответа.
            await Configuration.Load();

            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);

            NativeLibraryConfig.All.WithLogCallback(delegate (LLamaLogLevel level, string message) { Console.Write($"{level}: {message}"); }); // Better GPT logs
            TheGPT.Initialize(contextSize, layersToGPU);

            if (!isGPTSummarizer)
            {
                _botClient = new TelegramBotClient(Configuration.MainConfig.BotTokens["main"]);
                Priority priority;

                if (!await MainServer.isAlreadyActive())
                {
                    priority = Priority.High;

                    MainServer.Initialize(cts.Token); // Инициируем главный сервер - сервер-дистрибьютор и слушатель входящих сообщений
                    Configuration.MainConfig.MainIP = Instruments.GetLocalIPAddress();
                    Configuration.IsCurrentConfigDifferent();
                    ReceiverOptions _receiverOptions = new ReceiverOptions
                    {
                        AllowedUpdates =
                        [
                            UpdateType.Message, // Сообщения (текст, фото/видео, голосовые/видео сообщения и т.д.)
                        ],
                        // Параметр, отвечающий за обработку сообщений, пришедших за то время, когда ваш бот был оффлайн
                        // True - не обрабатывать, False (стоит по умолчанию) - обрабаывать
                        DropPendingUpdates = true,
                    };

                    using var _cts = new CancellationTokenSource();
                    _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, _cts.Token); // Бот начинает принимать запросы
                }
                else priority = Priority.Normal;

                prioritizedIp = new PriorityIp { Ip = Instruments.GetLocalIPAddress(), Priority = priority };
                await MainServer.Register(prioritizedIp);
            }
            else
            {
                Configuration.MainConfig.GPTHosts[GPTHostID] = Instruments.GetLocalIPAddress();
                Configuration.IsCurrentConfigDifferent();
            }

            GPTServer.Initialize(isGPTSummarizer, cts.Token); // Инициируем сервер-обработчик сообщений

            await Task.Delay(100);
            Console.WriteLine($"\nБита Сатоши запущен! Чтобы выйти из программы, нажми CTRL+C.");

            DisplayActiveModes();

            await Task.Delay(-1); // Устанавливаем бесконечную задержку
        }
        static void OnExit(object sender, EventArgs e)
        {
            Configuration.IsCurrentConfigDifferent();
            cts.Cancel();
            cts.Dispose();
            Console.WriteLine("Exiting application...");
            Environment.Exit(0);
        }
        /// <summary>
        /// Displays active debug modes (bools) and such in console.
        /// </summary>
        static void DisplayActiveModes()
        {
            Console.WriteLine("\n*** Active server modes ***");
            if (telegramBotDebug) Console.WriteLine("TelegramBot debug mode is on!");
            if (useSummarizer) Console.WriteLine("Is using doc summarization");
            if (useSummarizerCluster) Console.WriteLine("Is using cluster mode for summarization");
            if (isGPTSummarizer) Console.WriteLine($"This instance is a summarizer in a cluster, id: {GPTHostID}");
            Console.WriteLine("*** End of active modes ***\n");
        }
        static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                // Обрабатываем приходящие боту Update
                switch (update.Type)
                {
                    case UpdateType.Message:
                        {
                            Message message = update.Message;
                            message.Text = message.Text is null ? "a" : message.Text;
                            if (message.Text.StartsWith("@SatoshisBat_bot"))
                            {
                                message.Text = message.Text.Remove(0, 16);
                                message.Text = TheGPT.CleanResponse(message.Text); // Clean message from user
                                MainServer.DistributeMessage(JSONMessage.FromMeassage(message));
                            }
                            return;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public static async Task<PriorityIp> HandleMessage(JSONMessage message)
        {
            Console.WriteLine($"Started processing {message.Id}"); // Start of preparations for processing

            chatId = message.Chatid;
            gpt_response = "";
            batch = "";

            Message msg_to_edit = await _botClient.SendMessage(
                chatId,
                "_",
                replyParameters: message.Id
            );
            messageId = msg_to_edit.MessageId; // End of preparations

            // Cancellation token source for cancellation. Make sure to dispose after use (which is done here through the using expression).
            using var tokenSource = new CancellationTokenSource();

            // The cancellation token will be used to communicate cancellation to tasks
            var token = tokenSource.Token;
            UIRandomPhrasesAnimation(token, ["...Пожалуйста, подождите..."], 500);
            if (telegramBotDebug) await Task.Delay(5000);

            Console.WriteLine($"Starting retrieval and augmentation.\nOriginal query: {message.Text}");
            string formatedMessage = await BroadenQuery(message.Text);
            Console.WriteLine($"Broadened & paraphrased query: {formatedMessage}");

            List<HttpRetriever.RetrieverDocument> docs = await GetDocumentsFromServer(formatedMessage);
            Console.WriteLine("Original docs:");
            foreach (HttpRetriever.Document doc in docs)
            {
                Console.WriteLine(doc.Value);
            }
            (string Texts, string Urls) = await getAndFormatTextsAndUrlsFromDocs(docs);
            Console.WriteLine("Finished retrieval and augmentation");

            tokenSource.Cancel(); // Stop animation

            await GPTResponse(message.Text, Texts);
            await UIRealTimeReponse();
            Console.WriteLine("Finished generating response");

            Message docmsg = await SendDocsToUser(Texts, "Релевантные документы:");
            Message URLmsg = await _botClient.SendMessage(
                chatId,
                Urls,
                replyParameters: messageId
            );
            Console.WriteLine($"Finished processing {message.Id}");
            return prioritizedIp;
        }
        static async Task<Message> SendDocsToUser(string docs, string msg)
        {
            var buffer = Encoding.UTF8.GetBytes(docs);
            await using var ms = new MemoryStream(buffer);
            return await _botClient.SendDocument(chatId, InputFile.FromStream(ms, "Релевантные документы.txt"),
                                                 msg, replyParameters: messageId);
        }
        static async Task UIRandomPhrasesAnimation(CancellationToken ct, string[] phrases, int animationSpeed)
        {
            Random rnd = new();
            while (true)
            {
                string phrase = phrases[rnd.Next(0, phrases.Length)];
                string[] frames = new string[phrase.Length * 2];
                int lenOfSplit = phrase.Length;
                frames[0] = phrase[0].ToString();
                frames[lenOfSplit * 2 - 1] = frames[0];
                for (int i = 1; i < lenOfSplit; i++)
                {
                    frames[i] = frames[i - 1] + phrase[i].ToString();
                    frames[lenOfSplit * 2 - i - 1] = frames[i];
                }
                await UIAnimateWait(ct, frames, animationSpeed);
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
            }
        }
        static async Task UIAnimateWait(CancellationToken ct, string[] animation, int animationSpeed)
        {
            foreach (string frame in animation)
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                try
                {
                    await _botClient.EditMessageText(chatId, messageId, frame);
                }
                catch (Exception e)
                {
                    if (e.Message != "Bad Request: message is not modified: specified new message content and reply markup are exactly the same as a current content and reply markup of the message")
                    {
                        Console.WriteLine($"Failed to show a frame ({frame})!:\n{e.Message}");
                    }
                }
                await Task.Delay(animationSpeed);
            }
        }
        static async Task<(string, string)> getAndFormatTextsAndUrlsFromDocs(List<HttpRetriever.RetrieverDocument> docs)
        {
            string resultTexts = "";
            string resultUrls = "";
            List<string> urls = new();
            List<string> texts = new();
            List<string> titles = new();
            foreach (HttpRetriever.RetrieverDocument doc in docs)
            {
                urls.Add(doc.Url);
                texts.Add(doc.Value);
                titles.Add(doc.Title);
            }
            texts = telegramBotDebug || !useSummarizer ? texts : await TheGPT.SummarizeDocs(texts);
            var sumedDocs = texts.Zip(urls, titles);
            foreach (var doc in sumedDocs)
            {
                resultTexts += $"{doc.Third}: {doc.First}\n\n";
                resultUrls += $"{doc.Third}: {doc.Second}\n";
            }
            return (resultTexts, resultUrls);
        }
        static async Task<List<HttpRetriever.RetrieverDocument>> GetDocumentsFromServer(string query)
        {
            if (!telegramBotDebug)
            {
                return await HttpRetriever.PostQuery(query);
            }
            List<HttpRetriever.RetrieverDocument> docs = new();
            for (int i = 0; i < 6; i++)
            {
                docs.Add(new HttpRetriever.RetrieverDocument(Instruments.LoremIpsum(1), "https://youtu.be/dQw4w9WgXcQ", "Статья 228"));
            }
            return docs;
        }
        static async Task<string> BroadenQuery(string query)
        {
            if (!telegramBotDebug)
            {
                return await TheGPT.GPTTaskGetter(TheGPT.TaskType.Broaden, query, true);
            }
            return Instruments.LoremIpsum(1);
        }
        static async Task GPTResponse(string user_input, string docs)
        {
            if (!telegramBotDebug)
            {
                await TheGPT.GetResponse(user_input, docs);
            }
            else
            {
                await UIValidator(Instruments.LoremIpsum(10));
            }
        }
        public static async Task UIValidator(string sent_response)
        {
            batch += sent_response;
            if (Instruments.CountWords(batch) > batch_size)
                await UIRealTimeReponse();
        }
        static async Task UIRealTimeReponse()
        {
            while (true)
            {
                string sent_response = TheGPT.CleanResponse(batch, false);
                Console.WriteLine(sent_response);
                gpt_response += sent_response;
                bool flag = true;
                try
                {
                    await _botClient.EditMessageText(chatId, messageId, gpt_response);
                }
                catch (ApiRequestException e)
                {
                    flag = false;
                    Console.WriteLine($"Failed to edit message! Batch won't be cleared. Exception: {e.Message}");
                    if (e.ErrorCode == 429)
                    {
                        int retryAfterSeconds = e.Parameters.RetryAfter.Value;
                        Console.WriteLine($"Retry after {retryAfterSeconds} seconds.");

                        await Task.Delay(retryAfterSeconds * 1000);
                        batch = "";
                        break;
                    }
                    if (e.Message == "Bad Request: MESSAGE_TOO_LONG")
                    {
                        await Task.Delay(500);
                        gpt_response = "";
                        Message msg_to_edit = await _botClient.SendMessage(
                            chatId,
                            "_",
                            replyParameters: messageId
                        );
                        messageId = msg_to_edit.MessageId;
                        await Task.Delay(500);
                        continue;
                    }
                    await Task.Delay(1000);
                    if (batch != "") continue;
                }
                if (flag)
                    batch = "";
                break;
            }
        }
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