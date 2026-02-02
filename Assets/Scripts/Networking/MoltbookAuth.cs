// Assets/Scripts/Networking/MoltbookAuth.cs
// Moltbook Identity Authentication für Unity
// Authentifiziert Spieler via Moltbook Identity Token

using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ValidationGame.Auth
{
    /// <summary>
    /// Response von Moltbook Identity Token Endpoint
    /// </summary>
    [Serializable]
    public class MoltbookIdentityResponse
    {
        public bool success;
        public string identity_token;
        public int expires_in;
        public string expires_at;
        public string error;
    }

    /// <summary>
    /// Response vom Game Server Auth Endpoint
    /// </summary>
    [Serializable]
    public class GameAuthResponse
    {
        public bool success;
        public string session_token;
        public string player_id;
        public string agent_name;
        public int karma;
        public int expires_in;
        public bool dev_mode;
        public string error;
    }

    /// <summary>
    /// Spieler-Profil vom Server
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string player_id;
        public string name;
        public int karma;
        public int total_score;
        public int matches_found;
        public int papers_validated;
        public float accuracy;
    }

    [Serializable]
    public class PlayerProfileResponse
    {
        public bool success;
        public PlayerProfile player;
        public string error;
    }

    /// <summary>
    /// Leaderboard Entry
    /// </summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string name;
        public int score;
        public int karma;
        public float accuracy;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public bool success;
        public LeaderboardEntry[] leaderboard;
    }

    /// <summary>
    /// Moltbook Authentication Manager
    /// Handles login via Moltbook Identity or Dev Mode
    /// </summary>
    public class MoltbookAuth : MonoBehaviour
    {
        [Header("Server Settings")]
        [Tooltip("Game Server URL (z.B. https://mcp.linn.games oder http://127.0.0.1:8089)")]
        [SerializeField] private string serverUrl = "http://127.0.0.1:8089";
        
        [Tooltip("Moltbook API URL")]
        [SerializeField] private string moltbookApiUrl = "https://www.moltbook.com/api/v1";
        
        [Tooltip("Game Audience für Token Verification")]
        [SerializeField] private string gameAudience = "mcp.linn.games";

        [Header("Dev Mode")]
        [Tooltip("Aktiviert Dev-Login ohne Moltbook")]
        [SerializeField] private bool devModeEnabled = true;
        
        [Tooltip("Device ID für Dev Mode")]
        [SerializeField] private string devDeviceId = "";

        [Header("Debug")]
        [SerializeField] private bool logRequests = true;

        // State
        private string _moltbookApiKey = "";
        private string _identityToken = "";
        private DateTime _identityTokenExpires = DateTime.MinValue;
        private string _sessionToken = "";
        private DateTime _sessionExpires = DateTime.MinValue;
        private PlayerProfile _currentPlayer;

        // Events
        public event Action<PlayerProfile> OnLoginSuccess;
        public event Action<string> OnLoginFailed;
        public event Action OnLogout;

        // Singleton
        public static MoltbookAuth Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Device ID generieren falls nicht gesetzt
            if (string.IsNullOrEmpty(devDeviceId))
            {
                devDeviceId = SystemInfo.deviceUniqueIdentifier;
            }

            // Gespeicherten API Key laden
            _moltbookApiKey = SecureStorage.LoadApiKey();
        }

        // ==================== Public API ====================

        /// <summary>
        /// Prüft ob eingeloggt
        /// </summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(_sessionToken) && DateTime.UtcNow < _sessionExpires;

        /// <summary>
        /// Aktuelles Spielerprofil
        /// </summary>
        public PlayerProfile CurrentPlayer => _currentPlayer;

        /// <summary>
        /// Session Token für API Requests
        /// </summary>
        public string SessionToken => _sessionToken;

        /// <summary>
        /// Login mit Moltbook API Key
        /// </summary>
        public async Task<bool> LoginWithMoltbook(string apiKey = null)
        {
            if (!string.IsNullOrEmpty(apiKey))
            {
                _moltbookApiKey = apiKey;
                SecureStorage.SaveApiKey(apiKey);
            }

            if (string.IsNullOrEmpty(_moltbookApiKey))
            {
                OnLoginFailed?.Invoke("Kein Moltbook API Key");
                return false;
            }

            try
            {
                // 1. Identity Token von Moltbook holen
                if (logRequests) Debug.Log("MoltbookAuth: Getting identity token...");
                
                var identityToken = await GetIdentityToken();
                if (string.IsNullOrEmpty(identityToken))
                {
                    OnLoginFailed?.Invoke("Konnte Identity Token nicht abrufen");
                    return false;
                }

                // 2. Mit Game Server authentifizieren
                if (logRequests) Debug.Log("MoltbookAuth: Authenticating with game server...");
                
                var authResult = await AuthenticateWithServer(identityToken);
                if (authResult == null || !authResult.success)
                {
                    OnLoginFailed?.Invoke(authResult?.error ?? "Server-Authentifizierung fehlgeschlagen");
                    return false;
                }

                // 3. Session speichern
                _sessionToken = authResult.session_token;
                _sessionExpires = DateTime.UtcNow.AddSeconds(authResult.expires_in);
                
                _currentPlayer = new PlayerProfile
                {
                    player_id = authResult.player_id,
                    name = authResult.agent_name,
                    karma = authResult.karma
                };

                if (logRequests) Debug.Log($"MoltbookAuth: Logged in as {authResult.agent_name} (Karma: {authResult.karma})");
                
                OnLoginSuccess?.Invoke(_currentPlayer);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"MoltbookAuth: Login error: {ex.Message}");
                OnLoginFailed?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Dev-Login ohne Moltbook (nur für Entwicklung!)
        /// </summary>
        public async Task<bool> LoginDevMode(string playerName = "DevPlayer")
        {
            if (!devModeEnabled)
            {
                OnLoginFailed?.Invoke("Dev Mode ist deaktiviert");
                return false;
            }

            try
            {
                if (logRequests) Debug.Log("MoltbookAuth: Dev login...");

                var url = $"{serverUrl}/api/auth/dev";
                var body = JsonUtility.ToJson(new DevAuthRequest { device_id = devDeviceId, name = playerName });

                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 10;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    OnLoginFailed?.Invoke($"Server error: {request.error}");
                    return false;
                }

                var response = JsonUtility.FromJson<GameAuthResponse>(request.downloadHandler.text);
                
                if (!response.success)
                {
                    OnLoginFailed?.Invoke(response.error ?? "Dev auth failed");
                    return false;
                }

                _sessionToken = response.session_token;
                _sessionExpires = DateTime.UtcNow.AddSeconds(response.expires_in);
                
                _currentPlayer = new PlayerProfile
                {
                    player_id = response.player_id,
                    name = response.agent_name,
                    karma = 0
                };

                if (logRequests) Debug.Log($"MoltbookAuth: Dev login as {response.agent_name}");
                
                OnLoginSuccess?.Invoke(_currentPlayer);
                return true;
            }
            catch (Exception ex)
            {
                OnLoginFailed?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Logout - Session löschen
        /// </summary>
        public void Logout()
        {
            _sessionToken = "";
            _sessionExpires = DateTime.MinValue;
            _currentPlayer = null;
            OnLogout?.Invoke();
        }

        /// <summary>
        /// Holt aktuelles Spielerprofil vom Server
        /// </summary>
        public async Task<PlayerProfile> GetProfile()
        {
            if (!IsLoggedIn) return null;

            var url = $"{serverUrl}/api/player/me";

            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {_sessionToken}");
            request.timeout = 10;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MoltbookAuth: Get profile failed: {request.error}");
                return null;
            }

            var response = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
            
            if (response.success && response.player != null)
            {
                _currentPlayer = response.player;
                return response.player;
            }

            return null;
        }

        /// <summary>
        /// Holt Leaderboard
        /// </summary>
        public async Task<LeaderboardEntry[]> GetLeaderboard(int limit = 10)
        {
            var url = $"{serverUrl}/api/leaderboard?limit={limit}";

            using var request = UnityWebRequest.Get(url);
            request.timeout = 10;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MoltbookAuth: Get leaderboard failed: {request.error}");
                return null;
            }

            var response = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text);
            return response.success ? response.leaderboard : null;
        }

        // ==================== Private Methods ====================

        private async Task<string> GetIdentityToken()
        {
            // Cached Token noch gültig?
            if (!string.IsNullOrEmpty(_identityToken) && DateTime.UtcNow.AddMinutes(5) < _identityTokenExpires)
            {
                return _identityToken;
            }

            var url = $"{moltbookApiUrl}/agents/me/identity-token";
            var body = $"{{\"audience\": \"{gameAudience}\"}}";

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {_moltbookApiKey}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MoltbookAuth: Get identity token failed: {request.error}");
                return null;
            }

            var response = JsonUtility.FromJson<MoltbookIdentityResponse>(request.downloadHandler.text);
            
            if (!response.success)
            {
                Debug.LogError($"MoltbookAuth: Identity token error: {response.error}");
                return null;
            }

            _identityToken = response.identity_token;
            _identityTokenExpires = DateTime.UtcNow.AddSeconds(response.expires_in);

            return _identityToken;
        }

        private async Task<GameAuthResponse> AuthenticateWithServer(string identityToken)
        {
            var url = $"{serverUrl}/api/auth/moltbook";

            using var request = new UnityWebRequest(url, "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("X-Moltbook-Identity", identityToken);
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MoltbookAuth: Server auth failed: {request.error}");
                return new GameAuthResponse { success = false, error = request.error };
            }

            return JsonUtility.FromJson<GameAuthResponse>(request.downloadHandler.text);
        }

        // Helper class for dev auth request
        [Serializable]
        private class DevAuthRequest
        {
            public string device_id;
            public string name;
        }
    }

    /// <summary>
    /// Sichere Speicherung für API Keys
    /// In Produktion: Platform Keychain verwenden!
    /// </summary>
    public static class SecureStorage
    {
        private const string PREFS_KEY = "moltbook_key_enc";

        public static void SaveApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                PlayerPrefs.DeleteKey(PREFS_KEY);
                return;
            }
            
            // Simple encoding - in Produktion VERSCHLÜSSELN!
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey));
            PlayerPrefs.SetString(PREFS_KEY, encoded);
            PlayerPrefs.Save();
        }

        public static string LoadApiKey()
        {
            var encoded = PlayerPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(encoded)) return "";

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch
            {
                return "";
            }
        }

        public static void ClearApiKey()
        {
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
        }
    }
}
