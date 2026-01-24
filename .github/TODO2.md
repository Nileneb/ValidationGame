# 🎮 Unity TODO: PaperRun ValidationGame

## 🔄 Architektur-Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        n8n BioBertAgent                                  │
│  1. PICO-Suche → paper-search-mcp (8090)                                │
│  2. Paper Download → /shared/papers                                      │
│  3. Process Paper → mcp-paperstream (8089)                              │
│     → BioBERT: Rule Embeddings (PICO-Kriterien)                         │
│     → BioBERT: Paper Section Embeddings                                 │
└───────────────────────────────┬─────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                     mcp-paperstream (8089)                               │
│  GET  /api/jobs/next        → Jobs mit Embeddings (Base64)              │
│  GET  /api/stream/unity     → SSE Real-time Updates                     │
│  POST /api/validation/submit ← Ergebnisse von Unity                     │
└───────────────────────────────┬─────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                        Unity ValidationGame                              │
│  1. Fetch Jobs (Rule + Paper Embeddings)                                │
│  2. Zeige "Rule-Figur" im Canvas (Spieler lernt Form)                   │
│  3. Spawne Paper-Embeddings als Voxel-Strukturen                        │
│  4. Spieler findet Rule-Figuren in Paper-Figuren                        │
│  5. Submit: paper_evaluated=true, found_items=[...]                     │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## ✅ ERLEDIGT

- [x] Unity 6.3 LTS Projekt erstellt
- [x] Packages: TextMeshPro, Newtonsoft JSON
- [x] ApiClient.cs mit `FetchJobs()` und `SubmitResults()`
- [x] VoxelStructureSpawner.cs (Basis)
- [x] PlayerController.cs (3-Lane Bewegung)
- [x] TrackGenerator.cs (Endless Track)
- [x] GameManager.cs, UIManager.cs, RuleMatcher.cs
- [x] Prefabs: Player, TrackSegment, Cube

---

## 🔴 KRITISCH - Embedding-Visualisierung

### 1. EmbeddingDecoder.cs (NEU)

**Dekodiert Base64 Embeddings zu float[]**

```csharp
// Assets/Scripts/Embedding/EmbeddingDecoder.cs
using System;
using UnityEngine;

public static class EmbeddingDecoder
{
    /// <summary>
    /// Dekodiert Base64-String zu float[768] Embedding
    /// </summary>
    public static float[] DecodeEmbedding(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;

        byte[] bytes = Convert.FromBase64String(base64);
        float[] embedding = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    /// <summary>
    /// Normalisiert Embedding auf Länge 1
    /// </summary>
    public static float[] Normalize(float[] embedding)
    {
        float magnitude = 0f;
        for (int i = 0; i < embedding.Length; i++)
            magnitude += embedding[i] * embedding[i];

        magnitude = Mathf.Sqrt(magnitude);

        float[] normalized = new float[embedding.Length];
        for (int i = 0; i < embedding.Length; i++)
            normalized[i] = embedding[i] / magnitude;

        return normalized;
    }
}
```

**TODO:**

- [ ] EmbeddingDecoder.cs erstellen
- [ ] Base64 → float[768] Konvertierung testen
- [ ] Embedding-Normalisierung implementieren

---

### 2. EmbeddingToVoxel.cs (NEU)

**Wandelt 768-dim Embedding in 3D Voxel-Struktur**

```csharp
// Assets/Scripts/Embedding/EmbeddingToVoxel.cs
using UnityEngine;
using System.Collections.Generic;

public static class EmbeddingToVoxel
{
    /// <summary>
    /// Konvertiert 768-dim Embedding zu 8x8x8 Voxel-Grid
    /// </summary>
    public static bool[,,] ToVoxelGrid(float[] embedding, float threshold = 0.5f)
    {
        bool[,,] grid = new bool[8, 8, 8]; // 512 Voxel

        // Mapping: 768 dims → 512 voxels (mit Überlappung)
        int embIdx = 0;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                for (int z = 0; z < 8; z++)
                {
                    // Durchschnitt von 1-2 Embedding-Werten
                    float val = embedding[embIdx % embedding.Length];
                    if (embIdx + 1 < embedding.Length)
                        val = (val + embedding[embIdx + 1]) / 2f;

                    // Normalisiere auf [0,1] und threshold
                    float normalized = (val + 1f) / 2f; // Annahme: Werte in [-1,1]
                    grid[x, y, z] = normalized > threshold;

                    embIdx += (embedding.Length / 512);
                }
            }
        }

        return grid;
    }

    /// <summary>
    /// Berechnet Farbwert basierend auf Embedding-Segment
    /// </summary>
    public static Color GetVoxelColor(float[] embedding, int x, int y, int z)
    {
        int idx = (x * 64 + y * 8 + z) % embedding.Length;

        // HSV-Farbe basierend auf Embedding-Werten
        float h = Mathf.Abs(embedding[idx]) % 1f;
        float s = 0.7f + 0.3f * Mathf.Abs(embedding[(idx + 100) % embedding.Length]);
        float v = 0.8f + 0.2f * Mathf.Abs(embedding[(idx + 200) % embedding.Length]);

        return Color.HSVToRGB(h, s, v);
    }
}
```

**TODO:**

- [ ] EmbeddingToVoxel.cs erstellen
- [ ] 768-dim → 8x8x8 Grid Mapping optimieren
- [ ] Farbkodierung nach Embedding-Werten testen
- [ ] Verschiedene Threshold-Werte für unterschiedliche Density

---

### 3. RuleFigureDisplay.cs (NEU)

**Zeigt Rule-Figur im UI Canvas an (Zielfigur für Spieler)**

```csharp
// Assets/Scripts/UI/RuleFigureDisplay.cs
using UnityEngine;

public class RuleFigureDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera ruleCamera;      // Ortho-Cam für Preview
    [SerializeField] private Transform ruleContainer; // Parent für Rule-Voxel
    [SerializeField] private GameObject voxelPrefab;

    [Header("Settings")]
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private float voxelScale = 0.3f;

    private bool[,,] currentRuleGrid;
    private string currentRuleId;

    /// <summary>
    /// Zeigt Rule-Figur aus Embedding an
    /// </summary>
    public void DisplayRule(string ruleId, string posEmbeddingB64)
    {
        if (ruleId == currentRuleId) return;

        currentRuleId = ruleId;

        // Clear old voxels
        foreach (Transform child in ruleContainer)
            Destroy(child.gameObject);

        // Decode embedding
        float[] embedding = EmbeddingDecoder.DecodeEmbedding(posEmbeddingB64);
        if (embedding == null) return;

        // Convert to voxel grid
        currentRuleGrid = EmbeddingToVoxel.ToVoxelGrid(embedding, 0.6f);

        // Spawn voxels
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                for (int z = 0; z < 8; z++)
                {
                    if (currentRuleGrid[x, y, z])
                    {
                        Vector3 pos = new Vector3(x - 4, y - 4, z - 4) * voxelScale;
                        GameObject voxel = Instantiate(voxelPrefab, ruleContainer);
                        voxel.transform.localPosition = pos;
                        voxel.transform.localScale = Vector3.one * voxelScale * 0.9f;

                        // Set color
                        var renderer = voxel.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material.color = EmbeddingToVoxel.GetVoxelColor(embedding, x, y, z);
                        }
                    }
                }
            }
        }
    }

    void Update()
    {
        // Rotate for better visibility
        if (ruleContainer != null)
        {
            ruleContainer.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}
```

**TODO:**

- [ ] RuleFigureDisplay.cs erstellen
- [ ] Ortho-Kamera für Rule-Preview einrichten
- [ ] Mini-3D-View im Canvas (RawImage + RenderTexture)
- [ ] Highlight-Animation wenn Match gefunden

---

## 🟡 HOCH - API Integration

### 4. VoxelData Klasse ERWEITERN

**Aktuelle Version:**

```csharp
[System.Serializable]
public class VoxelData
{
    public string job_id;
    public string paper_id;
    public string section_text;
    public string voxel_data;
}
```

**NEUE Version (für Embeddings):**

```csharp
[System.Serializable]
public class VoxelData
{
    public string job_id;
    public string paper_id;
    public int section_id;
    public string rule_id;
    public string question;
    public float threshold;
    public string section_text;

    // Embedding Data (Base64 encoded float[768])
    public string section_embedding_b64;  // Paper-Abschnitt Embedding
    public string pos_embedding_b64;       // Rule: Positive Phrases
    public string neg_embedding_b64;       // Rule: Negative Phrases

    // Optional: Pre-computed Voxel Grid
    public string voxel_data;

    // === DECODED (nicht serialisiert) ===
    [System.NonSerialized] public float[] sectionEmbedding;
    [System.NonSerialized] public float[] posEmbedding;
    [System.NonSerialized] public float[] negEmbedding;

    /// <summary>
    /// Dekodiert alle Base64-Embeddings
    /// </summary>
    public void DecodeEmbeddings()
    {
        sectionEmbedding = EmbeddingDecoder.DecodeEmbedding(section_embedding_b64);
        posEmbedding = EmbeddingDecoder.DecodeEmbedding(pos_embedding_b64);
        negEmbedding = EmbeddingDecoder.DecodeEmbedding(neg_embedding_b64);
    }
}
```

**TODO:**

- [ ] VoxelData Klasse erweitern
- [ ] `section_embedding_b64`, `pos_embedding_b64`, `neg_embedding_b64` Felder
- [ ] `DecodeEmbeddings()` Methode
- [ ] Testen mit echten Server-Daten

---

### 5. EmbeddingSimilarity.cs (NEU)

**Cosine Similarity zwischen Rule und Paper Embeddings**

```csharp
// Assets/Scripts/Embedding/EmbeddingSimilarity.cs
using UnityEngine;

public static class EmbeddingSimilarity
{
    /// <summary>
    /// Berechnet Cosine Similarity zwischen zwei Embeddings
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return 0f;

        float dot = 0f, normA = 0f, normB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        float denominator = Mathf.Sqrt(normA) * Mathf.Sqrt(normB);

        if (denominator < 0.0001f)
            return 0f;

        return dot / denominator;
    }

    /// <summary>
    /// Prüft ob Paper-Section zur Rule passt
    /// </summary>
    public static bool IsMatch(float[] sectionEmb, float[] posEmb, float[] negEmb, float threshold)
    {
        float posSim = CosineSimilarity(sectionEmb, posEmb);
        float negSim = CosineSimilarity(sectionEmb, negEmb);

        // Match wenn: ähnlich zu positiv UND unähnlich zu negativ
        return posSim >= threshold && posSim > negSim;
    }

    /// <summary>
    /// Berechnet kombinierten Match-Score
    /// </summary>
    public static float CalculateScore(float[] sectionEmb, float[] posEmb, float[] negEmb)
    {
        float posSim = CosineSimilarity(sectionEmb, posEmb);
        float negSim = CosineSimilarity(sectionEmb, negEmb);

        // Score = positive Ähnlichkeit minus negative Ähnlichkeit
        // Normalisiert auf [0, 1]
        return Mathf.Clamp01((posSim - negSim + 1f) / 2f);
    }
}
```

**TODO:**

- [ ] EmbeddingSimilarity.cs erstellen
- [ ] CosineSimilarity testen
- [ ] IsMatch-Logik mit threshold validieren
- [ ] Score-Berechnung für Punkte-System

---

### 6. SSEClient.cs (NEU)

**Server-Sent Events für Real-time Updates**

```csharp
// Assets/Scripts/Networking/SSEClient.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class SSEClient : MonoBehaviour
{
    [SerializeField] private string sseUrl = "http://localhost:8089/api/stream/unity";
    [SerializeField] private float reconnectDelay = 5f;

    public event Action<string, string> OnEventReceived;  // (eventType, jsonData)
    public event Action OnConnected;
    public event Action OnDisconnected;

    private bool isConnected = false;
    private Coroutine connectionCoroutine;

    public void Connect()
    {
        if (connectionCoroutine != null)
            StopCoroutine(connectionCoroutine);

        connectionCoroutine = StartCoroutine(SSEConnectionLoop());
    }

    public void Disconnect()
    {
        if (connectionCoroutine != null)
        {
            StopCoroutine(connectionCoroutine);
            connectionCoroutine = null;
        }
        isConnected = false;
        OnDisconnected?.Invoke();
    }

    private IEnumerator SSEConnectionLoop()
    {
        while (true)
        {
            yield return StartCoroutine(ConnectToSSE());

            // Reconnect nach Delay
            Debug.Log($"SSE: Reconnecting in {reconnectDelay}s...");
            yield return new WaitForSeconds(reconnectDelay);
        }
    }

    private IEnumerator ConnectToSSE()
    {
        string clientId = SystemInfo.deviceUniqueIdentifier;
        string url = $"{sseUrl}?client_id={clientId}";

        Debug.Log($"SSE: Connecting to {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Accept", "text/event-stream");
            request.SetRequestHeader("Cache-Control", "no-cache");

            // Use DownloadHandlerBuffer for streaming
            var handler = new DownloadHandlerBuffer();
            request.downloadHandler = handler;

            var operation = request.SendWebRequest();

            int lastProcessedLength = 0;

            while (!operation.isDone)
            {
                // Check for new data
                if (handler.data != null && handler.data.Length > lastProcessedLength)
                {
                    if (!isConnected)
                    {
                        isConnected = true;
                        OnConnected?.Invoke();
                    }

                    string newData = System.Text.Encoding.UTF8.GetString(
                        handler.data, lastProcessedLength,
                        handler.data.Length - lastProcessedLength
                    );

                    ProcessSSEData(newData);
                    lastProcessedLength = handler.data.Length;
                }

                yield return null;
            }

            isConnected = false;
            OnDisconnected?.Invoke();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"SSE: Connection error: {request.error}");
            }
        }
    }

    private void ProcessSSEData(string data)
    {
        // Parse SSE format: "event: xxx\ndata: {...}\n\n"
        string[] lines = data.Split('\n');
        string eventType = null;
        string eventData = null;

        foreach (string line in lines)
        {
            if (line.StartsWith("event: "))
                eventType = line.Substring(7).Trim();
            else if (line.StartsWith("data: "))
                eventData = line.Substring(6).Trim();
        }

        if (!string.IsNullOrEmpty(eventType) && !string.IsNullOrEmpty(eventData))
        {
            Debug.Log($"SSE Event: {eventType}");
            OnEventReceived?.Invoke(eventType, eventData);
        }
    }
}
```

**TODO:**

- [ ] SSEClient.cs erstellen
- [ ] SSE-Parsing testen
- [ ] Event-Handler für: `new_paper`, `paper_validated`, `leaderboard_update`
- [ ] Reconnect-Logik bei Verbindungsabbruch

---

## 🟢 MITTEL - Gameplay Matching

### 7. MatchValidator.cs (NEU)

**Validiert Spieler-Aktionen und berechnet Similarity**

```csharp
// Assets/Scripts/Game/MatchValidator.cs
using UnityEngine;
using System.Collections.Generic;

public class MatchValidator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float matchThreshold = 0.75f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RuleFigureDisplay ruleFigureDisplay;

    private VoxelData currentJob;
    private List<string> foundItems = new List<string>();
    private float jobStartTime;

    /// <summary>
    /// Startet neuen Validierungs-Job
    /// </summary>
    public void StartJob(VoxelData job)
    {
        currentJob = job;
        currentJob.DecodeEmbeddings();
        foundItems.Clear();
        jobStartTime = Time.time;

        // Zeige Rule-Figur
        ruleFigureDisplay.DisplayRule(job.rule_id, job.pos_embedding_b64);
    }

    /// <summary>
    /// Spieler hat Figur gesammelt - prüfe Match
    /// </summary>
    public ValidationResult ValidateCollection(float[] collectedEmbedding)
    {
        if (currentJob == null) return null;

        float similarity = EmbeddingSimilarity.CosineSimilarity(
            collectedEmbedding,
            currentJob.posEmbedding
        );

        bool isMatch = EmbeddingSimilarity.IsMatch(
            collectedEmbedding,
            currentJob.posEmbedding,
            currentJob.negEmbedding,
            currentJob.threshold
        );

        if (isMatch)
        {
            foundItems.Add(currentJob.rule_id);
        }

        return new ValidationResult
        {
            job_id = currentJob.job_id,
            is_match = isMatch,
            similarity = similarity,
            time_spent = Time.time - jobStartTime,
            found_items = string.Join(",", foundItems)
        };
    }

    /// <summary>
    /// Spieler überspringt/lehnt ab
    /// </summary>
    public ValidationResult SkipJob(bool reject)
    {
        if (currentJob == null) return null;

        return new ValidationResult
        {
            job_id = currentJob.job_id,
            is_match = false,
            similarity = 0f,
            time_spent = Time.time - jobStartTime,
            found_items = ""
        };
    }
}
```

**TODO:**

- [ ] MatchValidator.cs erstellen
- [ ] Integration mit CollectiblePaper.cs
- [ ] Visual Feedback bei Match/No-Match
- [ ] Punkte-Vergabe basierend auf Similarity

---

### 8. ValidationResult ERWEITERN

**Aktuelle Version:**

```csharp
[System.Serializable]
public class ValidationResult
{
    public string job_id;
    public bool is_match;
}
```

**NEUE Version:**

```csharp
[System.Serializable]
public class ValidationResult
{
    public string job_id;
    public bool is_match;          // Spieler-Entscheidung / Similarity > threshold
    public float similarity;       // Berechneter Cosine-Score [0-1]
    public float time_spent;       // Sekunden für Entscheidung
    public string found_items;     // Komma-separierte Liste: "is_rct,has_placebo"

    // Optional für detailliertes Tracking
    public int voxels_collected;   // Anzahl gesammelter Voxel
    public float accuracy;         // Wie genau war der Spieler?
}
```

**TODO:**

- [ ] ValidationResult erweitern
- [ ] Alle Felder beim Submit befüllen
- [ ] Server-Kompatibilität testen

---

## 🔵 NIEDRIG - Polish & UX

### 9. UI Erweiterungen

**TODO:**

- [ ] Rule-Figur Preview Panel (oben rechts)
- [ ] Similarity-Meter (Fortschrittsbalken unter Rule)
- [ ] "Rule gefunden!" Popup-Animation
- [ ] Paper-Info Overlay (Titel, Journal, Abstract-Preview)
- [ ] Connection-Status-Icon (grün/rot für MCP-Server)
- [ ] Leaderboard-Button → Leaderboard Scene

---

### 10. VoxelStructureSpawner.cs ERWEITERN

**Aktuelle Logik erweitern für Embedding-basierte Strukturen:**

**TODO:**

- [ ] `SpawnFromEmbedding(float[] embedding)` Methode
- [ ] Paper-Voxel aus `section_embedding_b64` generieren
- [ ] Kollision → Embedding extrahieren für Similarity-Check
- [ ] LOD für Performance (weniger Voxel bei Distanz)

---

### 11. Android Build & Testing

**TODO:**

- [ ] Touch Controls für Swipe-Gesten optimieren
- [ ] Performance-Test mit 768-dim Embeddings
- [ ] Memory-Optimierung (Embeddings cachen)
- [ ] APK Build erstellen
- [ ] Test auf echtem Android-Gerät

---

## 📡 API Endpoints (mcp-paperstream:8089)

| Endpoint                             | Methode | Unity-Nutzung             |
| ------------------------------------ | ------- | ------------------------- |
| `/api/jobs/next?device_id=X&limit=5` | GET     | Jobs mit Embeddings holen |
| `/api/validation/submit`             | POST    | Ergebnisse senden         |
| `/api/stream/unity`                  | GET SSE | Real-time Events          |
| `/api/rules`                         | GET     | Alle aktiven Rules        |
| `/health`                            | GET     | Server-Status prüfen      |

---

## 🎯 Kern-Spielmechanik

```
1. Unity holt Job → Enthält:
   - section_embedding_b64 (Paper-Abschnitt als 768-dim Vektor)
   - pos_embedding_b64 (Rule: "Was suchen wir?" - z.B. "RCT")
   - neg_embedding_b64 (Rule: "Was ist NICHT gemeint?" - z.B. "Review")

2. Unity zeigt:
   - Rule-Figur im Canvas (pos_embedding als 3D-Shape = Zielfigur)
   - Paper-Voxel auf der Strecke (section_embedding als sammelbare Struktur)

3. Spieler sammelt/validiert:
   - Sammelt Voxel-Strukturen
   - Vergleicht mental mit Rule-Figur
   - Bei Match: Punkte!

4. Unity berechnet automatisch:
   - Cosine Similarity zwischen gesammelten Voxeln und Rule
   - is_match = similarity > threshold

5. Unity sendet zurück:
   POST /api/validation/submit
   {
     "device_id": "android_xxx",
     "results": [{
       "job_id": "job_abc123",
       "is_match": true,
       "similarity": 0.82,
       "time_spent": 12.5,
       "found_items": "is_rct,has_placebo"
     }]
   }
```

---

## 📁 Neue Datei-Struktur

```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs          ✅
│   └── RuleMatcher.cs          ✅
├── Embedding/                  ← NEU
│   ├── EmbeddingDecoder.cs     🔴 KRITISCH
│   ├── EmbeddingToVoxel.cs     🔴 KRITISCH
│   └── EmbeddingSimilarity.cs  🟡 HOCH
├── Game/
│   ├── PlayerController.cs     ✅
│   ├── TrackGenerator.cs       ✅
│   └── MatchValidator.cs       🟢 MITTEL (NEU)
├── Networking/
│   ├── ApiClient.cs            ✅ (erweitern: VoxelData)
│   └── SSEClient.cs            🟡 HOCH (NEU)
├── UI/
│   ├── UIManager.cs            ✅
│   └── RuleFigureDisplay.cs    🔴 KRITISCH (NEU)
└── Voxel/
    ├── VoxelStructureSpawner.cs ✅ (erweitern)
    ├── CollectiblePaper.cs      ✅
    └── VoxelMover.cs            ✅
```

---

## 🚀 Nächste Schritte (Priorität)

1. **EmbeddingDecoder.cs** erstellen → Base64 zu float[]
2. **EmbeddingToVoxel.cs** erstellen → float[] zu 3D-Grid
3. **VoxelData** erweitern → Embedding-Felder hinzufügen
4. **RuleFigureDisplay.cs** erstellen → Rule als 3D-Preview
5. **EmbeddingSimilarity.cs** erstellen → Match-Berechnung
6. **ApiClient.cs** testen → Mit echten Server-Jobs
7. **SSEClient.cs** erstellen → Real-time Updates
8. **MatchValidator.cs** erstellen → Gameplay-Integration
9. **Android Build** → APK testen

---

## 📋 Default Rules (PICO-Kriterien)

Diese Rules sind im Server vordefiniert:

| Rule ID                    | Frage                        | Threshold |
| -------------------------- | ---------------------------- | --------- |
| `is_rct`                   | Ist dies ein RCT?            | 0.75      |
| `has_placebo`              | Gibt es Placebo-Kontrolle?   | 0.70      |
| `is_blinded`               | Ist die Studie verblindet?   | 0.70      |
| `reports_primary_outcome`  | Primärer Endpunkt berichtet? | 0.65      |
| `sample_size_adequate`     | Stichprobe > 50?             | 0.60      |
| `has_statistical_analysis` | Statistische Analyse?        | 0.65      |

Jede Rule hat:

- `positive_phrases` → BioBERT Embedding = pos_embedding
- `negative_phrases` → BioBERT Embedding = neg_embedding
- `threshold` → Mindest-Similarity für Match

---

_Zuletzt aktualisiert: 2026-01-24_
