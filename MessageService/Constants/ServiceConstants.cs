namespace MessageService.Constants;

public static class ServiceConstants
{
    // Имена HTTP-клиентов
    public const string MessageServiceHttpClientName = "MessageServiceApi";
    public const string UserServiceHttpClientName = "UserServiceApi";

    // Базовые пути API для MessageService
    public const string MessageServiceBaseApiPath = "/api/Messages";
    public const string MessageServiceMessagesSincePath = "/messages/since/";
    public const string MessageServiceUsersOnlinePath = "/users/online/";

    public const string ChatServiceHttpClientName = "ChatServiceApi";
    public const string ChatServiceBaseApiPath = "/api/chats";
}