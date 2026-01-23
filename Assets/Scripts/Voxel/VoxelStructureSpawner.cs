// Scripts/Voxel/VoxelStructureSpawner.cs
// Spawnt 3D Voxel-Strukturen aus JSON-Daten vom MCP-Server
// Jede Struktur repräsentiert einen Paper-Abschnitt

using UnityEngine;
using System.Collections.Generic;

// === Datenklassen für JSON-Serialisierung ===

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
        float estimatedSize = Mathf.Pow(voxelCount, 1f/3f) * cubeSize;
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
