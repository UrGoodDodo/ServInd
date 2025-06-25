using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EndVictoryResponse
{
    public bool Victory;
    public string Message;
}
[Serializable]
public class GameStateUpdate
{
    public string Message;
    public int HostHp;
    public int GuestHp;
    public string CurrentPlayerNickName;
}
[Serializable]
public class JoinLobbyResponse
{
    public bool Success;

    public string Message;

    public string HostNickName;

    public string HostAvatarBase64;

    public string HostAvatarFilename;

    public string GuestNickName;

    public string GuestAvatarBase64;

    public string GuestAvatarFilename;
}
[Serializable]
public class LeaveLobbyResponse
{
    public bool Success;
    public string Message;
}
[Serializable]
public class LobbyClosedResponse
{
    public string Message;
}
[Serializable]
public class LoginRequest
{

    public string Nickname;


    public string Password;
}
[Serializable]
public class LoginResponse
{
    public bool Success;
    public string Message;

    public Guid UserId;

    public string Nickname;

    public string AvatarBase64;

    public string AvatarExtension;

    public string JwtToken;
}

[Serializable]
public class RegisterRequest
{

    public string Nickname;


    public string Password;


    public string AvatarBase64;

    public string AvatarFilename;
}
 [Serializable]
public class RegisterResponse
{
    public bool Success;
    public string Message;

    public string Nickname;

    public string AvatarBase64;

    public string AvatarExtension;
}
[Serializable]
public class StartGameResponse
{
    public bool Success;
    public string Message;
    public string CurrentPlayerConnectionId;
    public string GroupName;
}
[Serializable]
public class StartLobbyResponse
{
    public bool Success;

    public string Message; 
}

