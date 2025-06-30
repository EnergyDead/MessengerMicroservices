using Microsoft.AspNetCore.SignalR.Client;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace ChatClientConsole
{
    public class Program
    {
        private const string UserServiceUrl = "http://localhost:5267";
        private const string ChatServiceUrl = "http://localhost:5000";
        private const string MessageServiceHubUrl = "http://localhost:5240/chathub";
        private const string MessageServiceHttpUrl = "http://localhost:5240";
        private const string NotificationServiceUrl = "http://localhost:5070";

        private static HubConnection? _connection;
        private static Guid _currentUserId;
        private static Guid _currentChatId;
        private static readonly HttpClient HttpClient = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static readonly ConcurrentDictionary<Guid, ChatMessage> _currentChatMessages = new();

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.WriteLine("--- Консольный клиент SignalR для чата ---");

            await ChooseUserAction();

            if (_currentUserId == Guid.Empty)
            {
                Console.WriteLine("Не удалось войти или создать пользователя. Завершение.");
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

        static async Task ChooseUserAction()
        {
            while (_currentUserId == Guid.Empty)
            {
                Console.WriteLine("\n--- Действие пользователя ---");
                Console.WriteLine("1. Создать нового пользователя");
                Console.WriteLine("2. Войти по существующему GUID пользователя");
                Console.Write("Ваш выбор: ");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await CreateNewUser();
                        break;
                    case "2":
                        await LoginExistingUser();
                        break;
                    default:
                        Console.WriteLine("Неверный выбор. Пожалуйста, введите 1 или 2.");
                        break;
                }
            }
        }

        static async Task CreateNewUser()
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

        static async Task LoginExistingUser()
        {
            Console.Write("\nВведите GUID существующего пользователя: ");
            var userIdString = Console.ReadLine();

            if (Guid.TryParse(userIdString, out var userId))
            {
                try
                {
                    var response = await HttpClient.GetAsync($"{UserServiceUrl}/api/users/{userId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var userResponse = await response.Content.ReadAsStringAsync();
                        var user = JsonSerializer.Deserialize<UserResponse>(userResponse, JsonOptions);
                        if (user != null)
                        {
                            _currentUserId = user.Id;
                            Console.WriteLine($"Успешно вошли как пользователь: ID = {_currentUserId}, Имя = {user.Name}");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Не удалось десериализовать данные пользователя.");
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Ошибка входа: Пользователь с ID {userId} не найден или недоступен. Код: {response.StatusCode}");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Исключение при попытке входа: {ex.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Некорректный формат GUID.");
                Console.ResetColor();
            }
        }


        static async Task ShowMainMenu()
        {
            while (_currentChatId == Guid.Empty)
            {
                Console.WriteLine("\n--- Главное меню ---");
                Console.WriteLine("1. Создать новый чат (личный/групповой)");
                Console.WriteLine("2. Войти в существующий чат по ID");
                Console.WriteLine("3. Показать мои чаты и выбрать");
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
                    case "3":
                        await ViewExistingChatsAndJoin();
                        break;
                    default:
                        Console.WriteLine("Неверный выбор. Пожалуйста, введите 1, 2 или 3.");
                        break;
                }
            }
        }

        private static async Task ViewExistingChatsAndJoin()
        {
            Console.WriteLine("\n--- Мои существующие чаты ---");
            try
            {
                var response = await HttpClient.GetAsync($"{ChatServiceUrl}/api/chats/user/{_currentUserId}");
                if (response.IsSuccessStatusCode)
                {
                    var chatsJson = await response.Content.ReadAsStringAsync();
                    var chats = JsonSerializer.Deserialize<List<ChatResponse>>(chatsJson, JsonOptions);

                    if (chats == null || !chats.Any())
                    {
                        Console.WriteLine("У вас пока нет чатов.");
                        return;
                    }

                    Console.WriteLine("Список ваших чатов:");
                    for (int i = 0; i < chats.Count; i++)
                    {
                        var chat = chats[i];
                        string chatName = chat.Name ?? "Личный чат"; // Отображаем имя или "Личный чат"
                        Console.WriteLine($"{i + 1}. ID: {chat.Id} | Тип: {chat.Type} | Имя: {chatName}");
                    }

                    Console.Write("Введите номер чата для входа (или '0' для отмены): ");
                    var choice = Console.ReadLine();
                    if (int.TryParse(choice, out int chatIndex) && chatIndex > 0 && chatIndex <= chats.Count)
                    {
                        var selectedChat = chats[chatIndex - 1];
                        await TryJoinChat(selectedChat.Id);
                    }
                    else if (choice != "0")
                    {
                        Console.WriteLine("Неверный ввод.");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"Ошибка загрузки чатов: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Исключение при получении списка чатов: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static async Task TryJoinChat(Guid chatId)
        {
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
                    case "1":
                        {
                            Console.Write("Введите ID второго пользователя (GUID): ");
                            var user2IdString = Console.ReadLine() ?? "";
                            if (!Guid.TryParse(user2IdString, out var user2Id))
                            {
                                Console.WriteLine("Некорректный ID пользователя.");
                                return;
                            }

                            // Дополнительная проверка: существует ли второй пользователь
                            if (!await CheckUserExists(user2Id))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Пользователь с ID {user2Id} не существует.");
                                Console.ResetColor();
                                return;
                            }

                            requestBody = JsonSerializer.Serialize(new { user1Id = _currentUserId, user2Id });
                            response = await HttpClient.PostAsync($"{ChatServiceUrl}/api/chats/personal",
                                new StringContent(requestBody, Encoding.UTF8, "application/json"));
                            break;
                        }
                    case "2":
                        {
                            Console.Write("Введите название группы: ");
                            string groupName = Console.ReadLine() ?? "";

                            Console.Write("Введите ID участников через запятую (минимум 2 других пользователя): ");
                            string participantIdsString = Console.ReadLine() ?? "";
                            var participantGuids = new List<Guid>();

                            foreach (var idStr in participantIdsString.Split(','))
                            {
                                if (Guid.TryParse(idStr.Trim(), out Guid parsedId))
                                {
                                    participantGuids.Add(parsedId);
                                }
                            }

                            participantGuids = participantGuids.Where(id => id != _currentUserId).Distinct().ToList();
                            participantGuids.Insert(0, _currentUserId);

                            foreach (var pId in participantGuids)
                            {
                                if (!await CheckUserExists(pId))
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"Участник с ID {pId} не существует. Создание чата отменено.");
                                    Console.ResetColor();
                                    return;
                                }
                            }

                            if (participantGuids.Count < 2)
                            {
                                Console.WriteLine(
                                    "Для группового чата требуется минимум 2 уникальных участника (включая вас).");
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

            await LoadChatHistory();
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
                await _connection.InvokeAsync("JoinChatGroup", _currentChatId, _currentUserId);
            }

            Console.WriteLine("Введите сообщение (или 'menu' для действий, 'exit' для выхода из чата):");

            string? input;
            do
            {
                Console.Write("> ");
                input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) continue;

                if (input.ToLower() == "exit")
                {
                    break;
                }
                else if (input.ToLower() == "menu")
                {
                    await ShowChatActionsMenu();
                    Console.WriteLine("Введите сообщение (или 'menu' для действий, 'exit' для выхода из чата):");
                }
                else
                {
                    if (_connection is { State: HubConnectionState.Connected })
                    {
                        try
                        {
                            await _connection.InvokeAsync("SendMessage", _currentChatId, _currentUserId,
                                input);
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
                    }
                }
            } while (true);
        }

        static async Task ConnectToSignalR()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(MessageServiceHubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<ChatMessage>("ReceiveMessage", (message) =>
            {
                _currentChatMessages.TryAdd(message.Id, message);
                DisplayMessage(message);
                Console.Write("> ");
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

            _connection.On<Guid, string, DateTimeOffset, bool>("MessageEdited", (messageId, newContent, timestamp, isEdited) =>
            {
                if (_currentChatMessages.TryGetValue(messageId, out var existingMessage))
                {
                    existingMessage.Content = newContent;
                    existingMessage.Timestamp = timestamp; // Обновляем метку времени, т.к. хаб обновляет её
                    existingMessage.IsEdited = isEdited;
                    Console.WriteLine($"\n[ОБНОВЛЕНО] {existingMessage.SenderId.ToString().Substring(0, 8)}: {existingMessage.Content} (ред. {existingMessage.Timestamp.ToLocalTime():HH:mm:ss})\n> ");
                }
                else
                {
                    Console.WriteLine($"\n[ОБНОВЛЕНО] Сообщение ID {messageId} было отредактировано: {newContent}\n> ");
                }
            });

            _connection.On<Guid, string, DateTimeOffset, bool>("MessageDeleted", (messageId, newContent, timestamp, isDeleted) =>
            {
                if (_currentChatMessages.TryGetValue(messageId, out var existingMessage))
                {
                    existingMessage.Content = newContent; // Например, "[Сообщение удалено]"
                    existingMessage.Timestamp = timestamp;
                    existingMessage.IsDeleted = isDeleted;
                    Console.WriteLine($"\n[УДАЛЕНО] Сообщение {existingMessage.SenderId.ToString().Substring(0, 8)}: {existingMessage.Content}\n> ");
                }
                else
                {
                    Console.WriteLine($"\n[УДАЛЕНО] Сообщение ID {messageId} было удалено.\n> ");
                }
            });

            _connection.On<Guid, bool>("UserStatusChanged", (userId, isOnline) =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                string status = isOnline ? "онлайн" : "оффлайн";
                Console.WriteLine($"\n[СТАТУС] Пользователь {userId.ToString().Substring(0, 8)} теперь {status}\n> ");
                Console.ResetColor();
            });

            _connection.On<Guid, Guid, List<Guid>>("MessagesRead", (readerId, chatId, messageIds) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[ПРОЧИТАНО] Пользователь {readerId.ToString().Substring(0, 8)} прочитал сообщения в чате {chatId}: {string.Join(", ", messageIds.Select(id => id.ToString().Substring(0, 8)))}\n> ");
                Console.ResetColor();

                foreach (var msgId in messageIds)
                {
                    if (_currentChatMessages.TryGetValue(msgId, out var msg))
                    {
                        msg.IsRead = true;
                    }
                }
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

                    _currentChatMessages.Clear();

                    if (messages != null && messages.Any())
                    {
                        Console.WriteLine("--- История сообщений ---");
                        foreach (var msg in messages.OrderBy(m => m.Timestamp))
                        {
                            _currentChatMessages.TryAdd(msg.Id, msg);
                            DisplayMessage(msg);
                        }
                        Console.WriteLine("--- Конец истории ---");

                        await MarkAllMessagesAsRead(_currentChatId, _currentUserId);
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

        private static void DisplayMessage(ChatMessage msg)
        {
            string status = msg.IsEdited ? " (ред.)" : msg.IsDeleted ? " (уд.)" : "";
            string readStatus = msg.IsRead ? " (прочитано)" : "";
            string senderPrefix = msg.SenderId == _currentUserId ? "[Я]" : $"[{msg.SenderId.ToString().Substring(0, 8)}]";
            Console.WriteLine(
                $"{senderPrefix} [{msg.Timestamp.ToLocalTime():HH:mm:ss}] {msg.Content}{status}{readStatus}");
        }

        private static async Task ShowChatActionsMenu()
        {
            Console.WriteLine("\n--- Действия в чате ---");
            Console.WriteLine("1. Редактировать сообщение");
            Console.WriteLine("2. Удалить сообщение");
            Console.WriteLine("3. Пометить все сообщения прочитанными");
            Console.WriteLine("4. Проверить онлайн статус пользователя");
            Console.WriteLine("0. Назад в чат");
            Console.Write("Ваш выбор: ");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    await EditChatMessage();
                    break;
                case "2":
                    await DeleteChatMessage();
                    break;
                case "3":
                    await MarkAllMessagesAsRead(_currentChatId, _currentUserId);
                    break;
                case "4":
                    await CheckUserOnlineStatus();
                    break;
                case "0":
                    Console.WriteLine("Возврат в чат.");
                    break;
                default:
                    Console.WriteLine("Неверный выбор.");
                    break;
            }
        }

        private static async Task EditChatMessage()
        {
            Console.Write("Введите ID сообщения для редактирования: ");
            var messageIdString = Console.ReadLine();
            if (!Guid.TryParse(messageIdString, out var messageId))
            {
                Console.WriteLine("Некорректный ID сообщения.");
                return;
            }

            if (!_currentChatMessages.ContainsKey(messageId))
            {
                Console.WriteLine("Сообщение с таким ID не найдено в текущем чате.");
                return;
            }

            Console.Write("Введите новый текст сообщения: ");
            var newContent = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(newContent))
            {
                Console.WriteLine("Содержимое сообщения не может быть пустым.");
                return;
            }

            if (_connection is { State: HubConnectionState.Connected })
            {
                try
                {
                    await _connection.InvokeAsync("EditMessage", _currentChatId, messageId, newContent, _currentUserId);
                    Console.WriteLine($"Запрос на редактирование сообщения {messageId} отправлен.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Ошибка при отправке запроса на редактирование: {ex.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("SignalR соединение не активно. Редактирование невозможно.");
                Console.ResetColor();
            }
        }

        private static async Task DeleteChatMessage()
        {
            Console.Write("Введите ID сообщения для удаления: ");
            var messageIdString = Console.ReadLine();
            if (!Guid.TryParse(messageIdString, out var messageId))
            {
                Console.WriteLine("Некорректный ID сообщения.");
                return;
            }

            if (!_currentChatMessages.ContainsKey(messageId))
            {
                Console.WriteLine("Сообщение с таким ID не найдено в текущем чате.");
                return;
            }
            
            Console.Write($"Вы уверены, что хотите удалить сообщение {messageId.ToString().Substring(0, 8)}? (да/нет): ");
            var confirmation = Console.ReadLine()?.ToLower();
            if (confirmation != "да")
            {
                Console.WriteLine("Удаление отменено.");
                return;
            }

            if (_connection is { State: HubConnectionState.Connected })
            {
                try
                {
                    await _connection.InvokeAsync("DeleteMessage", _currentChatId, messageId, _currentUserId);
                    Console.WriteLine($"Запрос на удаление сообщения {messageId} отправлен.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Ошибка при отправке запроса на удаление: {ex.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("SignalR соединение не активно. Удаление невозможно.");
                Console.ResetColor();
            }
        }

        private static async Task MarkAllMessagesAsRead(Guid chatId, Guid userId)
        {
            try
            {
                // Находим все непрочитанные сообщения, отправленные НЕ текущим пользователем
                var unreadMessageIds = _currentChatMessages.Values
                    .Where(m => m.ChatId == chatId && m.SenderId != userId && !m.IsRead)
                    .Select(m => m.Id)
                    .ToList();

                if (!unreadMessageIds.Any())
                {
                    Console.WriteLine("Нет новых непрочитанных сообщений для пометки.");
                    return;
                }

                // Отправляем HTTP POST запрос в MessageService
                var requestBody = JsonSerializer.Serialize(new { chatId = chatId, readerId = userId, messageIds = unreadMessageIds });
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync($"{MessageServiceHttpUrl}/api/messages/markread", content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Сообщения помечены как прочитанные.");
                    // Локально обновим статус прочитанных сообщений
                    foreach (var msgId in unreadMessageIds)
                    {
                        if (_currentChatMessages.TryGetValue(msgId, out var msg))
                        {
                            msg.IsRead = true;
                        }
                    }
                    // В реальном UI здесь можно было бы перерисовать сообщения
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Ошибка при пометке сообщений прочитанными: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Исключение при пометке сообщений прочитанными: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static async Task CheckUserOnlineStatus()
        {
            Console.Write("Введите ID пользователя для проверки статуса: ");
            var userIdString = Console.ReadLine();
            if (!Guid.TryParse(userIdString, out var targetUserId))
            {
                Console.WriteLine("Некорректный ID пользователя.");
                return;
            }

            if (_connection is { State: HubConnectionState.Connected })
            {
                try
                {
                    var isOnline = await _connection.InvokeAsync<bool>("IsUserOnline", targetUserId);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\nПользователь {targetUserId.ToString().Substring(0, 8)}: {(isOnline ? "ОНЛАЙН" : "ОФФЛАЙН")}\n> ");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Ошибка при проверке статуса пользователя: {ex.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("SignalR соединение не активно. Невозможно проверить статус.");
                Console.ResetColor();
            }
        }

        private static async Task<bool> CheckUserExists(Guid userId)
        {
            try
            {
                var response = await HttpClient.GetAsync($"{UserServiceUrl}/api/users/{userId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nОшибка при проверке существования пользователя {userId}: {ex.Message}\n> ");
                Console.ResetColor();
                return false;
            }
        }


        // DTOs
        public class UserResponse
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = default!;
            public string Email { get; set; } = default!;
        }

        public class ChatResponse
        {
            public Guid Id { get; set; }
            public string Type { get; set; } = null!;
            public string? Name { get; set; }
            public List<Guid> ParticipantIds { get; set; } = [];
        }

        public class ChatMessage
        {
            public Guid Id { get; set; }
            public Guid ChatId { get; set; }
            public Guid SenderId { get; set; }
            public string Content { get; set; } = null!;
            public DateTimeOffset Timestamp { get; set; }
            public bool IsEdited { get; set; }
            public bool IsDeleted { get; set; }
            public bool IsRead { get; set; }
        }
    }
}