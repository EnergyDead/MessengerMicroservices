using ChatClientConsole.DTOs.ChatDTOs;

namespace ChatClientConsole.Services;

public class ChatServiceClient : BaseApiClient
{
    public ChatServiceClient(HttpClient httpClient) : base(httpClient)
    {
    }

    public async Task<List<ChatResponse>?> GetUserChats()
    {
        return await GetAsync<List<ChatResponse>>("api/chats");
    }

    public async Task<ChatResponse?> GetChatById(Guid chatId)
    {
        return await GetAsync<ChatResponse>($"api/chats/{chatId}");
    }

    public async Task<ChatResponse?> CreatePersonalChat(CreatePersonalChatRequest request)
    {
        return await PostAsync<CreatePersonalChatRequest, ChatResponse>("api/chats/personal", request);
    }

    public async Task<ChatResponse?> CreateGroupChat(CreateGroupChatRequest request)
    {
        return await PostAsync<CreateGroupChatRequest, ChatResponse>("api/chats/group", request);
    }
}