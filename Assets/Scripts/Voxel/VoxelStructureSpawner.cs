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
            voxel_positions = GenerateDefaultVoxels();
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
        if (data == null || data.voxel_positions == null)
        {
            Debug.LogError("VoxelStructureSpawner: Ungültige Voxel-Daten!");
            return null;
        }

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
        if (cubePrefab == null)
        {
            Debug.LogError("VoxelStructureSpawner: Kein Cube Prefab zugewiesen!");
            return;
        }

        // Material mit Farbe erstellen
        Material instanceMaterial = null;
        if (voxelMaterial != null && data.color != null)
        {
            instanceMaterial = new Material(voxelMaterial);
            instanceMaterial.color = new Color(data.color.r, data.color.g, data.color.b);
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

            // Material zuweisen
            if (instanceMaterial != null)
            {
                Renderer renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = instanceMaterial;
                }
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
