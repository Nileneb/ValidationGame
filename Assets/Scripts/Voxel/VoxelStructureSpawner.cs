// Scripts/Voxel/VoxelStructureSpawner.cs
// Spawnt 3D Voxel-Strukturen aus JSON-Daten vom MCP-Server
// Jede Struktur repräsentiert einen Paper-Abschnitt

using UnityEngine;
using System.Collections.Generic;

// === Datenklassen für JSON-Serialisierung ===

[System.Serializable]
public class VoxelData
{
    // Server-Felder (neue API)
    public string job_id;
    public string paper_id;
    public int section_id;
    public string rule_id;
    public string question;
    public float threshold;
    public string section_text;

    // Voxel-Positionen als JSON String (muss geparst werden)
    public string voxel_data;

    // Embeddings als Base64 (müssen dekodiert werden)
    public string section_embedding_b64;
    public string pos_embedding_b64;
    public string neg_embedding_b64;

    // Zur Laufzeit dekodiert (NonSerialized = wird nicht aus JSON gelesen)
    [System.NonSerialized] public float[] section_embedding;
    [System.NonSerialized] public float[] pos_embedding;
    [System.NonSerialized] public float[] neg_embedding;
    [System.NonSerialized] public List<VoxelPosition> voxel_positions;

    // --- Legacy Felder (für Kompatibilität) ---
    public string section;  // Alias für section_id
    public ColorData color;
    [System.NonSerialized] public float[] embedding;

    /// <summary>
    /// Dekodiert Base64-Embeddings und Voxel-Positionen
    /// MUSS nach dem Deserialisieren aufgerufen werden!
    /// </summary>
    public void DecodeData()
    {
        // Base64 → float[]
        if (!string.IsNullOrEmpty(section_embedding_b64))
        {
            section_embedding = DecodeBase64ToFloatArray(section_embedding_b64);
            embedding = section_embedding;  // Legacy-Alias
            Debug.Log($"VoxelData.DecodeData: section_embedding dekodiert! Länge={section_embedding?.Length}, erste Werte: {(section_embedding != null && section_embedding.Length > 3 ? $"{section_embedding[0]:F3}, {section_embedding[1]:F3}, {section_embedding[2]:F3}" : "NULL")}");
        }
        else
        {
            Debug.LogWarning($"VoxelData.DecodeData: section_embedding_b64 ist LEER für {paper_id}!");
        }

        if (!string.IsNullOrEmpty(pos_embedding_b64))
        {
            pos_embedding = DecodeBase64ToFloatArray(pos_embedding_b64);
        }

        if (!string.IsNullOrEmpty(neg_embedding_b64))
        {
            neg_embedding = DecodeBase64ToFloatArray(neg_embedding_b64);
        }

        // Voxel-Daten parsen
        if (!string.IsNullOrEmpty(voxel_data))
        {
            voxel_positions = ParseVoxelData(voxel_data);
        }

        // Legacy: section aus section_id
        if (string.IsNullOrEmpty(section) && section_id > 0)
        {
            section = $"section_{section_id}";
        }

        // Fallback: Default-Farbe wenn keine angegeben
        if (color == null)
        {
            color = new ColorData { r = 0.5f, g = 0.7f, b = 1.0f };
        }

        // Fallback: Leere Voxel-Liste wenn keine Daten
        if (voxel_positions == null || voxel_positions.Count == 0)
        {
            // VERSUCHE ZUERST aus Embedding zu generieren!
            GenerateVoxelsFromEmbedding(0.85f);
        }
    }

    private float[] DecodeBase64ToFloatArray(string base64)
    {
        try
        {
            byte[] bytes = System.Convert.FromBase64String(base64);
            float[] floats = new float[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"VoxelData: Base64 decode error: {e.Message}");
            return new float[768];  // Fallback: leeres Embedding
        }
    }

    private List<VoxelPosition> ParseVoxelData(string jsonArray)
    {
        // Erwartet Format: "[[0,0,0], [1,0,0], ...]" ODER "[[0,0,0,0.5], ...]" (mit density)
        var positions = new List<VoxelPosition>();

        if (string.IsNullOrEmpty(jsonArray))
        {
            Debug.LogWarning("VoxelData: voxel_data ist leer");
            return positions;
        }

        try
        {
            // Einfacher Parser für [[x,y,z], ...] oder [[x,y,z,d], ...] Format
            string cleaned = jsonArray.Trim().Trim('[', ']').Replace(" ", "");

            // Leerer Array?
            if (string.IsNullOrEmpty(cleaned))
            {
                Debug.LogWarning("VoxelData: voxel_data Array ist leer nach Bereinigung");
                return positions;
            }

            string[] groups = cleaned.Split(new string[] { "],[" }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string group in groups)
            {
                string[] coords = group.Trim('[', ']').Split(',');
                if (coords.Length >= 3)
                {
                    // Versuche float zu parsen (für Fälle wie "0.0" statt "0")
                    float fx, fy, fz;
                    if (float.TryParse(coords[0], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out fx) &&
                        float.TryParse(coords[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out fy) &&
                        float.TryParse(coords[2], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out fz))
                    {
                        positions.Add(new VoxelPosition
                        {
                            x = Mathf.RoundToInt(fx),
                            y = Mathf.RoundToInt(fy),
                            z = Mathf.RoundToInt(fz)
                        });
                    }
                }
            }

            if (positions.Count == 0)
            {
                Debug.LogWarning($"VoxelData: Konnte keine Positionen parsen aus: {jsonArray.Substring(0, Mathf.Min(100, jsonArray.Length))}...");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"VoxelData: Voxel parse error: {e.Message}\nInput (first 100 chars): {jsonArray.Substring(0, Mathf.Min(100, jsonArray.Length))}");
        }

        return positions;
    }

    private List<VoxelPosition> GenerateDefaultVoxels()
    {
        // Generiert einen einfachen 3x3x3 Würfel als Fallback
        var positions = new List<VoxelPosition>();
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                for (int z = 0; z < 3; z++)
                {
                    positions.Add(new VoxelPosition { x = x, y = y, z = z });
                }
            }
        }
        return positions;
    }

    /// <summary>
    /// Generiert Voxel-Positionen aus Embedding wenn keine voxel_data vorhanden
    /// </summary>
    public void GenerateVoxelsFromEmbedding(float cubeCount = 50f)
    {
        // Wenn bereits Voxel-Positionen existieren, nicht überschreiben
        if (voxel_positions != null && voxel_positions.Count > 0) return;

        // Embedding vorhanden?
        float[] emb = section_embedding ?? embedding;
        if (emb != null && emb.Length >= 768)
        {
            voxel_positions = EmbeddingToVoxel.ConvertToPositions(emb, cubeCount);

            // Farbe aus Embedding
            Color col = EmbeddingToVoxel.GetColorFromEmbedding(emb);
            color = new ColorData { r = col.r, g = col.g, b = col.b };
        }
        else
        {
            voxel_positions = GenerateDefaultVoxels();
        }
    }

    /// <summary>
    /// Konvertiert VoxelGridData (neues Format aus ApiClient) zu VoxelPositions
    /// </summary>
    public void ImportFromVoxelGridData(VoxelGridData gridData)
    {
        if (gridData == null || gridData.voxels == null)
        {
            Debug.LogWarning("VoxelData: VoxelGridData ist null");
            return;
        }

        voxel_positions = new List<VoxelPosition>();

        foreach (float[] voxel in gridData.voxels)
        {
            if (voxel != null && voxel.Length >= 3)
            {
                voxel_positions.Add(new VoxelPosition
                {
                    x = Mathf.RoundToInt(voxel[0]),
                    y = Mathf.RoundToInt(voxel[1]),
                    z = Mathf.RoundToInt(voxel[2])
                });
            }
        }

        Debug.Log($"VoxelData: Importiert {voxel_positions.Count} Positionen aus VoxelGridData");
    }
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

// === Spawner Klasse ===

public class VoxelStructureSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Material voxelMaterial;

    [Header("Settings")]
    [SerializeField] private float cubeSize = 1f;  // 1 = normale Würfelgröße!
    [SerializeField] private float spawnDistance = 50f;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Lane Settings")]
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private int laneCount = 3;

    [Header("References")]
    [SerializeField] private Transform player;

    private List<GameObject> activeStructures = new List<GameObject>();

    void Awake()
    {
        // ERZWINGE diese Werte (Unity cached alte Inspector-Werte)
        cubeSize = 1f;
        Debug.Log($"VoxelStructureSpawner: cubeSize = {cubeSize}");
    }

    /// <summary>
    /// Spawnt eine Voxel-Struktur aus JSON-String
    /// </summary>
    public GameObject SpawnFromJson(string json)
    {
        VoxelData data = JsonUtility.FromJson<VoxelData>(json);
        return SpawnFromData(data);
    }

    /// <summary>
    /// Spawnt eine Voxel-Struktur aus VoxelData-Objekt
    /// </summary>
    public GameObject SpawnFromData(VoxelData data)
    {
        if (data == null)
        {
            Debug.LogError("VoxelStructureSpawner: data ist NULL!");
            return null;
        }

        // DEBUG: Was haben wir?
        Debug.Log($"SpawnFromData: paper_id={data.paper_id}");
        Debug.Log($"  section_embedding_b64: {(string.IsNullOrEmpty(data.section_embedding_b64) ? "LEER" : $"{data.section_embedding_b64.Length} chars")}");
        Debug.Log($"  section_embedding: {(data.section_embedding == null ? "NULL" : $"{data.section_embedding.Length} floats")}");
        Debug.Log($"  embedding: {(data.embedding == null ? "NULL" : $"{data.embedding.Length} floats")}");

        // IMMER aus Embedding generieren mit Pyramiden-Algorithmus!
        float[] emb = data.section_embedding ?? data.embedding;
        if (emb != null && emb.Length >= 768)
        {
            data.voxel_positions = EmbeddingToVoxel.ConvertToPositions(emb, 50f);  // 50 Würfel
            Debug.Log($"VoxelStructureSpawner: Generiert {data.voxel_positions.Count} Voxels aus Embedding");
        }
        else
        {
            Debug.LogError($"VoxelStructureSpawner: Kein Embedding für {data.paper_id}! emb={emb}, length={emb?.Length}");
            return null;
        }

        Debug.Log($"VoxelStructureSpawner: Spawne {data.voxel_positions.Count} Voxels für {data.paper_id}");

        // Parent-Objekt erstellen
        GameObject structure = new GameObject($"Paper_{data.paper_id}_{data.section}");

        // Zufällige Lane auswählen
        int randomLane = Random.Range(0, laneCount);
        float xPos = (randomLane - 1) * laneDistance;

        // Position setzen (vor dem Spieler)
        float spawnZ = player != null ? player.position.z + spawnDistance : spawnDistance;
        structure.transform.position = new Vector3(xPos, 1f, spawnZ);

        // Cubes spawnen
        SpawnCubes(structure.transform, data);

        // Collider für Einsammeln hinzufügen (exakte Bounding Box)
        AddCollider(structure, data);

        // Collectible Component
        CollectiblePaper collectible = structure.AddComponent<CollectiblePaper>();
        collectible.Initialize(data);

        // Bewegung hinzufügen
        VoxelMover mover = structure.AddComponent<VoxelMover>();
        mover.moveSpeed = -moveSpeed;  // Negativ = auf Spieler zu

        activeStructures.Add(structure);
        return structure;
    }

    void SpawnCubes(Transform parent, VoxelData data)
    {
        // Auto-create cube prefab wenn nicht zugewiesen
        if (cubePrefab == null)
        {
            Debug.LogWarning("VoxelStructureSpawner: Kein Cube Prefab - erstelle automatisch!");
            cubePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubePrefab.SetActive(false);  // Template, nicht sichtbar
            cubePrefab.name = "AutoCubePrefab";
            // Collider entfernen vom Template (Performance)
            Collider col = cubePrefab.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }

        // EINFACH: Eine Farbe aus Embedding
        float[] emb = data.section_embedding ?? data.embedding;
        Color color = EmbeddingToVoxel.GetColorFromEmbedding(emb);

        // Ein Material (shared für alle Cubes dieser Struktur)
        Material mat = null;
        if (voxelMaterial != null)
        {
            mat = new Material(voxelMaterial);
            mat.color = color;
        }

        Debug.Log($"SpawnCubes: Spawne {data.voxel_positions.Count} Cubes, cubeSize={cubeSize}");

        // DEBUG: Erste 5 Positionen ausgeben
        bool allZero = true;
        for (int i = 0; i < Mathf.Min(5, data.voxel_positions.Count); i++)
        {
            var p = data.voxel_positions[i];
            Debug.Log($"  Voxel[{i}]: x={p.x}, y={p.y}, z={p.z}");
            if (p.x != 0 || p.y != 0 || p.z != 0) allZero = false;
        }
        
        // PROBLEM: Alle Positionen sind 0,0,0!
        // Dann generiere ECHTE Positionen aus Embedding
        if (allZero || data.voxel_positions.Count < 3)
        {
            Debug.LogWarning("SpawnCubes: Alle Positionen sind 0,0,0! Generiere neu aus Embedding...");
            float[] embForVoxels = data.section_embedding ?? data.embedding;
            if (embForVoxels != null && embForVoxels.Length >= 768)
            {
                data.voxel_positions = EmbeddingToVoxel.ConvertToPositions(embForVoxels, 50f);  // 50 Würfel
                Debug.Log($"SpawnCubes: NEU generiert: {data.voxel_positions.Count} Voxel");
            }
            else
            {
                // FALLBACK: Einfache Test-Struktur
                Debug.LogWarning("SpawnCubes: Kein Embedding! Erstelle L-Form als Test...");
                data.voxel_positions = new List<VoxelPosition>
                {
                    // L-Form
                    new VoxelPosition { x = 0, y = 0, z = 0 },
                    new VoxelPosition { x = 0, y = 1, z = 0 },
                    new VoxelPosition { x = 0, y = 2, z = 0 },
                    new VoxelPosition { x = 0, y = 3, z = 0 },
                    new VoxelPosition { x = 1, y = 0, z = 0 },
                    new VoxelPosition { x = 2, y = 0, z = 0 },
                };
            }
        }

        foreach (var pos in data.voxel_positions)
        {
            GameObject cube = Instantiate(cubePrefab, parent);

            // WICHTIG: Cube aktivieren (Prefab könnte deaktiviert sein)
            cube.SetActive(true);

            // SIMPEL: Position = x,y,z direkt als Unity-Koordinaten
            float px = pos.x * cubeSize;
            float py = pos.y * cubeSize;
            float pz = pos.z * cubeSize;
            
            cube.transform.localPosition = new Vector3(px, py, pz);
            cube.transform.localScale = Vector3.one * cubeSize;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (mat != null)
                    renderer.material = mat;
                else
                    renderer.material.color = color;
            }
        }
    }

    void AddCollider(GameObject structure, VoxelData data)
    {
        BoxCollider collider = structure.AddComponent<BoxCollider>();
        collider.isTrigger = true;

        // Berechne exakte Bounding Box aus den Voxel-Positionen
        if (data.voxel_positions != null && data.voxel_positions.Count > 0)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var pos in data.voxel_positions)
            {
                min.x = Mathf.Min(min.x, pos.x);
                min.y = Mathf.Min(min.y, pos.y);
                min.z = Mathf.Min(min.z, pos.z);
                max.x = Mathf.Max(max.x, pos.x);
                max.y = Mathf.Max(max.y, pos.y);
                max.z = Mathf.Max(max.z, pos.z);
            }

            // Collider-Size = (max - min + 1) * cubeSize (+ 1 weil Würfel 1 Unit breit)
            Vector3 size = new Vector3(
                (max.x - min.x + 1) * cubeSize,
                (max.y - min.y + 1) * cubeSize,
                (max.z - min.z + 1) * cubeSize
            );

            // Center = Mitte der Bounding Box
            Vector3 center = new Vector3(
                (min.x + max.x) / 2f * cubeSize + cubeSize / 2f,
                (min.y + max.y) / 2f * cubeSize + cubeSize / 2f,
                (min.z + max.z) / 2f * cubeSize + cubeSize / 2f
            );

            collider.size = size;
            collider.center = center;
        }
        else
        {
            // Fallback
            collider.size = Vector3.one * cubeSize;
            collider.center = Vector3.one * cubeSize / 2f;
        }

        // WICHTIG: Rigidbody für Trigger-Kollision erforderlich!
        Rigidbody rb = structure.AddComponent<Rigidbody>();
        rb.isKinematic = true;  // Keine Physik, nur Trigger
        rb.useGravity = false;
    }

    void Update()
    {
        // Cleanup: Structures hinter Spieler zerstören
        if (player == null) return;

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

    // Setter für Player-Referenz
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }
}
