using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Server;
using Server.Data;
using Server.Dtos;
using Server.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class MainHubTests
{
    private string? _sentMessage;

    private MainHub CreateHub(AppDbContext dbContext, IWebHostEnvironment env, string responseMethod = "RegisterResponse")
    {
        var mockClients = new Mock<IHubCallerClients>();
        var mockCaller = new Mock<ISingleClientProxy>();

        mockCaller
            .Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] != null)
                    _sentMessage = args[0].ToString();
            })
            .Returns(Task.CompletedTask);

        mockClients.Setup(clients => clients.Caller).Returns(mockCaller.Object);

        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("connection1");

        var hub = new MainHub(dbContext, env)
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        return hub;
    }

    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task StartLobby_ValidUser_ShouldCreateLobbyAndAddHost()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext("StartLobbyTestDb");
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("wwwroot");

        // Очистка глобального состояния
        MainHub.GetLobbies().Clear();

        StartLobbyResponse? sentResponse = null;

        var mockClients = new Mock<IHubCallerClients>();
        var mockClientProxy = new Mock<ISingleClientProxy>();

        mockClientProxy
            .Setup(cp => cp.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is string json)
                {
                    sentResponse = JsonSerializer.Deserialize<StartLobbyResponse>(json);
                }
            })
            .Returns(Task.CompletedTask);

        mockClients.Setup(c => c.Caller).Returns(mockClientProxy.Object);

        var mockGroups = new Mock<IGroupManager>();
        mockGroups
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        // Мокируем User с Identity.Name = "testuser"
        var mockIdentity = new Mock<IIdentity>();
        mockIdentity.Setup(i => i.Name).Returns("testuser");

        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity).Returns(mockIdentity.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("connection1");
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        var hub = new MainHub(dbContext, envMock.Object)
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        string lobbyName = "TestLobby";

        // Act
        await hub.StartLobby(lobbyName);

        // Assert
        var lobbies = MainHub.GetLobbies();
        Assert.True(lobbies.ContainsKey(lobbyName));

        var lobby = lobbies[lobbyName];
        Assert.NotNull(lobby);
        Assert.Equal("connection1", lobby.HostConnectionId);
        Assert.Equal("testuser", lobby.HostName);
        Assert.Null(lobby.GuestConnectionId);
        Assert.Null(lobby.GuestName);

        Assert.NotNull(sentResponse);
        Assert.True(sentResponse!.Success);
        Assert.Contains("успешно", sentResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JoinLobby_ValidGuest_ShouldJoinLobbyAndNotifyCaller()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext("JoinLobbyTestDb");
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("wwwroot");

        string? sentJson = null;

        // Мокаем Clients
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroupClientProxy = new Mock<ISingleClientProxy>();

        mockGroupClientProxy
            .Setup(cp => cp.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is string s)
                    sentJson = s;
            })
            .Returns(Task.CompletedTask);

        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockGroupClientProxy.Object);

        var mockGroups = new Mock<IGroupManager>();
        mockGroups
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        // Identity
        var mockIdentity = new Mock<IIdentity>();
        mockIdentity.Setup(i => i.Name).Returns("guestUser");

        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity).Returns(mockIdentity.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("guestConnectionId");
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Инициализация хаба
        var hub = new MainHub(dbContext, envMock.Object)
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Подготовка лобби
        var lobbies = MainHub.GetLobbies();
        lobbies.Clear();
        var lobby = new Lobby
        {
            HostConnectionId = "hostConnectionId",
            HostName = "hostUser"
        };
        lobbies.TryAdd("TestLobby", lobby);

        // Подготовка аватарок
        var avatarDir = Path.Combine("wwwroot", "avatars");
        Directory.CreateDirectory(avatarDir);
        var hostAvatarPath = Path.Combine(avatarDir, "host.png");
        var guestAvatarPath = Path.Combine(avatarDir, "guest.png");

        await File.WriteAllBytesAsync(hostAvatarPath, new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(guestAvatarPath, new byte[] { 4, 5, 6 });

        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Nickname = "hostUser",
            PasswordHash = "hash",
            AvatarUrl = "/avatars/host.png"
        });

        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Nickname = "guestUser",
            PasswordHash = "hash",
            AvatarUrl = "/avatars/guest.png"
        });

        await dbContext.SaveChangesAsync();

        // Act
        await hub.JoinLobby("TestLobby");

        // Assert
        Assert.Equal("guestConnectionId", lobby.GuestConnectionId);
        Assert.Equal("guestUser", lobby.GuestName);

        Assert.NotNull(sentJson);

        var response = JsonSerializer.Deserialize<JoinLobbyResponse>(sentJson!);
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("guestUser присоединился к лобби", response.Message);

        Assert.Equal("hostUser", response.HostNickName);
        Assert.Equal("guestUser", response.GuestNickName);

        Assert.False(string.IsNullOrWhiteSpace(response.HostAvatarBase64));
        Assert.False(string.IsNullOrWhiteSpace(response.GuestAvatarBase64));
        Assert.Equal("host.png", response.HostAvatarFilename);
        Assert.Equal("guest.png", response.GuestAvatarFilename);

        // Cleanup
        if (File.Exists(hostAvatarPath)) File.Delete(hostAvatarPath);
        if (File.Exists(guestAvatarPath)) File.Delete(guestAvatarPath);
    }

    [Fact]
    public async Task LeaveLobby_AsGuest_ShouldLeaveSuccessfully()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext("LeaveLobbyGuestDb");
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("wwwroot");

        string? sentJson = null;

        var mockClients = new Mock<IHubCallerClients>();
        var mockGroupProxy = new Mock<ISingleClientProxy>();

        mockGroupProxy
            .Setup(cp => cp.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is string json)
                {
                    sentJson = json;
                }
            })
            .Returns(Task.CompletedTask);

        mockClients
            .Setup(c => c.Group(It.Is<string>(s => s == "TestLobby")))
            .Returns(mockGroupProxy.Object);

        var mockGroups = new Mock<IGroupManager>();
        mockGroups
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var mockIdentity = new Mock<IIdentity>();
        mockIdentity.Setup(i => i.Name).Returns("GuestUser");

        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity).Returns(mockIdentity.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("guestConnection");
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        var hub = new MainHub(dbContext, envMock.Object)
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Подготовка лобби с гостем
        var lobbies = MainHub.GetLobbies();
        lobbies.Clear();
        lobbies.TryAdd("TestLobby", new Lobby
        {
            HostConnectionId = "hostConnection",
            HostName = "HostUser",
            GuestConnectionId = "guestConnection",
            GuestName = "GuestUser"
        });

        // Act
        await hub.LeaveLobby("TestLobby");

        // Assert
        Assert.NotNull(sentJson);
        var response = JsonSerializer.Deserialize<LeaveLobbyResponse>(sentJson!);
        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Contains("гость покинул", response.Message, StringComparison.OrdinalIgnoreCase);

        var lobby = lobbies["TestLobby"];
        Assert.Null(lobby.GuestConnectionId);
        Assert.Null(lobby.GuestName);

        mockGroups.Verify(g => g.RemoveFromGroupAsync("guestConnection", "TestLobby", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartGame_ShouldFail_WhenLobbyIsNotFull()
    {
        // Arrange
        var lobbies = MainHub.GetLobbies();
        lobbies.Clear();

        string? sentJson = null;

        var mockClients = new Mock<IHubCallerClients>();
        var mockCaller = new Mock<ISingleClientProxy>();

        mockCaller
            .Setup(c => c.SendCoreAsync("StartGameResponse", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is string json)
                {
                    sentJson = json;
                }
            })
            .Returns(Task.CompletedTask);

        mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);

        // Мокаем контекст
        var mockIdentity = new Mock<IIdentity>();
        mockIdentity.Setup(i => i.Name).Returns("HostUser");

        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity).Returns(mockIdentity.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("host-conn-id");
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        // Создание экземпляра MainHub
        var hub = new MainHub(null!, null!)
        {
            Clients = mockClients.Object,
            Context = mockContext.Object,
            Groups = new Mock<IGroupManager>().Object
        };

        // Добавление неполного лобби
        lobbies.TryAdd("TestLobby", new Lobby
        {
            HostConnectionId = "host-conn-id",
            HostName = "HostUser",
            GuestConnectionId = null,
            GuestName = null
        });

        // Act
        await hub.StartGame("TestLobby");

        // Assert
        Assert.NotNull(sentJson);

        var response = JsonSerializer.Deserialize<StartGameResponse>(sentJson!);
        Assert.NotNull(response);
        Assert.False(response!.Success);
        Assert.Contains("не заполнено", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(response.CurrentPlayerConnectionId);
        Assert.Null(response.GroupName);
    }

    [Fact]
    public async Task StartGame_ShouldStartGame_WhenLobbyIsFull()
    {
        // Arrange
        var lobbies = MainHub.GetLobbies();
        var games = MainHub.GetGames();
        lobbies.Clear();
        games.Clear();

        string? sentJson = null;

        // Мокаем отправку в группу
        var mockClients = new Mock<IHubCallerClients>();
        var mockGroupProxy = new Mock<IClientProxy>();

        mockGroupProxy
            .Setup(proxy => proxy.SendCoreAsync("StartGameResponse", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is string json)
                {
                    sentJson = json;
                }
            })
            .Returns(Task.CompletedTask);

        mockClients.Setup(c => c.Group("TestLobby")).Returns(mockGroupProxy.Object);

        // Мокаем контекст (подключившийся хост)
        var mockIdentity = new Mock<IIdentity>();
        mockIdentity.Setup(i => i.Name).Returns("HostUser");

        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity).Returns(mockIdentity.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("host-conn-id");
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        var hub = new MainHub(null!, null!)
        {
            Clients = mockClients.Object,
            Context = mockContext.Object,
            Groups = new Mock<IGroupManager>().Object
        };

        // Полное лобби: и хост, и гость присутствуют
        lobbies.TryAdd("TestLobby", new Lobby
        {
            HostConnectionId = "host-conn-id",
            HostName = "HostUser",
            GuestConnectionId = "guest-conn-id",
            GuestName = "GuestUser"
        });

        // Act
        await hub.StartGame("TestLobby");

        // Assert
        Assert.NotNull(sentJson);

        var response = JsonSerializer.Deserialize<StartGameResponse>(sentJson!);
        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Equal("host-conn-id", response.CurrentPlayerConnectionId);
        Assert.Equal("TestLobby", response.GroupName);
        Assert.Contains("началась", response.Message, StringComparison.OrdinalIgnoreCase);

        // Проверяем, что игра действительно создана и активна
        Assert.True(games.ContainsKey("TestLobby"));
        var game = games["TestLobby"];
        Assert.True(game.IsGameStarted);
        Assert.Equal("host-conn-id", game.CurrentPlayerConnectionId);
    }

    [Fact]
    public async Task MakeMove_ValidPlayer_MakesMoveAndPassesTurn()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext("MakeMoveTestDb");
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("wwwroot");

        var hostConnectionId = "hostConnection";
        var guestConnectionId = "guestConnection";
        const string groupName = "TestLobby";

        MainHub.GetLobbies().Clear();
        MainHub.GetGames().Clear();

        string? sentJson = null;

        var mockGroupProxy = new Mock<IClientProxy>();
        mockGroupProxy
            .Setup(proxy => proxy.SendCoreAsync("GameStateUpdate", It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is string s)
                    sentJson = s;
            })
            .Returns(Task.CompletedTask);

        var mockClients = new Mock<IHubCallerClients>();
        mockClients.Setup(c => c.Group(groupName)).Returns(mockGroupProxy.Object);

        var mockGroups = new Mock<IGroupManager>();

        // Мокаем контекст хоста
        var mockIdentity = new Mock<IIdentity>();
        mockIdentity.Setup(i => i.Name).Returns("HostUser");

        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(u => u.Identity).Returns(mockIdentity.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(hostConnectionId);
        mockContext.Setup(c => c.User).Returns(mockUser.Object);

        var hub = new MainHub(dbContext, envMock.Object)
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Лобби
        var lobby = new Lobby
        {
            HostConnectionId = hostConnectionId,
            GuestConnectionId = guestConnectionId,
            HostName = "HostUser",
            GuestName = "GuestUser"
        };
        MainHub.GetLobbies().TryAdd(groupName, lobby);

        // Игра
        var gameSession = new GameSession
        {
            GroupName = groupName,
            HostHp = 30,
            GuestHp = 30,
            IsGameStarted = true,
            TurnStartTime = DateTime.UtcNow,
            CurrentPlayerConnectionId = hostConnectionId
        };
        MainHub.GetGames().TryAdd(groupName, gameSession);

        // Act: хост делает фиксированный урон (action = 1)
        await hub.MakeMove(groupName, 1);

        // Assert
        Assert.NotNull(sentJson);
        var gameState = JsonSerializer.Deserialize<GameStateUpdate>(sentJson!);
        Assert.NotNull(gameState);

        Assert.Equal(30, gameState!.HostHp);
        Assert.Equal(25, gameState.GuestHp); // -5 урона

        Assert.Equal("GuestUser", gameState.CurrentPlayerNickName);
        Assert.Contains("нанёс фиксированный урон", gameState.Message, StringComparison.OrdinalIgnoreCase);

        // Проверка, что ход передан гостю
        Assert.Equal(guestConnectionId, MainHub.GetGames()[groupName].CurrentPlayerConnectionId);
    }

    [Fact]
    public async Task MakeMove_WrongPlayer_AttemptsMoveAndGetsInvalidMove()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext("MakeMoveInvalidTestDb");
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("wwwroot");

        MainHub.GetLobbies().Clear();
        MainHub.GetGames().Clear();

        var hostConnectionId = "hostConnection";
        var guestConnectionId = "guestConnection";
        string groupName = "TestLobby";

        string? sentJson = null;

        var mockCallerProxy = new Mock<ISingleClientProxy>();
        mockCallerProxy
            .Setup(cp => cp.SendCoreAsync("GameStateUpdate", It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args.Length > 0 && args[0] is string s)
                    sentJson = s;
            })
            .Returns(Task.CompletedTask);

        var mockClients = new Mock<IHubCallerClients>();
        mockClients.Setup(c => c.Caller).Returns(mockCallerProxy.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(guestConnectionId); // ❗ гость пытается походить, но не его очередь

        var mockGroups = new Mock<IGroupManager>();

        var hub = new MainHub(dbContext, envMock.Object)
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        var lobby = new Lobby
        {
            HostConnectionId = hostConnectionId,
            GuestConnectionId = guestConnectionId,
            HostName = "Host",
            GuestName = "Guest"
        };
        MainHub.GetLobbies().TryAdd(groupName, lobby);

        var gameSession = new GameSession
        {
            GroupName = groupName,
            HostHp = 30,
            GuestHp = 30,
            CurrentPlayerConnectionId = hostConnectionId, // сейчас очередь хоста
            TurnStartTime = DateTime.UtcNow,
            IsGameStarted = true
        };
        MainHub.GetGames().TryAdd(groupName, gameSession);

        // Act
        await hub.MakeMove(groupName, 1);

        // Assert
        Assert.NotNull(sentJson);

        var callerGameState = JsonSerializer.Deserialize<GameStateUpdate>(sentJson!);
        Assert.NotNull(callerGameState);
        Assert.Equal("Сейчас не ваш ход.", callerGameState!.Message);
    }

    [Fact]
    public async Task MakeMove_WhenOpponentHpZero_DeclaresWinnerAndEndsGame()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext("GameOverTestDb");
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("wwwroot");

        MainHub.GetLobbies().Clear();
        MainHub.GetGames().Clear();

        string hostConnectionId = "hostConnection";
        string guestConnectionId = "guestConnection";
        string groupName = "TestLobby";

        var lobby = new Lobby
        {
            HostConnectionId = hostConnectionId,
            GuestConnectionId = guestConnectionId,
            HostName = "HostUser",
            GuestName = "GuestUser"
        };
        MainHub.GetLobbies().TryAdd(groupName, lobby);

        var gameSession = new GameSession
        {
            GroupName = groupName,
            HostHp = 30,
            GuestHp = 3, // погибнет от урона
            CurrentPlayerConnectionId = hostConnectionId,
            IsGameStarted = true,
            TurnStartTime = DateTime.UtcNow
        };
        MainHub.GetGames().TryAdd(groupName, gameSession);

        string? winnerJson = null;
        string? loserJson = null;

        var mockWinnerClient = new Mock<ISingleClientProxy>();
        mockWinnerClient
            .Setup(c => c.SendCoreAsync("EndVictoryResponse", It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args[0] is string s) winnerJson = s;
            })
            .Returns(Task.CompletedTask);

        var mockLoserClient = new Mock<ISingleClientProxy>();
        mockLoserClient
            .Setup(c => c.SendCoreAsync("EndVictoryResponse", It.IsAny<object[]>(), default))
            .Callback<string, object[], CancellationToken>((method, args, token) =>
            {
                if (args[0] is string s) loserJson = s;
            })
            .Returns(Task.CompletedTask);

        var mockClients = new Mock<IHubCallerClients>();
        mockClients.Setup(c => c.Client(hostConnectionId)).Returns(mockWinnerClient.Object);
        mockClients.Setup(c => c.Client(guestConnectionId)).Returns(mockLoserClient.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(hostConnectionId);

        var mockGroups = new Mock<IGroupManager>();

        var hub = new MainHub(dbContext, envMock.Object)
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Act: Хост наносит фиксированный урон
        await hub.MakeMove(groupName, 1);

        // Assert
        Assert.NotNull(winnerJson);
        Assert.NotNull(loserJson);

        var winnerResponse = JsonSerializer.Deserialize<EndVictoryResponse>(winnerJson!);
        var loserResponse = JsonSerializer.Deserialize<EndVictoryResponse>(loserJson!);

        Assert.NotNull(winnerResponse);
        Assert.NotNull(loserResponse);

        Assert.True(winnerResponse!.Victory);
        Assert.Contains("победили", winnerResponse.Message, StringComparison.OrdinalIgnoreCase);

        Assert.False(loserResponse!.Victory);
        Assert.Contains("проиграли", loserResponse.Message, StringComparison.OrdinalIgnoreCase);

        Assert.False(MainHub.GetGames().ContainsKey(groupName));
    }

}
