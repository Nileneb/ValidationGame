# Unity PaperStream Integration - TODO

## 📋 Übersicht

| Task                         | Priorität  | Aufwand | Status |
| ---------------------------- | ---------- | ------- | ------ |
| VoxelMeshBuilder.cs          | 🔴 HOCH    | 2h      | ⬜     |
| PaperStreamSSE.cs            | 🔴 HOCH    | 1.5h    | ⬜     |
| VoxelGridData Classes        | 🔴 HOCH    | 30min   | ⬜     |
| Voxel Material/Gradient      | 🟡 MITTEL  | 1h      | ⬜     |
| Paper Info UI Panel          | 🟡 MITTEL  | 1h      | ⬜     |
| GPU Instancing (Optimierung) | 🟢 NIEDRIG | 1h      | ⬜     |

---

## ⚠️ WICHTIG: Server-Besonderheiten

### Zwei Voxel-Formate vom Server:

| Quelle            | Grid          | Format               |
| ----------------- | ------------- | -------------------- |
| PDF-basiert       | `16 x 8 x 16` | `[x, y, z, density]` |
| Embedding-basiert | `8 x 8 x 12`  | 3D Array             |

**→ Immer `grid_size` aus Response lesen, nicht hardcoden!**

### Colored Mode:

- `colored=false`: `[x, y, z, density]` (4 Werte)
- `colored=true`: `[x, y, z, density, r, g, b]` (7 Werte)

---

## 1. VoxelGridData.cs 🔴

**Pfad:** `Assets/Scripts/Data/VoxelGridData.cs`

```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class VoxelGridData
{
    public int[] grid_size;  // [X, Y, Z] - VARIABEL!
    public float[][] voxels; // [[x, y, z, density], ...]
    public VoxelStats stats;
    public int page;

    public int GridX => grid_size != null && grid_size.Length > 0 ? grid_size[0] : 16;
    public int GridY => grid_size != null && grid_size.Length > 1 ? grid_size[1] : 8;
    public int GridZ => grid_size != null && grid_size.Length > 2 ? grid_size[2] : 16;
}

[Serializable]
public class VoxelStats
{
    public int total;
    public float density_avg;
    public float fill_ratio;
}

[Serializable]
public class ColoredVoxelData
{
    public int[] grid_size;
    public float[][] voxels; // [[x, y, z, density, r, g, b], ...]
    public bool colored;
    public VoxelStats stats;
}

[Serializable]
public class PaperValidatedEvent
{
    public string paper_id;
    public string title;
    public Dictionary<string, bool> rules_results;
    public VoxelGridData voxel_data;
    public string thumbnail_base64;
    public string timestamp;
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
```

### Checkliste:

- [ ] VoxelGridData mit dynamischer grid_size
- [ ] ColoredVoxelData für RGB-Modus
- [ ] Alle SSE Event-Typen abgedeckt

---

## 2. VoxelMeshBuilder.cs 🔴

**Pfad:** `Assets/Scripts/Rendering/VoxelMeshBuilder.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

public class VoxelMeshBuilder : MonoBehaviour
{
    [Header("Settings")]
    public float voxelSize = 0.5f;
    public Material voxelMaterial;
    public Gradient densityGradient;

    [Header("Optimization")]
    public bool useCombinedMesh = true;
    public int maxVoxelsPerMesh = 2000;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();

        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }

    /// <summary>
    /// Baut Mesh aus VoxelGridData
    /// </summary>
    public void BuildMesh(VoxelGridData data)
    {
        if (data == null || data.voxels == null || data.voxels.Length == 0)
        {
            Debug.LogWarning("[VoxelMesh] No voxel data");
            return;
        }

        Debug.Log($"[VoxelMesh] Building mesh: {data.stats.total} voxels, grid {data.GridX}x{data.GridY}x{data.GridZ}");

        if (useCombinedMesh && data.stats.total <= maxVoxelsPerMesh)
        {
            BuildCombinedMesh(data);
        }
        else
        {
            BuildInstancedMesh(data);
        }
    }

    /// <summary>
    /// Kombiniertes Mesh für kleine Voxel-Anzahl (<2000)
    /// </summary>
    private void BuildCombinedMesh(VoxelGridData data)
    {
        ClearMesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        Mesh cubeMesh = CreateCubeMesh();
        Vector3[] cubeVerts = cubeMesh.vertices;
        int[] cubeTris = cubeMesh.triangles;

        int vertOffset = 0;

        foreach (float[] voxel in data.voxels)
        {
            if (voxel.Length < 4) continue;

            float x = voxel[0];
            float y = voxel[1];
            float z = voxel[2];
            float density = voxel[3];

            Vector3 pos = new Vector3(x, y, z) * voxelSize;
            Color col = densityGradient.Evaluate(density);

            // Vertices hinzufügen
            foreach (Vector3 v in cubeVerts)
            {
                vertices.Add(pos + v * voxelSize * 0.95f);
                colors.Add(col);
            }

            // Triangles mit Offset
            foreach (int t in cubeTris)
            {
                triangles.Add(t + vertOffset);
            }

            vertOffset += cubeVerts.Length;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _meshFilter.mesh = mesh;
        _meshRenderer.material = voxelMaterial;
    }

    /// <summary>
    /// GPU Instancing für große Voxel-Anzahl (>2000)
    /// </summary>
    private void BuildInstancedMesh(VoxelGridData data)
    {
        // Fallback auf einfachere Methode oder DrawMeshInstanced
        ClearMesh();

        Mesh cubeMesh = CreateCubeMesh();
        MaterialPropertyBlock props = new MaterialPropertyBlock();

        List<Matrix4x4> matrices = new List<Matrix4x4>();
        List<Vector4> colorData = new List<Vector4>();

        foreach (float[] voxel in data.voxels)
        {
            if (voxel.Length < 4) continue;

            Vector3 pos = new Vector3(voxel[0], voxel[1], voxel[2]) * voxelSize;
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * voxelSize * 0.95f);
            matrices.Add(matrix);

            Color col = densityGradient.Evaluate(voxel[3]);
            colorData.Add(col);
        }

        // Unity Limit: 1023 per batch
        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            Matrix4x4[] batch = matrices.GetRange(i, count).ToArray();
            Graphics.DrawMeshInstanced(cubeMesh, 0, voxelMaterial, batch, count, props);
        }
    }

    private void ClearMesh()
    {
        if (_meshFilter != null && _meshFilter.mesh != null)
        {
            _meshFilter.mesh.Clear();
        }
    }

    private Mesh CreateCubeMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
        DestroyImmediate(temp);
        return mesh;
    }

    /// <summary>
    /// Für colored=true Modus
    /// </summary>
    public void BuildColoredMesh(ColoredVoxelData data)
    {
        if (data == null || data.voxels == null) return;

        ClearMesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        Mesh cubeMesh = CreateCubeMesh();
        Vector3[] cubeVerts = cubeMesh.vertices;
        int[] cubeTris = cubeMesh.triangles;

        int vertOffset = 0;

        foreach (float[] voxel in data.voxels)
        {
            if (voxel.Length < 7) continue; // x,y,z,density,r,g,b

            Vector3 pos = new Vector3(voxel[0], voxel[1], voxel[2]) * voxelSize;
            Color col = new Color(voxel[4] / 255f, voxel[5] / 255f, voxel[6] / 255f);

            foreach (Vector3 v in cubeVerts)
            {
                vertices.Add(pos + v * voxelSize * 0.95f);
                colors.Add(col);
            }

            foreach (int t in cubeTris)
            {
                triangles.Add(t + vertOffset);
            }

            vertOffset += cubeVerts.Length;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();

        _meshFilter.mesh = mesh;
        _meshRenderer.material = voxelMaterial;
    }
}
```

### Checkliste:

- [ ] Dynamische grid_size Unterstützung
- [ ] BuildCombinedMesh für <2000 Voxels
- [ ] BuildInstancedMesh für >2000 Voxels
- [ ] BuildColoredMesh für RGB-Daten
- [ ] Density → Color Gradient

---

## 3. PaperStreamSSE.cs 🔴

**Pfad:** `Assets/Scripts/Network/PaperStreamSSE.cs`

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

public class PaperStreamSSE : MonoBehaviour
{
    [Header("Connection")]
    public string serverUrl = "http://localhost:8089";
    public string clientId = "unity_game";
    public float reconnectDelay = 5f;
    public float heartbeatTimeout = 30f;

    [Header("References")]
    public VoxelMeshBuilder voxelBuilder;
    public PaperInfoPanel infoPanel;

    // Events für andere Scripts
    public event Action<PaperValidatedEvent> OnPaperValidated;
    public event Action<LeaderboardEvent> OnLeaderboardUpdate;
    public event Action<NewPaperEvent> OnNewPaper;
    public event Action OnConnected;
    public event Action OnDisconnected;

    private bool _isConnected = false;
    private float _lastHeartbeat;
    private Coroutine _sseCoroutine;

    public bool IsConnected => _isConnected;

    void Start()
    {
        Connect();
    }

    public void Connect()
    {
        if (_sseCoroutine != null)
            StopCoroutine(_sseCoroutine);

        _sseCoroutine = StartCoroutine(SSEListener());
    }

    public void Disconnect()
    {
        if (_sseCoroutine != null)
        {
            StopCoroutine(_sseCoroutine);
            _sseCoroutine = null;
        }
        _isConnected = false;
        OnDisconnected?.Invoke();
    }

    private IEnumerator SSEListener()
    {
        string url = $"{serverUrl}/api/stream/unity?client_id={clientId}";
        Debug.Log($"[SSE] Connecting to {url}");

        using (UnityWebRequest request = new UnityWebRequest(url, "GET"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "text/event-stream");
            request.SetRequestHeader("Cache-Control", "no-cache");
            request.timeout = 0; // Kein Timeout für SSE

            var operation = request.SendWebRequest();

            StringBuilder buffer = new StringBuilder();
            long lastPosition = 0;
            _lastHeartbeat = Time.time;

            while (!operation.isDone)
            {
                // Heartbeat-Timeout prüfen
                if (Time.time - _lastHeartbeat > heartbeatTimeout)
                {
                    Debug.LogWarning("[SSE] Heartbeat timeout");
                    break;
                }

                // Neue Daten lesen
                if (request.downloadHandler.data != null)
                {
                    long currentLength = request.downloadedBytes;
                    if (currentLength > lastPosition)
                    {
                        byte[] newBytes = new byte[currentLength - lastPosition];
                        Array.Copy(request.downloadHandler.data, lastPosition, newBytes, 0, newBytes.Length);
                        string newData = Encoding.UTF8.GetString(newBytes);

                        buffer.Append(newData);
                        ProcessBuffer(buffer);
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

        // Reconnect
        _isConnected = false;
        OnDisconnected?.Invoke();
        Debug.Log($"[SSE] Reconnecting in {reconnectDelay}s...");
        yield return new WaitForSeconds(reconnectDelay);
        Connect();
    }

    private void ProcessBuffer(StringBuilder buffer)
    {
        string content = buffer.ToString();

        // SSE Events sind durch doppelte Newlines getrennt
        while (content.Contains("\n\n"))
        {
            int eventEnd = content.IndexOf("\n\n");
            string eventBlock = content.Substring(0, eventEnd);
            content = content.Substring(eventEnd + 2);

            ParseEvent(eventBlock);
        }

        // Rest behalten
        buffer.Clear();
        buffer.Append(content);
    }

    private void ParseEvent(string eventBlock)
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

        HandleEvent(eventType, eventData);
    }

    private void HandleEvent(string eventType, string data)
    {
        Debug.Log($"[SSE] Event: {eventType}");
        _lastHeartbeat = Time.time;

        switch (eventType)
        {
            case "connected":
                _isConnected = true;
                OnConnected?.Invoke();
                break;

            case "heartbeat":
                // Nur Heartbeat aktualisieren
                break;

            case "paper_validated":
                try
                {
                    var evt = JsonUtility.FromJson<PaperValidatedEvent>(data);
                    OnPaperValidated?.Invoke(evt);

                    // Voxel-Mesh bauen
                    if (evt.voxel_data != null && voxelBuilder != null)
                    {
                        voxelBuilder.BuildMesh(evt.voxel_data);
                    }

                    // Info-Panel aktualisieren
                    if (infoPanel != null)
                    {
                        infoPanel.DisplayPaper(evt);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SSE] Failed to parse paper_validated: {e.Message}");
                }
                break;

            case "leaderboard_update":
                try
                {
                    var evt = JsonUtility.FromJson<LeaderboardEvent>(data);
                    OnLeaderboardUpdate?.Invoke(evt);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SSE] Failed to parse leaderboard: {e.Message}");
                }
                break;

            case "new_paper":
                try
                {
                    var evt = JsonUtility.FromJson<NewPaperEvent>(data);
                    OnNewPaper?.Invoke(evt);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SSE] Failed to parse new_paper: {e.Message}");
                }
                break;

            default:
                Debug.Log($"[SSE] Unknown event type: {eventType}");
                break;
        }
    }

    void OnDestroy()
    {
        Disconnect();
    }
}
```

### Checkliste:

- [ ] SSE-Verbindung mit Reconnect
- [ ] Heartbeat-Timeout
- [ ] Event-Parsing
- [ ] Automatisches Voxel-Mesh bauen
- [ ] C# Events für andere Scripts

---

## 4. Voxel Material 🟡

**Pfad:** `Assets/Materials/VoxelDensity.mat`

### Gradient Setup:

```
Low Density (0.0):  RGB(50, 50, 150)   - Dunkelblau
Mid Density (0.5):  RGB(50, 150, 50)   - Grün
High Density (1.0): RGB(200, 50, 50)   - Rot
```

### Shader (Optional, für Vertex Colors):

```hlsl
// Assets/Shaders/VertexColor.shader
Shader "Custom/VertexColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
```

### Checkliste:

- [ ] Material mit Vertex Color Support
- [ ] Density Gradient im Inspector einstellbar
- [ ] Optional: Custom Shader

---

## 5. Paper Info UI Panel 🟡

**Pfad:** `Assets/Scripts/UI/PaperInfoPanel.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PaperInfoPanel : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI paperIdText;
    public TextMeshProUGUI statsText;
    public Image thumbnailImage;
    public Transform rulesContainer;
    public GameObject ruleItemPrefab;

    public void DisplayPaper(PaperValidatedEvent paper)
    {
        if (paper == null) return;

        titleText.text = paper.title ?? "Unknown";
        paperIdText.text = paper.paper_id ?? "";

        // Stats
        if (paper.voxel_data != null && paper.voxel_data.stats != null)
        {
            statsText.text = $"Voxels: {paper.voxel_data.stats.total}\n" +
                           $"Density: {paper.voxel_data.stats.density_avg:F2}\n" +
                           $"Fill: {paper.voxel_data.stats.fill_ratio:P0}";
        }

        // Thumbnail (Base64)
        if (!string.IsNullOrEmpty(paper.thumbnail_base64))
        {
            try
            {
                byte[] imageBytes = System.Convert.FromBase64String(paper.thumbnail_base64);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(imageBytes);
                thumbnailImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }
            catch { }
        }

        // Rules Results
        if (paper.rules_results != null && rulesContainer != null)
        {
            // Clear old
            foreach (Transform child in rulesContainer)
            {
                Destroy(child.gameObject);
            }

            // Add new
            foreach (var kvp in paper.rules_results)
            {
                GameObject item = Instantiate(ruleItemPrefab, rulesContainer);
                var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = kvp.Key;
                    texts[1].text = kvp.Value ? "✓" : "✗";
                    texts[1].color = kvp.Value ? Color.green : Color.red;
                }
            }
        }
    }
}
```

### Checkliste:

- [ ] UI Panel Prefab erstellen
- [ ] RuleResultItem Prefab
- [ ] Base64 Thumbnail dekodieren
- [ ] Animation bei neuem Paper

---

## 📁 Ordnerstruktur

```
Assets/
├── Scripts/
│   ├── Data/
│   │   └── VoxelGridData.cs
│   ├── Rendering/
│   │   └── VoxelMeshBuilder.cs
│   ├── Network/
│   │   └── PaperStreamSSE.cs
│   └── UI/
│       └── PaperInfoPanel.cs
├── Materials/
│   └── VoxelDensity.mat
├── Shaders/
│   └── VertexColor.shader
└── Prefabs/
    ├── VoxelPaper.prefab
    └── UI/
        └── RuleResultItem.prefab
```

---

## 🧪 Test-Szenario

1. **Server starten:**

   ```bash
   cd mcp-paperstream
   python -m paperstream.server_integrated
   ```

2. **Unity Play Mode starten**

3. **Test-Paper submitten:**

   ```bash
   curl -X POST http://localhost:8089/api/papers/submit \
     -H "Content-Type: application/json" \
     -d '{"paper_id": "test123", "title": "Test Paper", "pdf_url": "..."}'
   ```

4. **Erwartetes Ergebnis:**
   - SSE verbindet
   - Paper wird prozessiert
   - `paper_validated` Event kommt
   - Voxel-Mesh erscheint in Unity

---

## ⏱️ Zeitschätzung

| Task                | Zeit   |
| ------------------- | ------ |
| VoxelGridData.cs    | 30 min |
| VoxelMeshBuilder.cs | 2h     |
| PaperStreamSSE.cs   | 1.5h   |
| Material + Shader   | 1h     |
| UI Panel            | 1h     |
| Testing             | 1h     |
| **Gesamt**          | **7h** |

---

## 🐛 Bekannte Server-Bugs (zu fixen)

1. **section_to_voxel() fehlt** - Export vorhanden, Funktion nicht
2. **Grid-Inkonsistenz** - 16x8x16 vs 8x8x12 (Unity muss dynamisch sein)
3. **Consensus → SSE** - Prüfen ob voxel_data mitgegeben wird
