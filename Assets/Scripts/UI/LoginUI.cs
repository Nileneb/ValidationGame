// Assets/Scripts/UI/LoginUI.cs
// Login-Screen für Moltbook Authentication

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ValidationGame.Auth;

public class LoginUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField apiKeyInput;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button loginMoltbookButton;
    [SerializeField] private Button loginDevButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("After Login")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI karmaText;

    private MoltbookAuth _auth;

    void Start()
    {
        _auth = MoltbookAuth.Instance;
        
        if (_auth == null)
        {
            Debug.LogError("LoginUI: MoltbookAuth not found!");
            return;
        }

        // Events
        _auth.OnLoginSuccess += OnLoginSuccess;
        _auth.OnLoginFailed += OnLoginFailed;
        _auth.OnLogout += OnLogout;

        // Buttons
        if (loginMoltbookButton != null)
            loginMoltbookButton.onClick.AddListener(OnMoltbookLoginClicked);
        
        if (loginDevButton != null)
            loginDevButton.onClick.AddListener(OnDevLoginClicked);

        // Bereits eingeloggt?
        if (_auth.IsLoggedIn)
        {
            ShowMainMenu();
        }
        else
        {
            ShowLoginPanel();
        }
    }

    void OnDestroy()
    {
        if (_auth != null)
        {
            _auth.OnLoginSuccess -= OnLoginSuccess;
            _auth.OnLoginFailed -= OnLoginFailed;
            _auth.OnLogout -= OnLogout;
        }
    }

    // ==================== Button Handlers ====================

    async void OnMoltbookLoginClicked()
    {
        var apiKey = apiKeyInput?.text?.Trim();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            ShowStatus("Bitte API Key eingeben", true);
            return;
        }

        ShowLoading(true);
        ShowStatus("Verbinde mit Moltbook...", false);

        await _auth.LoginWithMoltbook(apiKey);
    }

    async void OnDevLoginClicked()
    {
        var playerName = playerNameInput?.text?.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "TestPlayer";
        }

        ShowLoading(true);
        ShowStatus("Dev Login...", false);

        await _auth.LoginDevMode(playerName);
    }

    // ==================== Auth Events ====================

    void OnLoginSuccess(PlayerProfile player)
    {
        ShowLoading(false);
        ShowStatus($"Willkommen, {player.name}!", false);
        
        // Kurz warten, dann zum Hauptmenü
        Invoke(nameof(ShowMainMenu), 1f);
    }

    void OnLoginFailed(string error)
    {
        ShowLoading(false);
        ShowStatus($"Login fehlgeschlagen: {error}", true);
    }

    void OnLogout()
    {
        ShowLoginPanel();
    }

    // ==================== UI Helpers ====================

    void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        ShowLoading(false);
        ShowStatus("", false);
    }

    void ShowMainMenu()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // Player Info anzeigen
        var player = _auth.CurrentPlayer;
        if (player != null)
        {
            if (playerNameText != null)
                playerNameText.text = player.name;
            
            if (karmaText != null)
                karmaText.text = $"Karma: {player.karma}";
        }
    }

    void ShowLoading(bool show)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(show);
        
        if (loginMoltbookButton != null)
            loginMoltbookButton.interactable = !show;
        
        if (loginDevButton != null)
            loginDevButton.interactable = !show;
    }

    void ShowStatus(string message, bool isError)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = isError ? Color.red : Color.white;
        }
    }
}
