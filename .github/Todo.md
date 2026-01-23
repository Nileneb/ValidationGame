# 🎮 Unity TODO: PaperRun Endless Runner

## 🎯 Ziel

Endless Runner Game auf Android mit 3D Voxel-Gebilden aus wissenschaftlichen Papers

---

## 📐 Projekt-Setup

### Priorität: ⚡ KRITISCH

- [x] Unity Projekt erstellen
  - [x] Unity 6.3 LTS (stabil für Android)
  - [ ] 3D Template
  - [ ] Build Settings: Windows + Android
- [x] Packages installieren
  - [x] TextMeshPro (für UI)
  - [x] Newtonsoft JSON (für API Communication)
  - [ ] ProBuilder (optional, für Level Design)
- [ ] Projekt-Struktur
  ```
  Assets/
  ├── Scripts/
  │   ├── Core/
  │   ├── Game/
  │   ├── Networking/
  │   ├── UI/
  │   └── Voxel/
  ├── Prefabs/
  │   ├── Voxels/
  │   ├── Environment/
  │   └── UI/
  ├── Materials/
  ├── Scenes/
  │   ├── MainMenu.unity
  │   ├── Game.unity
  │   └── Leaderboard.unity
  └── Resources/
  ```

---

## 🏃 Endless Runner Mechanik

### Priorität: ⚡⚡⚡ KRITISCH

#### 1. Player Controller

```csharp
// Scripts/Game/PlayerController.cs

using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 10f;
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private float laneChangeSpeed = 5f;

    private int currentLane = 1; // 0=links, 1=mitte, 2=rechts
    private Vector3 targetPosition;

    [Header("Input")]
    [SerializeField] private float swipeThreshold = 50f;
    private Vector2 touchStartPos;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        // Forward Movement (konstant)
        transform.Translate(Vector3.forward * forwardSpeed * Time.deltaTime);

        // Touch Input
        HandleInput();

        // Lane Movement
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            laneChangeSpeed * Time.deltaTime
        );
    }

    void HandleInput()
    {
        // Swipe Detection für Android
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                Vector2 swipeDelta = touch.position - touchStartPos;

                // Horizontal Swipe
                if (Mathf.Abs(swipeDelta.x) > swipeThreshold)
                {
                    if (swipeDelta.x > 0) MoveLane(1);  // Rechts
                    else MoveLane(-1);                   // Links
                }
            }
        }

        // Fallback: Keyboard für Testing
        if (Input.GetKeyDown(KeyCode.A)) MoveLane(-1);
        if (Input.GetKeyDown(KeyCode.D)) MoveLane(1);
    }

    void MoveLane(int direction)
    {
        currentLane = Mathf.Clamp(currentLane + direction, 0, 2);
        float xPos = (currentLane - 1) * laneDistance;
        targetPosition = new Vector3(xPos, transform.position.y, transform.position.z);
    }
}
```

**TODO:**

- [ ] PlayerController Script erstellen
- [ ] Player Prefab mit Capsule + Collider
- [ ] Swipe Detection testen
- [ ] Jump Mechanik (optional)

#### 2. Track Generator (Endless)

```csharp
// Scripts/Game/TrackGenerator.cs

using UnityEngine;
using System.Collections.Generic;

public class TrackGenerator : MonoBehaviour
{
    [Header("Track Segments")]
    [SerializeField] private GameObject trackSegmentPrefab;
    [SerializeField] private float segmentLength = 20f;
    [SerializeField] private int visibleSegments = 5;

    [Header("References")]
    [SerializeField] private Transform player;

    private Queue<GameObject> activeSegments = new Queue<GameObject>();
    private float nextSpawnZ = 0f;

    void Start()
    {
        // Initial Segments spawnen
        for (int i = 0; i < visibleSegments; i++)
        {
            SpawnSegment();
        }
    }

    void Update()
    {
        // Wenn Spieler Segment hinter sich lässt → neues spawnen
        if (player.position.z > nextSpawnZ - (visibleSegments * segmentLength))
        {
            SpawnSegment();
            RecycleSegment();
        }
    }

    void SpawnSegment()
    {
        GameObject segment = Instantiate(
            trackSegmentPrefab,
            new Vector3(0, 0, nextSpawnZ),
            Quaternion.identity
        );
        activeSegments.Enqueue(segment);
        nextSpawnZ += segmentLength;
    }

    void RecycleSegment()
    {
        if (activeSegments.Count > visibleSegments)
        {
            GameObject oldSegment = activeSegments.Dequeue();
            Destroy(oldSegment);
        }
    }
}
```

**TODO:**

- [ ] TrackGenerator Script erstellen
- [ ] Track Segment Prefab (20m Plane mit Textur)
- [ ] Object Pooling statt Instantiate/Destroy (Performance!)

---

## 📦 Voxel System

### Priorität: ⚡⚡⚡ KRITISCH

#### 3. Voxel Structure Spawner

```csharp
// Scripts/Voxel/VoxelStructureSpawner.cs

using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class VoxelData
{
    public string paper_id;
    public string section;
    public List<VoxelPosition> voxel_positions;
    public ColorData color;
    public float[] embedding;
}

[System.Serializable]
public class VoxelPosition
{
    public int x;
    public int y;
    public int z;
}

[System.Serializable]
public class ColorData
{
    public float r;
    public float g;
    public float b;
}

public class VoxelStructureSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Material voxelMaterial;

    [Header("Settings")]
    [SerializeField] private float cubeSize = 0.5f;
    [SerializeField] private float spawnDistance = 50f;
    [SerializeField] private float moveSpeed = 5f;

    [Header("References")]
    [SerializeField] private Transform player;

    private List<GameObject> activeStructures = new List<GameObject>();

    public GameObject SpawnFromJson(string json)
    {
        VoxelData data = JsonUtility.FromJson<VoxelData>(json);

        // Parent-Objekt
        GameObject structure = new GameObject($"Paper_{data.paper_id}_{data.section}");
        structure.transform.position = new Vector3(
            Random.Range(-3f, 3f),  // Zufällige Lane
            1f,
            player.position.z + spawnDistance
        );

        // Cubes spawnen
        foreach (var pos in data.voxel_positions)
        {
            GameObject cube = Instantiate(cubePrefab, structure.transform);
            cube.transform.localPosition = new Vector3(
                pos.x * cubeSize,
                pos.y * cubeSize,
                pos.z * cubeSize
            );

            // Farbe setzen
            Material mat = new Material(voxelMaterial);
            mat.color = new Color(data.color.r, data.color.g, data.color.b);
            cube.GetComponent<Renderer>().material = mat;
        }

        // Collider für Einsammeln
        BoxCollider collider = structure.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(8 * cubeSize, 8 * cubeSize, 12 * cubeSize);
        collider.center = new Vector3(4 * cubeSize, 4 * cubeSize, 6 * cubeSize);

        // Collectible Component
        CollectiblePaper collectible = structure.AddComponent<CollectiblePaper>();
        collectible.Initialize(data);

        // Bewegung
        VoxelMover mover = structure.AddComponent<VoxelMover>();
        mover.moveSpeed = -moveSpeed;  // Auf Spieler zu

        activeStructures.Add(structure);
        return structure;
    }

    void Update()
    {
        // Cleanup: Structures hinter Spieler zerstören
        for (int i = activeStructures.Count - 1; i >= 0; i--)
        {
            if (activeStructures[i] != null &&
                activeStructures[i].transform.position.z < player.position.z - 20f)
            {
                Destroy(activeStructures[i]);
                activeStructures.RemoveAt(i);
            }
        }
    }
}
```

**TODO:**

- [ ] VoxelStructureSpawner Script erstellen
- [ ] Cube Prefab (1x1x1 Cube mit Emissive Material)
- [ ] Material mit Glow-Effekt (Shader Graph)
- [ ] Spawn-Timing an Job-Fetching koppeln

#### 4. Collectible Paper

```csharp
// Scripts/Voxel/CollectiblePaper.cs

using UnityEngine;

public class CollectiblePaper : MonoBehaviour
{
    public string paperId { get; private set; }
    public string section { get; private set; }
    public float[] embedding { get; private set; }

    [SerializeField] private GameObject collectEffectPrefab;
    [SerializeField] private AudioClip collectSound;

    public void Initialize(VoxelData data)
    {
        paperId = data.paper_id;
        section = data.section;
        embedding = data.embedding;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    void Collect(GameObject player)
    {
        // Event triggern
        GameManager.Instance.OnPaperCollected(this);

        // VFX
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }

        // SFX
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        // Zerstören
        Destroy(gameObject);
    }
}
```

**TODO:**

- [ ] CollectiblePaper Script erstellen
- [ ] Particle System für Collection Effect (Sterne, Funken)
- [ ] Audio Clip einbinden

#### 5. Voxel Mover

```csharp
// Scripts/Voxel/VoxelMover.cs

using UnityEngine;

public class VoxelMover : MonoBehaviour
{
    public float moveSpeed = -5f;
    [SerializeField] private bool rotateSlowly = true;
    [SerializeField] private float rotationSpeed = 30f;

    void Update()
    {
        // Nach vorne bewegen (auf Spieler zu)
        transform.Translate(0, 0, moveSpeed * Time.deltaTime, Space.World);

        // Optional: Langsam rotieren
        if (rotateSlowly)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}
```

---

## 🌐 Networking (MCP-Server ↔ Unity)

### Priorität: ⚡⚡⚡ KRITISCH

#### 6. API Client

```csharp
// Scripts/Networking/ApiClient.cs

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class ApiClient : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string serverUrl = "http://localhost:8089";
    [SerializeField] private string deviceId;

    void Start()
    {
        // Device ID generieren falls nicht vorhanden
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = SystemInfo.deviceUniqueIdentifier;
            PlayerPrefs.SetString("device_id", deviceId);
        }
    }

    // Jobs vom Server holen
    public IEnumerator FetchJobs(int limit, System.Action<List<VoxelData>> callback)
    {
        string url = $"{serverUrl}/api/jobs/next?device_id={deviceId}&limit={limit}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                JobsResponse response = JsonUtility.FromJson<JobsResponse>(json);
                callback?.Invoke(response.jobs);
            }
            else
            {
                Debug.LogError($"Failed to fetch jobs: {request.error}");
                callback?.Invoke(new List<VoxelData>());
            }
        }
    }

    // Validation Result submitten
    public IEnumerator SubmitResults(List<ValidationResult> results, System.Action<bool> callback)
    {
        string url = $"{serverUrl}/api/validation/submit";

        SubmitRequest submitData = new SubmitRequest
        {
            device_id = deviceId,
            results = results
        };

        string json = JsonUtility.ToJson(submitData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Results submitted successfully");
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError($"Failed to submit results: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    // Aktive Regeln holen
    public IEnumerator FetchActiveRules(System.Action<List<RuleData>> callback)
    {
        string url = $"{serverUrl}/api/rules/active";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                RulesResponse response = JsonUtility.FromJson<RulesResponse>(json);
                callback?.Invoke(response.rules);
            }
            else
            {
                Debug.LogError($"Failed to fetch rules: {request.error}");
                callback?.Invoke(new List<RuleData>());
            }
        }
    }
}

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
```

**TODO:**

- [ ] ApiClient Script erstellen
- [ ] UnityWebRequest für REST API
- [ ] Error Handling + Retry Logic
- [ ] Caching für Offline-Modus

---

## 🎯 Game Manager & Rule Matching

### Priorität: ⚡⚡ WICHTIG

#### 7. Game Manager

```csharp
// Scripts/Core/GameManager.cs

using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private VoxelStructureSpawner voxelSpawner;
    [SerializeField] private RuleMatcher ruleMatcher;

    [Header("Game State")]
    [SerializeField] private int totalPoints = 0;
    [SerializeField] private int papersValidated = 0;
    [SerializeField] private int matchesFound = 0;

    private RuleData currentActiveRule;
    private Queue<VoxelData> jobQueue = new Queue<VoxelData>();
    private List<ValidationResult> pendingResults = new List<ValidationResult>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Regeln laden
        StartCoroutine(apiClient.FetchActiveRules(OnRulesLoaded));

        // Initiale Jobs holen
        FetchMoreJobs();

        // Periodisch neue Jobs holen
        InvokeRepeating(nameof(FetchMoreJobs), 10f, 30f);

        // Periodisch Results submitten
        InvokeRepeating(nameof(SubmitPendingResults), 5f, 15f);
    }

    void OnRulesLoaded(List<RuleData> rules)
    {
        if (rules.Count > 0)
        {
            currentActiveRule = rules[0];  // Erste Regel aktiv
            Debug.Log($"Active Rule: {currentActiveRule.question}");
        }
    }

    void FetchMoreJobs()
    {
        StartCoroutine(apiClient.FetchJobs(10, OnJobsReceived));
    }

    void OnJobsReceived(List<VoxelData> jobs)
    {
        foreach (var job in jobs)
        {
            jobQueue.Enqueue(job);
        }

        Debug.Log($"Received {jobs.Count} jobs. Queue size: {jobQueue.Count}");
    }

    void Update()
    {
        // Jobs spawnen wenn Queue nicht leer
        if (jobQueue.Count > 0 && Random.value < 0.1f * Time.deltaTime)
        {
            VoxelData job = jobQueue.Dequeue();
            string json = JsonUtility.ToJson(job);
            voxelSpawner.SpawnFromJson(json);
        }
    }

    public void OnPaperCollected(CollectiblePaper paper)
    {
        // Rule Matching
        MatchResult result = ruleMatcher.CheckMatch(
            paper.embedding,
            currentActiveRule
        );

        // Punkte addieren
        totalPoints += result.points;

        if (result.is_match)
        {
            matchesFound++;
            Debug.Log($"MATCH! +{result.points} points");
        }

        // UI Update
        UIManager.Instance.UpdateScore(totalPoints, matchesFound);
        UIManager.Instance.ShowFeedback(result.feedback);

        // Result speichern für späteren Submit
        ValidationResult validationResult = new ValidationResult
        {
            job_id = $"job_{paper.paperId}_{paper.section}",
            paper_id = paper.paperId,
            rule_id = currentActiveRule.rule_id,
            is_match = result.is_match,
            similarity = result.similarity,
            confidence = result.similarity,
            points_earned = result.points,
            time_taken_ms = 0
        };

        pendingResults.Add(validationResult);
    }

    void SubmitPendingResults()
    {
        if (pendingResults.Count > 0)
        {
            List<ValidationResult> toSubmit = new List<ValidationResult>(pendingResults);
            pendingResults.Clear();

            StartCoroutine(apiClient.SubmitResults(toSubmit, (success) =>
            {
                if (success)
                {
                    Debug.Log($"Submitted {toSubmit.Count} results");
                }
                else
                {
                    // Bei Fehler wieder hinzufügen
                    pendingResults.AddRange(toSubmit);
                }
            }));
        }
    }
}
```

**TODO:**

- [ ] GameManager Script erstellen
- [ ] Singleton Pattern
- [ ] Queue Management für Jobs

#### 8. Rule Matcher

```csharp
// Scripts/Core/RuleMatcher.cs

using UnityEngine;

public class RuleMatcher : MonoBehaviour
{
    [System.Serializable]
    public class MatchResult
    {
        public bool is_match;
        public float similarity;
        public int points;
        public string feedback;
    }

    public MatchResult CheckMatch(float[] paperEmbedding, RuleData rule)
    {
        // Cosine Similarity berechnen
        float similarity = CosineSimilarity(paperEmbedding, rule.pos_embedding);

        bool isMatch = similarity >= rule.threshold;
        int points = isMatch ? Mathf.RoundToInt(100 * similarity) : 10;

        string feedback = isMatch
            ? $"🎉 MATCH! Das ist {rule.question}!"
            : $"Weiter suchen! (Similarity: {similarity:F2})";

        return new MatchResult
        {
            is_match = isMatch,
            similarity = similarity,
            points = points,
            feedback = feedback
        };
    }

    float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Mathf.Sqrt(magnitudeA);
        magnitudeB = Mathf.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f) return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
```

---

## 🎨 UI System

### Priorität: ⚡⚡ WICHTIG

#### 9. UI Manager

```csharp
// Scripts/UI/UIManager.cs

using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI matchesText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private TextMeshProUGUI activeRuleText;

    [Header("Feedback")]
    [SerializeField] private float feedbackDuration = 2f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void UpdateScore(int points, int matches)
    {
        scoreText.text = $"Punkte: {points}";
        matchesText.text = $"Matches: {matches}";
    }

    public void ShowFeedback(string message)
    {
        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);

        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), feedbackDuration);
    }

    void HideFeedback()
    {
        feedbackText.gameObject.SetActive(false);
    }

    public void SetActiveRule(string ruleQuestion)
    {
        activeRuleText.text = $"Suche: {ruleQuestion}";
    }
}
```

**TODO:**

- [ ] Canvas mit TextMeshPro Komponenten
- [ ] Score, Matches, Feedback Display
- [ ] Animations für Feedback (Fade In/Out)

---

## 🎮 Zusätzliche Features

### Priorität: ⚡ NICE-TO-HAVE

#### 10. Kamera System

- [ ] Virtual Camera
- [ ] Follow Player (Third Person)
- [ ] Smooth Damping

#### 11. Particle Effects

- [ ] Collection Burst Effect
- [ ] Match Effect (goldenes Leuchten)
- [ ] Trail Effect für Player

#### 12. Audio

- [ ] Background Music (looping)
- [ ] Collection Sound
- [ ] Match Sound (höher pitched)
- [ ] Miss Sound (tiefer pitched)

#### 13. Leaderboard Scene

- [ ] ScrollView mit Top 100
- [ ] Eigene Position highlighten
- [ ] Refresh Button

---

## 📱 Android Build

### Priorität: ⚡⚡ WICHTIG

- [ ] Build Settings konfigurieren
  - [ ] Platform: Android
  - [ ] Minimum API Level: 24 (Android 7.0)
  - [ ] Target API Level: 33 (Android 13)
- [ ] Player Settings
  - [ ] Bundle Identifier: com.scidiffreview.paperrun
  - [ ] Version: 0.1.0
  - [ ] Icon + Splash Screen
- [ ] Permissions
  - [ ] Internet Access
  - [ ] External Storage (für Cache)
- [ ] Performance
  - [ ] Texture Compression: ASTC
  - [ ] Graphics API: Vulkan + OpenGL ES 3
  - [ ] Scripting Backend: IL2CPP
  - [ ] Target Architectures: ARMv7 + ARM64
- [ ] Testing
  - [ ] Logcat für Debugging
  - [ ] FPS Counter
  - [ ] Memory Profiler

---

## ⏱️ Zeitschätzung Unity

| Feature           | Tage          | Priorität |
| ----------------- | ------------- | --------- |
| Projekt Setup     | 0.5           | ⚡⚡⚡    |
| Player Controller | 1             | ⚡⚡⚡    |
| Track Generator   | 1             | ⚡⚡⚡    |
| Voxel System      | 2             | ⚡⚡⚡    |
| Networking (API)  | 2             | ⚡⚡⚡    |
| Game Manager      | 1             | ⚡⚡⚡    |
| Rule Matcher      | 1             | ⚡⚡⚡    |
| UI System         | 1             | ⚡⚡      |
| VFX + Audio       | 1             | ⚡        |
| Leaderboard       | 1             | ⚡        |
| Android Build     | 1             | ⚡⚡      |
| Testing + Polish  | 2             | ⚡⚡      |
| **TOTAL**         | **14.5 Tage** |           |

---

## 🎯 Milestones

### Milestone 1: Prototype (Tag 1-5)

- ✅ Endless Runner funktioniert
- ✅ Voxel-Gebilde spawnen
- ✅ Einsammeln funktioniert

### Milestone 2: Networking (Tag 6-9)

- ✅ API Communication klappt
- ✅ Jobs werden geladen
- ✅ Results werden submitted

### Milestone 3: Polish (Tag 10-14)

- ✅ UI komplett
- ✅ VFX + SFX
- ✅ Android Build läuft stabil

---

## 🚀 Quick Start Checklist

- [x] Unity 6.3 LTS installiert
- [x] Android SDK installiert (via Unity Hub)
- [x] TextMeshPro Package importiert
- [x] MCP-Server läuft lokal
- [ ] Test-Device verbunden (USB Debugging)
- [x] Erste Scene erstellt
- [ ] Player Controller getestet
- [ ] Voxel-Cube spawnt

---

## 📚 Ressourcen

- Unity Endless Runner Tutorial: https://learn.unity.com
- Cinemachine Docs: https://docs.unity3d.com/Packages/com.unity.cinemachine
- UnityWebRequest Guide: https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest.html
- Android Build Guide: https://docs.unity3d.com/Manual/android-BuildProcess.html
