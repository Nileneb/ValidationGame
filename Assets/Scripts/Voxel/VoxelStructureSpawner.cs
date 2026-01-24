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
            GenerateVoxelsFromEmbedding(0.3f);
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
        // Erwartet Format: "[[0,0,0], [1,0,0], ...]"
        var positions = new List<VoxelPosition>();

        try
        {
            // Einfacher Parser für [[x,y,z], ...] Format
            string cleaned = jsonArray.Trim('[', ']').Replace(" ", "");
            string[] groups = cleaned.Split(new string[] { "],[" }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string group in groups)
            {
                string[] coords = group.Trim('[', ']').Split(',');
                if (coords.Length >= 3)
                {
                    positions.Add(new VoxelPosition
                    {
                        x = int.Parse(coords[0]),
                        y = int.Parse(coords[1]),
                        z = int.Parse(coords[2])
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"VoxelData: Voxel parse error: {e.Message}, using default voxels");
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
    public void GenerateVoxelsFromEmbedding(float threshold = 0.3f)
    {
        // Wenn bereits Voxel-Positionen existieren, nicht überschreiben
        if (voxel_positions != null && voxel_positions.Count > 0) return;

        // Embedding vorhanden?
        float[] emb = section_embedding ?? embedding;
        if (emb != null && emb.Length >= 768)
        {
            voxel_positions = EmbeddingToVoxel.ConvertToPositions(emb, threshold);

            // Farbe aus Embedding
            Color col = EmbeddingToVoxel.GetColorFromEmbedding(emb);
            color = new ColorData { r = col.r, g = col.g, b = col.b };
        }
        else
        {
            voxel_positions = GenerateDefaultVoxels();
        }
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
    [SerializeField] private float cubeSize = 0.5f;
    [SerializeField] private float spawnDistance = 50f;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Lane Settings")]
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private int laneCount = 3;

    [Header("References")]
    [SerializeField] private Transform player;

    private List<GameObject> activeStructures = new List<GameObject>();

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

        // Sicherstellen dass Voxel-Positionen existieren
        if (data.voxel_positions == null || data.voxel_positions.Count == 0)
        {
            Debug.LogWarning($"VoxelStructureSpawner: Keine voxel_positions für {data.paper_id} - generiere aus Embedding!");
            data.GenerateVoxelsFromEmbedding(0.3f);
        }

        if (data.voxel_positions == null || data.voxel_positions.Count == 0)
        {
            Debug.LogError("VoxelStructureSpawner: Konnte keine Voxel-Positionen generieren!");
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

        // Collider für Einsammeln hinzufügen
        AddCollider(structure, data.voxel_positions.Count);

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
        }

        // EINFACH: Eine Farbe aus Embedding
        float[] emb = data.section_embedding ?? data.embedding;
        Color color = EmbeddingToVoxel.GetColorFromEmbedding(emb);

        // Ein Material
        Material mat = null;
        if (voxelMaterial != null)
        {
            mat = new Material(voxelMaterial);
            mat.color = color;
        }

        foreach (var pos in data.voxel_positions)
        {
            GameObject cube = Instantiate(cubePrefab, parent);
            cube.transform.localPosition = new Vector3(
                pos.x * cubeSize,
                pos.y * cubeSize,
                pos.z * cubeSize
            );
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

    void AddCollider(GameObject structure, int voxelCount)
    {
        BoxCollider collider = structure.AddComponent<BoxCollider>();
        collider.isTrigger = true;

        // Collider-Größe basierend auf Voxel-Anzahl schätzen
        float estimatedSize = Mathf.Pow(voxelCount, 1f / 3f) * cubeSize;
        collider.size = new Vector3(estimatedSize * 2, estimatedSize * 2, estimatedSize * 2);
        collider.center = new Vector3(estimatedSize / 2, estimatedSize / 2, estimatedSize / 2);

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
