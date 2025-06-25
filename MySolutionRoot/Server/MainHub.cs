using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Dtos;
using Server.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;

namespace Server
{
    [Authorize]
    public class MainHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        private static readonly ConcurrentDictionary<string, Lobby> _lobbies = new ConcurrentDictionary<string, Lobby>();
        private static readonly ConcurrentDictionary<string, GameSession> _games = new();

        public static ConcurrentDictionary<string, GameSession> GetGames() => _games;
        public static ConcurrentDictionary<string, Lobby> GetLobbies() => _lobbies;



        public MainHub(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public override async Task OnConnectedAsync() // при подключении пользователя
        {
            Console.WriteLine($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();

        }

        public override async Task OnDisconnectedAsync(Exception? ex) // при отключении пользователя
        {
            Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(ex);

        }

        public async Task StartLobby(string groupName)
        {
            var connectionId = Context.ConnectionId;

            // Проверка: участвует ли уже в каком-либо лобби
            if (_lobbies.Values.Any(l => l.HostConnectionId == connectionId || l.GuestConnectionId == connectionId))
            {
                var failResponse = new StartLobbyResponse
                {
                    Success = false,
                    Message = "Вы уже находитесь в лобби"
                };
                await Clients.Caller.SendAsync("StartLobbyResponse", JsonSerializer.Serialize(failResponse));
                return;
            }

            if (_lobbies.ContainsKey(groupName))
            {
                var failResponse = new StartLobbyResponse
                {
                    Success = false,
                    Message = "Лобби с таким именем уже существует"
                };
                await Clients.Caller.SendAsync("StartLobbyResponse", JsonSerializer.Serialize(failResponse));
                return;
            }

            var nickname = Context.User?.Identity?.Name;
            var lobby = new Lobby
            {
                HostConnectionId = connectionId,
                HostName = nickname,
                GuestConnectionId = null,
                GuestName = null
            };

            _lobbies[groupName] = lobby;

            await Groups.AddToGroupAsync(connectionId, groupName);

            Console.WriteLine($"[ЛОББИ] Пользователь с ConnectionId {connectionId} создал лобби {groupName}");

            var successResponse = new StartLobbyResponse
            {
                Success = true,
                Message = $"Вы успешно создали лобби '{groupName}'"
            };
            await Clients.Caller.SendAsync("StartLobbyResponse", JsonSerializer.Serialize(successResponse));
        }

        public async Task JoinLobby(string groupName)
        {
            var connectionId = Context.ConnectionId;
            var nickname = Context.User?.Identity?.Name;
            Console.WriteLine($"[ЛОББИ] join");

            if (!_lobbies.TryGetValue(groupName, out var lobby))
            {
                await Clients.Caller.SendAsync("JoinLobbyResponse", JsonSerializer.Serialize(new JoinLobbyResponse
                {
                    Success = false,
                    Message = "Лобби не найдено"
                }));
                return;
            }

            if (lobby.GuestConnectionId != null)
            {
                await Clients.Caller.SendAsync("JoinLobbyResponse", JsonSerializer.Serialize(new JoinLobbyResponse
                {
                    Success = false,
                    Message = "Лобби заполнено"
                }));
                return;
            }

            lobby.GuestConnectionId = connectionId;
            lobby.GuestName = nickname;

            await Groups.AddToGroupAsync(connectionId, groupName);

            var hostUser = await _db.Users.FirstOrDefaultAsync(u => u.Nickname == lobby.HostName);
            var guestUser = await _db.Users.FirstOrDefaultAsync(u => u.Nickname == nickname);

            var response = new JoinLobbyResponse
            {
                Success = true,
                Message = $"{nickname} присоединился к лобби",
                HostNickName = hostUser?.Nickname,
                HostAvatarBase64 = await GetAvatarBase64Async(hostUser?.AvatarUrl),
                HostAvatarFilename = Path.GetFileName(hostUser?.AvatarUrl ?? ""),
                GuestNickName = guestUser?.Nickname,
                GuestAvatarBase64 = await GetAvatarBase64Async(guestUser?.AvatarUrl),
                GuestAvatarFilename = Path.GetFileName(guestUser?.AvatarUrl ?? "")
            };

            await Clients.Group(groupName).SendAsync("JoinLobbyResponse", JsonSerializer.Serialize(response));
        }

        public async Task LeaveLobby(string groupName)
        {
            var connectionId = Context.ConnectionId;
            var userName = Context.User?.Identity?.Name;

            if (!_lobbies.TryGetValue(groupName, out var lobby))
            {
                await Clients.Caller.SendAsync("LeaveLobbyResponse", JsonSerializer.Serialize(new LeaveLobbyResponse
                {
                    Success = false,
                    Message = "Лобби не найдено."
                }));
                return;
            }

            bool isHost = lobby.HostConnectionId == connectionId && lobby.HostName == userName;
            bool isGuest = lobby.GuestConnectionId == connectionId && lobby.GuestName == userName;

            if (!isHost && !isGuest)
            {
                await Clients.Caller.SendAsync("LeaveLobbyResponse", JsonSerializer.Serialize(new LeaveLobbyResponse
                {
                    Success = false,
                    Message = "Вы не состоите в этом лобби."
                }));
                return;
            }

            if (isHost)
            {
                // Уведомить гостя о закрытии, если он есть
                if (lobby.GuestConnectionId != null)
                {
                    await Groups.RemoveFromGroupAsync(lobby.GuestConnectionId, groupName);
                    await Clients.Client(lobby.GuestConnectionId).SendAsync("LobbyClosedResponse", JsonSerializer.Serialize(new LobbyClosedResponse
                    {
                        Message = "Хост покинул лобби. Лобби закрыто."
                    }));
                }

                await Groups.RemoveFromGroupAsync(connectionId, groupName);
                await Clients.Client(connectionId).SendAsync("LobbyClosedResponse", JsonSerializer.Serialize(new LobbyClosedResponse
                {
                    Message = "Вы покинули лобби. Лобби закрыто."
                }));

                _lobbies.TryRemove(groupName, out _);
                return;
            }

            // Гость покидает лобби
            lobby.GuestConnectionId = null;
            lobby.GuestName = null;

            await Clients.Group(groupName).SendAsync("LeaveLobbyResponse", JsonSerializer.Serialize(new LeaveLobbyResponse
            {
                Success = true,
                Message = "Гость покинул лобби."
            }));

            await Groups.RemoveFromGroupAsync(connectionId, groupName);

            Console.WriteLine($"Гость {userName} покинул лобби {groupName}.");
        }

        public async Task StartGame(string groupName)
        {
            if (!_lobbies.TryGetValue(groupName, out var lobby) || !lobby.IsFull)
            {
                await Clients.Caller.SendAsync("StartGameResponse", JsonSerializer.Serialize(new StartGameResponse
                {
                    Success = false,
                    Message = "Невозможно начать игру. Лобби не заполнено."
                }));
                return;
            }

            var game = new GameSession
            {
                GroupName = groupName,
                CurrentPlayerConnectionId = lobby.HostConnectionId,
                IsGameStarted = true,
                TurnStartTime = DateTime.UtcNow,
                HostHp = 30,  
                GuestHp = 30
            };

            _games[groupName] = game;

            await Clients.Group(groupName).SendAsync("StartGameResponse", JsonSerializer.Serialize(new StartGameResponse
            {
                Success = true,
                Message = "Игра началась!",
                CurrentPlayerConnectionId = game.CurrentPlayerConnectionId,
                GroupName = game.GroupName
            }));
        }

        public async Task MakeMove(string groupName, int action)
        {
            if (!_games.TryGetValue(groupName, out var game))
                return;

            if (Context.ConnectionId != game.CurrentPlayerConnectionId)
            {
                await Clients.Caller.SendAsync("GameStateUpdate", JsonSerializer.Serialize(new GameStateUpdate
                {
                    Message = "Сейчас не ваш ход."
                }));
                return;
            }

            if (action != 1 && action != 2 && action != 3) 
            {
                await Clients.Caller.SendAsync("GameStateUpdate", JsonSerializer.Serialize(new GameStateUpdate
                {
                    Message = "Неправильное действие."
                }));
                return;
            }

            var lobby = _lobbies[groupName];
            var opponentId = game.CurrentPlayerConnectionId == lobby.HostConnectionId ? lobby.GuestConnectionId : lobby.HostConnectionId;

            var rand = new Random();
            string resultMessage = "";
            int effectValue = 0;

            switch (action)
            {
                case 1:
                    effectValue = 5;
                    resultMessage = "Нанёс фиксированный урон 5";
                    break;
                case 2:
                    effectValue = rand.Next(2, 9);
                    resultMessage = $"Нанёс случайный урон {effectValue}";
                    break;
                case 3:
                    effectValue = rand.Next(2, 5);
                    resultMessage = $"Восстановил {effectValue} HP";
                    break;
                default:
                    return;
            }

            if (action == 3)
            {
                if (Context.ConnectionId == lobby.HostConnectionId)
                    game.HostHp = Math.Min(30, game.HostHp + effectValue);
                else
                    game.GuestHp = Math.Min(30, game.GuestHp + effectValue);
            }
            else
            {
                if (opponentId == lobby.HostConnectionId)
                    game.HostHp = Math.Max(0, game.HostHp - effectValue);
                else
                    game.GuestHp = Math.Max(0, game.GuestHp - effectValue);
            }

            // Проверка победителя ДО передачи хода
            if (game.HostHp == 0 || game.GuestHp == 0)
            {
                string winnerId = game.HostHp == 0 ? lobby.GuestConnectionId : lobby.HostConnectionId;
                string loserId = game.HostHp == 0 ? lobby.HostConnectionId : lobby.GuestConnectionId;

                await Clients.Client(winnerId).SendAsync("EndVictoryResponse", JsonSerializer.Serialize(new EndVictoryResponse
                {
                    Victory = true,
                    Message = "Вы победили!"
                }));

                await Clients.Client(loserId).SendAsync("EndVictoryResponse", JsonSerializer.Serialize(new EndVictoryResponse
                {
                    Victory = false,
                    Message = "Вы проиграли!"
                }));

                _games.TryRemove(groupName, out _);
                return;
            }

            game.CurrentPlayerConnectionId = opponentId;
            game.TurnStartTime = DateTime.UtcNow;

            string currentPlayerNickName = game.CurrentPlayerConnectionId == lobby.HostConnectionId ? lobby.HostName : lobby.GuestName ?? "";

            await Clients.Group(groupName).SendAsync("GameStateUpdate", JsonSerializer.Serialize(new GameStateUpdate
            {
                HostHp = game.HostHp,
                GuestHp = game.GuestHp,
                CurrentPlayerNickName = currentPlayerNickName,
                Message = resultMessage
            }));
        }



        private async Task<string> GetAvatarBase64Async(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl)) return "";

            try
            {
                var avatarPath = Path.Combine(_env.WebRootPath ?? "wwwroot", avatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(avatarPath))
                {
                    var bytes = await File.ReadAllBytesAsync(avatarPath);
                    return Convert.ToBase64String(bytes);
                }
            }
            catch
            {
                // Ошибки можно логировать
            }

            return "";
        }
    }
}