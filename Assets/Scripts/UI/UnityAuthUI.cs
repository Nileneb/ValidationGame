// Assets/Scripts/UI/UnityAuthUI.cs
// Login UI für Unity Gaming Services Authentication
// Zeigt Unity Auth + Server Auth Status

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ValidationGame.Auth;

public class UnityAuthUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject profilePanel;

    [Header("Login Panel")]
    [SerializeField] private Button signInAnonymousButton;
    [SerializeField] private Button signInGoogleButton;
    [SerializeField] private Button signInAppleButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Profile Panel")]
    [SerializeField] private TextMeshProUGUI playerIdText;
    [SerializeField] private TextMeshProUGUI accountTypeText;
    [SerializeField] private TextMeshProUGUI serverStatusText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button signOutButton;
    [SerializeField] private Button linkGoogleButton;
    [SerializeField] private Button playButton;

    [Header("References")]
    [SerializeField] private ApiClient apiClient;

    private UnityAuthManager _auth;

    void Start()
    {
        _auth = UnityAuthManager.Instance;
        
        if (_auth == null)
        {
            Debug.LogError("UnityAuthUI: UnityAuthManager not found!");
            ShowStatus("Auth Manager nicht gefunden!", true);
            return;
        }

        // Find ApiClient if not assigned
        if (apiClient == null)
            apiClient = FindObjectOfType<ApiClient>();

        // Unity Auth Events
        _auth.OnSignedIn += OnUnitySignedIn;
        _auth.OnSignedOut += OnUnitySignedOut;
        _auth.OnSignInFailed += OnSignInFailed;

        // Server Auth Events
        if (apiClient != null)
        {
            apiClient.OnServerAuthenticated += OnServerAuthenticated;
            apiClient.OnServerAuthFailed += OnServerAuthFailed;
        }

        // Buttons
        if (signInAnonymousButton != null)
            signInAnonymousButton.onClick.AddListener(OnAnonymousClicked);
        
        if (signInGoogleButton != null)
            signInGoogleButton.onClick.AddListener(OnGoogleClicked);
        
        if (signInAppleButton != null)
        {
            signInAppleButton.onClick.AddListener(OnAppleClicked);
            // Apple nur auf iOS
            #if !UNITY_IOS
            signInAppleButton.gameObject.SetActive(false);
            #endif
        }

        if (signOutButton != null)
            signOutButton.onClick.AddListener(OnSignOutClicked);

        if (linkGoogleButton != null)
            linkGoogleButton.onClick.AddListener(OnLinkGoogleClicked);

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        // Initial state
        UpdateUI();
    }

    void OnDestroy()
    {
        if (_auth != null)
        {
            _auth.OnSignedIn -= OnUnitySignedIn;
            _auth.OnSignedOut -= OnUnitySignedOut;
            _auth.OnSignInFailed -= OnSignInFailed;
        }

        if (apiClient != null)
        {
            apiClient.OnServerAuthenticated -= OnServerAuthenticated;
            apiClient.OnServerAuthFailed -= OnServerAuthFailed;
        }
    }

    // ==================== Button Handlers ====================

    async void OnAnonymousClicked()
    {
        ShowLoading(true);
        ShowStatus("Anonym einloggen...", false);
        await _auth.SignInAnonymouslyAsync();
        // Server auth wird automatisch von ApiClient getriggert
    }

    void OnGoogleClicked()
    {
        ShowLoading(true);
        ShowStatus("Google Login...", false);
        
        #if UNITY_ANDROID
        // Google Play Games Login triggern
        ShowStatus("Google: Implementiere PlayGames Plugin", true);
        ShowLoading(false);
        #else
        ShowStatus("Google Login nur auf Android", true);
        ShowLoading(false);
        #endif
    }

    void OnAppleClicked()
    {
        ShowLoading(true);
        ShowStatus("Apple Login...", false);
        
        #if UNITY_IOS
        ShowStatus("Apple: Implementiere Apple Sign In", true);
        ShowLoading(false);
        #else
        ShowStatus("Apple Login nur auf iOS", true);
        ShowLoading(false);
        #endif
    }

    void OnSignOutClicked()
    {
        _auth.SignOut();
        // ApiClient wird automatisch benachrichtigt
    }

    async void OnLinkGoogleClicked()
    {
        ShowStatus("Account mit Google verknüpfen...", false);
        ShowStatus("Link: Implementiere PlayGames Plugin", true);
    }

    void OnPlayClicked()
    {
        // Zum Spiel wechseln
        if (apiClient != null && apiClient.IsFullyAuthenticated)
        {
            // Scene wechseln oder Panel schließen
            gameObject.SetActive(false);
        }
        else
        {
            ShowStatus("Noch nicht vollständig eingeloggt...", true);
        }
    }

    // ==================== Unity Auth Events ====================

    void OnUnitySignedIn(string playerId)
    {
        ShowStatus("Unity ✓ - Verbinde zum Server...", false);
        UpdateUI();
    }

    void OnUnitySignedOut()
    {
        ShowLoading(false);
        UpdateUI();
    }

    void OnSignInFailed(string error)
    {
        ShowLoading(false);
        ShowStatus($"Unity Login fehlgeschlagen: {error}", true);
    }

    // ==================== Server Auth Events ====================

    void OnServerAuthenticated(string sessionToken)
    {
        ShowLoading(false);
        ShowStatus("✓ Vollständig eingeloggt!", false);
        UpdateUI();
        
        // Stats laden
        if (apiClient != null)
            StartCoroutine(apiClient.FetchMyProfile(OnProfileLoaded));
    }

    void OnServerAuthFailed(string error)
    {
        ShowLoading(false);
        ShowStatus($"Server-Auth fehlgeschlagen: {error}", true);
        UpdateServerStatus("❌ Server: Verbindung fehlgeschlagen");
    }

    void OnProfileLoaded(PlayerProfile profile)
    {
        if (profile != null && statsText != null)
        {
            statsText.text = $"Score: {profile.total_score}\n" +
                           $"Papers: {profile.papers_validated}\n" +
                           $"Accuracy: {profile.accuracy * 100:F1}%";
        }
    }

    // ==================== UI Helpers ====================

    void UpdateUI()
    {
        bool unitySignedIn = _auth != null && _auth.IsSignedIn;
        bool serverAuth = apiClient != null && apiClient.IsServerAuthenticated;

        if (loginPanel != null)
            loginPanel.SetActive(!unitySignedIn);
        
        if (profilePanel != null)
            profilePanel.SetActive(unitySignedIn);

        if (unitySignedIn)
        {
            // Player ID (gekürzt)
            if (playerIdText != null)
            {
                string shortId = _auth.PlayerId.Length > 12 
                    ? _auth.PlayerId.Substring(0, 12) + "..." 
                    : _auth.PlayerId;
                playerIdText.text = $"Unity ID: {shortId}";
            }
            
            // Account Type
            if (accountTypeText != null)
            {
                string type = _auth.IsAnonymous ? "🎭 Gast (Anonym)" : "✓ Verknüpfter Account";
                accountTypeText.text = type;
            }

            // Server Status
            UpdateServerStatus(serverAuth 
                ? $"✓ Server: {apiClient?.PlayerId ?? "verbunden"}" 
                : "⏳ Server: Verbinde...");

            // Link-Button nur bei anonymen Accounts
            if (linkGoogleButton != null)
                linkGoogleButton.gameObject.SetActive(_auth.IsAnonymous);

            // Play-Button nur wenn vollständig auth
            if (playButton != null)
                playButton.interactable = serverAuth;
        }
        else
        {
            // Reset stats
            if (statsText != null)
                statsText.text = "";
        }
    }

    void UpdateServerStatus(string status)
    {
        if (serverStatusText != null)
            serverStatusText.text = status;
    }

    void ShowLoading(bool show)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(show);
        
        if (signInAnonymousButton != null)
            signInAnonymousButton.interactable = !show;
        
        if (signInGoogleButton != null)
            signInGoogleButton.interactable = !show;
        
        if (signInAppleButton != null)
            signInAppleButton.interactable = !show;
    }

    void ShowStatus(string message, bool isError)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = isError ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }
    }
}
