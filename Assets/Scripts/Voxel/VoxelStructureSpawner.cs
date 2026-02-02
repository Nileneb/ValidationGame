// Assets/Scripts/Voxel/VoxelStructureSpawner.cs
// Spawnt Voxel-Strukturen aus Chunk-Objekten nach DATAMODEL.md
// Jede Struktur = ein Chunk mit seinem 8×8×12 Voxel-Grid

using UnityEngine;
using System.Collections.Generic;
using ValidationGame.Data;

public class VoxelStructureSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Material voxelMaterial;

    [Header("Settings")]
    [SerializeField] private float cubeSize = 1f;
    [SerializeField] private float spawnDistance = 50f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float voxelThreshold = 0.3f;

    [Header("Lane Settings")]
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private int laneCount = 3;

    [Header("References")]
    [SerializeField] private Transform player;

    private List<GameObject> activeStructures = new List<GameObject>();

    void Awake()
    {
        cubeSize = 1f;
        EmbeddingToVoxel.DefaultThreshold = voxelThreshold;
    }

    // ============================================================
    // NEUE API - Chunk-basiert nach DATAMODEL.md
    // ============================================================

    /// <summary>
    /// Spawnt eine Voxel-Struktur aus einem Chunk-Objekt
    /// </summary>
    public GameObject SpawnFromChunk(Chunk chunk, string parentId = "")
    {
        if (chunk == null)
        {
            Debug.LogError("VoxelStructureSpawner: Chunk ist NULL!");
            return null;
        }

        // Chunk dekodieren falls noch nicht geschehen
        if (chunk.voxelGrid == null || chunk.voxelGrid.voxelCount == 0)
        {
            chunk.Decode(voxelThreshold);
        }

        if (chunk.voxelGrid == null || chunk.voxelGrid.voxelCount == 0)
        {
            Debug.LogWarning($"VoxelStructureSpawner: Chunk {chunk.chunk_id} hat keine Voxels");
            return null;
        }

        Debug.Log($"VoxelStructureSpawner: Spawne Chunk {chunk.chunk_id} ({chunk.chunk_type}) mit {chunk.voxelGrid.voxelCount} Voxels");

        // Parent-GameObject erstellen
        string name = string.IsNullOrEmpty(parentId) 
            ? $"Chunk_{chunk.chunk_id}_{chunk.chunk_type}"
            : $"{parentId}_Chunk_{chunk.chunk_id}";
        
        GameObject structure = new GameObject(name);
        chunk.gameObject = structure;

        // Position: Entweder aus Chunk oder zufällige Lane
        Vector3 position;
        if (chunk.position != null && (chunk.position.x != 0 || chunk.position.y != 0 || chunk.position.z != 0))
        {
            position = chunk.position.ToVector3();
        }
        else
        {
            int randomLane = Random.Range(0, laneCount);
            float xPos = (randomLane - 1) * laneDistance;
            float spawnZ = player != null ? player.position.z + spawnDistance : spawnDistance;
            position = new Vector3(xPos, 1f, spawnZ);
        }
        structure.transform.position = position;

        // Voxels spawnen
        SpawnVoxelsFromChunk(structure.transform, chunk);

        // Collider hinzufügen
        AddColliderFromGrid(structure, chunk.voxelGrid);

        activeStructures.Add(structure);
        return structure;
    }

    /// <summary>
    /// Spawnt ein komplettes Molecule (mehrere verbundene Chunks)
    /// </summary>
    public GameObject SpawnFromMolecule(Molecule molecule)
    {
        if (molecule == null || molecule.chunks == null || molecule.chunks.Count == 0)
        {
            Debug.LogError("VoxelStructureSpawner: Molecule ist NULL oder leer!");
            return null;
        }

        molecule.DecodeAll(voxelThreshold);

        // Root-GameObject für das Molecule
        GameObject root = new GameObject($"Molecule_{molecule.molecule_id}");
        molecule.rootObject = root;

        // Position
        int randomLane = Random.Range(0, laneCount);
        float xPos = (randomLane - 1) * laneDistance;
        float spawnZ = player != null ? player.position.z + spawnDistance : spawnDistance;
        root.transform.position = new Vector3(xPos, 1f, spawnZ);

        // Chunks spawnen
        float chunkSpacing = 10f;  // Abstand zwischen Chunks
        for (int i = 0; i < molecule.chunks.Count; i++)
        {
            var chunk = molecule.chunks[i];
            
            // Position relativ zum Root
            chunk.position = new ChunkPosition(i * chunkSpacing, 0, 0);
            
            GameObject chunkObj = SpawnChunkAsChild(root.transform, chunk);
            if (chunkObj != null)
            {
                chunkObj.transform.localPosition = new Vector3(i * chunkSpacing, 0, 0);
            }
        }

        // TODO: Wire-Rendering zwischen verbundenen Chunks

        // Bewegung hinzufügen
        VoxelMover mover = root.AddComponent<VoxelMover>();
        mover.moveSpeed = -moveSpeed;

        activeStructures.Add(root);
        return root;
    }

    /// <summary>
    /// Spawnt einen Chunk als Child eines Parents (für Molecules)
    /// </summary>
    private GameObject SpawnChunkAsChild(Transform parent, Chunk chunk)
    {
        if (chunk.voxelGrid == null || chunk.voxelGrid.voxelCount == 0)
        {
            chunk.Decode(voxelThreshold);
        }

        GameObject structure = new GameObject($"Chunk_{chunk.chunk_id}_{chunk.chunk_type}");
        structure.transform.SetParent(parent);
        chunk.gameObject = structure;

        SpawnVoxelsFromChunk(structure.transform, chunk);
        
        return structure;
    }

    /// <summary>
    /// Spawnt die Voxel-Cubes für einen Chunk
    /// Farbe = chunk.color * voxel.value (Intensität)
    /// </summary>
    private void SpawnVoxelsFromChunk(Transform parent, Chunk chunk)
    {
        EnsureCubePrefab();

        // Basis-Farbe aus Chunk
        Color baseColor = chunk.color?.ToUnityColor() ?? EmbeddingToVoxel.GetChunkTypeColor(chunk.chunk_type);

        // Material erstellen
        Material mat = null;
        if (voxelMaterial != null)
        {
            mat = new Material(voxelMaterial);
        }

        foreach (var voxel in chunk.voxelGrid.voxels)
        {
            GameObject cube = Instantiate(cubePrefab, parent);
            cube.SetActive(true);

            // Position: Voxel-Koordinaten * cubeSize
            cube.transform.localPosition = new Vector3(
                voxel.x * cubeSize,
                voxel.y * cubeSize,
                voxel.z * cubeSize
            );
            cube.transform.localScale = Vector3.one * cubeSize;

            // Farbe: baseColor * voxel.value (Intensität)
            Color voxelColor = baseColor * voxel.value;
            voxelColor.a = 1f;  // Alpha immer 1

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (mat != null)
                {
                    Material instanceMat = new Material(mat);
                    instanceMat.color = voxelColor;
                    renderer.material = instanceMat;
                }
                else
                {
                    renderer.material.color = voxelColor;
                }
            }
        }
    }

    // ============================================================
    // LEGACY API - Für Kompatibilität mit bestehendem Code
    // ============================================================

    /// <summary>
    /// [LEGACY] Spawnt aus JSON-String
    /// </summary>
    public GameObject SpawnFromJson(string json)
    {
        VoxelData data = JsonUtility.FromJson<VoxelData>(json);
        return SpawnFromData(data);
    }

    /// <summary>
    /// [LEGACY] Spawnt aus VoxelData-Objekt
    /// Konvertiert intern zu Chunk-Format
    /// </summary>
    public GameObject SpawnFromData(VoxelData data)
    {
        if (data == null)
        {
            Debug.LogError("VoxelStructureSpawner: data ist NULL!");
            return null;
        }

        // VoxelData.DecodeData() aufrufen
        data.DecodeData();

        // Konvertiere zu Chunk
        Chunk chunk = ConvertVoxelDataToChunk(data);
        
        // Spawne über neue API
        GameObject structure = SpawnFromChunk(chunk, data.paper_id);
        
        if (structure != null)
        {
            // Collectible Component für Gameplay
            CollectiblePaper collectible = structure.AddComponent<CollectiblePaper>();
            collectible.Initialize(data);

            // Bewegung hinzufügen
            VoxelMover mover = structure.AddComponent<VoxelMover>();
            mover.moveSpeed = -moveSpeed;
        }

        return structure;
    }

    /// <summary>
    /// Konvertiert altes VoxelData zu neuem Chunk-Format
    /// </summary>
    private Chunk ConvertVoxelDataToChunk(VoxelData data)
    {
        var chunk = new Chunk
        {
            chunk_id = data.section_id,
            chunk_type = data.section ?? "abstract",
            text_preview = data.section_text?.Substring(0, Mathf.Min(500, data.section_text?.Length ?? 0))
        };

        // Embedding übernehmen
        chunk.embedding = data.section_embedding ?? data.embedding;
        
        if (chunk.embedding != null)
        {
            // Base64 encodieren
            byte[] bytes = new byte[chunk.embedding.Length * 4];
            System.Buffer.BlockCopy(chunk.embedding, 0, bytes, 0, bytes.Length);
            chunk.embedding_b64 = System.Convert.ToBase64String(bytes);
        }

        // Farbe
        if (data.color != null)
        {
            chunk.color = new ChunkColor(data.color.r, data.color.g, data.color.b);
        }

        // Dekodieren und VoxelGrid erstellen
        chunk.Decode(voxelThreshold);

        return chunk;
    }

    // ============================================================
    // HELPER METHODS
    // ============================================================

    private void EnsureCubePrefab()
    {
        if (cubePrefab == null)
        {
            cubePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubePrefab.SetActive(false);
            cubePrefab.name = "AutoCubePrefab";
            Collider col = cubePrefab.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }
    }

    private void AddColliderFromGrid(GameObject structure, VoxelGrid grid)
    {
        BoxCollider collider = structure.AddComponent<BoxCollider>();
        collider.isTrigger = true;

        // Bounding Box aus Grid-Dimensionen
        collider.size = new Vector3(
            VoxelGrid.SIZE_X * cubeSize,
            VoxelGrid.SIZE_Y * cubeSize,
            VoxelGrid.SIZE_Z * cubeSize
        );
        collider.center = collider.size / 2f;

        // Rigidbody für Trigger
        Rigidbody rb = structure.AddComponent<Rigidbody>();
        rb.isKinematic = true;
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

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    public void SetThreshold(float threshold)
    {
        voxelThreshold = threshold;
        EmbeddingToVoxel.DefaultThreshold = threshold;
    }
}
