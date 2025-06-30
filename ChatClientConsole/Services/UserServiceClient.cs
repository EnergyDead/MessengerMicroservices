using ChatClientConsole.DTOs.AuthDTOs;

namespace ChatClientConsole.Services;

public class UserServiceClient : BaseApiClient
{
    public UserServiceClient(HttpClient httpClient) : base(httpClient)
    {
    }

    public async Task<AuthResponse?> Register(RegisterRequest request)
    {
        return await PostAsync<RegisterRequest, AuthResponse>("api/users/register", request);
    }

    public async Task<AuthResponse?> Login(LoginRequest request)
    {
        return await PostAsync<LoginRequest, AuthResponse>("api/users/login", request);
    }

    public async Task<UserResponse?> GetUserByEmail(string email)
    {
        return await GetAsync<UserResponse>($"api/users/by-email/{email}");
    }

    public async Task<UserResponse?> GetUserById(Guid userId)
    {
        return await GetAsync<UserResponse>($"api/users/{userId}");
    }
}