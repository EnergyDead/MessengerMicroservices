using ChatClientConsole.DTOs.MessageDTOs;

namespace ChatClientConsole.Services;

public class MessageServiceClient : BaseApiClient
{
    public MessageServiceClient(HttpClient httpClient) : base(httpClient)
    {
    }

    public async Task<List<MessageResponse>?> GetChatMessages(Guid chatId)
    {
        return await GetAsync<List<MessageResponse>>($"api/messages/chat/{chatId}");
    }

    public async Task<MessageResponse?> SendMessage(SendMessageRequest request)
    {
        return await PostAsync<SendMessageRequest, MessageResponse>("api/messages", request);
    }

    public async Task MarkMessagesAsRead(ReadMessagesRequest request)
    {
        await PostAsync<ReadMessagesRequest, object>("api/messages/read", request);
    }

    public async Task<MessageResponse?> EditMessage(EditMessageRequest request)
    {
        return await PutAsync<EditMessageRequest, MessageResponse>("api/messages", request);
    }

    public async Task DeleteMessage(DeleteMessageRequest request)
    {
        await DeleteAsync($"api/messages/{request.MessageId}");
    }
}