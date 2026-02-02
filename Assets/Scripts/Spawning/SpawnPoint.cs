// Scripts/Spawning/SpawnPoint.cs
// Ein einzelner Spawn-Punkt der EINEN Job bearbeitet
// Spawnt die Punktwolke eines Papers nach und nach (animiert)

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SpawnPoint: Bearbeitet einen Job und spawnt dessen Punktwolke nach und nach.
/// Jeder SpawnPoint kann unabhängig arbeiten → paralleles Spawning möglich.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private bool isBusy = false;
    [SerializeField] private string currentJobId = "";

    [Header("Spawn Settings")]
    [Tooltip("Wie viele Voxels pro Sekunde spawnen")]
    public float voxelsPerSecond = 20f;

    [Tooltip("Delay zwischen Spawn-Batches")]
    public float spawnBatchDelay = 0.05f;

    [Tooltip("Voxels pro Batch")]
    public int voxelsPerBatch = 5;

    [Header("Voxel Settings")]
    [Tooltip("Größe eines Voxels")]
    public float voxelSize = 0.3f;

    [Tooltip("Material für Voxels")]
    public Material voxelMaterial;

    [Header("References")]
    [Tooltip("Container für gespawnte Voxels")]
    public Transform voxelContainer;

    // Interner State
    private VoxelData _currentJob;
    private GameObject _currentStructure;
    private List<GameObject> _spawnedVoxels = new List<GameObject>();
    private Coroutine _spawnCoroutine;

    // Events
    public System.Action<SpawnPoint, VoxelData> OnJobStarted;
    public System.Action<SpawnPoint, VoxelData> OnJobCompleted;
    public System.Action<SpawnPoint, GameObject> OnStructureReady;

    void Awake()
    {
        // Container erstellen falls nicht vorhanden
        if (voxelContainer == null)
        {
            voxelContainer = new GameObject("VoxelContainer").transform;
            voxelContainer.SetParent(transform);
            voxelContainer.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// Ist dieser SpawnPoint frei für einen neuen Job?
    /// </summary>
    public bool IsAvailable => !isBusy;

    /// <summary>
    /// Aktueller Job-ID (leer wenn frei)
    /// </summary>
    public string CurrentJobId => currentJobId;

    /// <summary>
    /// Aktuelles Job-Objekt
    /// </summary>
    public VoxelData CurrentJob => _currentJob;

    /// <summary>
    /// Startet einen neuen Job an diesem SpawnPoint
    /// </summary>
    public bool StartJob(VoxelData job)
    {
        if (isBusy)
        {
            Debug.LogWarning($"SpawnPoint '{name}': Bereits beschäftigt mit Job '{currentJobId}'");
            return false;
        }

        if (job == null)
        {
            Debug.LogError($"SpawnPoint '{name}': Job ist NULL!");
            return false;
        }

        // Alten Job aufräumen
        ClearCurrentJob();

        _currentJob = job;
        currentJobId = job.job_id ?? job.paper_id ?? "unknown";
        isBusy = true;

        Debug.Log($"SpawnPoint '{name}': Starte Job '{currentJobId}'");
        OnJobStarted?.Invoke(this, job);

        // Spawning starten
        _spawnCoroutine = StartCoroutine(SpawnJobCoroutine(job));

        return true;
    }

    /// <summary>
    /// Spawnt die Punktwolke nach und nach
    /// </summary>
    private IEnumerator SpawnJobCoroutine(VoxelData job)
    {
        // Struktur-Container erstellen
        string structureName = $"Paper_{job.paper_id ?? job.job_id}";
        _currentStructure = new GameObject(structureName);
        _currentStructure.transform.SetParent(voxelContainer);
        _currentStructure.transform.localPosition = Vector3.zero;

        // === MOLECULE MODE: Chunks spawnen ===
        if (job.HasChunks)
        {
            yield return SpawnMoleculeChunks(job);
        }
        // === LEGACY MODE: Voxel-Positionen spawnen ===
        else
        {
            yield return SpawnVoxelPositions(job);
        }

        // Collider für Einsammeln
        AddColliderToStructure();

        // CollectiblePaper Komponente
        CollectiblePaper collectible = _currentStructure.AddComponent<CollectiblePaper>();
        collectible.Initialize(job);

        Debug.Log($"SpawnPoint '{name}': Job '{currentJobId}' fertig - {_spawnedVoxels.Count} Voxels");

        OnStructureReady?.Invoke(this, _currentStructure);
        OnJobCompleted?.Invoke(this, job);

        // Nicht mehr busy, aber Structure bleibt
        isBusy = false;
    }

    /// <summary>
    /// Spawnt Molecule-Chunks (für neue API)
    /// NEU: CHIFFRE-Algorithmus - Jeder Chunk bekommt eigene Voxel-Struktur!
    /// Chunks werden räumlich verteilt und durch Wires verbunden.
    /// </summary>
    private IEnumerator SpawnMoleculeChunks(VoxelData job)
    {
        // === ZUERST: Alle Embeddings dekodieren ===
        if (job.section_embedding == null && !string.IsNullOrEmpty(job.paper_embedding_b64))
        {
            Debug.Log($"SpawnPoint: Dekodiere Embeddings für '{job.paper_id}'...");
            job.DecodeData();
        }
        
        string paperId = job.paper_id ?? System.Guid.NewGuid().ToString();
        
        // === CHUNK-BASIERTE VISUALISIERUNG ===
        // Jeder Chunk bekommt seinen eigenen 3D-Container!
        if (job.chunks != null && job.chunks.Length > 0)
        {
            Debug.Log($"SpawnPoint CHIFFRE: Paper '{paperId}' hat {job.chunks.Length} Chunks");
            
            // Chunk-Container für Positionen speichern (für Wire-Verbindungen)
            List<GameObject> chunkContainers = new List<GameObject>();
            
            // Chunk-Spacing: Wie weit sind Chunks auseinander?
            float chunkSpacing = 15f;  // 15 Units zwischen Chunks
            
            for (int chunkIdx = 0; chunkIdx < job.chunks.Length; chunkIdx++)
            {
                var chunk = job.chunks[chunkIdx];
                
                // Chunk-Embedding dekodieren falls nötig
                if (chunk.embedding == null && !string.IsNullOrEmpty(chunk.embedding_b64))
                {
                    chunk.DecodeEmbedding();
                }
                
                float[] chunkEmb = chunk.embedding;
                if (chunkEmb == null || chunkEmb.Length < 768)
                {
                    Debug.LogWarning($"  Chunk {chunkIdx} '{chunk.section_name}' hat kein Embedding, überspringe...");
                    continue;
                }
                
                // === CHUNK-CONTAINER erstellen ===
                string chunkName = $"Chunk_{chunkIdx}_{chunk.section_name ?? "unknown"}";
                GameObject chunkContainer = new GameObject(chunkName);
                chunkContainer.transform.SetParent(_currentStructure.transform);
                
                // Chunk-Position: Verteile Chunks im 3D-Raum
                // Verwende chunk.position falls vorhanden, sonst verteile linear
                Vector3 chunkPos;
                if (chunk.position != null)
                {
                    chunkPos = chunk.GetPosition() * chunkSpacing;
                }
                else
                {
                    // Lineare Verteilung entlang X-Achse
                    float xOffset = (chunkIdx - (job.chunks.Length - 1) / 2f) * chunkSpacing;
                    chunkPos = new Vector3(xOffset, 0, 0);
                }
                chunkContainer.transform.localPosition = chunkPos;
                
                // Farbe aus Chunk-Embedding
                Color chunkColor = chunk.GetUnityColor();
                if (chunkColor == Color.white || chunkColor == default)
                {
                    chunkColor = EmbeddingToVoxel.GetColorFromEmbedding(chunkEmb);
                }
                
                Debug.Log($"  Chunk {chunkIdx} '{chunk.section_name}': {chunkEmb.Length}-dim Embedding, Position: {chunkPos}");
                
                // === CHIFFRE-Algorithmus: Chunk-Embedding → Voxel-Struktur ===
                GameObject voxelStructure = EmbeddingToVoxel.CreateStructure(
                    chunkEmb,
                    chunkContainer.transform,
                    voxelSize,
                    -1f,  // Dynamischer Threshold
                    chunkColor,
                    voxelMaterial,
                    null  // CHIFFRE ignoriert uniqueId - nur Embedding zählt!
                );
                
                if (voxelStructure != null)
                {
                    int cubeCount = voxelStructure.transform.childCount;
                    for (int i = 0; i < cubeCount; i++)
                    {
                        _spawnedVoxels.Add(voxelStructure.transform.GetChild(i).gameObject);
                    }
                    Debug.Log($"    → {cubeCount} Cubes erstellt");
                }
                
                chunkContainers.Add(chunkContainer);
                
                // Kurze Pause für Animation
                yield return new WaitForSeconds(0.05f);
            }
            
            // === WIRE-VERBINDUNGEN zwischen Chunks ===
            if (chunkContainers.Count > 1)
            {
                yield return SpawnChunkWires(chunkContainers, job.chunks);
            }
            
            Debug.Log($"SpawnPoint CHIFFRE: Paper '{paperId}' fertig - {chunkContainers.Count} Chunks, {_spawnedVoxels.Count} Voxels");
        }
        else
        {
            // === FALLBACK: Nur Paper-Level Embedding (kein Chunks) ===
            float[] emb = job.section_embedding ?? job.embedding;
            if (emb != null && emb.Length >= 768)
            {
                Debug.Log($"SpawnPoint CHIFFRE: Paper '{paperId}' ohne Chunks, nutze Paper-Embedding");
                
                Color paperColor = EmbeddingToVoxel.GetColorFromEmbedding(emb);
                
                GameObject voxelStructure = EmbeddingToVoxel.CreateStructure(
                    emb,
                    _currentStructure.transform,
                    voxelSize,
                    -1f,
                    paperColor,
                    voxelMaterial,
                    null
                );
                
                if (voxelStructure != null)
                {
                    int cubeCount = voxelStructure.transform.childCount;
                    for (int i = 0; i < cubeCount; i++)
                    {
                        _spawnedVoxels.Add(voxelStructure.transform.GetChild(i).gameObject);
                    }
                    Debug.Log($"SpawnPoint CHIFFRE: {cubeCount} Cubes für Paper '{paperId}' erstellt!");
                }
            }
            else
            {
                Debug.LogError($"SpawnPoint: Kein Embedding für Paper '{paperId}'!");
                yield return SpawnMoleculeChunksLegacy(job);
            }
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Zeichnet Wire-Verbindungen zwischen Chunk-Containern
    /// Zeigt an, welche Chunks zu einem Paper gehören
    /// </summary>
    private IEnumerator SpawnChunkWires(List<GameObject> chunkContainers, ChunkData[] chunks)
    {
        if (chunkContainers.Count < 2) yield break;
        
        Debug.Log($"  Zeichne {chunkContainers.Count - 1} Wire-Verbindungen...");
        
        // Wire-Container
        GameObject wireContainer = new GameObject("Wires");
        wireContainer.transform.SetParent(_currentStructure.transform);
        wireContainer.transform.localPosition = Vector3.zero;
        
        for (int i = 0; i < chunkContainers.Count - 1; i++)
        {
            Vector3 fromPos = chunkContainers[i].transform.localPosition;
            Vector3 toPos = chunkContainers[i + 1].transform.localPosition;
            
            // Farben der verbundenen Chunks
            Color fromColor = i < chunks.Length ? chunks[i].GetUnityColor() : Color.white;
            Color toColor = (i + 1) < chunks.Length ? chunks[i + 1].GetUnityColor() : Color.white;
            
            // Wire als LineRenderer
            GameObject wireObj = new GameObject($"Wire_{i}_to_{i + 1}");
            wireObj.transform.SetParent(wireContainer.transform);
            wireObj.transform.localPosition = Vector3.zero;
            
            LineRenderer lr = wireObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, fromPos);
            lr.SetPosition(1, toPos);
            
            // Wire-Styling
            lr.startWidth = 0.3f;
            lr.endWidth = 0.3f;
            lr.startColor = fromColor;
            lr.endColor = toColor;
            
            // Material (einfaches Unlit)
            if (voxelMaterial != null)
            {
                Material wireMat = new Material(Shader.Find("Sprites/Default"));
                wireMat.color = Color.Lerp(fromColor, toColor, 0.5f);
                lr.material = wireMat;
            }
            else
            {
                lr.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
        
        // Zusätzlich: connects_to Verbindungen (falls definiert)
        for (int i = 0; i < chunks.Length && i < chunkContainers.Count; i++)
        {
            if (chunks[i].connects_to == null || chunks[i].connects_to.Length == 0) continue;
            
            Vector3 fromPos = chunkContainers[i].transform.localPosition;
            Color fromColor = chunks[i].GetUnityColor();
            
            foreach (int targetId in chunks[i].connects_to)
            {
                // Finde Ziel-Chunk
                int targetIdx = System.Array.FindIndex(chunks, c => c.chunk_id == targetId);
                if (targetIdx < 0 || targetIdx >= chunkContainers.Count) continue;
                if (targetIdx == i + 1) continue;  // Schon als sequentielle Verbindung gezeichnet
                
                Vector3 toPos = chunkContainers[targetIdx].transform.localPosition;
                Color toColor = chunks[targetIdx].GetUnityColor();
                
                // Spezielle Verbindung (gestrichelt wäre schön, aber LineRenderer kann das nicht einfach)
                GameObject wireObj = new GameObject($"Wire_{i}_to_{targetId}_special");
                wireObj.transform.SetParent(wireContainer.transform);
                wireObj.transform.localPosition = Vector3.zero;
                
                LineRenderer lr = wireObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.SetPosition(0, fromPos);
                lr.SetPosition(1, toPos);
                lr.startWidth = 0.15f;  // Dünner für spezielle Verbindungen
                lr.endWidth = 0.15f;
                lr.startColor = new Color(fromColor.r, fromColor.g, fromColor.b, 0.5f);
                lr.endColor = new Color(toColor.r, toColor.g, toColor.b, 0.5f);
                lr.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Legacy-Methode: Ein Cube pro Chunk (Fallback wenn kein Embedding)
    /// </summary>
    private IEnumerator SpawnMoleculeChunksLegacy(VoxelData job)
    {
        float scale = job.molecule_config?.scale ?? 1.0f;

        foreach (var chunk in job.chunks)
        {
            // Chunk als Würfel
            GameObject cubeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeObj.name = $"Chunk_{chunk.chunk_id}_{chunk.section_name ?? chunk.chunk_type}";
            cubeObj.transform.SetParent(_currentStructure.transform);
            cubeObj.transform.localPosition = chunk.GetPosition() * scale;
            cubeObj.transform.localScale = Vector3.one * voxelSize * 2f;  // Chunks größer

            // Farbe
            var renderer = cubeObj.GetComponent<MeshRenderer>();
            if (renderer != null && voxelMaterial != null)
            {
                Material mat = new Material(voxelMaterial);
                mat.color = chunk.GetUnityColor();
                renderer.material = mat;
            }

            // Collider entfernen (nur Structure-Collider)
            var col = cubeObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // ChunkInfo für Interaktion
            var info = cubeObj.AddComponent<ChunkInfo>();
            info.Initialize(chunk);

            _spawnedVoxels.Add(cubeObj);

            // Kurze Pause für Animation
            yield return new WaitForSeconds(0.1f);
        }

        // Verbindungslinien zwischen Chunks
        yield return SpawnChunkConnections(job.chunks, scale);
    }

    /// <summary>
    /// Spawnt Verbindungslinien zwischen Chunks
    /// </summary>
    private IEnumerator SpawnChunkConnections(ChunkData[] chunks, float scale)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.connects_to == null || chunk.connects_to.Length == 0) continue;

            Vector3 fromPos = chunk.GetPosition() * scale;

            foreach (int targetId in chunk.connects_to)
            {
                // Ziel-Chunk finden
                ChunkData target = System.Array.Find(chunks, c => c.chunk_id == targetId);
                if (target == null) continue;

                Vector3 toPos = target.GetPosition() * scale;

                // Linie erstellen
                GameObject lineObj = new GameObject($"Connection_{chunk.chunk_id}_to_{targetId}");
                lineObj.transform.SetParent(_currentStructure.transform);

                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, fromPos);
                lr.SetPosition(1, toPos);
                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;
                lr.useWorldSpace = false;

                if (voxelMaterial != null)
                {
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = chunk.GetUnityColor();
                    lr.endColor = target.GetUnityColor();
                }
            }
        }

        yield return null;
    }

    /// <summary>
    /// Spawnt Voxel-Positionen (Legacy Mode)
    /// </summary>
    private IEnumerator SpawnVoxelPositions(VoxelData job)
    {
        // Embedding dekodieren falls nötig
        if (job.section_embedding == null && !string.IsNullOrEmpty(job.paper_embedding_b64))
        {
            job.DecodeData();
        }

        // Voxel-Positionen generieren falls nicht vorhanden
        List<VoxelPosition> positions = job.voxel_positions;
        if (positions == null || positions.Count == 0)
        {
            float[] emb = job.section_embedding ?? job.embedding;
            if (emb != null && emb.Length >= 768)
            {
                positions = EmbeddingToVoxel.ConvertToVoxelPositions(emb);
            }
        }

        if (positions == null || positions.Count == 0)
        {
            Debug.LogWarning($"SpawnPoint '{name}': Keine Voxel-Positionen für Job '{currentJobId}'");
            yield break;
        }

        // Farbe aus Embedding
        float[] embedding = job.section_embedding ?? job.embedding;
        Color color = embedding != null
            ? EmbeddingToVoxel.GetColorFromEmbedding(embedding)
            : Color.white;

        // Material vorbereiten
        Material mat = null;
        if (voxelMaterial != null)
        {
            mat = new Material(voxelMaterial);
            mat.color = color;
        }

        // Voxels in Batches spawnen
        int spawned = 0;
        foreach (var pos in positions)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Voxel_{spawned}";
            cube.transform.SetParent(_currentStructure.transform);
            cube.transform.localPosition = new Vector3(pos.x, pos.y, pos.z) * voxelSize;
            cube.transform.localScale = Vector3.one * voxelSize * 0.9f;

            if (mat != null)
            {
                cube.GetComponent<MeshRenderer>().material = mat;
            }

            // Collider entfernen
            var col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _spawnedVoxels.Add(cube);
            spawned++;

            // Batch-Pause
            if (spawned % voxelsPerBatch == 0)
            {
                yield return new WaitForSeconds(spawnBatchDelay);
            }
        }
    }

    /// <summary>
    /// Fügt Collider zur fertigen Struktur hinzu
    /// </summary>
    private void AddColliderToStructure()
    {
        if (_currentStructure == null) return;

        // Bounds berechnen
        Bounds bounds = new Bounds(_currentStructure.transform.position, Vector3.zero);
        foreach (var voxel in _spawnedVoxels)
        {
            if (voxel != null)
            {
                bounds.Encapsulate(voxel.transform.position);
            }
        }

        // Box Collider als Trigger
        BoxCollider col = _currentStructure.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = _currentStructure.transform.InverseTransformPoint(bounds.center);
        col.size = bounds.size + Vector3.one * voxelSize;
    }

    /// <summary>
    /// Räumt den aktuellen Job auf
    /// </summary>
    public void ClearCurrentJob()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }

        // Voxels zerstören
        foreach (var voxel in _spawnedVoxels)
        {
            if (voxel != null) Destroy(voxel);
        }
        _spawnedVoxels.Clear();

        // Structure zerstören
        if (_currentStructure != null)
        {
            Destroy(_currentStructure);
            _currentStructure = null;
        }

        _currentJob = null;
        currentJobId = "";
        isBusy = false;
    }

    /// <summary>
    /// Gibt die fertige Struktur zurück (zum Bewegen)
    /// </summary>
    public GameObject GetStructure()
    {
        return _currentStructure;
    }

    void OnDestroy()
    {
        ClearCurrentJob();
    }

    // === EDITOR GIZMOS ===

#if UNITY_EDITOR
    [Header("Gizmo Settings")]
    [SerializeField] private float gizmoSize = 3f;

    void OnDrawGizmos()
    {
        // SpawnPoint als Würfel anzeigen
        Gizmos.color = isBusy ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * gizmoSize);

        // Richtungs-Anzeige
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * gizmoSize * 2);
    }

    void OnDrawGizmosSelected()
    {
        // Detaillierter wenn ausgewählt
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(transform.position, Vector3.one * gizmoSize);

        // VoxelContainer Bereich
        if (voxelContainer != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(voxelContainer.position, Vector3.one * gizmoSize * 2);
        }
    }
#endif
}
