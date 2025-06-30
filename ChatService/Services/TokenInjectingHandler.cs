using System.Net.Http.Headers;

namespace ChatService.Services;

public class TokenInjectingHandler : DelegatingHandler
{
    private readonly ServiceTokenManager _serviceTokenManager;

    public TokenInjectingHandler(HttpMessageHandler innerHandler, ServiceTokenManager serviceTokenManager)
        : base(innerHandler)
    {
        _serviceTokenManager = serviceTokenManager;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _serviceTokenManager.GetServiceTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}