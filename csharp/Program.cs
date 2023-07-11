using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using dotenv.net;

// Step 1 - Some setup
// Note: Take a look at the request.http file from VS code to see how to POST using REST client and build the supporting objects
const string SYSTEM_MESSAGE = "You are a general assistant";
var prompt_tokens = 0;
var completion_tokens = 0;
var history = new List<Message>();

// Step 2 - Get the OpenAI KEY and URI
// TODO: Add a .env file and add the API KEY and URI
DotEnv.Load();
var api_key = Environment.GetEnvironmentVariable("OPENAI_KEY");
var uri = Environment.GetEnvironmentVariable("OPENAI_URI");
if (api_key is null || uri is null)
{
    Console.WriteLine("Please add the OPENAI_KEY and OPENAI_URI to the .env file and run again.");
    return;
}

// Step 3 - Configure the HttpClient (acting like a singleton for the life of the application)
var client = new HttpClient();
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
client.DefaultRequestHeaders.Add("api-key", api_key);


// Step 4 - Activate the bot for the first time
history.Add(new Message("system", SYSTEM_MESSAGE));
var ans = await GetCompletionAsync(history);
if (ans.Item1 is not null)
{
    history.Add(new Message("system", ans.Item1));
    Console.WriteLine(ans.Item1);
    Console.WriteLine("Type 'quit' to exit the application or 'history' to look at the history\n");
    prompt_tokens = +ans.Item2;
    completion_tokens = +ans.Item3;
}

// Step 5 - Enter the loop
while (true)
{
    Console.WriteLine($"Tokens In: {prompt_tokens} Out: {completion_tokens} --> What is your question?\n");
    var prompt = Console.ReadLine();
    if (string.Compare(prompt, "quit", StringComparison.InvariantCultureIgnoreCase) == 0)
    {
        Console.WriteLine("Goodbye");
        break;
    }
    if (string.Compare(prompt, "history", StringComparison.InvariantCultureIgnoreCase) == 0)
    {
        foreach (var item in history)
        {
            Console.WriteLine($"{item.Role}: {item.Content}");
        }
        continue;
    }
    //Console.WriteLine(prompt);
    history.Add(new Message("user", prompt ?? string.Empty));
    ans = await GetCompletionAsync(history);
    if (ans.Item1 is not null)
    {
        history.Add(new Message("system", ans.Item1));
        Console.WriteLine("\n" + ans.Item1 + "\n");
        prompt_tokens = +ans.Item2;
        completion_tokens = +ans.Item3;
    }
}
return;

// Supporting classes and methods
async Task<Tuple<string?, int, int, int>> GetCompletionAsync(List<Message> history)
{
    var prompt = new Prompt(history, 100, 0.3d);
    var json = JsonSerializer.Serialize(prompt);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    // Note: We could have retry logic here
    var response = await client.PostAsync(new Uri(uri), content);

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Error: {response.StatusCode}");
        return new Tuple<string?, int, int, int>(null, 0, 0, 0);
    }

    var completion = await response.Content.ReadAsStringAsync();
    var completionObject = JsonSerializer.Deserialize<Completion>(completion);

    if (completionObject is not null)
    {
        var choice = completionObject.Choices[0];
        if (choice is not null)
        {
            return new Tuple<string?, int, int, int>(choice.Message.Content,
                completionObject.Usage.CompletionTokens,
                completionObject.Usage.PromptTokens,
                completionObject.Usage.TotalTokens);
        }
    }
    return new Tuple<string?, int, int, int>(null, 0, 0, 0); ;
}

public record Message([property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public record Prompt([property: JsonPropertyName("messages")] List<Message> Messages,
    [property: JsonPropertyName("max_tokens")] int Max_tokens,
    [property: JsonPropertyName("temperature")] double temperature);

public record Choice([property: JsonPropertyName("role")] int Index,
    [property: JsonPropertyName("finish_reason")] string FinishReason,
    [property: JsonPropertyName("message")] Message Message);

public record Usage([property: JsonPropertyName("completion_tokens")] int CompletionTokens,
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("total_tokens")] int TotalTokens);

public record Completion(
    [property: JsonPropertyName("role")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("choices")] List<Choice> Choices,
    [property: JsonPropertyName("usage")] Usage Usage);
