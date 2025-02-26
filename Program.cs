using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using TelegramBotik;
using TelegramBotik.instruments;
using System.Text.RegularExpressions;
using LLama.Native;

class Program
{
    // Это клиент для работы с Telegram Bot API, который позволяет отправлять сообщения, управлять ботом, подписываться на обновления и многое другое.
    static ITelegramBotClient _botClient;

    // Это объект с настройками работы бота. Здесь мы будем указывать, какие типы Update мы будем получать, Timeout бота и так далее.
    static ReceiverOptions _receiverOptions;
    static ChatId chatId;
    static int messageId;
    static string gpt_response;
    static string batch;
    static int currentMessageSize = 0;
    static int batch_size = 5;

    //Переменные для настройки бота и GPT
    static bool telegramBotDebug = false; // Режим для разработки фич телеграм бота без GPT функций, чтобы включить, поменять на True
    public static string pathToConfig = @"C:\Users\alext\OneDrive\Документы\codeshinenegans\TelegramBotik\TelegramBotik\config.json"; // Измени на свой путь к конфиг файлу
    public static uint contextSize = 8192; // Кол-во токенов, которые может обработать GPT, можно попробовать увеличить, если модель ничего не генерирует
    public static int layersToGPU = 18; // Часть GPT, которую обрабатывает видеокарта, см. диспетчер задач, если использующаяся память превышает колв-о выделенной памяти, уменьшай.
    static bool isGPTSummarizer = true; // Режим телеграмм-бот(false) или GPT для суммаризации документов(true)
    static string GPTSummarizerID = "1"; // уникальный id GPT для суммаризации, замените на любое натуральное число
    public static bool useSummarizerCluster = false;

    static async Task Main()
    {
        if (isGPTSummarizer)
        {
            Configuration.Initialize();
            if(Configuration.GPTHosts.TryGetValue(GPTSummarizerID, out string ip))
            {
                Console.WriteLine("ID is in Hosts!");
                if(ip != GPTServer.GetLocalIPAddress())
                {
                    Configuration.GPTHosts[GPTSummarizerID] = GPTServer.GetLocalIPAddress();
                    Configuration.SaveConfigfile();
                    Console.WriteLine("LocalIP of GPT server doesn't match one on Github! IP has been saved to file, please push to github!");
                }
                else
                {
                    Console.WriteLine("LocalIP of host matches saved IP.");
                }
            }
            else
            {
                Console.WriteLine("New GPTHost!");
                Configuration.GPTHosts[GPTSummarizerID] = GPTServer.GetLocalIPAddress();
                Configuration.SaveConfigfile();
            }
            TheGPT.Initialize(contextSize, layersToGPU);
            GPTServer.Initialize();
        }
        else
        {
            NativeLibraryConfig.All.WithLogCallback(delegate (LLamaLogLevel level, string message) { Console.Write($"{level}: {message}"); });

            _botClient = new TelegramBotClient("7316728850:AAHE1Kp7iknvSCa-cks6lzlo5cabB_5K6Ao"); // Присваиваем нашей переменной значение, в параметре передаем Token, полученный от BotFather
            _receiverOptions = new ReceiverOptions // Также присваем значение настройкам бота
            {
                AllowedUpdates = new[] // Тут указываем типы получаемых Update`ов, о них подробнее расказано тут https://core.telegram.org/bots/api#update
                {
                UpdateType.Message, // Сообщения (текст, фото/видео, голосовые/видео сообщения и т.д.)
            },
                // Параметр, отвечающий за обработку сообщений, пришедших за то время, когда ваш бот был оффлайн
                // True - не обрабатывать, False (стоит по умолчанию) - обрабаывать
                DropPendingUpdates = true,
            };

            using var cts = new CancellationTokenSource();

            // UpdateHander - обработчик приходящих Update`ов
            // ErrorHandler - обработчик ошибок, связанных с Bot API
            _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token); // Запускаем бота

            var me = await _botClient.GetMe(); // Создаем переменную, в которую помещаем информацию о нашем боте.

            if (!telegramBotDebug)
            {
                Configuration.Initialize();
                TheGPT.Initialize(contextSize, layersToGPU);
                HttpRetriever.Initialize($"http://{Configuration.HostIP}:8000");
            }

            Console.WriteLine($"{me.FirstName} запущен!");

            await Task.Delay(-1); // Устанавливаем бесконечную задержку, чтобы наш бот работал постоянно
        }
    }
    private static async Task<(string, string)> getFormatedTextsAndUrlsFromDocs(List<HttpRetriever.RetrieverDocument> docs)
    {
        string resultTexts = "";
        string resultUrls = "\n";
        int counter = 1;
        List<string> urls = new();
        List<string> texts = new();
        foreach (HttpRetriever.RetrieverDocument doc in docs)
        {
            urls.Add(doc.Url);
            texts.Add(doc.Value);
        }
        texts = telegramBotDebug ? texts : await TheGPT.SummarizeDocs(texts);
        var sumedDocs = texts.Zip(urls);
        foreach (var doc in sumedDocs) 
        {
            resultTexts += $"{counter}) {doc.First}\n";
            resultUrls += $"{doc.Second}\n";
            counter++;
        }
        return (resultTexts, resultUrls);
    }
    private static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Обязательно ставим блок try-catch, чтобы наш бот не "падал" в случае каких-либо ошибок
        try
        {
            // Сразу же ставим конструкцию switch, чтобы обрабатывать приходящие Update
            switch (update.Type)
            {
                case UpdateType.Message:
                    {
                        Message message = update.Message;
                        message.Text = message.Text is null ? "a" : message.Text;
                        if (message.Text.StartsWith("@SatoshisBat_bot"))
                        {
                            Console.WriteLine($"Started processing {message.Id}");
                            message.Text = message.Text.Remove(0, 16);
                            User sent_from = message.From;
                            chatId = message.Chat.Id;
                            gpt_response = "";
                            batch = "";

                            string firstName = sent_from.FirstName is null ? "" : sent_from.FirstName;
                            string lastName = sent_from.LastName is null ? "" : $" {sent_from.LastName}";
                            Message msg_to_edit = await botClient.SendMessage(
                                chatId,
                                "_",
                                replyParameters: message.Id
                            );
                            messageId = msg_to_edit.MessageId;

                            Console.WriteLine($"Original query: {message.Text}");
                            string formatedMessage = await BroadenQuery(message.Text);
                            Console.WriteLine($"Broadened & paraphrased query: {formatedMessage}");

                            List<HttpRetriever.RetrieverDocument> docs = await GetDocumentsFromServer(formatedMessage);
                            Console.WriteLine("Original docs:");
                            foreach(HttpRetriever.Document doc in docs)
                            {
                                Console.WriteLine(doc.Value);
                            }
                            (string Texts, string Urls) = await getFormatedTextsAndUrlsFromDocs(docs);

                            await GPTResponse($"{firstName}{lastName}", message.Text, Texts);
                            await RealTimeReponse(batch);

                            Console.WriteLine("Finished generating");

                            Message docmsg = await botClient.SendMessage(
                                chatId,
                                $"{Texts}{Urls}",
                                replyParameters: messageId
                            );
                            Console.WriteLine($"Finished processing {message.Id}");
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
    private static string LoremIpsum(int times)
    {
        return string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit.", times));
    }
    private static async Task<List<HttpRetriever.RetrieverDocument>> GetDocumentsFromServer(string query)
    {
        if (!telegramBotDebug)
        {
            return await HttpRetriever.PostQuery(query);
        }
        List<HttpRetriever.RetrieverDocument> docs = new();
        for (int i = 0; i < 6; i++)
        {
            docs.Add(new HttpRetriever.RetrieverDocument(LoremIpsum(1), "https://youtu.be/dQw4w9WgXcQ"));
        }
        return docs;
    }
    private static async Task<string> BroadenQuery(string query)
    {
        if (!telegramBotDebug)
        {
            return await TheGPT.BroadenQuery(query);
        }
        return LoremIpsum(1);
    }
    private static async Task GPTResponse(string user, string user_input, string docs)
    {
        if (!telegramBotDebug)
        {
            await TheGPT.GetResponse(user, user_input, docs);
        }
        await Validator(LoremIpsum(10));
    }
    private static async Task RealTimeReponse(string sent_response)
    {
        sent_response = TheGPT.CleanGPTResponse(sent_response);
        Console.WriteLine(sent_response);
        gpt_response += sent_response;
        bool flag = true;
        try
        {
            await _botClient.EditMessageText(chatId, messageId, gpt_response);
        }
        catch (Exception) 
        {
            Console.WriteLine("Failed to edit message in one attempt! Batch won't be cleared.");
            flag = false;
        }
        if (flag)
        {
            batch = "";
        }
    }
    private static int wordCount(string text)
    {
        var regex = new Regex(string.Format(@"\b?\b"),
                          RegexOptions.IgnoreCase);
        return regex.Matches(text).Count;
    }
    public static async Task Validator(string sent_response)
    {
        batch += sent_response;
        if (wordCount(batch) > batch_size)
        {
            currentMessageSize += batch.Length;
            if(currentMessageSize > 4096)
            {
                currentMessageSize = 0 + batch.Length;
                Message msg_to_edit = await _botClient.SendMessage(
                    chatId,
                    "_"
                );
                messageId = msg_to_edit.MessageId;
                gpt_response = batch;
            }
            await RealTimeReponse(batch);
        }
    }
    private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        // Тут создадим переменную, в которую поместим код ошибки и её сообщение 
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