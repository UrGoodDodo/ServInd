using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

public class TestScript : MonoBehaviour
{
    public string signalRHubURL = "http://localhost:5000/Hub";
    public string apiBaseUrl = "http://localhost:5000/api/Auth";

    public string hubMethodAll = "SendPayloadAll";
    public string hubMethodCaller = "SendPayloadCaller";

    public string messageToSendAll = "Hello World All";
    public string messageToSendCaller = "Hello World Caller";

    public string statusText = "Awaiting Connection...";
    public string connectedText = "Connection Started";
    public string disconnectedText = "Connection Disconnected";

    private const string HANDLER_ALL = "ReceivePayloadAll";
    private const string HANDLER_CALLER = "ReceivePayloadCaller";

    private Text uiText;
    private string currentText = "";

    private SignalR signalR;
    public string connectionID;
    public string loginName;
    private string jwtToken;

    private UIManager uiManager;

    void Start()
    {
        uiManager = GameObject.FindWithTag("UI").GetComponent<UIManager>();
        uiText = GameObject.Find("Text").GetComponent<Text>();
        DisplayMessage(statusText);
    }

    void Update()
    {
        if (uiText.text != currentText)
        {
            StartCoroutine(RebuildLayout());
            currentText = uiText.text;
        }
    }

    void DisplayMessage(string message)
    {
        uiText.text += $"{message}\n";
    }
    public void Login(string name, string password)
    {
        StartCoroutine(LoginCoroutine(name, password));
    }
    public  void Register(string name, string password, string avatarBase64, string avatarFilename)
    {
        StartCoroutine(RegisterCoroutine(name, password, avatarBase64, avatarFilename));
    }
    IEnumerator RegisterCoroutine(string name, string password, string avatarBase64, string avatarFilename)
    {
        var json1 = new RegisterRequest
        {
            Nickname = name,
            Password = password,
            AvatarBase64 = avatarBase64,
            AvatarFilename = avatarFilename
        };
        var s = JsonUtility.ToJson(json1);

        using var www = new UnityWebRequest($"{apiBaseUrl}/register", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(s);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {

            Debug.LogError($"Register failed: {www.error}");
            Debug.LogError("Response body: " + www.downloadHandler.text);
            DisplayMessage($"Register error: {www.downloadHandler.text}");
            yield break;
        }

        string text = www.downloadHandler.text;
        Debug.Log("RAW JSON register response: " + text);
        var regResp = JsonUtility.FromJson<RegisterResponse>(text);
        Debug.Log("[Parsed] " + JsonUtility.ToJson(regResp));


        DisplayMessage($"[Register] {regResp.Message}");
        Debug.Log(regResp.Success);
        Debug.Log(regResp.Message  + regResp.Nickname);
        if (regResp.Success)
        {
            Debug.Log("aaaaa");
            uiManager.Login(regResp.Nickname, regResp.AvatarBase64);
            Login(regResp.Nickname, password);
        }
    }
    IEnumerator  LoginCoroutine(string name, string password)
    {
        var json1 = new LoginRequest
        {
            Nickname = name,
            Password = password,
        };
        var s = JsonUtility.ToJson(json1);

        using var www = new UnityWebRequest($"{apiBaseUrl}/login", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(s);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            DisplayMessage($"Login error: {www.downloadHandler.text}");
            yield break;
        }

        var loginResp = JsonUtility.FromJson<LoginResponse>(
                            www.downloadHandler.text);

        jwtToken = loginResp.JwtToken;
        loginName = loginResp.Nickname;
        uiManager.Login(loginResp.Nickname, loginResp.AvatarBase64);
        DisplayMessage(loginResp.Message);
        InitAndConnectSignalR();
    }
    private void InitAndConnectSignalR()
    {
        string urlWithToken = $"{signalRHubURL}"
            + $"?access_token={Uri.EscapeDataString(jwtToken)}";

        signalR = new SignalR();
        signalR.Init(urlWithToken);
        signalR.On(HANDLER_ALL, (string payload) =>
        {
            var json = JsonUtility.FromJson<JsonPayload>(payload);
            DisplayMessage($"{HANDLER_ALL}: {json.Message}");
        });
        signalR.On(HANDLER_CALLER, (string payload) =>
        {
            var json = JsonUtility.FromJson<JsonPayload>(payload);
            DisplayMessage($"{HANDLER_CALLER}: {json.Message}");
        });


        signalR.On<string>("StartLobbyResponse", (payload) =>
        {
            var json = JsonUtility.FromJson<StartLobbyResponse>(payload);
            Debug.Log("Start lobby" + json.Message + json.Success);

            if (json.Success)
            {
                uiManager.OpenLobbyHost();
            }
            DisplayMessage(json.Message);

        });

        signalR.On<string>("JoinLobbyResponse", (payload) =>
        {
            var json = JsonUtility.FromJson<JoinLobbyResponse>(payload);
            Debug.Log("Join lobby" + json.Message);
            if (json.Success)
            {
                uiManager.UpdateLobby(json.HostNickName, json.GuestNickName, json.HostAvatarBase64, json.GuestAvatarBase64);
            }
            DisplayMessage(json.Message);

        });
        signalR.On<string>("LeaveLobbyResponse", (payload) =>
        {
            var json = JsonUtility.FromJson<LeaveLobbyResponse>(payload);
            Debug.Log("Leave lobby" + json.Message);
            DisplayMessage(json.Message);

        });
        signalR.On<string>("LobbyClosed", (payload) =>
        {
            var json = JsonUtility.FromJson<LobbyClosedResponse>(payload);
            Debug.Log("Lobby Closed " + json.Message);
            DisplayMessage(json.Message);

        });


        signalR.On<string>("StartGameResponse", (payload) =>
        {
            var json = JsonUtility.FromJson<StartGameResponse>(payload);
            Debug.Log("Start Game  " + json.Message);
            uiManager.StartGame(json.CurrentPlayerConnectionId);
            DisplayMessage(json.Message);

        });

        signalR.On<string>("EndVictoryResponse", (payload) =>
        {
            var json = JsonUtility.FromJson<EndVictoryResponse>(payload);
            Debug.Log("Game over  " + json.Message);
            uiManager.GameOver(json.Victory);
            DisplayMessage(json.Message);

        });
        signalR.On<string>("GameStateUpdate", (payload) =>
        {
            var json = JsonUtility.FromJson<GameStateUpdate>(payload);
            Debug.Log("Game update  " + json.Message);
            uiManager.UpdateGameState(json.CurrentPlayerNickName, json.HostHp, json.GuestHp);
            DisplayMessage(json.Message);

        });


        signalR.ConnectionStarted += (object sender, ConnectionEventArgs e) =>
        {
            Debug.Log($"Connected: {e.ConnectionId}");
            connectionID = e.ConnectionId;
            DisplayMessage(connectedText);

        };
        signalR.ConnectionClosed += (object sender, ConnectionEventArgs e) =>
        {
            Debug.Log($"Disconnected: {e.ConnectionId}");
            DisplayMessage(disconnectedText);
        };

        signalR.Connect();

    }
    public  void Logout()
    {
        string s = "";
        signalR.Invoke("Logout",s);
    }

    public void CreateLobby(string groupName)
    {
        var json1 = new LobbyRequest
        {
            GroupName = groupName
        };
        signalR.Invoke("StartLobby", groupName);
    }
    public void JoinLobby(string groupName)
    {
        var json1 = new LobbyRequest
        {
            GroupName = groupName
        };
        signalR.Invoke("JoinLobby", groupName);
    }

    public  void LeaveLobby(string groupName)
    {
        var json1 = new LobbyRequest
        {
            GroupName = groupName
        };
        signalR.Invoke("LeaveLobby", groupName);
    }

    public void StartGame(string groupName)
    {
        var json1 = new LobbyRequest
        {
            GroupName = groupName
        };
        signalR.Invoke("StartGame", groupName);
    }

    public  void MakeMove(string groupName, int action)
    {
        var json1 = new MoveRequest
        {
            GroupName = groupName,
            Action = action
        };
        signalR.Invoke("MakeMove", groupName, action);
    }

    IEnumerator RebuildLayout()
    {
        yield return null;

        LayoutRebuilder.MarkLayoutForRebuild(uiText.rectTransform);
    }

    
}
[Serializable]
public class JsonPayload { public string Message; }

[Serializable]
public class LobbyRequest {  public string GroupName; }
[Serializable]
public class MoveRequest { public string GroupName; public int Action; }