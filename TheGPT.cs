using LLama.Common;
using LLama;
using LLama.Sampling;
using System.Text.RegularExpressions;
using TelegramBotik.instruments;

namespace TelegramBotik
{
    public static class TheGPT
    {
        static string modelPath = @"C:\Users\alext\Downloads\gemma-2-9b-it-Q5_K_M.gguf"; // change it to your own model path. "C:\Users\alext\Downloads\gemma-2-9b-it-Q5_K_M.gguf"
        static InferenceParams? inferenceParams;
        static string patternToTrim = @"(\bUser\W)|(\bAssistant\W)|(\bSystem\W)";
        static SessionState resetState;
        static ChatSession mainsession;
        public class TaskType // Enumerator type class
        {
            private TaskType((string, string) value) { Value = value; }
            public (string, string) Value { get; private set; }
            public (string, string) GetValue()
            {
                return Value;
            }

            public static TaskType Summarize { get { return new TaskType(("summarization", "Summarizing document...")); } }
            public static TaskType Broaden { get { return new TaskType(("broadening", "Broadening query...")); } }
        }
        public static void Initialize(uint _ContextSize, int _GpuLayerCount)
        {
            ModelParams parameters = new ModelParams(modelPath)
            {
                ContextSize = _ContextSize, // The longest length of chat as memory.
                GpuLayerCount = _GpuLayerCount // How many layers to offload to GPU. Please adjust it according to your GPU memory.
            };

            LLamaWeights model = LLamaWeights.LoadFromFile(parameters);
            LLamaContext context = model.CreateContext(parameters);

            InteractiveExecutor executor = new InteractiveExecutor(context);

            ChatHistory new_history = new();
            resetState = new ChatSession(executor, new_history).GetSessionState();

            mainsession = new(executor, new_history);

            inferenceParams = new InferenceParams()
            { // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
                SamplingPipeline = new DefaultSamplingPipeline() { Temperature = 0.75f },
                AntiPrompts = new List<string> {"User:", "System:", "User: ", "System: ", "\nUser:", "\nSystem:", "\nUser: ", "\nSystem: "} // Stop generation once antiprompts appear.
            };
        }
        static async Task onGPTTask()
        {
            await Configuration.Load();
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
        /// <summary>
        /// Removes unnecessary patterns specified in patternToTrim and whitespace characters from start and end of string (if trimWhiteSpace is true).
        /// </summary>
        public static string CleanResponse(string response, bool trimWhiteSpace = true)
        {
            string regexedResponse = Regex.Replace(response, patternToTrim, string.Empty);
            if (trimWhiteSpace)
            {
                regexedResponse = regexedResponse.TrimStart().TrimEnd();
            }
            return regexedResponse;
        }
        private static async Task<string> GPTTask(string system, string assistant, string user, bool showHistory)
        {
            string result = "";
            mainsession.LoadSession(resetState);
            mainsession.AddMessage(new ChatHistory.Message(AuthorRole.System, system));
            mainsession.AddMessage(new ChatHistory.Message(AuthorRole.User, ""));
            mainsession.AddMessage(new ChatHistory.Message(AuthorRole.Assistant, assistant));
            await foreach (var text in mainsession.ChatAsync(new ChatHistory.Message(AuthorRole.User, user), inferenceParams))
            {
                Console.WriteLine(text);
                result += text;
            }
            if (showHistory)
            {
                ShowHistory(mainsession.History);
            }
            return CleanResponse(result);
        }
        public static async Task<List<string>> AsyncSummarizeDocs(List<string> docs)
        {
            Dictionary<Task<string>, string> tasks = new();
            tasks[GPTTaskGetter(TaskType.Summarize, docs.First())] = "0";
            docs.RemoveAt(0);
            List<(string, string)> order = new() { ("0", "None") };

            foreach (string id in Configuration.MainConfig.GPTHosts.Keys)
            {
                tasks[HttpRetriever.Post(@$"http://{Configuration.MainConfig.GPTHosts[id]}:9111", @"/summarize", new HttpRetriever.Document(docs.First()))] = id;
                docs.RemoveAt(0);
                order.Add((id, "None"));
            }
            while (docs.Count > 0)
            {
                Task<string> finishedTask = await Task.WhenAny(tasks.Keys);

                int index = order.FindIndex(new Predicate<(string, string)>(x => { return x.Item1 == tasks[finishedTask] && x.Item2 == "None"; }));
                string id = order[index].Item1; 
                order[index] = (order[index].Item1, await finishedTask);

                tasks.Remove(finishedTask);
                tasks[HttpRetriever.Post(@$"http://{Configuration.MainConfig.GPTHosts[id]}:9111", @"/summarize", new HttpRetriever.Document(docs.First()))] = id;
                docs.RemoveAt(0);
                order.Add((id, "None"));
            }
            List<string> result = new();
            foreach(var doc in order)
            {
                result.Add(doc.Item2);
            }
            return result;
        }
        public static async Task<List<string>> SummarizeDocs(List<string> docs)
        {
            if (Program.useSummarizerCluster)
            {
                return await AsyncSummarizeDocs(docs);
            }
            List<string> result = new();
            foreach (var doc in docs)
            {
                result.Add(await GPTTaskGetter(TaskType.Summarize ,doc, true));
            }
            return result;
        }
        /// <summary>
        /// Function for accessing additional functionality of TheGPT, use TheGPT.TaskType to access said functionality
        /// </summary>
        public static async Task<string> GPTTaskGetter(TaskType type, string doc, bool showHistory = false)
        {
            await onGPTTask();

            (string promptType, string message) = type.GetValue();
            Console.WriteLine(message);
            return await GPTTask(
                Configuration.MainConfig.Prompts[promptType].System,
                $"{Configuration.MainConfig.Prompts[promptType].Assistant}{doc}",
                Configuration.MainConfig.Prompts[promptType].User,
                showHistory
                );
        }
        public static async Task GetResponse(string user_input, string docs)
        {
            await onGPTTask();
            Console.WriteLine("Trying to send a message");
            mainsession.LoadSession(resetState);

            Console.WriteLine(user_input);
            Console.WriteLine(docs);
            mainsession.AddMessage(new ChatHistory.Message(AuthorRole.System, Configuration.MainConfig.Prompts["mainresponse"].System));
            mainsession.AddMessage(new ChatHistory.Message(AuthorRole.User, ""));
            mainsession.AddMessage(new ChatHistory.Message(AuthorRole.Assistant, $"{Configuration.MainConfig.Prompts["mainresponse"].Assistant}{docs}"));

            await foreach (var text in mainsession.ChatAsync(new ChatHistory.Message(AuthorRole.User, $"{Configuration.MainConfig.Prompts["mainresponse"].User}{user_input}"), inferenceParams))
            {
                Console.WriteLine(text);
                await Program.UIValidator(text);
            }
            ShowHistory(mainsession.History);
        }
    }
}
