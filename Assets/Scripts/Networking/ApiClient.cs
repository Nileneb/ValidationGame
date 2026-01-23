// Scripts/Networking/ApiClient.cs
// REST API Client für Kommunikation mit dem MCP-Server
// Holt Jobs, submittet Validation Results, lädt Regeln

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class ApiClient : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://localhost:8089";
    [SerializeField] private string deviceId;

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;

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
    public string job_id;
    public string paper_id;
    public string rule_id;
    public bool is_match;
    public float similarity;
    public float confidence;
    public int points_earned;
    public int time_taken_ms;
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
