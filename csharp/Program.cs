// mkdir gptbot && cd gptbot
// dotnet new console
// dotnet add package dotenv.net
// touch .env
// code .

// Getting an OpenAI API key and URL from the Azure Portal
// Calling an API from requests.http
// Converting the prompt and completion json to c# records using Bing

// References:
// OpenAI API Reference
//   https://learn.microsoft.com/en-us/azure/cognitive-services/openai/reference
// Prompt Engineering
//   https://help.openai.com/en/articles/6654000-best-practices-for-prompt-engineering-with-openai-api

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using dotenv.net;

// Step 1 - App Configuration
const string DEFAULT_SYSTEM_MESSAGE = "You are a general assistant. Greet me by saying \"Hello, I am your chatbot assistant. How may I be of service?\"";
var conversation = new List<Message>();

// Step 2 - Read API key and URI from either .env file or environment variables
DotEnv.Load();
string apikey = Environment.GetEnvironmentVariable("OPENAI_KEY") ?? "";
string uri = Environment.GetEnvironmentVariable("OPENAI_URI") ?? "";

if (uri == string.Empty || apikey == string.Empty)
{
    Console.WriteLine("Please set OPENAI_KEY and OPENAI_URI in your .env file or environment variables.");
    return;
}

// Step 3 - Configure the HttpClient headers to use the api key
var client = new HttpClient();
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
client.DefaultRequestHeaders.Add("api-key", $"{apikey}");

// Step 4 - Call the bot for the first time
conversation.Add(new Message(Role.system.ToString(), DEFAULT_SYSTEM_MESSAGE));
var ans = await GetGptCompletionAsync(conversation);
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine(ans.Item1 + "\n");
var ctokens = ans.Item2;
var ptokens = ans.Item3;


// Step 5 - Loop through the conversation having the ability to exit
var firstTime = true;
while (true)
{
    if (!firstTime)
    {
        ColorWrite($"Tokens In:{ptokens} Out:{ctokens} -> What is your question?\n");
    }
    else
    {
        firstTime = false;
    }
    Console.ForegroundColor = ConsoleColor.Cyan;
    var prompt = Console.ReadLine();
    var lprompt = prompt?.ToLower();
    if (lprompt == "exit" || lprompt == "quit")
    {
        ColorWrite("Goodbye!");
        break;
    }
    else if (lprompt == "history")
    {
        ColorWrite("\nConversation History:\n", ConsoleColor.Yellow);
        foreach (var message in conversation)
        {
            ColorWrite("Role: ", ConsoleColor.Blue, false);
            ColorWrite($"{message.Role}", ConsoleColor.Green, false);
            ColorWrite(" Content: ", ConsoleColor.Blue, false);
            var outMsg = message.Content.Length < 60 ? message.Content : message.Content.Substring(0, 60) + "...";
            ColorWrite($"{outMsg}", ConsoleColor.Green);

        }
        ColorWrite("");
        continue;
    }
    else if (prompt is not null)
    {
        conversation.Add(new Message(Role.user.ToString(), prompt));
        ans = await GetGptCompletionAsync(conversation);
        ColorWrite("\n" + ans.Item1 + "\n", ConsoleColor.Green);

        conversation.Add(new Message(Role.system.ToString(), ans.Item1 ?? ""));
        ctokens += ans.Item2;
        ptokens += ans.Item3;
    }
}
return;

// Supporting classes and methods
void ColorWrite(string text, ConsoleColor color = ConsoleColor.White, bool newLine = true)
{
    Console.ForegroundColor = color;
    if (newLine)
    {
        Console.WriteLine(text);
    }
    else
    {
        Console.Write(text);
    }
}

async Task<Tuple<string?, int, int>> GetGptCompletionAsync(List<Message> history)
{
    var prompt = new Prompt(history, 1000, 0.3d);
    var json = JsonSerializer.Serialize(prompt);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    // Note: We could add retry logic here
    var response = await client.PostAsync(new Uri(uri), content);

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Error: {response.StatusCode}");
        return new Tuple<string?, int, int>(null, 0, 0);
    }

    var completion = await response.Content.ReadAsStringAsync();
    var completionObject = JsonSerializer.Deserialize<Completion>(completion);

    if (completionObject is not null)
    {
        var choice = completionObject.Choices[0];
        if (choice is not null)
        {
            return new Tuple<string?, int, int>(choice.Message.Content,
                completionObject.Usage.CompletionTokens,
                completionObject.Usage.PromptTokens);
        }
    }
    return new Tuple<string?, int, int>(null, 0, 0);
}

record Message([property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

record Prompt([property: JsonPropertyName("messages")] List<Message> Messages,
    [property: JsonPropertyName("max_tokens")] int Max_tokens,
    [property: JsonPropertyName("temperature")] double temperature,
    [property: JsonPropertyName("n")] int N = 1,
    [property: JsonPropertyName("stop")] string? Stop = null
    );

record Choice([property: JsonPropertyName("role")] int Index,
    [property: JsonPropertyName("finish_reason")] string FinishReason,
    [property: JsonPropertyName("message")] Message Message);

record Usage([property: JsonPropertyName("completion_tokens")] int CompletionTokens,
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("total_tokens")] int TotalTokens);

record Completion(
    [property: JsonPropertyName("role")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("choices")] List<Choice> Choices,
    [property: JsonPropertyName("usage")] Usage Usage);

enum Role
{
    system,
    user
}
