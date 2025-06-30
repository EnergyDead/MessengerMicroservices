using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatClientConsole.Services;

public abstract class BaseApiClient
{
    protected readonly HttpClient _httpClient;
    protected string _jwtToken = string.Empty;

    public BaseApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void SetJwtToken(string token)
    {
        _jwtToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
    }

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string requestUri, TRequest request)
    {
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(requestUri, content);
        response.EnsureSuccessStatusCode();
        return await JsonSerializer.DeserializeAsync<TResponse>(await response.Content.ReadAsStreamAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    protected async Task<TResponse?> GetAsync<TResponse>(string requestUri)
    {
        var response = await _httpClient.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();
        return await JsonSerializer.DeserializeAsync<TResponse>(await response.Content.ReadAsStreamAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    protected async Task<TResponse?> PutAsync<TRequest, TResponse>(string requestUri, TRequest request)
    {
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(requestUri, content);
        response.EnsureSuccessStatusCode();
        return await JsonSerializer.DeserializeAsync<TResponse>(await response.Content.ReadAsStreamAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    protected async Task DeleteAsync(string requestUri)
    {
        var response = await _httpClient.DeleteAsync(requestUri);
        response.EnsureSuccessStatusCode();
    }
}