using Microsoft.Extensions.Configuration;
using ChatClientConsole;
using ChatClientConsole.DTOs.AuthDTOs;
using ChatClientConsole.DTOs.ChatDTOs;
using ChatClientConsole.DTOs.MessageDTOs;
using ChatClientConsole.Services;
using Microsoft.AspNetCore.SignalR.Client;

public class Program
{
    private static AppSettings _appSettings = new();
    private static UserServiceClient _userServiceClient = default!;
    private static ChatServiceClient _chatServiceClient = default!;
    private static MessageServiceClient _messageServiceClient = default!;

    private static AuthResponse? _currentUser = null;
    private static Dictionary<Guid, string> _userNamesCache = new();

    private static Dictionary<Guid, ChatResponse>
        _userChats = new();

    private static Guid _currentChatId = Guid.Empty;
    private static Guid? _lastSentMessageId;

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("Запуск Messenger Console Client...");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();
        var configuration = builder.Build();
        configuration.Bind(_appSettings);

        var userServiceHttpClient = new HttpClient { BaseAddress = new Uri(_appSettings.ApiSettings.UserServiceUrl) };
        var chatServiceHttpClient = new HttpClient { BaseAddress = new Uri(_appSettings.ApiSettings.ChatServiceUrl) };
        var messageServiceHttpClient = new HttpClient
            { BaseAddress = new Uri(_appSettings.ApiSettings.MessageServiceUrl) };

        _userServiceClient = new UserServiceClient(userServiceHttpClient);
        _chatServiceClient = new ChatServiceClient(chatServiceHttpClient);
        _messageServiceClient = new MessageServiceClient(messageServiceHttpClient);

        await AuthenticateUserLoop();

        if (_currentUser != null)
        {
            Console.Clear(); // Очищаем консоль после логина
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nДобро пожаловать, {_currentUser.Username}! (ID: {_currentUser.UserId})");
            Console.ResetColor();

            _userNamesCache[_currentUser.UserId] = _currentUser.Username;

            await MainChatLoop();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nВыход из приложения. Аутентификация не удалась.");
            Console.ResetColor();
        }

        Console.WriteLine("\nНажмите любую клавишу для выхода.");
        Console.ReadKey();
    }

    private static async Task AuthenticateUserLoop()
    {
        while (_currentUser == null)
        {
            Console.WriteLine("\n--- Аутентификация ---");
            Console.WriteLine("1. Войти");
            Console.WriteLine("2. Зарегистрировать новый аккаунт");
            Console.WriteLine("3. Выход");
            Console.Write("Выберите опцию: ");
            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await HandleLogin();
                        break;
                    case "2":
                        await HandleRegister();
                        break;
                    case "3":
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Неверный выбор. Пожалуйста, попробуйте снова.");
                        Console.ResetColor();
                        break;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(
                    $"Ошибка API: {httpEx.Message}. Проверьте, запущены ли бэкенд-сервисы и корректны ли URL.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Произошла непредвиденная ошибка: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    private static async Task HandleLogin()
    {
        Console.WriteLine("\n--- Вход ---");
        Console.Write("Введите Email: ");
        var email = Console.ReadLine() ?? string.Empty;
        Console.Write("Введите Пароль: ");
        var password = GetPasswordInput(); // Пользовательский метод для скрытия пароля

        var loginRequest = new LoginRequest { Email = email, Password = password };
        var authResponse = await _userServiceClient.Login(loginRequest);

        if (authResponse != null && !string.IsNullOrEmpty(authResponse.Token))
        {
            _currentUser = authResponse;
            // Устанавливаем токен для всех клиентов, чтобы они могли делать авторизованные запросы
            _userServiceClient.SetJwtToken(_currentUser.Token);
            _chatServiceClient.SetJwtToken(_currentUser.Token);
            _messageServiceClient.SetJwtToken(_currentUser.Token);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Вход не удался. Неверные учетные данные или ошибка сервера.");
            Console.ResetColor();
        }
    }

    private static async Task HandleRegister()
    {
        Console.WriteLine("\n--- Регистрация нового аккаунта ---");
        Console.Write("Введите Имя пользователя: ");
        var username = Console.ReadLine() ?? string.Empty;
        Console.Write("Введите Email: ");
        var email = Console.ReadLine() ?? string.Empty;
        Console.Write("Введите Пароль: ");
        var password = GetPasswordInput(); // Пользовательский метод для скрытия пароля

        var registerRequest = new RegisterRequest { Username = username, Email = email, Password = password };
        var authResponse = await _userServiceClient.Register(registerRequest);

        if (authResponse != null && !string.IsNullOrEmpty(authResponse.Token))
        {
            _currentUser = authResponse;
            // Устанавливаем токен для всех клиентов
            _userServiceClient.SetJwtToken(_currentUser.Token);
            _chatServiceClient.SetJwtToken(_currentUser.Token);
            _messageServiceClient.SetJwtToken(_currentUser.Token);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Регистрация не удалась. Email/имя пользователя могут быть заняты или ошибка сервера.");
            Console.ResetColor();
        }
    }

    private static string GetPasswordInput()
    {
        var pass = new System.Text.StringBuilder();
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(true);
            if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
            {
                pass.Append(key.KeyChar);
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
            {
                pass.Remove(pass.Length - 1, 1);
                Console.Write("\b \b"); // Стираем звездочку
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine(); // Новая строка после Enter
        return pass.ToString();
    }

    private static async Task MainChatLoop()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("--- Ваши Чаты ---");

            await LoadUserChats();

            if (_userChats.Count != 0)
            {
                var chatIndex = 1;
                foreach (var chatEntry in _userChats)
                {
                    var chat = chatEntry.Value;
                    var chatDisplayName = chat.Type == ChatType.Personal.ToString()
                        ? await GetPersonalChatDisplayName(chat)
                        : chat.Name ?? "Групповой чат";
                    Console.WriteLine($"{chatIndex++}. {chatDisplayName} (ID: {chat.Id})");
                }
            }
            else
            {
                Console.WriteLine("У вас пока нет чатов.");
            }

            Console.WriteLine("\n--- Опции ---");
            Console.WriteLine("C. Создать новый чат");
            Console.WriteLine("V. Войти в чат (по номеру)");
            Console.WriteLine("L. Выйти (Log out)");
            Console.Write("Ваш выбор: ");

            var choice = Console.ReadLine()?.ToUpper();

            switch (choice)
            {
                case "C":
                    await CreateNewChat();
                    break;
                case "V":
                    await EnterChat();
                    break;
                case "L":
                    _currentUser = null;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Вы успешно вышли из аккаунта.");
                    Console.ResetColor();
                    return;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Неверный выбор. Пожалуйста, попробуйте снова.");
                    Console.ResetColor();
                    await Task.Delay(1000);
                    break;
            }
        }
    }

    private static async Task LoadUserChats()
    {
        try
        {
            var chats = await _chatServiceClient.GetUserChats();
            _userChats.Clear();
            if (chats != null)
            {
                foreach (var chat in chats)
                {
                    _userChats[chat.Id] = chat;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка при загрузке чатов: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task<string> GetPersonalChatDisplayName(ChatResponse chat)
    {
        if (chat.Type != ChatType.Personal.ToString() || !chat.ParticipantIds.Any())
        {
            return "Неизвестный личный чат";
        }

        var otherParticipantId = chat.ParticipantIds.FirstOrDefault(id => id != _currentUser?.UserId);

        if (otherParticipantId == Guid.Empty)
        {
            return "Чат с самим собой или нет другого участника";
        }

        if (_userNamesCache.TryGetValue(otherParticipantId, out var username))
        {
            return username;
        }
        else
        {
            try
            {
                var user = await _userServiceClient.GetUserById(otherParticipantId);
                if (user != null)
                {
                    _userNamesCache[user.Id] = user.Username;
                    return user.Username;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка при получении имени пользователя для {otherParticipantId}: {ex.Message}");
                Console.ResetColor();
            }
        }

        return "Неизвестный пользователь";
    }

    private static async Task CreateNewChat()
    {
        Console.WriteLine("\n--- Создать новый чат ---");
        Console.WriteLine("1. Личный чат (по Email)");
        Console.WriteLine("2. Групповой чат");
        Console.WriteLine("B. Назад");
        Console.Write("Ваш выбор: ");

        var choice = Console.ReadLine()?.ToUpper();

        switch (choice)
        {
            case "1":
                await CreatePersonalChat();
                break;
            case "2":
                await CreateGroupChat();
                break;
            case "B":
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Неверный выбор.");
                Console.ResetColor();
                await Task.Delay(1000);
                break;
        }
    }

    private static async Task CreatePersonalChat()
    {
        Console.Write("Введите Email пользователя, с которым хотите создать личный чат: ");
        var recipientEmail = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(recipientEmail))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Email не может быть пустым.");
            Console.ResetColor();
            await Task.Delay(1000);
            return;
        }

        if (recipientEmail.Equals(_currentUser?.Email, StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Нельзя создать личный чат с самим собой.");
            Console.ResetColor();
            await Task.Delay(1000);
            return;
        }

        try
        {
            var recipient = await _userServiceClient.GetUserByEmail(recipientEmail);
            if (recipient == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Пользователь с Email '{recipientEmail}' не найден.");
                Console.ResetColor();
                await Task.Delay(1000);
                return;
            }

            var createRequest = new CreatePersonalChatRequest
            {
                User1Id = _currentUser!.UserId,
                User2Id = recipient.Id
            };

            var newChat = await _chatServiceClient.CreatePersonalChat(createRequest);
            if (newChat != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Личный чат с {recipient.Username} успешно создан (ID: {newChat.Id}).");
                Console.ResetColor();
                _userChats[newChat.Id] = newChat;
                await Task.Delay(2000);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Не удалось создать личный чат.");
                Console.ResetColor();
                await Task.Delay(1000);
            }
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Личный чат с этим пользователем уже существует.");
            Console.ResetColor();
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка при создании личного чата: {ex.Message}");
            Console.ResetColor();
            await Task.Delay(2000);
        }
    }

    private static async Task CreateGroupChat()
    {
        Console.Write("Введите имя группового чата: ");
        var chatName = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(chatName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Имя чата не может быть пустым.");
            Console.ResetColor();
            await Task.Delay(1000);
            return;
        }

        Console.WriteLine("Введите Email-ы участников, разделенные запятыми (вы будете добавлены автоматически):");
        var participantEmailsInput = Console.ReadLine()?.Trim();
        var participantEmails =
            participantEmailsInput?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList() ?? new List<string>();

        var participantIds = new List<Guid>();
        if (_currentUser != null)
        {
            participantIds.Add(_currentUser.UserId);
        }

        foreach (var email in participantEmails.Distinct())
        {
            try
            {
                var user = await _userServiceClient.GetUserByEmail(email);
                if (user != null)
                {
                    participantIds.Add(user.Id);
                    Console.WriteLine($"Добавлен участник: {user.Username} ({user.Email})");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Внимание: Пользователь с Email '{email}' не найден и не будет добавлен.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Внимание: Ошибка при разрешении Email '{email}': {ex.Message}");
                Console.ResetColor();
            }
        }

        participantIds = participantIds.Distinct().ToList(); // Удаляем дубликаты, если есть

        if (!participantIds.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Необходимо добавить хотя бы одного участника (кроме себя) для группового чата.");
            Console.ResetColor();
            await Task.Delay(2000);
            return;
        }

        try
        {
            var createRequest = new CreateGroupChatRequest
            {
                Name = chatName,
                ParticipantIds = participantIds
            };

            var newChat = await _chatServiceClient.CreateGroupChat(createRequest);
            if (newChat != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Групповой чат '{newChat.Name}' успешно создан (ID: {newChat.Id}).");
                Console.ResetColor();
                _userChats[newChat.Id] = newChat; // Добавляем в локальный кэш
                await Task.Delay(2000);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Не удалось создать групповой чат.");
                Console.ResetColor();
                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка при создании группового чата: {ex.Message}");
            Console.ResetColor();
            await Task.Delay(2000);
        }
    }


    private static async Task EnterChat()
    {
        Console.Write("Введите номер чата для входа: ");
        if (int.TryParse(Console.ReadLine(), out int chatIndex) && chatIndex > 0 && chatIndex <= _userChats.Count)
        {
            var selectedChatEntry = _userChats.ElementAt(chatIndex - 1);
            _currentChatId = selectedChatEntry.Key;

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Yellow;
            string chatName = selectedChatEntry.Value.Type == ChatType.Personal.ToString()
                ? await GetPersonalChatDisplayName(selectedChatEntry.Value)
                : selectedChatEntry.Value.Name ?? "Групповой чат";
            Console.WriteLine($"--- Чат: {chatName} (ID: {_currentChatId}) ---");
            Console.ResetColor();

            await ChatRoomLoop();
            _currentChatId = Guid.Empty;
            _lastSentMessageId = null;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Неверный номер чата.");
            Console.ResetColor();
            await Task.Delay(1000);
        }
    }

    private static async Task ChatRoomLoop()
    {
        var chatHistory = new List<MessageResponse>();

        await LoadChatHistory(chatHistory);

        var hubConnection = await ConnectToSignalR(chatHistory);

        var inputTask = Task.Run(async () =>
        {
            while (_currentChatId != Guid.Empty)
            {
                Console.Write("\nВведите сообщение (или '/exit', '/edit', '/delete'): ");
                var input = Console.ReadLine();

                if (input?.ToLower() == "/exit")
                {
                    break;
                }
                else if (input?.ToLower() == "/edit")
                {
                    await HandleEditMessage(hubConnection);
                }
                else if (input?.ToLower() == "/delete")
                {
                    await HandleDeleteMessage(hubConnection);
                }
                else if (!string.IsNullOrWhiteSpace(input))
                {
                    await SendMessage(hubConnection, input);
                }
            }
        });

        var signalRReceiveTask = Task.Run(async () =>
        {
            while (_currentChatId != Guid.Empty)
            {
                await Task.Delay(100);
            }
        });

        await Task.WhenAny(inputTask, signalRReceiveTask);

        try
        {
            await hubConnection.StopAsync();
        }
        catch
        {
            /* ignore */
        }

        hubConnection.Remove("ReceiveMessage");
        hubConnection.Remove("MessageEdited");
        hubConnection.Remove("MessageDeleted");
        hubConnection.Remove("UserStatusChanged");
    }

    private static async Task<HubConnection> ConnectToSignalR(List<MessageResponse> chatHistory)
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(_appSettings.ApiSettings.MessageServiceUrl + "chathub",
                options => { options.AccessTokenProvider = () => Task.FromResult(_currentUser?.Token); })
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<MessageResponse>("ReceiveMessage", async (message) =>
        {
            if (message.ChatId == _currentChatId)
            {
                await DisplayMessage(message);
            }
        });

        hubConnection.On<Guid, string>("MessageEdited", (messageId, newContent) =>
        {
            if (chatHistory.FirstOrDefault(m => m.Id == messageId)?.ChatId == _currentChatId)
            {
                var msg = chatHistory.FirstOrDefault(m => m.Id == messageId);
                if (msg != null)
                {
                    msg.Content = newContent;
                    msg.IsEdited = true;
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"\n[Уведомление]: Сообщение от {msg.SenderUsername} изменено: \"{newContent}\"");
                    Console.ResetColor();
                }
            }

            return Task.CompletedTask;
        });

        hubConnection.On<Guid>("MessageDeleted", (messageId) =>
        {
            if (chatHistory.FirstOrDefault(m => m.Id == messageId)?.ChatId == _currentChatId)
            {
                var msg = chatHistory.FirstOrDefault(m => m.Id == messageId);
                if (msg != null)
                {
                    msg.IsDeleted = true;
                    msg.Content = "[Сообщение удалено]";
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"\n[Уведомление]: Сообщение от {msg.SenderUsername} удалено.");
                    Console.ResetColor();
                }
            }
        });

        hubConnection.On<Guid, bool>("UserStatusChanged", async (userId, isOnline) =>
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            string username = await GetUserName(userId);
            Console.WriteLine($"\n[Уведомление]: Пользователь {username} теперь {(isOnline ? "онлайн" : "офлайн")}.");
            Console.ResetColor();
        });

        hubConnection.On<Guid, List<Guid>>("MessagesRead", (chatId, _) =>
        {
            if (chatId != _currentChatId) return;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"\n[Уведомление]: Несколько сообщений в этом чате были прочитаны.");
            Console.ResetColor();
        });


        try
        {
            await hubConnection.StartAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Подключено к SignalR хабу.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Не удалось подключиться к SignalR хабу: {ex.Message}");
            Console.ResetColor();
        }

        return hubConnection;
    }

    private static async Task LoadChatHistory(List<MessageResponse> chatHistory)
    {
        try
        {
            var messages = await _messageServiceClient.GetChatMessages(_currentChatId);
            chatHistory.Clear();
            if (messages != null)
            {
                chatHistory.AddRange(messages.OrderBy(m => m.Timestamp));
                foreach (var message in chatHistory)
                {
                    await DisplayMessage(message);
                }
            }
            else
            {
                Console.WriteLine("В этом чате пока нет сообщений.");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка при загрузке истории чата: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task SendMessage(HubConnection hubConnection, string content)
    {
        if (hubConnection.State == HubConnectionState.Connected)
        {
            try
            {
                
                var message = await hubConnection.InvokeAsync<MessageResponse>("SendMessage", new SendMessageRequest() { ChatId = _currentChatId, Content = content});
                _lastSentMessageId = message.Id;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("SignalR соединение не установлено. Не могу отправить сообщение.");
            Console.ResetColor();
        }
    }

    private static async Task HandleEditMessage(HubConnection hubConnection)
    {
        if (_lastSentMessageId == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Нет последнего отправленного вами сообщения для редактирования.");
            Console.ResetColor();
            return;
        }

        Console.Write("Введите новый текст сообщения: ");
        var newContent = Console.ReadLine() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newContent))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Новое содержимое сообщения не может быть пустым.");
            Console.ResetColor();
            return;
        }

        try
        {
            await hubConnection.InvokeAsync("EditMessage", _lastSentMessageId.Value, newContent);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Запрос на редактирование отправлен.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка при редактировании сообщения: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task HandleDeleteMessage(HubConnection hubConnection)
    {
        if (_lastSentMessageId == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Нет последнего отправленного вами сообщения для удаления.");
            Console.ResetColor();
            return;
        }

        Console.Write("Вы уверены, что хотите удалить последнее сообщение? (y/n): ");
        var confirmation = Console.ReadLine()?.ToLower();
        if (confirmation == "y")
        {
            try
            {
                await hubConnection.InvokeAsync("DeleteMessage", _lastSentMessageId.Value);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Запрос на удаление отправлен.");
                Console.ResetColor();
                _lastSentMessageId = null;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка при удалении сообщения: {ex.Message}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine("Удаление отменено.");
        }
    }

    private static async Task DisplayMessage(MessageResponse message)
    {
        string senderName = await GetUserName(message.SenderId);

        Console.ForegroundColor = ConsoleColor.White;

        if (message.IsDeleted)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[{message.Timestamp:HH:mm}] {senderName}: [Сообщение удалено]");
        }
        else if (message.IsEdited)
        {
            Console.WriteLine($"[{message.Timestamp:HH:mm}] {senderName} (ред.): {message.Content}");
        }
        else
        {
            Console.WriteLine($"[{message.Timestamp:HH:mm}] {senderName}: {message.Content}");
        }

        Console.ResetColor(); // Сброс цвета после вывода
    }

    private static async Task<string> GetUserName(Guid userId)
    {
        if (_userNamesCache.TryGetValue(userId, out var username))
        {
            return username;
        }

        try
        {
            var user = await _userServiceClient.GetUserById(userId);
            if (user != null)
            {
                _userNamesCache[userId] = user.Username;
                return user.Username;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Внимание: Не удалось получить имя пользователя для ID {userId}: {ex.Message}");
            Console.ResetColor();
        }

        return userId.ToString().Substring(0, 8); // Возвращаем часть GUID, если имя не найдено
    }
}