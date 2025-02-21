using LLama.Common;
using LLama;
using LLama.Sampling;
using System.Text.RegularExpressions;
using TelegramBotik.instruments;

namespace TelegramBotik
{
    public static class TheGPT
    {
        static string modelPath = @"C:\Users\alext\Downloads\Qwen2.5-7B.Q5_K_M.gguf"; // change it to your own model path. "C:\Users\alext\Downloads\gemma-2-9b-it-Q5_K_M.gguf"
        static LLamaWeights? model;
        static LLamaContext? context;
        static InteractiveExecutor? executor;
        static Dictionary<string, ChatSession> user_histories = new();
        static InferenceParams? inferenceParams;
        static string patternToTrim = @"(\bUser\W)|(\bAssistant\W)|(\bSystem\W)";
        public static void Initialize(uint _ContextSize, int _GpuLayerCount)
        {
            ModelParams parameters = new ModelParams(modelPath)
            {
                ContextSize = _ContextSize, // The longest length of chat as memory.
                GpuLayerCount = _GpuLayerCount // How many layers to offload to GPU. Please adjust it according to your GPU memory.
            };

            model = LLamaWeights.LoadFromFile(parameters);
            context = model.CreateContext(parameters);

            executor = new InteractiveExecutor(context);

            inferenceParams = new InferenceParams()
            { // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
                SamplingPipeline = new DefaultSamplingPipeline() { Temperature = 0.75f },
                AntiPrompts = new List<string> {"User:", "System:", "User: ", "System: ", "\nUser:", "\nSystem:", "\nUser: ", "\nSystem: "} // Stop generation once antiprompts appear.
            };
        }
        private static void ShowHistory(ChatHistory history)
        {
            Console.WriteLine("\t***History***");
            foreach (ChatHistory.Message message in history.Messages)
            {
                Console.WriteLine(message.Content);
            }
            Console.WriteLine("\t***End of History***");
        }
        public static string CleanGPTResponse(string response)
        {
            return Regex.Replace(response, patternToTrim, string.Empty).TrimStart().TrimEnd();
        }
        private static async Task<string> GPTTask(string system, string assistant, string user, bool showHistory)
        {
            string result = "";
            ChatHistory new_history = new();
            SessionState resetSession = new ChatSession(executor, new_history).GetSessionState();
            new_history.AddMessage(AuthorRole.System, system);
            new_history.AddMessage(AuthorRole.Assistant, assistant);
            ChatSession mainsession = new(executor, new_history);
            await foreach (var text in mainsession.ChatAsync(new ChatHistory.Message(AuthorRole.User, user), inferenceParams))
            {
                Console.WriteLine(text);
                result += text;
            }
            if (showHistory)
            {
                ShowHistory(mainsession.History);
            }
            mainsession.LoadSession(resetSession);
            return CleanGPTResponse(result);
        }
        public static async Task<string> SummarizeDoc(string doc, bool showHistory = false)
        {
            Console.WriteLine("Summarizing doc...");
            return await GPTTask(
                Configuration.Prompts["summarization"].System,
                $"{Configuration.Prompts["summarization"].Assistant}{doc}",
                Configuration.Prompts["summarization"].User, 
                showHistory
                );
        }
        public static async Task<string> BroadenQuery(string query, bool showHistory = true)
        {
            return await GPTTask(
                Configuration.Prompts["broadening"].System,
                $"{Configuration.Prompts["broadening"].Assistant}{query}",
                Configuration.Prompts["broadening"].User,
                showHistory
                );
        }
        public static async Task GetResponse(string user, string user_input, string docs)
        {
            Console.WriteLine("Trying to send a message");
            ChatHistory new_history = new();
            SessionState resetSession = new ChatSession(executor, new_history).GetSessionState();

            Console.WriteLine(user_input);
            Console.WriteLine(docs);
            new_history.AddMessage(AuthorRole.System, Configuration.Prompts["mainresponse"].System);
            new_history.AddMessage(AuthorRole.Assistant, $"{Configuration.Prompts["mainresponse"].Assistant}{user_input}");
            ChatSession mainsession = new(executor, new_history);

            await foreach (var text in mainsession.ChatAsync(new ChatHistory.Message(AuthorRole.User, $"{Configuration.Prompts["mainresponse"].User}{docs}"), inferenceParams))
            {
                await Program.Validator(text);
            }
            ShowHistory(mainsession.History);
            mainsession.LoadSession(resetSession);
        }
    }
}
