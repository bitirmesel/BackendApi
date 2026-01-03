using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class FluentMeService
{
    private readonly HttpClient _client;

    private const string FLUENT_SCORE_BASE = "https://thefluent.me/api/swagger/score"; // sende nasılsa
    private const string FLUENT_API_KEY = "xxx";

    public FluentMeService(HttpClient client)
    {
        _client = client;
    }

    public async Task<string> SendScoreToFluentMeByUrl(string token, string postId, string audioUrl)
    {
        var url = $"{FLUENT_SCORE_BASE}/{postId}?scale=100";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Add("x-access-token", token);
        req.Headers.Add("x-api-key", FLUENT_API_KEY);

        var bodyJson = JsonSerializer.Serialize(new { audio_provided = audioUrl });
        req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        using var resp = await _client.SendAsync(req);
        var respBody = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"FluentMe score failed: {(int)resp.StatusCode} - {respBody}");

        return respBody;
    }
}
