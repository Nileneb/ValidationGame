using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ValidationGame.Data;
using ValidationGame.Auth;

public class ApiClient : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://127.0.0.1:8089";
    [SerializeField] private string deviceId;

    [Header("Authentication")]
    [Tooltip("Erfordert Server-Auth vor API-Calls")]
    [SerializeField] private bool requireAuth = true;
    [Tooltip("Automatisch beim Server authentifizieren nach Unity Sign-In")]
    [SerializeField] private bool autoAuthWithServer = true;

    [Header("SSE Settings")]
    [SerializeField] private bool autoConnectSSE = true;
    [SerializeField] private float sseReconnectDelay = 5f;
    [SerializeField] private float sseHeartbeatTimeout = 30f;

    [Header("References")]
    public VoxelMeshBuilder voxelBuilder;
    public PaperInfoPanel infoPanel;
    public RuleMatcher ruleMatcher;

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;

    // Events
    public event Action<PaperValidatedEvent> OnPaperValidated;
    public event Action<LeaderboardEvent> OnLeaderboardUpdate;
    public event Action<NewPaperEvent> OnNewPaper;
    public event Action<JobEvent> OnJobReceived;
    public event Action<Molecule> OnActiveRuleLoaded;
    public event Action OnSSEConnected;
    public event Action OnSSEDisconnected;
    public event Action<string> OnServerAuthenticated;  // session_token
    public event Action<string> OnServerAuthFailed;     // error message

    // SSE Status
    private bool _sseConnected = false;
    private float _lastHeartbeat;
    private Coroutine _sseCoroutine;
    private Coroutine _jobStreamCoroutine;
    private int _sseReconnectAttempts = 0;

    // Server Auth Status
    private string _sessionToken = null;
    private string _playerId = null;
    private bool _isAuthenticatingWithServer = false;

    public bool IsSSEConnected => _sseConnected;
    public string DeviceId => deviceId;
    
    /// <summary>
    /// Prüft ob beim Server authentifiziert
    /// </summary>
    public bool IsServerAuthenticated => !string.IsNullOrEmpty(_sessionToken);
    
    /// <summary>
    /// Prüft ob Unity Auth eingeloggt
    /// </summary>
    public bool IsUnitySignedIn => UnityAuthManager.Instance != null && UnityAuthManager.Instance.IsSignedIn;
    
    /// <summary>
    /// Kombinierte Auth-Prüfung (Unity + Server)
    /// </summary>
    public bool IsFullyAuthenticated => IsUnitySignedIn && IsServerAuthenticated;
    
    /// <summary>
    /// Server Session Token (für externe Nutzung)
    /// </summary>
    public string SessionToken => _sessionToken;
    
    /// <summary>
    /// Server Player ID
    /// </summary>
    public string PlayerId => _playerId;

    void Start()
    {
        InitializeDeviceId();
        
        // Subscribe to Unity Auth events
        if (UnityAuthManager.Instance != null)
        {
            UnityAuthManager.Instance.OnSignedIn += OnUnitySignedIn;
            UnityAuthManager.Instance.OnSignedOut += OnUnitySignedOut;
            
            // Falls schon eingeloggt, direkt Server-Auth starten
            if (UnityAuthManager.Instance.IsSignedIn && autoAuthWithServer)
            {
                StartCoroutine(AuthenticateWithServer());
            }
        }
        else
        {
            // Kein Unity Auth Manager - Fallback auf anonyme Auth
            StartCoroutine(RegisterDevice());
            if (autoConnectSSE) ConnectSSE();
        }
    }

    private void OnUnitySignedIn(string unityPlayerId)
    {
        deviceId = unityPlayerId;
        if (logRequests) Debug.Log($"[ApiClient] Unity signed in: {unityPlayerId}");
        
        if (autoAuthWithServer)
        {
            StartCoroutine(AuthenticateWithServer());
        }
    }

    private void OnUnitySignedOut()
    {
        if (logRequests) Debug.Log("[ApiClient] Unity signed out - clearing server session");
        _sessionToken = null;
        _playerId = null;
        DisconnectSSE();
    }

    private void InitializeDeviceId()
    {
        // Nutze Unity Auth PlayerId wenn verfügbar
        if (UnityAuthManager.Instance != null && UnityAuthManager.Instance.IsSignedIn)
        {
            deviceId = UnityAuthManager.Instance.PlayerId;
            if (logRequests) Debug.Log($"[ApiClient] Using Unity PlayerId: {deviceId}");
            return;
        }

        // Fallback: lokale Device ID
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = PlayerPrefs.GetString("device_id", "");
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = SystemInfo.deviceUniqueIdentifier;
                PlayerPrefs.SetString("device_id", deviceId);
                PlayerPrefs.Save();
            }
        }
        if (logRequests) Debug.Log($"[ApiClient] Using Device ID: {deviceId}");
    }

    // ============================================================
    // SERVER AUTHENTICATION
    // ============================================================

    /// <summary>
    /// Authentifiziert beim PaperStream Server mit Unity idToken
    /// POST /api/auth/unity
    /// </summary>
    public IEnumerator AuthenticateWithServer()
    {
        if (_isAuthenticatingWithServer)
        {
            if (logRequests) Debug.Log("[ApiClient] Already authenticating...");
            yield break;
        }

        if (UnityAuthManager.Instance == null || !UnityAuthManager.Instance.IsSignedIn)
        {
            Debug.LogWarning("[ApiClient] Cannot auth with server - Unity not signed in");
            OnServerAuthFailed?.Invoke("Unity not signed in");
            yield break;
        }

        _isAuthenticatingWithServer = true;
        string idToken = UnityAuthManager.Instance.AccessToken;

        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogError("[ApiClient] Unity AccessToken is null/empty");
            _isAuthenticatingWithServer = false;
            OnServerAuthFailed?.Invoke("No access token");
            yield break;
        }

        string url = $"{serverUrl}/api/auth/unity";
        if (logRequests) Debug.Log($"[ApiClient] Authenticating with server: {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {idToken}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();

            _isAuthenticatingWithServer = false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<ServerAuthResponse>(json);

                    if (response.success)
                    {
                        _sessionToken = response.session_token;
                        _playerId = response.player_id;
                        
                        if (logRequests)
                        {
                            Debug.Log($"[ApiClient] ✅ Server auth successful!");
                            Debug.Log($"[ApiClient] Player ID: {_playerId}");
                            Debug.Log($"[ApiClient] Session expires in: {response.expires_in}s");
                        }

                        OnServerAuthenticated?.Invoke(_sessionToken);

                        // Jetzt SSE verbinden
                        if (autoConnectSSE) ConnectSSE();
                    }
                    else
                    {
                        Debug.LogError($"[ApiClient] Server auth failed: {response.error}");
                        OnServerAuthFailed?.Invoke(response.error);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ApiClient] Server auth parse error: {e.Message}");
                    OnServerAuthFailed?.Invoke(e.Message);
                }
            }
            else
            {
                string error = $"HTTP {request.responseCode}: {request.error}";
                Debug.LogError($"[ApiClient] Server auth request failed: {error}");
                
                // Versuche Error-Body zu lesen
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.LogError($"[ApiClient] Response: {request.downloadHandler.text}");
                }
                
                OnServerAuthFailed?.Invoke(error);
            }
        }
    }

    /// <summary>
    /// Anonyme Server-Auth (für lokales Testing ohne Unity)
    /// POST /api/auth/anonymous
    /// </summary>
    public IEnumerator AuthenticateAnonymously()
    {
        string url = $"{serverUrl}/api/auth/anonymous";
        string json = $"{{\"device_id\":\"{deviceId}\"}}";

        if (logRequests) Debug.Log($"[ApiClient] Anonymous auth: {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<ServerAuthResponse>(request.downloadHandler.text);
                    if (response.success)
                    {
                        _sessionToken = response.session_token;
                        _playerId = response.player_id;
                        if (logRequests) Debug.Log($"[ApiClient] ✅ Anonymous auth: {_playerId}");
                        OnServerAuthenticated?.Invoke(_sessionToken);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ApiClient] Anonymous auth error: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[ApiClient] Anonymous auth failed: {request.error}");
            }
        }
    }

    /// <summary>
    /// Fügt Session-Token Header zu Request hinzu
    /// </summary>
    private void AddAuthHeader(UnityWebRequest request)
    {
        if (!string.IsNullOrEmpty(_sessionToken))
        {
            request.SetRequestHeader("Authorization", $"Bearer {_sessionToken}");
        }
    }

    // ============================================================
    // NEUE API nach DATAMODEL.md
    // ============================================================

    /// <summary>
    /// GET /api/rule/active
    /// Lädt die aktive Rule als Molecule
    /// </summary>
    public IEnumerator FetchActiveRule(Action<Molecule, float, string> callback)
    {
        string url = $"{serverUrl}/api/rule/active";
        if (logRequests) Debug.Log($"[ApiClient] Fetching active rule from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            AddAuthHeader(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                if (logRequests) Debug.Log($"[ApiClient] Active Rule Response: {json.Substring(0, Mathf.Min(300, json.Length))}...");

                try
                {
                    var response = JsonUtility.FromJson<ActiveRuleResponse>(json);

                    if (response?.rule != null)
                    {
                        response.rule.DecodeAll();

                        if (ruleMatcher != null)
                        {
                            ruleMatcher.SetActiveRule(response.rule, response.threshold, response.question);
                        }

                        OnActiveRuleLoaded?.Invoke(response.rule);
                        callback?.Invoke(response.rule, response.threshold, response.question);
                    }
                    else
                    {
                        Debug.LogWarning("[ApiClient] Keine aktive Rule vom Server");
                        callback?.Invoke(null, 0.7f, "");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ApiClient] JSON Parse Error (active rule): {e.Message}");
                    callback?.Invoke(null, 0.7f, "");
                }
            }
            else
            {
                Debug.LogError($"[ApiClient] Failed to fetch active rule: {request.error}");
                callback?.Invoke(null, 0.7f, "");
            }
        }
    }

    /// <summary>
    /// SSE /api/jobs/stream
    /// Verbindet zum Job-Stream und empfängt Jobs in Echtzeit
    /// </summary>
    public void ConnectJobStream()
    {
        if (_jobStreamCoroutine != null)
        {
            StopCoroutine(_jobStreamCoroutine);
        }
        _jobStreamCoroutine = StartCoroutine(JobStreamListener());
    }

    public void DisconnectJobStream()
    {
        if (_jobStreamCoroutine != null)
        {
            StopCoroutine(_jobStreamCoroutine);
            _jobStreamCoroutine = null;
        }
    }

    private IEnumerator JobStreamListener()
    {
        string url = $"{serverUrl}/api/jobs/stream?device_id={UnityWebRequest.EscapeURL(deviceId)}";
        if (logRequests) Debug.Log($"[JobStream] Connecting to {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "GET"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "text/event-stream");
            request.SetRequestHeader("Cache-Control", "no-cache");
            AddAuthHeader(request);
            request.timeout = 0;

            var operation = request.SendWebRequest();

            StringBuilder buffer = new StringBuilder();
            long lastPosition = 0;

            while (!operation.isDone)
            {
                if (request.downloadHandler.data != null)
                {
                    long currentLength = (long)request.downloadedBytes;
                    if (currentLength > lastPosition)
                    {
                        byte[] newBytes = new byte[currentLength - lastPosition];
                        Array.Copy(request.downloadHandler.data, lastPosition, newBytes, 0, newBytes.Length);
                        string newData = Encoding.UTF8.GetString(newBytes);
                        buffer.Append(newData);
                        ProcessJobStreamBuffer(buffer);
                        lastPosition = currentLength;
                    }
                }
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[JobStream] Error: {request.error}");
            }
        }

        // Reconnect
        yield return new WaitForSeconds(sseReconnectDelay);
        ConnectJobStream();
    }

    private void ProcessJobStreamBuffer(StringBuilder buffer)
    {
        string content = buffer.ToString();
        while (content.Contains("\n\n"))
        {
            int eventEnd = content.IndexOf("\n\n");
            string eventBlock = content.Substring(0, eventEnd);
            content = content.Substring(eventEnd + 2);
            ParseJobStreamEvent(eventBlock);
        }
        buffer.Clear();
        buffer.Append(content);
    }

    private void ParseJobStreamEvent(string eventBlock)
    {
        string eventType = "";
        string eventData = "";

        foreach (string line in eventBlock.Split('\n'))
        {
            if (line.StartsWith("event:")) eventType = line.Substring(6).Trim();
            else if (line.StartsWith("data:")) eventData = line.Substring(5).Trim();
        }

        if (eventType == "job" && !string.IsNullOrEmpty(eventData))
        {
            try
            {
                var job = JsonUtility.FromJson<JobEvent>(eventData);
                if (job != null)
                {
                    job.chunk?.Decode();
                    OnJobReceived?.Invoke(job);
                    if (logRequests) Debug.Log($"[JobStream] Job received: {job.job_id}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[JobStream] Parse error: {e.Message}");
            }
        }
    }

    /// <summary>
    /// POST /api/jobs/{job_id}/response
    /// Sendet Spieler-Aktion an Server
    /// </summary>
    public IEnumerator SubmitJobResponse(JobResponse response, Action<bool, JobResponseResult> callback)
    {
        string url = $"{serverUrl}/api/jobs/{response.job_id}/response";
        string json = JsonUtility.ToJson(response);

        if (logRequests) Debug.Log($"[ApiClient] Submitting response to {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            AddAuthHeader(request);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var result = JsonUtility.FromJson<JobResponseResult>(request.downloadHandler.text);
                    callback?.Invoke(true, result);
                }
                catch
                {
                    callback?.Invoke(true, null);
                }
            }
            else
            {
                Debug.LogError($"[ApiClient] Failed to submit response: {request.error}");
                callback?.Invoke(false, null);
            }
        }
    }

    // ============================================================
    // PLAYER API
    // ============================================================

    /// <summary>
    /// GET /api/player/me
    /// Holt eigenes Spielerprofil
    /// </summary>
    public IEnumerator FetchMyProfile(Action<PlayerProfile> callback)
    {
        if (!IsServerAuthenticated)
        {
            Debug.LogWarning("[ApiClient] Not authenticated - cannot fetch profile");
            callback?.Invoke(null);
            yield break;
        }

        string url = $"{serverUrl}/api/player/me";
        if (logRequests) Debug.Log($"[ApiClient] Fetching profile from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            AddAuthHeader(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var profile = JsonUtility.FromJson<PlayerProfile>(request.downloadHandler.text);
                    callback?.Invoke(profile);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ApiClient] Profile parse error: {e.Message}");
                    callback?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[ApiClient] Failed to fetch profile: {request.error}");
                callback?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// GET /api/leaderboard
    /// Holt Leaderboard
    /// </summary>
    public IEnumerator FetchLeaderboard(int limit, Action<List<LeaderboardEntry>> callback)
    {
        string url = $"{serverUrl}/api/leaderboard?limit={limit}";
        if (logRequests) Debug.Log($"[ApiClient] Fetching leaderboard from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text);
                    callback?.Invoke(response?.entries ?? new List<LeaderboardEntry>());
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ApiClient] Leaderboard parse error: {e.Message}");
                    callback?.Invoke(new List<LeaderboardEntry>());
                }
            }
            else
            {
                Debug.LogError($"[ApiClient] Failed to fetch leaderboard: {request.error}");
                callback?.Invoke(new List<LeaderboardEntry>());
            }
        }
    }

    // ============================================================
    // LEGACY SSE (Unity-Stream)
    // ============================================================

    public void ConnectSSE()
    {
        if (_sseCoroutine != null) StopCoroutine(_sseCoroutine);
        _sseCoroutine = StartCoroutine(SSEListener());
    }

    public void DisconnectSSE()
    {
        if (_sseCoroutine != null)
        {
            StopCoroutine(_sseCoroutine);
            _sseCoroutine = null;
        }
        _sseConnected = false;
        OnSSEDisconnected?.Invoke();
    }

    private IEnumerator SSEListener()
    {
        string url = $"{serverUrl}/api/stream/unity?client_id={UnityWebRequest.EscapeURL(deviceId)}";
        if (logRequests) Debug.Log($"[SSE] Connecting to {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "GET"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "text/event-stream");
            request.SetRequestHeader("Cache-Control", "no-cache");
            AddAuthHeader(request);
            request.timeout = 0;

            var operation = request.SendWebRequest();
            StringBuilder buffer = new StringBuilder();
            long lastPosition = 0;
            _lastHeartbeat = Time.time;

            while (!operation.isDone)
            {
                if (Time.time - _lastHeartbeat > sseHeartbeatTimeout)
                {
                    if (logRequests) Debug.LogWarning("[SSE] Heartbeat Timeout");
                    break;
                }

                if (request.downloadHandler.data != null)
                {
                    long currentLength = (long)request.downloadedBytes;
                    if (currentLength > lastPosition)
                    {
                        byte[] newBytes = new byte[currentLength - lastPosition];
                        Array.Copy(request.downloadHandler.data, lastPosition, newBytes, 0, newBytes.Length);
                        buffer.Append(Encoding.UTF8.GetString(newBytes));
                        ProcessSSEBuffer(buffer);
                        lastPosition = currentLength;
                    }
                }
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SSE] Error: {request.error}");
            }
        }

        _sseConnected = false;
        OnSSEDisconnected?.Invoke();

        _sseReconnectAttempts++;
        float delay = Mathf.Min(sseReconnectDelay * _sseReconnectAttempts, 60f);
        yield return new WaitForSeconds(delay);
        ConnectSSE();
    }

    private void ProcessSSEBuffer(StringBuilder buffer)
    {
        string content = buffer.ToString();
        while (content.Contains("\n\n"))
        {
            int eventEnd = content.IndexOf("\n\n");
            string eventBlock = content.Substring(0, eventEnd);
            content = content.Substring(eventEnd + 2);
            ParseSSEEvent(eventBlock);
        }
        buffer.Clear();
        buffer.Append(content);
    }

    private void ParseSSEEvent(string eventBlock)
    {
        string eventType = "";
        string eventData = "";

        foreach (string line in eventBlock.Split('\n'))
        {
            if (line.StartsWith("event:")) eventType = line.Substring(6).Trim();
            else if (line.StartsWith("data:")) eventData = line.Substring(5).Trim();
        }

        if (string.IsNullOrEmpty(eventType) || string.IsNullOrEmpty(eventData)) return;

        HandleSSEEvent(eventType, eventData);
    }

    private void HandleSSEEvent(string eventType, string data)
    {
        _lastHeartbeat = Time.time;

        switch (eventType)
        {
            case "connected":
                _sseConnected = true;
                _sseReconnectAttempts = 0;
                OnSSEConnected?.Invoke();
                break;

            case "heartbeat":
                break;

            case "paper_validated":
                HandlePaperValidatedEvent(data);
                break;

            case "leaderboard_update":
                HandleLeaderboardEvent(data);
                break;

            case "new_paper":
                HandleNewPaperEvent(data);
                break;

            default:
                if (logRequests) Debug.Log($"[SSE] Unknown event: {eventType}");
                break;
        }
    }

    private void HandlePaperValidatedEvent(string data)
    {
        try
        {
            var evt = JsonUtility.FromJson<PaperValidatedEventAlt>(data);
            if (evt?.paper_id != null)
            {
                var paperEvt = new PaperValidatedEvent
                {
                    paper_id = evt.paper_id,
                    title = evt.title,
                    voxel_data = evt.voxel_data,
                    thumbnail_base64 = evt.thumbnail_base64,
                    timestamp = evt.timestamp
                };
                OnPaperValidated?.Invoke(paperEvt);

                if (evt.voxel_data != null && voxelBuilder != null)
                    voxelBuilder.BuildMesh(evt.voxel_data);

                if (infoPanel != null)
                    infoPanel.DisplayPaper(paperEvt, evt.rules_results);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SSE] Parse error (paper_validated): {e.Message}");
        }
    }

    private void HandleLeaderboardEvent(string data)
    {
        try
        {
            var evt = JsonUtility.FromJson<LeaderboardEvent>(data);
            OnLeaderboardUpdate?.Invoke(evt);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SSE] Parse error (leaderboard): {e.Message}");
        }
    }

    private void HandleNewPaperEvent(string data)
    {
        try
        {
            var evt = JsonUtility.FromJson<NewPaperEvent>(data);
            OnNewPaper?.Invoke(evt);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SSE] Parse error (new_paper): {e.Message}");
        }
    }

    // ============================================================
    // LEGACY REST API
    // ============================================================

    public IEnumerator RegisterDevice()
    {
        string url = $"{serverUrl}/api/devices/register";
        string json = $"{{\"device_id\":\"{deviceId}\",\"device_name\":\"Unity\",\"device_model\":\"{SystemInfo.deviceModel}\",\"os_version\":\"{SystemInfo.operatingSystem}\",\"app_version\":\"1.0.0\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("[ApiClient] Device registered!");
            else
                Debug.LogWarning($"[ApiClient] Device registration failed: {request.error}");
        }
    }

    public IEnumerator FetchJobs(int limit, Action<List<VoxelData>> callback)
    {
        string url = $"{serverUrl}/api/jobs/next?device_id={UnityWebRequest.EscapeURL(deviceId)}&limit={limit}";
        if (logRequests) Debug.Log($"[ApiClient] Fetching jobs from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            AddAuthHeader(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                if (logRequests) Debug.Log($"[ApiClient] Jobs response: {json.Substring(0, Mathf.Min(200, json.Length))}...");
                
                try
                {
                    // Server returns single job: {"status": "assigned", "job": {...}}
                    SingleJobResponse response = JsonUtility.FromJson<SingleJobResponse>(json);
                    
                    List<VoxelData> jobs = new List<VoxelData>();
                    if (response != null && response.status == "assigned" && response.job != null)
                    {
                        jobs.Add(response.job);
                        if (logRequests) Debug.Log($"[ApiClient] Got job for paper: {response.job.paper_id}");
                    }
                    else if (response != null && response.status == "no_jobs")
                    {
                        if (logRequests) Debug.Log("[ApiClient] No jobs available");
                    }
                    
                    callback?.Invoke(jobs);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ApiClient] JSON Parse Error: {e.Message}\nJSON: {json.Substring(0, Mathf.Min(500, json.Length))}");
                    callback?.Invoke(new List<VoxelData>());
                }
            }
            else
            {
                Debug.LogError($"[ApiClient] Failed to fetch jobs: {request.error}");
                callback?.Invoke(new List<VoxelData>());
            }
        }
    }

    public IEnumerator SubmitResults(List<ValidationResult> results, Action<bool> callback)
    {
        string url = $"{serverUrl}/api/validation/submit";
        SubmitRequest submitData = new SubmitRequest { device_id = deviceId, results = results };
        string json = JsonUtility.ToJson(submitData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            AddAuthHeader(request);
            yield return request.SendWebRequest();

            callback?.Invoke(request.result == UnityWebRequest.Result.Success);
        }
    }

    public IEnumerator FetchActiveRules(Action<List<RuleData>> callback)
    {
        string url = $"{serverUrl}/api/rules";
        if (logRequests) Debug.Log($"[ApiClient] Fetching rules from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            AddAuthHeader(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    RulesResponse response = JsonUtility.FromJson<RulesResponse>(request.downloadHandler.text);
                    callback?.Invoke(response?.rules ?? new List<RuleData>());
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ApiClient] JSON Parse Error: {e.Message}");
                    callback?.Invoke(new List<RuleData>());
                }
            }
            else
            {
                Debug.LogError($"[ApiClient] Failed to fetch rules: {request.error}");
                callback?.Invoke(new List<RuleData>());
            }
        }
    }

    public string GetDeviceId() => deviceId;
    public void SetServerUrl(string url) => serverUrl = url;

    void OnDestroy()
    {
        DisconnectSSE();
        
        if (UnityAuthManager.Instance != null)
        {
            UnityAuthManager.Instance.OnSignedIn -= OnUnitySignedIn;
            UnityAuthManager.Instance.OnSignedOut -= OnUnitySignedOut;
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) DisconnectSSE();
        else if (autoConnectSSE && IsServerAuthenticated) ConnectSSE();
    }
}

// ============================================================
// DATA CLASSES
// ============================================================

[Serializable]
public class ServerAuthResponse
{
    public bool success;
    public string session_token;
    public string player_id;
    public string unity_player_id;
    public int expires_in;
    public string error;
    public bool anonymous;
}

[Serializable]
public class PlayerProfile
{
    public string player_id;
    public string unity_player_id;
    public int total_score;
    public int matches_found;
    public int papers_validated;
    public float accuracy;
    public string created_at;
    public string last_login;
}

[Serializable]
public class LeaderboardResponse
{
    public List<LeaderboardEntry> entries;
}

[Serializable]
public class JobsResponse
{
    public List<VoxelData> jobs;  // For batch endpoint (if used)
}

/// <summary>
/// Response from GET /api/jobs/next - returns single job
/// </summary>
[Serializable]
public class SingleJobResponse
{
    public string status;      // "assigned" | "no_jobs" | "error"
    public VoxelData job;      // The job data (null if no_jobs)
    public string device_id;
}

[Serializable]
public class RulesResponse
{
    public List<RuleData> rules;
}

[Serializable]
public class SubmitRequest
{
    public string device_id;
    public List<ValidationResult> results;
}

[Serializable]
public class ValidationResult
{
    public string job_id;
    public string paper_id;
    public string rule_id;
    public string section;
    public bool is_match;
    public float similarity;
    public int points_earned;
}

[Serializable]
public class RuleData
{
    public string rule_id;
    public string question;
    public float threshold;
    public bool is_active;
    [NonSerialized] public float[] pos_embedding;
    [NonSerialized] public float[] neg_embedding;
}

/// <summary>
/// Job Event aus SSE /api/jobs/stream
/// </summary>
[Serializable]
public class JobEvent
{
    public string job_id;
    public string paper_id;
    public Chunk chunk;
    public int timeout_ms;
}

/// <summary>
/// Server-Response nach POST /api/jobs/{job_id}/response
/// </summary>
[Serializable]
public class JobResponseResult
{
    public bool accepted;
    public int points;
    public string message;
    public bool consensus_reached;
    public string final_classification;
}

// SSE Event Classes
[Serializable]
public class PaperValidatedEvent
{
    public string paper_id;
    public string device_id;
    public string title;
    public VoxelGridData voxel_data;
    public string thumbnail_base64;
    public string timestamp;
    public ValidationResultSSE result;
}

[Serializable]
public class ValidationResultSSE
{
    public string rule_id;
    public float similarity;
    public bool is_match;
    public int points;
}

[Serializable]
public class PaperValidatedEventAlt
{
    public string paper_id;
    public string title;
    public RuleResultSSE[] rules_results;
    public VoxelGridData voxel_data;
    public string thumbnail_base64;
    public string timestamp;
}

[Serializable]
public class RuleResultSSE
{
    public string rule_id;
    public string rule_name;
    public bool passed;
    public float similarity;
    public bool is_match;
    public int points;
}

[Serializable]
public class LeaderboardEntry
{
    public string device_id;
    public string player_name;
    public string player_id;
    public int total_points;
    public int score;
    public float accuracy;
    public int rank;
}

[Serializable]
public class LeaderboardEvent
{
    public LeaderboardEntry[] top_players;
    public int[] changed_ranks;
}

[Serializable]
public class NewPaperEvent
{
    public string paper_id;
    public string title;
    public int sections_count;
}

[Serializable]
public class VoxelGridData
{
    public int[] grid_size;
    public float[][] voxels;
    public VoxelStatsData stats;
    public int page;

    public int GridX => grid_size != null && grid_size.Length > 0 ? grid_size[0] : 8;
    public int GridY => grid_size != null && grid_size.Length > 1 ? grid_size[1] : 8;
    public int GridZ => grid_size != null && grid_size.Length > 2 ? grid_size[2] : 12;
}

[Serializable]
public class VoxelStatsData
{
    public int total;
    public float density_avg;
    public float fill_ratio;
}

[Serializable]
public class ColoredVoxelData
{
    public int[] grid_size;
    public float[][] voxels;
    public bool colored;
    public VoxelStatsData stats;
}

// VoxelData and VoxelColor are defined in Assets/Scripts/Data/VoxelData.cs
// to avoid duplicate definitions
