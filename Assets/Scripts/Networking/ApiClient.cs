// Scripts/Networking/ApiClient.cs
// REST API Client für Kommunikation mit dem MCP-Server
// Holt Jobs, submittet Validation Results, lädt Regeln
// Inkl. SSE (Server-Sent Events) für Echtzeit-Updates

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class ApiClient : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://127.0.0.1:8089";
    [SerializeField] private string deviceId;

    [Header("SSE Settings")]
    [Tooltip("SSE-Stream automatisch beim Start verbinden")]
    [SerializeField] private bool autoConnectSSE = true;

    [Tooltip("Verzögerung vor Reconnect bei Verbindungsabbruch (Sekunden)")]
    [SerializeField] private float sseReconnectDelay = 5f;

    [Tooltip("Timeout ohne Heartbeat bevor Reconnect (Sekunden)")]
    [SerializeField] private float sseHeartbeatTimeout = 30f;

    [Header("SSE References")]
    [Tooltip("VoxelMeshBuilder für automatisches Mesh-Rendering")]
    public VoxelMeshBuilder voxelBuilder;

    [Tooltip("PaperInfoPanel für UI-Updates")]
    public PaperInfoPanel infoPanel;

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;

    // SSE Events für andere Scripts
    public event Action<PaperValidatedEvent> OnPaperValidated;
    public event Action<LeaderboardEvent> OnLeaderboardUpdate;
    public event Action<NewPaperEvent> OnNewPaper;
    public event Action OnSSEConnected;
    public event Action OnSSEDisconnected;

    // SSE Status
    private bool _sseConnected = false;
    private float _lastHeartbeat;
    private Coroutine _sseCoroutine;
    private int _sseReconnectAttempts = 0;

    public bool IsSSEConnected => _sseConnected;

    void Start()
    {
        // Device ID generieren oder aus PlayerPrefs laden
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

        if (logRequests)
        {
            Debug.Log($"ApiClient initialisiert mit Device ID: {deviceId}");
        }

        // Device beim Server registrieren
        StartCoroutine(RegisterDevice());

        // SSE-Stream starten falls aktiviert
        if (autoConnectSSE)
        {
            ConnectSSE();
        }
    }

    // ===== SSE FUNKTIONEN =====

    /// <summary>
    /// SSE-Verbindung zum Server herstellen
    /// </summary>
    public void ConnectSSE()
    {
        if (_sseCoroutine != null)
        {
            StopCoroutine(_sseCoroutine);
        }
        _sseCoroutine = StartCoroutine(SSEListener());
    }

    /// <summary>
    /// SSE-Verbindung trennen
    /// </summary>
    public void DisconnectSSE()
    {
        if (_sseCoroutine != null)
        {
            StopCoroutine(_sseCoroutine);
            _sseCoroutine = null;
        }
        _sseConnected = false;
        OnSSEDisconnected?.Invoke();

        if (logRequests) Debug.Log("[SSE] Verbindung getrennt");
    }

    /// <summary>
    /// SSE Listener Coroutine
    /// </summary>
    private IEnumerator SSEListener()
    {
        string url = $"{serverUrl}/api/stream/unity?client_id={UnityWebRequest.EscapeURL(deviceId)}";

        if (logRequests) Debug.Log($"[SSE] Verbinde zu {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "GET"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "text/event-stream");
            request.SetRequestHeader("Cache-Control", "no-cache");
            // Note: "Connection" header wird automatisch von Unity verwaltet
            request.timeout = 0; // Kein Timeout für SSE

            var operation = request.SendWebRequest();

            StringBuilder buffer = new StringBuilder();
            long lastPosition = 0;
            _lastHeartbeat = Time.time;

            while (!operation.isDone)
            {
                // Heartbeat-Timeout prüfen
                if (Time.time - _lastHeartbeat > sseHeartbeatTimeout)
                {
                    if (logRequests) Debug.LogWarning("[SSE] Heartbeat Timeout - Reconnecting...");
                    break;
                }

                // Neue Daten lesen
                if (request.downloadHandler.data != null)
                {
                    long currentLength = (long)request.downloadedBytes;

                    if (currentLength > lastPosition)
                    {
                        byte[] newBytes = new byte[currentLength - lastPosition];
                        Array.Copy(request.downloadHandler.data, lastPosition, newBytes, 0, newBytes.Length);
                        string newData = Encoding.UTF8.GetString(newBytes);

                        buffer.Append(newData);
                        ProcessSSEBuffer(buffer);
                        lastPosition = currentLength;
                    }
                }

                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SSE] Fehler: {request.error}");
            }
        }

        // Verbindung verloren → Reconnect
        _sseConnected = false;
        OnSSEDisconnected?.Invoke();

        _sseReconnectAttempts++;
        float delay = Mathf.Min(sseReconnectDelay * _sseReconnectAttempts, 60f);

        if (logRequests) Debug.Log($"[SSE] Reconnect in {delay:F1}s (Versuch {_sseReconnectAttempts})...");

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
            if (line.StartsWith("event:"))
                eventType = line.Substring(6).Trim();
            else if (line.StartsWith("data:"))
                eventData = line.Substring(5).Trim();
        }

        if (string.IsNullOrEmpty(eventType) || string.IsNullOrEmpty(eventData))
            return;

        HandleSSEEvent(eventType, eventData);
    }

    private void HandleSSEEvent(string eventType, string data)
    {
        if (logRequests) Debug.Log($"[SSE] Event: {eventType}");
        _lastHeartbeat = Time.time;

        switch (eventType)
        {
            case "connected":
                _sseConnected = true;
                _sseReconnectAttempts = 0;
                OnSSEConnected?.Invoke();
                if (logRequests) Debug.Log("[SSE] Verbunden!");
                break;

            case "heartbeat":
                // Nur Timer aktualisieren
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
                if (logRequests) Debug.Log($"[SSE] Unbekannter Event-Typ: {eventType}");
                break;
        }
    }

    private void HandlePaperValidatedEvent(string data)
    {
        try
        {
            var evt = JsonUtility.FromJson<PaperValidatedEventAlt>(data);

            if (evt != null && evt.paper_id != null)
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

                // Voxel-Mesh automatisch bauen
                if (evt.voxel_data != null && voxelBuilder != null)
                {
                    voxelBuilder.BuildMesh(evt.voxel_data);
                }

                // Info-Panel aktualisieren
                if (infoPanel != null)
                {
                    infoPanel.DisplayPaper(paperEvt, evt.rules_results);
                }

                if (logRequests)
                {
                    int voxelCount = evt.voxel_data?.stats?.total ?? 0;
                    Debug.Log($"[SSE] Paper validiert: {evt.title} ({voxelCount} Voxels)");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SSE] Fehler beim Parsen von paper_validated: {e.Message}");
        }
    }

    private void HandleLeaderboardEvent(string data)
    {
        try
        {
            var evt = JsonUtility.FromJson<LeaderboardEvent>(data);
            OnLeaderboardUpdate?.Invoke(evt);

            if (logRequests)
            {
                int playerCount = evt.top_players?.Length ?? 0;
                Debug.Log($"[SSE] Leaderboard aktualisiert: {playerCount} Spieler");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SSE] Fehler beim Parsen von leaderboard_update: {e.Message}");
        }
    }

    private void HandleNewPaperEvent(string data)
    {
        try
        {
            var evt = JsonUtility.FromJson<NewPaperEvent>(data);
            OnNewPaper?.Invoke(evt);

            if (logRequests)
            {
                Debug.Log($"[SSE] Neues Paper: {evt.title} ({evt.sections_count} Sections)");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SSE] Fehler beim Parsen von new_paper: {e.Message}");
        }
    }

    // ===== REST API FUNKTIONEN =====

    /// <summary>
    /// Registriert dieses Device beim Server
    /// </summary>
    public IEnumerator RegisterDevice()
    {
        string url = $"{serverUrl}/api/devices/register";

        string json = $"{{\"device_id\":\"{deviceId}\",\"device_name\":\"Unity\",\"device_model\":\"{SystemInfo.deviceModel}\",\"os_version\":\"{SystemInfo.operatingSystem}\",\"app_version\":\"1.0.0\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("ApiClient: Device registriert!");
            }
            else
            {
                Debug.LogWarning($"ApiClient: Device-Registration fehlgeschlagen: {request.error}");
            }
        }
    }

    /// <summary>
    /// Holt Jobs vom Server
    /// </summary>
    public IEnumerator FetchJobs(int limit, System.Action<List<VoxelData>> callback)
    {


        // URL mit Device-ID (erforderlich für Job-Zuweisung)
        string url = $"{serverUrl}/api/jobs/next?device_id={UnityWebRequest.EscapeURL(deviceId)}&limit={limit}";

        if (logRequests) Debug.Log($"ApiClient: Fetching jobs from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Timeout auf 30 Sekunden (BioBERT braucht Zeit beim ersten Laden)
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {

                string json = request.downloadHandler.text;

                if (logRequests)
                {
                    Debug.Log($"ApiClient: Jobs Response: {json.Substring(0, Mathf.Min(500, json.Length))}...");
                }

                try
                {
                    JobsResponse response = JsonUtility.FromJson<JobsResponse>(json);

                    if (response != null && response.jobs != null)
                    {
                        Debug.Log($"ApiClient: {response.jobs.Count} Jobs erhalten");
                        callback?.Invoke(response.jobs);
                    }
                    else
                    {
                        Debug.LogWarning("ApiClient: Keine Jobs verfügbar");
                        callback?.Invoke(new List<VoxelData>());
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ApiClient: JSON Parse Error: {e.Message}");
                    callback?.Invoke(new List<VoxelData>());
                }
            }
            else
            {

                Debug.LogError($"ApiClient: Failed to fetch jobs: {request.error}");
                Debug.LogError($"ApiClient: Response Code: {request.responseCode}");


                callback?.Invoke(new List<VoxelData>());

            }
        }
    }

    /// <summary>
    /// Submittet Validation Results an den Server
    /// </summary>
    public IEnumerator SubmitResults(List<ValidationResult> results, System.Action<bool> callback)
    {
        string url = $"{serverUrl}/api/validation/submit";

        SubmitRequest submitData = new SubmitRequest
        {
            device_id = deviceId,
            results = results
        };

        string json = JsonUtility.ToJson(submitData);

        if (logRequests) Debug.Log($"ApiClient: Submitting {results.Count} results to {url}");
        if (logRequests) Debug.Log($"ApiClient: Submit JSON: {json}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (logRequests) Debug.Log("ApiClient: Results submitted successfully");
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError($"ApiClient: Failed to submit results: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// Holt aktive Regeln vom Server
    /// WICHTIG: Endpoint ist /api/rules (nicht /api/rules/active!)
    /// </summary>
    public IEnumerator FetchActiveRules(System.Action<List<RuleData>> callback)
    {
        // KORREKTUR: Server bietet /api/rules, nicht /api/rules/active
        string url = $"{serverUrl}/api/rules";

        if (logRequests) Debug.Log($"ApiClient: Fetching active rules from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Timeout auf 30 Sekunden setzen (BioBERT braucht Zeit beim ersten Laden)
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                if (logRequests)
                {
                    Debug.Log($"ApiClient: Rules Response: {json.Substring(0, Mathf.Min(500, json.Length))}...");
                }

                try
                {
                    RulesResponse response = JsonUtility.FromJson<RulesResponse>(json);

                    // Null-Check und leere Liste handling
                    if (response != null && response.rules != null)
                    {
                        Debug.Log($"ApiClient: {response.rules.Count} Regeln geladen");
                        callback?.Invoke(response.rules);
                    }
                    else
                    {
                        Debug.LogWarning("ApiClient: Leere Rules-Response vom Server");
                        callback?.Invoke(new List<RuleData>());
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ApiClient: JSON Parse Error: {e.Message}");
                    Debug.LogError($"ApiClient: Raw JSON: {json}");
                    callback?.Invoke(new List<RuleData>());
                }
            }
            else
            {
                Debug.LogError($"ApiClient: Failed to fetch rules: {request.error}");
                Debug.LogError($"ApiClient: Response Code: {request.responseCode}");
                callback?.Invoke(new List<RuleData>());
            }
        }
    }

    // Getter für Device ID
    public string GetDeviceId() => deviceId;

    // Setter für Server URL (z.B. für Tests)
    public void SetServerUrl(string url)
    {
        serverUrl = url;
    }

    void OnDestroy()
    {
        DisconnectSSE();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            DisconnectSSE();
        }
        else if (autoConnectSSE)
        {
            ConnectSSE();
        }
    }
}

// === Response/Request Klassen ===

[System.Serializable]
public class JobsResponse
{
    public List<VoxelData> jobs;
}

[System.Serializable]
public class RulesResponse
{
    public List<RuleData> rules;
}

[System.Serializable]
public class SubmitRequest
{
    public string device_id;
    public List<ValidationResult> results;
}

[System.Serializable]
public class ValidationResult
{
    // Basis-Felder (Server-Kompatibel)
    public string job_id;
    public string paper_id;
    public string rule_id;
    public string section;
    public bool is_match;
    public float similarity;
    public int points_earned;
}

[System.Serializable]
public class RuleData
{
    public string rule_id;
    public string question;
    public float threshold;
    public bool is_active;

    // Embeddings kommen NICHT von /api/rules, sondern mit Jobs!
    // Diese werden bei Bedarf aus VoxelData gelesen
    [System.NonSerialized] public float[] pos_embedding;
    [System.NonSerialized] public float[] neg_embedding;
}

// === SSE Event Klassen ===

[System.Serializable]
public class PaperValidatedEvent
{
    public string paper_id;
    public string device_id;
    public string title;
    public VoxelGridData voxel_data;
    public string thumbnail_base64;
    public string timestamp;

    // Result Daten (für Validation)
    public ValidationResultSSE result;
}

[System.Serializable]
public class ValidationResultSSE
{
    public string rule_id;
    public float similarity;
    public bool is_match;
    public int points;
}

[System.Serializable]
public class PaperValidatedEventAlt
{
    public string paper_id;
    public string title;
    public RuleResultSSE[] rules_results;
    public VoxelGridData voxel_data;
    public string thumbnail_base64;
    public string timestamp;
}

[System.Serializable]
public class RuleResultSSE
{
    public string rule_id;
    public string rule_name;
    public bool passed;
    public float similarity;
    public bool is_match; // Alias für passed
    public int points;
}

[System.Serializable]
public class LeaderboardEntry
{
    public string device_id;
    public string player_name;
    public int total_points;
    public int rank;
}

[System.Serializable]
public class LeaderboardEvent
{
    public LeaderboardEntry[] top_players;
    public int[] changed_ranks;
}

[System.Serializable]
public class NewPaperEvent
{
    public string paper_id;
    public string title;
    public int sections_count;
}

[System.Serializable]
public class VoxelGridData
{
    public int[] grid_size;  // [X, Y, Z] - DYNAMISCH!
    public float[][] voxels; // [[x, y, z, density], ...]
    public VoxelStatsData stats;
    public int page;

    // Default: 8x8x12 Grid (768 voxels = BioBERT embedding dimension)
    // X: horizontal position (8 columns)
    // Y: height (8 layers based on text density)
    // Z: vertical position (12 rows, top to bottom)
    public int GridX => grid_size != null && grid_size.Length > 0 ? grid_size[0] : 8;
    public int GridY => grid_size != null && grid_size.Length > 1 ? grid_size[1] : 8;
    public int GridZ => grid_size != null && grid_size.Length > 2 ? grid_size[2] : 12;
}

[System.Serializable]
public class VoxelStatsData
{
    public int total;
    public float density_avg;
    public float fill_ratio;
}

/// <summary>
/// Voxel-Daten mit RGB-Farbinformation (colored=true Modus)
/// Format: [[x, y, z, density, r, g, b], ...]
/// </summary>
[System.Serializable]
public class ColoredVoxelData
{
    public int[] grid_size;
    public float[][] voxels; // [[x, y, z, density, r, g, b], ...]
    public bool colored;
    public VoxelStatsData stats;
}
