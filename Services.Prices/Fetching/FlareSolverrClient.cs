using System.Text.Json.Serialization;

namespace Services.Prices.Fetching;

/// <summary>
/// REWE's site sits behind Cloudflare bot protection - a plain HttpClient gets a 403/challenge page, not
/// the real response. FlareSolverr (https://github.com/FlareSolverr/FlareSolverr) drives a real browser to
/// solve the challenge and returns either the resulting cookies/user agent (to replay on a plain HttpClient
/// for calls that turn out not to need a real browser, e.g. the market-list POST) or, when the page itself
/// is requested through it, the fully rendered response body.
///
/// request.post is deliberately not used here: FlareSolverr submits `postData` as a literal HTML form body
/// (application/x-www-form-urlencoded) via browser navigation, not as a fetch/XHR with a chosen
/// Content-Type - confirmed live against a request echo - so it cannot carry a JSON POST body.
/// </summary>
internal sealed class FlareSolverrClient(HttpClient client)
{
    public async Task<FlareSolverrSolution?> GetAsync(
        string url, IReadOnlyList<FlareSolverrCookie>? cookies, CancellationToken ct)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/v1", new FlareSolverrRequest("request.get", url, 60000, cookies), ct);
        if (!response.IsSuccessStatusCode)
            return null;

        FlareSolverrResponse? result = await response.Content.ReadFromJsonAsync<FlareSolverrResponse>(ct);
        return result is { Status: "ok" } ? result.Solution : null;
    }

    [method: JsonConstructor]
    private record FlareSolverrRequest(
        [property: JsonPropertyName("cmd")] string Cmd,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("maxTimeout")] int MaxTimeout,
        [property: JsonPropertyName("cookies")] IReadOnlyList<FlareSolverrCookie>? Cookies);

    [method: JsonConstructor]
    private record FlareSolverrResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("solution")] FlareSolverrSolution? Solution);
}

[method: JsonConstructor]
internal record FlareSolverrSolution(
    [property: JsonPropertyName("userAgent")] string? UserAgent,
    [property: JsonPropertyName("response")] string? Response,
    [property: JsonPropertyName("cookies")] FlareSolverrCookie[]? Cookies);

[method: JsonConstructor]
internal record FlareSolverrCookie(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("domain")] string? Domain = null);
