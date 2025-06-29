using Microsoft.AspNetCore.SignalR.Client;
using System.Text;
using System.Text.Json;

namespace ChatClientConsole
{
    public class Program
    {
        private const string UserServiceUrl = "http://localhost:5267";
        private const string ChatServiceUrl = "http://localhost:5000";
        private const string MessageServiceHubUrl = "http://localhost:5240/chathub";
        private const string MessageServiceHttpUrl = "http://localhost:5240";

        private static HubConnection? _connection;
        private static Guid _currentUserId;
        private static Guid _currentChatId;
        private static readonly HttpClient HttpClient = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.WriteLine("--- Консольный клиент SignalR для чата ---");

            await CreateCurrentUser();
            if (_currentUserId == Guid.Empty)
            {
                Console.WriteLine("Не удалось создать пользователя. Завершение.");
                return;
            }

            Console.WriteLine($"Вы вошли как пользователь: {_currentUserId}");

            await ShowMainMenu();

            if (_currentChatId != Guid.Empty)
            {
                await EnterChatScreen();
            }
            else
            {
                Console.WriteLine("Чат не выбран. Завершение.");
            }

            Console.WriteLine("Приложение завершено. Нажмите любую клавишу для выхода...");
            Console.ReadKey();
            await (_connection?.StopAsync() ?? Task.CompletedTask);
        }

        static async Task CreateCurrentUser()
        {
            Console.WriteLine("\nПопытка создать нового пользователя...");
            try
            {
                var newUser = new
                {
                    name = $"User_{Guid.NewGuid().ToString().Substring(0, 4)}",
                    email = $"user_{Guid.NewGuid().ToString().Substring(0, 4)}@example.com"
                };
                var content = new StringContent(JsonSerializer.Serialize(newUser), Encoding.UTF8, "application/json");
                var response = await HttpClient.PostAsync($"{UserServiceUrl}/api/users", content);

                if (response.IsSuccessStatusCode)
                {
                    var userResponse = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<UserResponse>(userResponse, JsonOptions);
                    if (user != null)
                    {
                        _currentUserId = user.Id;
                        Console.WriteLine($"Пользователь успешно создан: ID = {_currentUserId}");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"Ошибка создания пользователя: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Исключение при создании пользователя: {ex.Message}");
                Console.ResetColor();
            }
        }

        static async Task ShowMainMenu()
        {
            while (_currentChatId == Guid.Empty)
            {
                Console.WriteLine("\n--- Главное меню ---");
                Console.WriteLine("1. Создать новый чат (личный/групповой)");
                Console.WriteLine("2. Войти в существующий чат");
                Console.Write("Ваш выбор: ");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await CreateNewChat();
                        break;
                    case "2":
                        await JoinExistingChat();
                        break;
                    default:
                        Console.WriteLine("Неверный выбор. Пожалуйста, введите 1 или 2.");
                        break;
                }
            }
        }

        private static async Task CreateNewChat()
        {
            Console.WriteLine("\n--- Создание нового чата ---");
            Console.WriteLine("1. Личный чат");
            Console.WriteLine("2. Групповой чат");
            Console.Write("Ваш выбор: ");
            var chatTypeChoice = Console.ReadLine();

            try
            {
                HttpResponseMessage? response;
                string requestBody;

                switch (chatTypeChoice)
                {
                    // Личный чат
                    case "1":
                    {
                        Console.Write("Введите ID второго пользователя (GUID): ");
                        var user2IdString = Console.ReadLine() ?? "";
                        if (!Guid.TryParse(user2IdString, out var user2Id))
                        {
                            Console.WriteLine("Некорректный ID пользователя.");
                            return;
                        }

                        requestBody = JsonSerializer.Serialize(new { user1Id = _currentUserId, user2Id });
                        response = await HttpClient.PostAsync($"{ChatServiceUrl}/api/chats/personal",
                            new StringContent(requestBody, Encoding.UTF8, "application/json"));
                        break;
                    }
                    // Групповой чат
                    case "2":
                    {
                        Console.Write("Введите название группы: ");
                        string groupName = Console.ReadLine() ?? "";

                        Console.Write("Введите ID участников через запятую (минимум 2 других пользователя): ");
                        string participantIdsString = Console.ReadLine() ?? "";
                        var participantGuids = participantIdsString.Split(',')
                            .Select(id => Guid.TryParse(id.Trim(), out Guid parsedId) ? parsedId : Guid.Empty)
                            .Where(id =>
                                id != Guid.Empty &&
                                id != _currentUserId) // Исключаем текущего пользователя и некорректные GUID
                            .Distinct()
                            .ToList();

                        participantGuids.Insert(0,
                            _currentUserId); // Добавляем текущего пользователя в список участников

                        if (participantGuids.Count < 3)
                        {
                            Console.WriteLine(
                                "Для группового чата требуется минимум 3 уникальных участника (включая вас).");
                            return;
                        }

                        requestBody = JsonSerializer.Serialize(new
                            { name = groupName, participantIds = participantGuids });
                        response = await HttpClient.PostAsync($"{ChatServiceUrl}/api/chats/group",
                            new StringContent(requestBody, Encoding.UTF8, "application/json"));
                        break;
                    }
                    default:
                        Console.WriteLine("Неверный выбор типа чата.");
                        return;
                }

                if (response?.IsSuccessStatusCode == true)
                {
                    var chatResponse = await response.Content.ReadAsStringAsync();
                    var chat = JsonSerializer.Deserialize<ChatResponse>(chatResponse, JsonOptions);
                    if (chat != null)
                    {
                        _currentChatId = chat.Id;
                        Console.WriteLine($"Чат успешно создан: ID = {_currentChatId}. Тип: {chat.Type}");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"Ошибка создания чата: {response?.StatusCode} - {await response?.Content.ReadAsStringAsync()}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Исключение при создании чата: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static async Task JoinExistingChat()
        {
            Console.Write("Введите ID существующего чата (GUID): ");
            var chatIdString = Console.ReadLine() ?? "";
            if (!Guid.TryParse(chatIdString, out Guid chatId))
            {
                Console.WriteLine("Некорректный Chat ID.");
                return;
            }

            // Опционально: Проверить существование чата через ChatService перед входом
            try
            {
                var response = await HttpClient.GetAsync($"{ChatServiceUrl}/api/chats/{chatId}");
                if (!response.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Чат с ID {chatId} не найден или недоступен.");
                    Console.ResetColor();
                    return;
                }

                var chatInfo =
                    JsonSerializer.Deserialize<ChatResponse>(await response.Content.ReadAsStringAsync(), JsonOptions);
                if (chatInfo != null && !chatInfo.ParticipantIds.Contains(_currentUserId))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Вы не являетесь участником чата {chatId}.");
                    Console.ResetColor();
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка при проверке чата: {ex.Message}");
                Console.ResetColor();
                return;
            }

            _currentChatId = chatId;
            Console.WriteLine($"Вы вошли в чат: {_currentChatId}");
        }

        private static async Task EnterChatScreen()
        {
            Console.WriteLine($"\n--- Вы в чате: {_currentChatId} ---");

            // --- 1. Загружаем историю сообщений ---
            await LoadChatHistory();

            // --- 2. Устанавливаем SignalR соединение ---
            await ConnectToSignalR();
            if (_connection is not { State: HubConnectionState.Connected })
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(
                    "Не удалось установить SignalR соединение. Сообщения в реальном времени не будут работать.");
                Console.ResetColor();
            }
            else
            {
                // Присоединяемся к группе чата после успешного подключения
                await _connection.InvokeAsync("JoinChatGroup", _currentChatId, _currentUserId);
            }

            Console.WriteLine("Введите сообщение (или 'exit' для выхода из чата):");

            string? messageContent;
            do
            {
                Console.Write("> ");
                messageContent = Console.ReadLine();

                if (messageContent?.ToLower() != "exit" && !string.IsNullOrWhiteSpace(messageContent))
                {
                    if (_connection is { State: HubConnectionState.Connected })
                    {
                        try
                        {
                            await _connection.InvokeAsync("SendMessage", _currentChatId, _currentUserId,
                                messageContent);
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Ошибка при отправке сообщения через SignalR: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("SignalR соединение не активно. Сообщение не отправлено в реальном времени.");
                        Console.ResetColor();
                        // Можно добавить логику для сохранения в БД через HTTP, если SignalR недоступен
                    }
                }
            } while (messageContent?.ToLower() != "exit");
        }

        static async Task LoadChatHistory()
        {
            Console.WriteLine("Загрузка истории сообщений...");
            try
            {
                var response = await HttpClient.GetAsync($"{MessageServiceHttpUrl}/api/messages/chat/{_currentChatId}");
                if (response.IsSuccessStatusCode)
                {
                    var messagesJson = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(messagesJson, JsonOptions);

                    if (messages != null && messages.Any())
                    {
                        Console.WriteLine("--- История сообщений ---");
                        foreach (var msg in messages)
                        {
                            Console.WriteLine(
                                $"[{msg.Timestamp.ToLocalTime():HH:mm:ss}] {msg.SenderId.ToString().Substring(0, 8)}: {msg.Content}");
                        }

                        Console.WriteLine("--- Конец истории ---");
                    }
                    else
                    {
                        Console.WriteLine("В этом чате пока нет сообщений.");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"Ошибка загрузки истории: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Исключение при загрузке истории: {ex.Message}");
                Console.ResetColor();
            }
        }

        static async Task ConnectToSignalR()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(MessageServiceHubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<ChatMessage>("ReceiveMessage",
                (message) =>
                {
                    Console.WriteLine(
                        $"\n[{message.Timestamp.ToLocalTime():HH:mm:ss}] {message.SenderId.ToString().Substring(0, 8)}: {message.Content}\n> ");
                });

            _connection.On<string>("ReceiveError", (errorMsg) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ОШИБКА] {errorMsg}\n> ");
                Console.ResetColor();
            });

            _connection.On<string>("ReceiveInfo", (infoMsg) =>
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"\n[ИНФО] {infoMsg}\n> ");
                Console.ResetColor();
            });

            _connection.Closed += async (error) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nСоединение с хабом разорвано: {error?.Message ?? "Неизвестная ошибка"}\n> ");
                Console.ResetColor();
            };

            _connection.Reconnecting += (error) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nПопытка переподключения к хабу...\n> ");
                Console.ResetColor();
                return Task.CompletedTask;
            };

            _connection.Reconnected += async (connectionId) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nПереподключение к хабу успешно. ConnectionId: {connectionId}\n> ");
                Console.ResetColor();
                // После переподключения, нужно снова присоединиться к группе чата
                if (_connection.State == HubConnectionState.Connected && _currentChatId != Guid.Empty)
                {
                    await _connection.InvokeAsync("JoinChatGroup", _currentChatId, _currentUserId);
                }
            };

            try
            {
                await _connection.StartAsync();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SignalR соединение установлено.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка при запуске SignalR соединения: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    // DTO для ответа от UserService при создании пользователя
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
    }

    // DTO для ответа от ChatService при создании/получении чата
    public class ChatResponse
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = null!;
        public string? Name { get; set; }
        public List<Guid> ParticipantIds { get; set; } = [];
    }

    // DTO для сообщений, отправляемых и получаемых через SignalR
    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid ChatId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = null!;
        public DateTimeOffset Timestamp { get; set; }
    }
}