using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server;
using Server.Models;
using Server.Dtos;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class GameTurnTimerService : BackgroundService
{
    private readonly IHubContext<MainHub> _hubContext;
    private readonly ILogger<GameTurnTimerService> _logger;

    private static readonly ConcurrentDictionary<string, GameSession> _games = MainHub.GetGames();
    private static readonly ConcurrentDictionary<string, Lobby> _lobbies = MainHub.GetLobbies();

    public GameTurnTimerService(IHubContext<MainHub> hubContext, ILogger<GameTurnTimerService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameTurnTimerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var (groupName, game) in _games)
                {
                    if (!game.IsGameStarted)
                        continue;

                    var elapsed = DateTime.UtcNow - game.TurnStartTime;
                    if (elapsed.TotalSeconds >= game.TurnDurationSeconds)
                    {
                        if (!_lobbies.TryGetValue(groupName, out var lobby))
                            continue;

                        var oldPlayer = game.CurrentPlayerConnectionId;
                        var newPlayer = oldPlayer == lobby.HostConnectionId
                            ? lobby.GuestConnectionId
                            : lobby.HostConnectionId;

                        if (string.IsNullOrEmpty(newPlayer))
                        {
                            _logger.LogWarning($"Game {groupName} has no next player to switch to.");
                            continue;
                        }

                        game.CurrentPlayerConnectionId = newPlayer;
                        game.TurnStartTime = DateTime.UtcNow;

                        string currentPlayerNick = newPlayer == lobby.HostConnectionId
                            ? lobby.HostName
                            : lobby.GuestName ?? "";

                        var update = new GameStateUpdate
                        {
                            Message = "Игрок пропустил ход. Ход передан другому игроку.",
                            HostHp = game.HostHp,
                            GuestHp = game.GuestHp,
                            CurrentPlayerNickName = currentPlayerNick
                        };

                        await _hubContext.Clients.Group(groupName)
                            .SendAsync("GameStateUpdate", update, stoppingToken);

                        _logger.LogInformation($"Game {groupName}: ход переключен с {oldPlayer} на {newPlayer} по таймауту.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в GameTurnTimerService");
            }

            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("GameTurnTimerService stopped.");
    }
}
