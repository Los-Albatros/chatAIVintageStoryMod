using System.Text;
using System.Text.Json;

namespace chatAIVintageStoryMod.Provider;

public class OllamaProvider : IAIProvider
{
    private readonly string _endpoint;
    private readonly string _model;
    private readonly HttpClient _http;

    public string Name => "Ollama";
    public bool IsAvailable => !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_model);

    public OllamaProvider(string endpoint, string model, HttpClient? http = null)
    {
        _endpoint = endpoint;
        _model = model;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        var payload = new { model = _model, prompt, stream = false };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(_endpoint, content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        return doc.RootElement.TryGetProperty("response", out var val)
            ? val.GetString() ?? "No response"
            : "No response";
    }
}
