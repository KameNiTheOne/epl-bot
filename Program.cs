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
    static bool telegramBotDebug = false; // Режим для разработки фич телеграм бота без GPT функций, чтобы включить, поменять на true
    public static uint contextSize = 8192; // Кол-во токенов, которые может обработать GPT, можно попробовать увеличить, если модель ничего не генерирует
    public static int layersToGPU = 8; // Часть GPT, которую обрабатывает видеокарта, см. диспетчер задач, если использующаяся память превышает колв-о выделенной памяти, уменьшай.
    static bool isGPTSummarizer = false; // Режим телеграмм-бот(false) или GPT для суммаризации документов(true)
    static string GPTSummarizerID = "1"; // уникальный id GPT для суммаризации, замените на любое натуральное число
    public static bool useSummarizerCluster = false;

    static async Task Main()
    {
        await Configuration.Load();

        Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);

        if (isGPTSummarizer)
        {
            Configuration.MainConfig.GPTHosts[GPTSummarizerID] = GPTServer.GetLocalIPAddress();
            TheGPT.Initialize(contextSize, layersToGPU);
            GPTServer.Initialize();
        }
        else
        {
            NativeLibraryConfig.All.WithLogCallback(delegate (LLamaLogLevel level, string message) { Console.Write($"{level}: {message}"); });

            _botClient = new TelegramBotClient(Configuration.MainConfig.BotTokens["main"]);
            _receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
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

            var me = await _botClient.GetMe();

            if (!telegramBotDebug)
            {
                TheGPT.Initialize(contextSize, layersToGPU);
                HttpRetriever.Initialize();
            }

            Console.WriteLine($"{me.FirstName} запущен! Чтобы выйти из программы, нажми CTRL+C.");

            await Task.Delay(-1); // Устанавливаем бесконечную задержку
        }
    }
    static void OnExit(object sender, EventArgs e)
    {
        Configuration.IsCurrentConfigDifferent();
        Console.WriteLine("Exiting application...");
    }
    static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            // Обрабатываем приходящие Update
            switch (update.Type)
            {
                case UpdateType.Message:
                    {
                        Message message = update.Message;
                        message.Text = message.Text is null ? "a" : message.Text;
                        if (message.Text.StartsWith("@SatoshisBat_bot"))
                        {
                            Console.WriteLine($"Started processing {message.Id}"); // Start of preparations for processing
                            message.Text = message.Text.Remove(0, 16);
                            message.Text = TheGPT.CleanResponse(message.Text); // Clean message from user

                            chatId = message.Chat.Id;
                            gpt_response = "";
                            batch = "";

                            Message msg_to_edit = await botClient.SendMessage(
                                chatId,
                                ".",
                                replyParameters: message.Id
                            );
                            messageId = msg_to_edit.MessageId; // End of preparations

                            // Cancellation token source for cancellation. Make sure to dispose after use (which is done here through the using expression).
                            using var tokenSource = new CancellationTokenSource();

                            // The cancellation token will be used to communicate cancellation to tasks
                            var token = tokenSource.Token;
                            UIAnimateWait(token, ["..", "...", "....", ".....", "....", "...", "..", "."], 400);

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
                            await UIRealTimeReponse(batch);

                            Console.WriteLine("Finished generating response");

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
    static string LoremIpsum(int times) // Returns LoremIpsum repeated <times> amount
    {
        return string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit.", times));
    }
    static async Task<(string, string)> getAndFormatTextsAndUrlsFromDocs(List<HttpRetriever.RetrieverDocument> docs)
    {
        string resultTexts = "";
        string resultUrls = "";
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
            resultTexts += $"Документ {counter}) {doc.First}\n";
            resultUrls += $"{doc.Second}\n";
            counter++;
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
            docs.Add(new HttpRetriever.RetrieverDocument(LoremIpsum(1), "https://youtu.be/dQw4w9WgXcQ"));
        }
        return docs;
    }
    static async Task<string> BroadenQuery(string query)
    {
        if (!telegramBotDebug)
        {
            return await TheGPT.GPTTaskGetter(TheGPT.TaskType.Broaden, query, true);
        }
        return LoremIpsum(1);
    }
    static async Task GPTResponse(string user_input, string docs)
    {
        if (!telegramBotDebug)
        {
            await TheGPT.GetResponse(user_input, docs);
        }
        else
        {
            await UIValidator(LoremIpsum(10));
        }
    }
    static async Task UIAnimateWait(CancellationToken ct, string[] animation, int animationSpeed)
    {
        while (true)
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
                catch (Exception) { Console.WriteLine("Failed to show a frame!"); }
                await Task.Delay(animationSpeed);
            }
        }
    }
    static int wordCount(string text)
    {
        var regex = new Regex(string.Format(@"\b?\b"),
                          RegexOptions.IgnoreCase);
        return regex.Matches(text).Count;
    }
    public static async Task UIValidator(string sent_response)
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
            await UIRealTimeReponse(batch);
        }
    }
    static async Task UIRealTimeReponse(string sent_response)
    {
        sent_response = TheGPT.CleanResponse(sent_response, false);
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