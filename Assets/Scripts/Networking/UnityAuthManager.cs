// Assets/Scripts/Networking/UnityAuthManager.cs
// Unity Gaming Services Authentication Manager
// Nutzt das offizielle Unity Authentication SDK

using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace ValidationGame.Auth
{
    /// <summary>
    /// Verwaltet Unity Authentication (UGS)
    /// Unterstützt: Anonymous, Google, Apple, Discord, Steam
    /// </summary>
    public class UnityAuthManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Automatisch anonym einloggen beim Start")]
        [SerializeField] private bool autoSignInAnonymous = true;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;

        // Events
        public event Action<string> OnSignedIn;      // PlayerId
        public event Action OnSignedOut;
        public event Action<string> OnSignInFailed;  // Error message

        // Singleton
        public static UnityAuthManager Instance { get; private set; }

        // Properties
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => AuthenticationService.Instance.PlayerId;
        public string AccessToken => AuthenticationService.Instance.AccessToken;
        public bool IsAnonymous => AuthenticationService.Instance.SessionTokenExists && 
                                   string.IsNullOrEmpty(AuthenticationService.Instance.PlayerInfo?.Id);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            await InitializeUnityServices();
            
            if (autoSignInAnonymous && !IsSignedIn)
            {
                await SignInAnonymouslyAsync();
            }
        }

        // ==================== Initialization ====================

        private async Task InitializeUnityServices()
        {
            try
            {
                await UnityServices.InitializeAsync();
                Log("Unity Services initialized");

                // Event handlers
                AuthenticationService.Instance.SignedIn += () =>
                {
                    Log($"Signed in: {PlayerId}");
                    OnSignedIn?.Invoke(PlayerId);
                };

                AuthenticationService.Instance.SignedOut += () =>
                {
                    Log("Signed out");
                    OnSignedOut?.Invoke();
                };

                AuthenticationService.Instance.SignInFailed += (err) =>
                {
                    Log($"Sign in failed: {err.Message}", true);
                    OnSignInFailed?.Invoke(err.Message);
                };
            }
            catch (Exception e)
            {
                Log($"Failed to initialize Unity Services: {e.Message}", true);
            }
        }

        // ==================== Anonymous Sign-In ====================

        /// <summary>
        /// Anonym einloggen (kein Account nötig)
        /// </summary>
        public async Task<bool> SignInAnonymouslyAsync()
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (AuthenticationException ex)
            {
                Log($"Anonymous sign-in failed: {ex.Message}", true);
                OnSignInFailed?.Invoke(ex.Message);
                return false;
            }
            catch (RequestFailedException ex)
            {
                Log($"Anonymous sign-in request failed: {ex.Message}", true);
                OnSignInFailed?.Invoke(ex.Message);
                return false;
            }
        }

        // ==================== Google Sign-In ====================

        /// <summary>
        /// Mit Google einloggen
        /// Benötigt Google Play Games Plugin und ID Token
        /// </summary>
        public async Task<bool> SignInWithGoogleAsync(string idToken)
        {
            try
            {
                await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
                return true;
            }
            catch (AuthenticationException ex)
            {
                Log($"Google sign-in failed: {ex.Message}", true);
                OnSignInFailed?.Invoke(ex.Message);
                return false;
            }
            catch (RequestFailedException ex)
            {
                Log($"Google sign-in request failed: {ex.Message}", true);
                OnSignInFailed?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Anonymen Account mit Google verknüpfen
        /// </summary>
        public async Task<bool> LinkWithGoogleAsync(string idToken)
        {
            try
            {
                await AuthenticationService.Instance.LinkWithGoogleAsync(idToken);
                Log("Linked with Google");
                return true;
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                Log("Account already linked with Google", true);
                OnSignInFailed?.Invoke("Account bereits mit Google verknüpft");
                return false;
            }
            catch (Exception ex)
            {
                Log($"Link with Google failed: {ex.Message}", true);
                OnSignInFailed?.Invoke(ex.Message);
                return false;
            }
        }

        // ==================== Apple Sign-In ====================

        /// <summary>
        /// Mit Apple einloggen (iOS)
        /// </summary>
        public async Task<bool> SignInWithAppleAsync(string idToken)
        {
            try
            {
                await AuthenticationService.Instance.SignInWithAppleAsync(idToken);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Apple sign-in failed: {ex.Message}", true);
                OnSignInFailed?.Invoke(ex.Message);
                return false;
            }
        }

        // ==================== Steam Sign-In ====================

        /// <summary>
        /// Mit Steam einloggen
        /// </summary>
        public async Task<bool> SignInWithSteamAsync(string sessionTicket)
        {
            try
            {
                await AuthenticationService.Instance.SignInWithSteamAsync(sessionTicket);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Steam sign-in failed: {ex.Message}", true);
                OnSignInFailed?.Invoke(ex.Message);
                return false;
            }
        }

        // ==================== Sign Out ====================

        /// <summary>
        /// Ausloggen
        /// </summary>
        public void SignOut()
        {
            AuthenticationService.Instance.SignOut();
        }

        /// <summary>
        /// Account komplett löschen
        /// </summary>
        public async Task DeleteAccountAsync()
        {
            try
            {
                await AuthenticationService.Instance.DeleteAccountAsync();
                Log("Account deleted");
            }
            catch (Exception ex)
            {
                Log($"Delete account failed: {ex.Message}", true);
            }
        }

        // ==================== Player Info ====================

        /// <summary>
        /// Holt Player Info vom Server
        /// </summary>
        public async Task<PlayerInfo> GetPlayerInfoAsync()
        {
            try
            {
                return await AuthenticationService.Instance.GetPlayerInfoAsync();
            }
            catch (Exception ex)
            {
                Log($"Get player info failed: {ex.Message}", true);
                return null;
            }
        }

        // ==================== Helper ====================

        private void Log(string message, bool isError = false)
        {
            if (!logEvents) return;
            
            if (isError)
                Debug.LogError($"[UnityAuth] {message}");
            else
                Debug.Log($"[UnityAuth] {message}");
        }
    }
}
