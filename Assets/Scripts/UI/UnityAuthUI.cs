// Assets/Scripts/UI/UnityAuthUI.cs
// Login UI für Unity Gaming Services Authentication

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
    [SerializeField] private Button signOutButton;
    [SerializeField] private Button linkGoogleButton;
    [SerializeField] private Button playButton;

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

        // Events
        _auth.OnSignedIn += OnSignedIn;
        _auth.OnSignedOut += OnSignedOut;
        _auth.OnSignInFailed += OnSignInFailed;

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

        // Initial state
        UpdateUI();
    }

    void OnDestroy()
    {
        if (_auth != null)
        {
            _auth.OnSignedIn -= OnSignedIn;
            _auth.OnSignedOut -= OnSignedOut;
            _auth.OnSignInFailed -= OnSignInFailed;
        }
    }

    // ==================== Button Handlers ====================

    async void OnAnonymousClicked()
    {
        ShowLoading(true);
        ShowStatus("Anonym einloggen...", false);
        await _auth.SignInAnonymouslyAsync();
        ShowLoading(false);
    }

    void OnGoogleClicked()
    {
        ShowLoading(true);
        ShowStatus("Google Login...", false);
        
        // Google Play Games Login triggern
        // Das ID Token kommt aus dem Google Play Games Plugin
        #if UNITY_ANDROID
        // Social.localUser.Authenticate mit Callback
        // Dann: _auth.SignInWithGoogleAsync(idToken)
        ShowStatus("Google Login: Implementiere PlayGames Plugin", true);
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
        // Apple Sign In Plugin triggern
        // Dann: _auth.SignInWithAppleAsync(idToken)
        ShowStatus("Apple Login: Implementiere Apple Sign In", true);
        ShowLoading(false);
        #else
        ShowStatus("Apple Login nur auf iOS", true);
        ShowLoading(false);
        #endif
    }

    void OnSignOutClicked()
    {
        _auth.SignOut();
    }

    async void OnLinkGoogleClicked()
    {
        ShowStatus("Account mit Google verknüpfen...", false);
        // Google ID Token holen, dann:
        // await _auth.LinkWithGoogleAsync(idToken);
        ShowStatus("Link: Implementiere PlayGames Plugin", true);
    }

    // ==================== Auth Events ====================

    void OnSignedIn(string playerId)
    {
        ShowLoading(false);
        UpdateUI();
    }

    void OnSignedOut()
    {
        UpdateUI();
    }

    void OnSignInFailed(string error)
    {
        ShowLoading(false);
        ShowStatus($"Login fehlgeschlagen: {error}", true);
    }

    // ==================== UI Helpers ====================

    void UpdateUI()
    {
        bool signedIn = _auth != null && _auth.IsSignedIn;

        if (loginPanel != null)
            loginPanel.SetActive(!signedIn);
        
        if (profilePanel != null)
            profilePanel.SetActive(signedIn);

        if (signedIn)
        {
            if (playerIdText != null)
                playerIdText.text = $"Player: {_auth.PlayerId.Substring(0, 8)}...";
            
            if (accountTypeText != null)
            {
                string type = _auth.IsAnonymous ? "Anonym (Gast)" : "Verknüpft";
                accountTypeText.text = type;
            }

            // Link-Button nur bei anonymen Accounts
            if (linkGoogleButton != null)
                linkGoogleButton.gameObject.SetActive(_auth.IsAnonymous);
        }

        ShowStatus("", false);
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
            statusText.color = isError ? Color.red : Color.white;
        }
    }
}
