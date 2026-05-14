using System.Text;
using System.Text.Json;

namespace chatAIVintageStoryMod.Provider;

public class OpenAIProvider : IAIProvider
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxTokens;
    private readonly HttpClient _http;

    public string Name => "OpenAI";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public OpenAIProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
    {
        _apiKey = apiKey;
        _endpoint = endpoint;
        _model = model;
        _temperature = temperature;
        _maxTokens = maxTokens;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        var payload = new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = _temperature,
            max_tokens = _maxTokens
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "No response";
    }
}
