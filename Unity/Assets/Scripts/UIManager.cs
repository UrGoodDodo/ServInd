using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Concurrent;
public class UIManager : MonoBehaviour
{
    public GameObject LoginPanel;
    public GameObject RegisterPanel;

    public GameObject loggedPanel;
    public TMP_Text nameText;
    public Image avaImage;

    public TMP_InputField lLoginField;
    public TMP_InputField lPassField;
    public TMP_InputField rLoginField;
    public TMP_InputField rPassField;

    private TestScript testScript;

    public Texture2D avatar1;
    public Texture2D avatar2;
    public Texture2D avatarBase;
    private Texture2D chosenAvatar;

    public string avatar1Filename;
    public string avatar2Filename;
    public string avatarBaseFilename;
    private string chosenAvatarFilename;

    public GameObject JoinPanel;
    public GameObject CreatePanel;

    public TMP_InputField groupJoin;
    public TMP_InputField groupCreate;


    public TMP_Text lobbyText;
    public TMP_Text player1;
    public TMP_Text player2;
    public Image p1Image;
    public Image p2Image;


    public Button playButton;
    public Button leaveLobbyButton;
    public Button joinButton;
    public Button createButton;
    
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private string groupName;


    public GameObject gameScreen;
    public Slider PlayerHP;
    public Slider EnemyHP;

    public TMP_Text GameLobbyName;

    public GameObject actionPanel;
    public GameObject turnText;


    private bool yourTurn;
    private bool gameIsActive;
    private bool isHost;

    public GameObject victoryScreen;
    public GameObject gameoverScreen;




    private void Start()
    {
        testScript = GameObject.FindWithTag("Server").GetComponent<TestScript>();
        Screen.SetResolution(1280, 720, false);
    }

    void Update()
    {
        while (_mainThreadActions.TryDequeue(out var action))
            action();

        if (gameIsActive)
        {
            if (yourTurn)
            {
                actionPanel.SetActive(true);
                turnText.SetActive(false);
            }
            else
            {
                actionPanel.SetActive(false);
                turnText.SetActive(true);
            }
        }

    }

    public void OpenLogin()
    {
        LoginPanel.SetActive(true);
    }

    public void OpenRegister()
    {
        RegisterPanel.SetActive(true);
    }
    public void CloseLogin()
    {
        LoginPanel.SetActive(false);
    }

    public void CloseRegister()
    {
        RegisterPanel.SetActive(false);
    }

    public void TryLogin()
    {
        string name = lLoginField.text;
        string password = lPassField.text;
       
        testScript.Login(name, password);
        LoginPanel.SetActive(false);
       
    }

    public void TryRegister()
    {
        string name = rLoginField.text;
        string password = rPassField.text;
        if (chosenAvatar == null)
        {
            chosenAvatar = avatarBase;
            chosenAvatarFilename = avatarBaseFilename;
        }

        byte[] pngBytes = chosenAvatar.EncodeToPNG();

        string avatarBase64 = Convert.ToBase64String(pngBytes);


        testScript.Register(name, password, avatarBase64, chosenAvatarFilename);
        RegisterPanel.SetActive(false);

    }

    public void ButtonAva1()
    {
        chosenAvatar = avatar1;
        chosenAvatarFilename = avatar1Filename;
    }
    public void ButtonAva2() 
    {
        chosenAvatar = avatar2;
        chosenAvatarFilename = avatar2Filename;
    }

    public void Login(string name, string avatar)
    {

        _mainThreadActions.Enqueue(() =>
        {
            loggedPanel.SetActive(true);
            nameText.text = name;
            bool loaded;
            Sprite sprite = LoadImage(avatar);
            avaImage.sprite = sprite;
        });
    }

    public void Registered()
    {
        Debug.Log("Пользователь зареган");
    }

    public void Join1()
    {
        JoinPanel.SetActive(true);
    }
    public void Join2()
    {
        groupName = groupJoin.text;
        testScript.JoinLobby(groupName);
        JoinPanel.SetActive(false);
    }

    public void Create1()
    {
        CreatePanel.SetActive(true);
    }
    public void Create2()
    {
        groupName = groupCreate.text;
        testScript.CreateLobby(groupName);
        CreatePanel.SetActive(false);
    }

    public void Logout()
    {
        if (groupName != null)
            Leave();
        testScript.Logout();
        loggedPanel.SetActive(false);
        
    }
    public void Leave()
    {
        testScript.LeaveLobby(groupName);
        lobbyText.text = "";
        this.player1.text = "";
        this.player2.text = "";
        joinButton.interactable = true;
        createButton.interactable = true;
        playButton.interactable = false;
        leaveLobbyButton.interactable = false;
        isHost = false;
    }

    public void OpenLobbyHost()
    {
        Debug.Log("aaaaaaaa");
        _mainThreadActions.Enqueue(() =>
        {
            playButton.interactable = true;
            joinButton.interactable = false;
            createButton.interactable= false;
            leaveLobbyButton.interactable = true;
            lobbyText.text = "Вы хост в лобби " + groupName;
            isHost = true;
        });
    }
    public void OpenLobbyGuest()
    {
        _mainThreadActions.Enqueue(() =>
        {
            playButton.interactable = false;
            joinButton.interactable = false;
            createButton.interactable = false;
            leaveLobbyButton.interactable = true;
            lobbyText.text = "Вы гость в лобби " + groupName;
            isHost = false;
        });
    }
    public void UpdateLobby(string player1, string player2, string p1Avatar,string p2Avatar)
    {
        _mainThreadActions.Enqueue(() =>
        {
            if (!isHost)
            {
                OpenLobbyGuest();
            }
            this.player1.text = "1. "+player1;
            this.player2.text = "2. "+player2;
            Sprite sprite = LoadImage(p1Avatar);
            Sprite sprite1 = LoadImage(p2Avatar);
            p1Image.gameObject.SetActive(true);
            p2Image.gameObject.SetActive(true);
            p1Image.sprite = sprite;
            p2Image.sprite = sprite1;
        });
    }

    public void OpenGame()
    {
        testScript.StartGame(groupName);
    }

    public void BaseAttack()
    {
        testScript.MakeMove(groupName, 1);
    }
    public void RandomAttack()
    {
        testScript.MakeMove(groupName, 2);
    }
    public void Heal()
    {
        testScript.MakeMove(groupName, 3);
    }

    public void LeaveGame()
    {
        gameScreen.SetActive(false);
        gameIsActive = false;
        Leave();
    }
    public void SetPlayerHP(int hp)
    {
        PlayerHP.value = hp;
    }
    public void SetEnemyHP(int hp)
    {
        EnemyHP.value = hp;
    }

    public void StartGame(string CurrentTurn)

    {
        _mainThreadActions.Enqueue(() =>
        {
            gameScreen.SetActive(true);
            GameLobbyName.text = "lobby "+ groupName;
            gameIsActive = true;
            SetPlayerHP(30);
            SetEnemyHP(30);
            if (CurrentTurn == testScript.connectionID)
            {
                yourTurn = true;
            }
            else
                yourTurn = false;
        });
    }

    public void GameOver(bool victory)
    {
        _mainThreadActions.Enqueue(() =>
        {
            gameIsActive = false;
        gameScreen.SetActive(false);
        if (victory)
        {
            victoryScreen.SetActive(true);
        }
        else
            gameoverScreen.SetActive(true);
        });
    }
    public void UpdateGameState(string CurrentTurn, int HostHp, int GuestHp)
    {
        _mainThreadActions.Enqueue(() =>
        {
            if (CurrentTurn == testScript.loginName)
                yourTurn = true;
            else
                yourTurn = false;

            if (isHost)
            {
                SetPlayerHP(HostHp);
                SetEnemyHP(GuestHp);
            }
            else
            {
                SetPlayerHP(GuestHp);
                SetEnemyHP(HostHp);
            }
        });
    }

    public void exitEndgame()
    {
        gameoverScreen.SetActive(false);
        victoryScreen.SetActive(false);
    }

    private Sprite LoadImage(string avatar)
    {
        byte[] imageBytes = Convert.FromBase64String(avatar);

        // 3. Создаём Texture2D и загружаем в него данные
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool loaded = tex.LoadImage(imageBytes); // автоматически расширит размер
        Sprite avatarSprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
         );

        if (loaded)
        {
            // 4. Отображаем в RawImage
            Debug.Log( "Login successful, avatar loaded!");
        }
        else
        {
            Debug.Log("Login OK, but failed to load avatar image.");
        }

        return avatarSprite;
    }


}
