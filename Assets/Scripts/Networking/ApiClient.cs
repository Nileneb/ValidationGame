
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ValidationGame.Data;

public class ApiClient : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://127.0.0.1:8089";
    [SerializeField] private string deviceId;

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

    // SSE Status
    private bool _sseConnected = false;
    private float _lastHeartbeat;
    private Coroutine _sseCoroutine;
    private Coroutine _jobStreamCoroutine;
    private int _sseReconnectAttempts = 0;

    public bool IsSSEConnected => _sseConnected;
    public string DeviceId => deviceId;

    void Start()
    {
        InitializeDeviceId();
        StartCoroutine(RegisterDevice());

        if (autoConnectSSE)
        {
            ConnectSSE();
        }
    }

    private void InitializeDeviceId()
    {
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
        if (logRequests) Debug.Log($"ApiClient: Device ID = {deviceId}");
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
        if (logRequests) Debug.Log($"ApiClient: Fetching active rule from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                if (logRequests) Debug.Log($"ApiClient: Active Rule Response: {json.Substring(0, Mathf.Min(300, json.Length))}...");

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
                        Debug.LogWarning("ApiClient: Keine aktive Rule vom Server");
                        callback?.Invoke(null, 0.7f, "");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"ApiClient: JSON Parse Error (active rule): {e.Message}");
                    callback?.Invoke(null, 0.7f, "");
                }
            }
            else
            {
                Debug.LogError($"ApiClient: Failed to fetch active rule: {request.error}");
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

        if (logRequests) Debug.Log($"ApiClient: Submitting response to {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

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
                Debug.LogError($"ApiClient: Failed to submit response: {request.error}");
                callback?.Invoke(false, null);
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
                Debug.Log("ApiClient: Device registered!");
            else
                Debug.LogWarning($"ApiClient: Device registration failed: {request.error}");
        }
    }

    public IEnumerator FetchJobs(int limit, Action<List<VoxelData>> callback)
    {
        string url = $"{serverUrl}/api/jobs/next?device_id={UnityWebRequest.EscapeURL(deviceId)}&limit={limit}";
        if (logRequests) Debug.Log($"ApiClient: Fetching jobs from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                try
                {
                    JobsResponse response = JsonUtility.FromJson<JobsResponse>(json);
                    callback?.Invoke(response?.jobs ?? new List<VoxelData>());
                }
                catch (Exception e)
                {
                    Debug.LogError($"ApiClient: JSON Parse Error: {e.Message}");
                    callback?.Invoke(new List<VoxelData>());
                }
            }
            else
            {
                Debug.LogError($"ApiClient: Failed to fetch jobs: {request.error}");
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
            yield return request.SendWebRequest();

            callback?.Invoke(request.result == UnityWebRequest.Result.Success);
        }
    }

    public IEnumerator FetchActiveRules(Action<List<RuleData>> callback)
    {
        string url = $"{serverUrl}/api/rules";
        if (logRequests) Debug.Log($"ApiClient: Fetching rules from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
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
                    Debug.LogError($"ApiClient: JSON Parse Error: {e.Message}");
                    callback?.Invoke(new List<RuleData>());
                }
            }
            else
            {
                Debug.LogError($"ApiClient: Failed to fetch rules: {request.error}");
                callback?.Invoke(new List<RuleData>());
            }
        }
    }

    public string GetDeviceId() => deviceId;
    public void SetServerUrl(string url) => serverUrl = url;

    void OnDestroy() => DisconnectSSE();

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) DisconnectSSE();
        else if (autoConnectSSE) ConnectSSE();
    }
}

// ============================================================
// DATA CLASSES
// ============================================================

[Serializable]
public class JobsResponse
{
    public List<VoxelData> jobs;
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
    public int total_points;
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

/// <summary>
/// Farbe für VoxelData (RGB 0-1)
/// </summary>
[Serializable]
public class VoxelColor
{
    public float r;
    public float g;
    public float b;

    public VoxelColor() { r = 0.5f; g = 0.5f; b = 0.5f; }
    public VoxelColor(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
    public Color ToUnityColor() => new Color(r, g, b);
}

/// <summary>
/// Legacy VoxelData für Paper-Jobs (backward compatibility)
/// Wrapper für GET /api/jobs/next Response
/// </summary>
[Serializable]
public class VoxelData
{
    public string job_id;
    public string paper_id;
    public string section;
    public string section_id;
    public string section_text;
    public string embedding_b64;
    public string rule_id;  // Optional: Zugehörige Rule
    public VoxelColor color; // Optional: Farbe

    // Runtime decoded
    [NonSerialized] public float[] embedding;
    [NonSerialized] public float[] section_embedding;
    [NonSerialized] public float[] pos_embedding;  // Alias für rule matching

    /// <summary>
    /// Dekodiert Base64 Embedding
    /// </summary>
    public void DecodeData()
    {
        if (!string.IsNullOrEmpty(embedding_b64))
        {
            try
            {
                byte[] bytes = System.Convert.FromBase64String(embedding_b64);
                embedding = new float[bytes.Length / 4];
                System.Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
                section_embedding = embedding; // Alias
                pos_embedding = embedding;     // Alias
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VoxelData.DecodeData: Base64 error: {e.Message}");
                embedding = new float[768];
                section_embedding = embedding;
                pos_embedding = embedding;
            }
        }
    }
}
