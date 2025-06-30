using ChatService.DTOs;

namespace ChatService.Services
{
    public class ServiceTokenManager
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private string? _serviceToken;
        private DateTimeOffset _tokenExpiresAt;

        public ServiceTokenManager(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private HttpClient? _userServiceHttpClientInstance;
        private HttpClient GetUserServiceHttpClient()
        {
            if (_userServiceHttpClientInstance == null)
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(_configuration["ServiceUrls:UserServiceUrl"] ?? throw new InvalidOperationException("UserServiceUrl is not configured"));
                _userServiceHttpClientInstance = client;
            }
            return _userServiceHttpClientInstance;
        }


        public async Task<string?> GetServiceTokenAsync()
        {
            if (string.IsNullOrEmpty(_serviceToken) || _tokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
            {
                await RequestNewServiceTokenAsync();
            }
            return _serviceToken;
        }

        private async Task RequestNewServiceTokenAsync()
        {
            var serviceName = _configuration["ServiceAuth:ServiceName"];
            var serviceSecret = _configuration["ServiceAuth:ServiceSecret"];

            if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(serviceSecret))
            {
                return;
            }

            try
            {
                var request = new ServiceLoginRequest { ServiceName = serviceName, ServiceSecret = serviceSecret };
                var clientToAuthService = GetUserServiceHttpClient();

                var response = await clientToAuthService.PostAsJsonAsync("api/ServiceAuth/service-login", request);

                response.EnsureSuccessStatusCode();

                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (authResponse != null && !string.IsNullOrEmpty(authResponse.Token))
                {
                    _serviceToken = authResponse.Token;
                    _tokenExpiresAt = authResponse.Expires;
                }
            }
            catch (Exception)
            {
                _serviceToken = null;
            }
        }
    }
}