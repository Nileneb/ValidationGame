// Scripts/Networking/ApiClient.cs
// REST API Client für mcp-paperstream (sauber, nur essenzielle Endpoints)
// 
// ESSENZIELLE ENDPOINTS:
// - GET /api/rules/active → Rule-Embeddings laden
// - GET /api/jobs/next → Paper-Jobs holen (BioBERT embeddings)
// - POST /api/validation/submit → Validation Results submitten
// - SSE /api/stream/unity → Echtzeit-Updates (optional)

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
    [SerializeField] private bool autoConnectSSE = true;
    [SerializeField] private float sseReconnectDelay = 5f;
    [SerializeField] private float sseHeartbeatTimeout = 30f;

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;

    // SSE Events
    public event Action<PaperValidatedEvent> OnPaperValidated;
    public event Action OnSSEConnected;
    public event Action OnSSEDisconnected;

    private bool _sseConnected = false;
    private float _lastHeartbeat;
    private Coroutine _sseCoroutine;
    private int _sseReconnectAttempts = 0;

    public bool IsSSEConnected => _sseConnected;
    public string ServerUrl => serverUrl;

    void Start()
    {
        // Device ID aus PlayerPrefs oder System-ID generieren
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
            Debug.Log($"ApiClient: Device ID = {deviceId}");

        // Device registrieren
        StartCoroutine(RegisterDevice());

        // SSE verbinden
        if (autoConnectSSE)
            ConnectSSE();
    }

    /// <summary>
    /// Registriert dieses Device beim Server
    /// </summary>
    private IEnumerator RegisterDevice()
    {
        string url = $"{serverUrl}/api/devices/register";
        string json = $"{{\"device_id\":\"{deviceId}\",\"device_name\":\"Unity Client\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"ApiClient: Device Registration failed: {request.error}");
            }
        }
    }

    // ===== ESSENZIELLE API ENDPOINTS =====

    /// <summary>
    /// Lädt aktive Rules vom Server (BioBERT Embeddings)
    /// Endpoint: GET /api/rules/active
    /// Response: { "rules": [{ "rule_id": "", "chunk_type": "positive"|"negative", "embedding_b64": "...", ... }] }
    /// </summary>
    public IEnumerator FetchActiveRules(Action<List<RuleData>> callback)
    {
        string url = $"{serverUrl}/api/rules/active";

        if (logRequests) Debug.Log($"ApiClient: Fetching rules...");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<RulesResponse>(json);

                    if (response?.rules != null)
                    {
                        // Dekodiere alle Embeddings
                        foreach (var rule in response.rules)
                        {
                            rule.DecodeData();
                        }
                        callback?.Invoke(response.rules);
                        if (logRequests)
                            Debug.Log($"ApiClient: {response.rules.Count} rules loaded");
                    }
                    else
                    {
                        callback?.Invoke(new List<RuleData>());
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"ApiClient: FetchActiveRules parse error: {e.Message}");
                    callback?.Invoke(new List<RuleData>());
                }
            }
            else
            {
                Debug.LogError($"ApiClient: FetchActiveRules failed: {request.error}");
                callback?.Invoke(new List<RuleData>());
            }
        }
    }

    /// <summary>
    /// Holt nächsten Job vom Server (Paper mit BioBERT Embeddings)
    /// Endpoint: GET /api/jobs/next?device_id=...
    /// Response: { "status": "assigned"|"no_jobs", "job": {...} }
    /// </summary>
    public IEnumerator FetchJobs(int limit, Action<List<VoxelData>> callback)
    {
        string url = $"{serverUrl}/api/jobs/next?device_id={UnityWebRequest.EscapeURL(deviceId)}";

        if (logRequests) Debug.Log($"ApiClient: Fetching jobs...");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;  // BioBERT kann Zeit brauchen
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var response = JsonUtility.FromJson<JobResponse>(json);

                    var jobs = new List<VoxelData>();

                    if (response?.status == "assigned" && response.job != null)
                    {
                        // Dekodiere Embeddings
                        if (!string.IsNullOrEmpty(response.job.paper_embedding_b64))
                        {
                            response.job.paper_embedding = EmbeddingUtils.DecodeBase64ToFloatArray(response.job.paper_embedding_b64);
                        }

                        // Dekodiere Chunk-Embeddings
                        if (response.job.chunks != null)
                        {
                            foreach (var chunk in response.job.chunks)
                            {
                                chunk.DecodeEmbedding();
                            }
                        }

                        jobs.Add(response.job);
                        if (logRequests)
                            Debug.Log($"ApiClient: Job assigned (paper={response.job.paper_id})");
                    }
                    else
                    {
                        if (logRequests)
                            Debug.Log($"ApiClient: No jobs available");
                    }

                    callback?.Invoke(jobs);
                }
                catch (Exception e)
                {
                    Debug.LogError($"ApiClient: FetchJobs parse error: {e.Message}");
                    callback?.Invoke(new List<VoxelData>());
                }
            }
            else
            {
                Debug.LogError($"ApiClient: FetchJobs failed: {request.error}");
                callback?.Invoke(new List<VoxelData>());
            }
        }
    }

    /// <summary>
    /// Submittet Validation Results ans mcp-paperstream
    /// Endpoint: POST /api/validation/submit
    /// Request: { "device_id": "", "results": [...] }
    /// </summary>
    public IEnumerator SubmitResults(List<ValidationResult> results, Action<bool> callback)
    {
        string url = $"{serverUrl}/api/validation/submit";

        var request_data = new SubmitRequest
        {
            device_id = deviceId,
            results = results
        };

        string json = JsonUtility.ToJson(request_data);

        if (logRequests)
            Debug.Log($"ApiClient: Submitting {results.Count} results...");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (logRequests)
                    Debug.Log($"ApiClient: Results submitted successfully");
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError($"ApiClient: SubmitResults failed: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    // ===== SSE (SERVER-SENT EVENTS) =====

    /// <summary>
    /// Verbindet zum SSE-Stream für Echtzeit-Updates
    /// </summary>
    public void ConnectSSE()
    {
        if (_sseCoroutine != null)
            StopCoroutine(_sseCoroutine);

        _sseCoroutine = StartCoroutine(SSEListener());
    }

    /// <summary>
    /// Trennt SSE-Verbindung
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
    }

    private IEnumerator SSEListener()
    {
        string url = $"{serverUrl}/api/stream/unity?client_id={UnityWebRequest.EscapeURL(deviceId)}";

        if (logRequests) Debug.Log($"ApiClient: SSE connecting...");

        using (UnityWebRequest request = new UnityWebRequest(url, "GET"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "text/event-stream");
            request.SetRequestHeader("Cache-Control", "no-cache");
            request.timeout = 0;  // Kein Timeout für SSE

            var operation = request.SendWebRequest();

            StringBuilder buffer = new StringBuilder();
            long lastPosition = 0;
            _lastHeartbeat = Time.time;

            while (!operation.isDone)
            {
                // Heartbeat-Timeout prüfen
                if (Time.time - _lastHeartbeat > sseHeartbeatTimeout)
                {
                    if (logRequests) Debug.LogWarning("ApiClient: SSE heartbeat timeout");
                    break;
                }

                // Verarbeite neue Daten
                if (request.downloadHandler.data != null)
                {
                    long currentLength = request.downloadedBytes;

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
                Debug.LogError($"ApiClient: SSE error: {request.error}");
            }
        }

        // Verbindung verloren → Reconnect
        _sseConnected = false;
        OnSSEDisconnected?.Invoke();

        _sseReconnectAttempts++;
        float delay = Mathf.Min(sseReconnectDelay * _sseReconnectAttempts, 60f);

        if (logRequests) Debug.Log($"ApiClient: SSE reconnect in {delay:F1}s");

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

        _lastHeartbeat = Time.time;

        switch (eventType)
        {
            case "connected":
                _sseConnected = true;
                _sseReconnectAttempts = 0;
                OnSSEConnected?.Invoke();
                if (logRequests) Debug.Log("ApiClient: SSE connected");
                break;

            case "heartbeat":
                break;  // Nur Timer aktualisieren

            case "paper_validated":
                try
                {
                    var evt = JsonUtility.FromJson<PaperValidatedEvent>(eventData);
                    OnPaperValidated?.Invoke(evt);
                    if (logRequests) Debug.Log($"ApiClient: Paper validated (SSE): {evt.paper_id}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"ApiClient: SSE parse error: {e.Message}");
                }
                break;

            default:
                if (logRequests) Debug.Log($"ApiClient: Unknown SSE event: {eventType}");
                break;
        }
    }
}

// ===== DATENKLASSEN FÜR JSON-SERIALISIERUNG =====

[System.Serializable]
public class JobResponse
{
    public string status;         // "assigned" or "no_jobs"
    public VoxelData job;
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
    public string paper_id;
    public string paper_section;
    public string rule_id;
    public string rule_type;           // "positive" or "negative"
    public bool is_match;
    public float cosine_similarity;
    public int points_earned;
    public string device_id;
}

[System.Serializable]
public class RuleData
{
    public string rule_id;
    public string chunk_type;          // "positive" or "negative"
    public string text_preview;
    public string embedding_b64;       // Base64-encoded 768-float32
    public ChunkColor color;
    public ChunkPosition position;

    // Dekodiert zur Laufzeit
    [System.NonSerialized] public float[] embedding;

    public void DecodeData()
    {
        if (!string.IsNullOrEmpty(embedding_b64))
        {
            embedding = EmbeddingUtils.DecodeBase64ToFloatArray(embedding_b64);
        }
    }
}

[System.Serializable]
public class PaperValidatedEvent
{
    public string paper_id;
    public string title;
    public string thumbnail_base64;
    public long timestamp;
}

