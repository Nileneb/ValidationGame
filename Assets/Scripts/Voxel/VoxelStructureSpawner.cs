// Scripts/Voxel/VoxelStructureSpawner.cs
// Spawnt 3D Voxel-Strukturen aus ChunkData JSON vom mcp-paperstream
// VEREINFACHT: Nur Kern-Funktionalität, kein Legacy-Code

using UnityEngine;
using System.Collections.Generic;

// ===== DATENKLASSEN =====

[System.Serializable]
public class ChunkData
{
    public int chunk_id;
    public string section_name;    // "abstract", "methods", etc.
    public string chunk_type;      // Paper: section_name, Rule: "positive"/"negative"
    public string text_preview;
    public string embedding_b64;   // Base64-encoded 768-float32
    public ChunkColor color;
    public ChunkPosition position;
    public int[] connects_to;

    [System.NonSerialized] public float[] embedding;

    public void DecodeEmbedding()
    {
        if (!string.IsNullOrEmpty(embedding_b64))
        {
            embedding = EmbeddingUtils.DecodeBase64ToFloatArray(embedding_b64);
        }
    }

    public Color GetUnityColor()
    {
        if (color != null)
            return new Color(color.r, color.g, color.b);
        return Color.white;
    }

    public Vector3 GetPosition()
    {
        if (position != null)
            return new Vector3(position.x, position.y, position.z);
        return Vector3.zero;
    }
}

[System.Serializable]
public class ChunkColor
{
    public float r;
    public float g;
    public float b;
}

[System.Serializable]
public class ChunkPosition
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class VoxelData
{
    public string paper_id;
    public string section;
    public string title;
    public string status;
    public string paper_text;
    public string paper_embedding_b64;
    public ChunkData[] chunks;

    [System.NonSerialized] public float[] paper_embedding;

    public void DecodeEmbeddings()
    {
        if (!string.IsNullOrEmpty(paper_embedding_b64))
        {
            paper_embedding = EmbeddingUtils.DecodeBase64ToFloatArray(paper_embedding_b64);
        }

        if (chunks != null)
        {
            foreach (var chunk in chunks)
            {
                chunk.DecodeEmbedding();
            }
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

// ===== SPAWNER =====

/// <summary>
/// Spawnt Voxel-Strukturen aus ChunkData (BioBERT Embeddings als 3D-Gitter)
/// </summary>
public class VoxelStructureSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Material voxelMaterial;

    [Header("Einstellungen")]
    [SerializeField] private float cubeSize = 0.5f;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Spawn-Bereich")]
    [SerializeField] private Transform player;
    [SerializeField] private float spawnDistance = 50f;
    [SerializeField] private float despawnDistance = 100f;

    private List<GameObject> activeStructures = new List<GameObject>();

    void Awake()
    {
        // Cube-Prefab erstellen falls nicht vorhanden
        if (cubePrefab == null)
        {
            cubePrefab = CreateDefaultCubePrefab();
        }

        // Material zuweisen
        if (voxelMaterial == null)
        {
            voxelMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        // Player finden
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    /// <summary>
    /// Erstellt eine Standard-Voxel-Struktur aus ChunkData
    /// </summary>
    public GameObject SpawnFromJson(VoxelData voxelData)
    {
        if (voxelData == null || voxelData.chunks == null)
        {
            Debug.LogError("VoxelStructureSpawner: VoxelData ist null!");
            return null;
        }

        // Dekodiere Embeddings
        voxelData.DecodeEmbeddings();

        // Parent-Container für alle Chunks
        GameObject container = new GameObject($"Paper_{voxelData.paper_id}");
        container.transform.position = GetSpawnPosition();

        // Spawne jeden Chunk
        if (voxelData.chunks != null)
        {
            foreach (var chunk in voxelData.chunks)
            {
                SpawnChunk(chunk, container.transform);
            }
        }

        // VoxelMover - bewegt Structure zum Spieler
        VoxelMover mover = container.AddComponent<VoxelMover>();
        mover.moveSpeed = -moveSpeed;

        // CollectiblePaper - Spieler-Kollision
        CollectiblePaper collectible = container.AddComponent<CollectiblePaper>();
        collectible.Initialize(voxelData);

        activeStructures.Add(container);
        return container;
    }

    /// <summary>
    /// Spawnt einen einzelnen Chunk als Voxel-Gitter
    /// </summary>
    private void SpawnChunk(ChunkData chunk, Transform parent)
    {
        if (chunk.embedding == null || chunk.embedding.Length == 0)
        {
            Debug.LogWarning($"VoxelStructureSpawner: Chunk {chunk.chunk_id} hat kein Embedding!");
            return;
        }

        // Container für diesen Chunk (unsichtbar)
        GameObject chunkObj = new GameObject($"Chunk_{chunk.chunk_id}_{chunk.section_name}");
        chunkObj.transform.SetParent(parent);
        chunkObj.transform.localPosition = chunk.GetPosition();

        // Konvertiere Embedding zu Voxel-Positionen
        List<Vector3> voxelPositions = EmbeddingToVoxel.ConvertToPositions(chunk.embedding, cubeSize);

        // Spawne Voxel
        foreach (var pos in voxelPositions)
        {
            GameObject voxel = Instantiate(cubePrefab, chunkObj.transform);
            voxel.transform.localPosition = pos;

            // Material und Farbe
            Renderer rend = voxel.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(voxelMaterial);
                mat.color = chunk.GetUnityColor();
                rend.material = mat;
            }
        }

        // ChunkContainer-Komponente
        ChunkContainer container = chunkObj.AddComponent<ChunkContainer>();
        container.Initialize(chunk);

        Debug.Log($"VoxelStructureSpawner: Chunk spawned - {chunk.section_name} ({voxelPositions.Count} voxels)");
    }

    private Vector3 GetSpawnPosition()
    {
        if (player == null)
            return new Vector3(Random.Range(-5f, 5f), 5f, 50f);

        return player.position + player.forward * spawnDistance + new Vector3(Random.Range(-3f, 3f), 0, 0);
    }

    void Update()
    {
        // Cleanup: Structures despawnen wenn zu weit weg
        for (int i = activeStructures.Count - 1; i >= 0; i--)
        {
            if (activeStructures[i] == null)
            {
                activeStructures.RemoveAt(i);
                continue;
            }

            if (player != null)
            {
                float dist = Vector3.Distance(activeStructures[i].transform.position, player.position);
                if (dist > despawnDistance)
                {
                    Destroy(activeStructures[i]);
                    activeStructures.RemoveAt(i);
                }
            }
        }
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    private GameObject CreateDefaultCubePrefab()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "VoxelCube";
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        return cube;
    }
}

