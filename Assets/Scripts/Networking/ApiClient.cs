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
        string url = $"{serverUrl}/api/jobs/next?device_id={deviceId}&limit={limit}";

        if (logRequests) Debug.Log($"ApiClient: Fetching jobs from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                if (logRequests) Debug.Log($"ApiClient: Jobs Response: {json.Substring(0, Mathf.Min(200, json.Length))}...");

                JobsResponse response = JsonUtility.FromJson<JobsResponse>(json);
                callback?.Invoke(response?.jobs ?? new List<VoxelData>());
            }
            else
            {
                Debug.LogError($"ApiClient: Failed to fetch jobs: {request.error}");
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
    /// </summary>
    public IEnumerator FetchActiveRules(System.Action<List<RuleData>> callback)
    {
        string url = $"{serverUrl}/api/rules/active";

        if (logRequests) Debug.Log($"ApiClient: Fetching active rules from {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                if (logRequests) Debug.Log($"ApiClient: Rules Response: {json.Substring(0, Mathf.Min(200, json.Length))}...");

                RulesResponse response = JsonUtility.FromJson<RulesResponse>(json);
                callback?.Invoke(response?.rules ?? new List<RuleData>());
            }
            else
            {
                Debug.LogError($"ApiClient: Failed to fetch rules: {request.error}");
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
    public float[] pos_embedding;
    public float[] neg_embedding;
    public float threshold;
}
