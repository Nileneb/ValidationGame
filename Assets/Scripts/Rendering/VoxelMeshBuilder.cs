// Scripts/Rendering/VoxelMeshBuilder.cs
// Baut Mesh-Strukturen aus Voxel-Daten vom Server
// Unterstützt dynamische Grid-Größen (16x8x16 oder 8x8x12)
// NEU: Paper-Molecule Visualisierung mit Chunks und Verbindungen
// Optimiert für weniger als 2000 Voxels (Combined) oder mehr als 2000 (Instanced)

using UnityEngine;
using System.Collections.Generic;

public class VoxelMeshBuilder : MonoBehaviour
{
    [Header("Voxel Settings")]
    [Tooltip("Größe eines einzelnen Voxels in Unity-Einheiten")]
    public float voxelSize = 0.5f;

    [Tooltip("Material für Standard-Voxel (z.B. Rules)")]
    public Material voxelMaterial;

    [Tooltip("Material für Paper-Embeddings")]
    public Material paperMaterial;

    [Tooltip("Farbverlauf basierend auf Density-Wert (0-1)")]
    public Gradient densityGradient;

    [Header("Paper-Molecule Settings")]
    [Tooltip("Größe eines Chunk-Würfels")]
    public float chunkSize = 1.0f;

    [Tooltip("Material für Verbindungslinien zwischen Chunks")]
    public Material connectionMaterial;

    [Tooltip("Dicke der Verbindungslinien")]
    public float connectionWidth = 0.1f;

    [Header("Optimization")]
    [Tooltip("Kombiniertes Mesh für kleine Voxel-Anzahl verwenden")]
    public bool useCombinedMesh = true;

    [Tooltip("Maximale Voxel-Anzahl für kombiniertes Mesh")]
    public int maxVoxelsPerMesh = 2000;

    [Tooltip("Abstand zwischen Voxeln (0.95 = 5% Lücke)")]
    [Range(0.8f, 1.0f)]
    public float voxelSpacing = 0.95f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Interne Referenzen
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _cachedCubeMesh;
    private LineRenderer _connectionLineRenderer;
    private List<GameObject> _chunkObjects = new List<GameObject>();

    void Awake()
    {
        // MeshFilter und MeshRenderer sicherstellen
        _meshFilter = GetComponent<MeshFilter>();
        if (_meshFilter == null)
            _meshFilter = gameObject.AddComponent<MeshFilter>();

        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer == null)
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Default Gradient erstellen falls nicht gesetzt
        if (densityGradient == null)
        {
            densityGradient = CreateDefaultGradient();
        }
    }

    /// <summary>
    /// Baut Mesh aus VoxelGridData (Standard-Format ohne Farben)
    /// </summary>
    public void BuildMesh(VoxelGridData data)
    {
        if (data == null || data.voxels == null || data.voxels.Length == 0)
        {
            if (showDebugLogs) Debug.LogWarning("[VoxelMesh] Keine Voxel-Daten vorhanden");
            return;
        }

        int voxelCount = data.stats != null ? data.stats.total : data.voxels.Length;

        if (showDebugLogs)
        {
            Debug.Log($"[VoxelMesh] Baue Mesh: {voxelCount} Voxels, Grid {data.GridX}x{data.GridY}x{data.GridZ}");
        }

        if (useCombinedMesh && voxelCount <= maxVoxelsPerMesh)
        {
            BuildCombinedMesh(data);
        }
        else
        {
            BuildInstancedMesh(data);
        }
    }

    /// <summary>
    /// Kombiniertes Mesh für kleine Voxel-Anzahl
    /// KEIN Greedy Meshing - alle Würfel vollständig rendern!
    /// </summary>
    private void BuildCombinedMesh(VoxelGridData data)
    {
        ClearMesh();

        Mesh cubeMesh = GetCubeMesh();
        Vector3[] cubeVerts = cubeMesh.vertices;
        int[] cubeTris = cubeMesh.triangles;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        int vertOffset = 0;

        // Jeden Voxel als vollständigen Würfel rendern (KEIN Greedy!)
        foreach (float[] voxel in data.voxels)
        {
            if (voxel == null || voxel.Length < 4) continue;

            Vector3 pos = new Vector3(voxel[0], voxel[1], voxel[2]) * voxelSize;
            float density = Mathf.Clamp01(voxel[3]);
            Color col = densityGradient.Evaluate(density);

            // Alle Vertices des Würfels hinzufügen
            foreach (Vector3 v in cubeVerts)
            {
                vertices.Add(pos + v * voxelSize * voxelSpacing);
                colors.Add(col);
            }

            // Triangles mit Offset
            foreach (int t in cubeTris)
            {
                triangles.Add(t + vertOffset);
            }

            vertOffset += cubeVerts.Length;
        }

        // Mesh erstellen
        Mesh mesh = new Mesh();
        mesh.name = "VoxelStructure_Full";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _meshFilter.mesh = mesh;

        // Material setzen
        if (voxelMaterial != null)
        {
            _meshRenderer.material = voxelMaterial;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[VoxelMesh] Combined Mesh erstellt: {vertices.Count} Vertices");
        }
    }

    /// <summary>
    /// Instanced Mesh für große Voxel-Anzahl
    /// </summary>
    private void BuildInstancedMesh(VoxelGridData data)
    {
        // Für große Mengen: GPU Instancing nutzen
        // Hier vereinfacht: Einfach mehrere Meshes erstellen
        if (showDebugLogs) Debug.Log("[VoxelMesh] Instanced Mesh (vereinfacht)");
        BuildCombinedMesh(data); // Fallback für jetzt
    }

    /// <summary>
    /// Baut Mesh aus ColoredVoxelData (mit RGB-Farben)
    /// </summary>
    public void BuildColoredMesh(ColoredVoxelData data)
    {
        if (data == null || data.voxels == null || data.voxels.Length == 0)
        {
            if (showDebugLogs) Debug.LogWarning("[VoxelMesh] Keine farbigen Voxel-Daten");
            return;
        }

        ClearMesh();

        Mesh cubeMesh = GetCubeMesh();
        Vector3[] cubeVerts = cubeMesh.vertices;
        int[] cubeTris = cubeMesh.triangles;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        int vertOffset = 0;

        foreach (float[] voxel in data.voxels)
        {
            if (voxel == null || voxel.Length < 6) continue;

            Vector3 pos = new Vector3(voxel[0], voxel[1], voxel[2]) * voxelSize;
            Color col = new Color(voxel[3], voxel[4], voxel[5]);

            foreach (Vector3 v in cubeVerts)
            {
                vertices.Add(pos + v * voxelSize * voxelSpacing);
                colors.Add(col);
            }

            foreach (int t in cubeTris)
            {
                triangles.Add(t + vertOffset);
            }

            vertOffset += cubeVerts.Length;
        }

        Mesh mesh = new Mesh();
        mesh.name = "ColoredVoxelStructure";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _meshFilter.mesh = mesh;

        if (paperMaterial != null)
        {
            _meshRenderer.material = paperMaterial;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[VoxelMesh] Colored Mesh: {data.voxels.Length} Voxels");
        }
    }

    /// <summary>
    /// Baut Mesh aus Raw Float Array
    /// </summary>
    public void BuildFromRawArray(float[][] voxels, int[] gridSize)
    {
        var data = new VoxelGridData
        {
            voxels = voxels,
            grid_size = gridSize
        };
        BuildMesh(data);
    }

    /// <summary>
    /// NEU: Baut Paper-Molecule aus Chunks
    /// </summary>
    public void BuildMolecule(VoxelData paperData)
    {
        if (paperData == null || !paperData.HasChunks)
        {
            if (showDebugLogs) Debug.LogWarning("[VoxelMesh] Keine Chunks vorhanden");
            return;
        }

        ClearMolecule();

        float scale = paperData.molecule_config?.scale ?? 1.0f;
        string layout = paperData.molecule_config?.layout ?? "chain";

        if (showDebugLogs)
        {
            Debug.Log($"[VoxelMesh] Molecule: {paperData.chunks.Length} Chunks, Layout: {layout}");
        }

        Dictionary<int, Vector3> chunkPositions = new Dictionary<int, Vector3>();

        // Chunks erstellen
        foreach (var chunk in paperData.chunks)
        {
            GameObject cubeObj = CreateChunkCube(chunk, scale, layout);
            cubeObj.transform.SetParent(transform);
            _chunkObjects.Add(cubeObj);

            chunkPositions[chunk.chunk_id] = cubeObj.transform.localPosition;
        }

        // Verbindungen erstellen
        BuildConnections(paperData.chunks, chunkPositions, scale);
    }

    /// <summary>
    /// NEU: Baut Rule-Dipol aus Chunks
    /// </summary>
    public void BuildRuleDipole(RuleData ruleData)
    {
        if (ruleData == null || !ruleData.HasChunks)
        {
            if (showDebugLogs) Debug.LogWarning("[VoxelMesh] Rule hat keine Chunks");
            return;
        }

        ClearMolecule();

        float scale = ruleData.molecule_config?.scale ?? 1.5f;
        string layout = ruleData.molecule_config?.layout ?? "dipole";

        if (showDebugLogs)
        {
            Debug.Log($"[VoxelMesh] Rule-Dipol: {ruleData.rule_id}, {ruleData.chunks.Length} Chunks");
        }

        Dictionary<int, Vector3> chunkPositions = new Dictionary<int, Vector3>();

        // Chunks erstellen
        foreach (var chunk in ruleData.chunks)
        {
            GameObject cubeObj = CreateChunkCube(chunk, scale, layout);
            cubeObj.transform.SetParent(transform);
            _chunkObjects.Add(cubeObj);

            chunkPositions[chunk.chunk_id] = cubeObj.transform.localPosition;
        }

        // Verbindungen erstellen
        BuildConnections(ruleData.chunks, chunkPositions, scale);
    }

    /// <summary>
    /// Erstellt einen Chunk-Würfel
    /// </summary>
    private GameObject CreateChunkCube(ChunkData chunk, float scale, string layout = "chain")
    {
        GameObject cubeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeObj.name = $"Chunk_{chunk.chunk_id}_{chunk.section_name ?? chunk.chunk_type ?? "unknown"}";

        // Position berechnen
        Vector3 pos;
        if (layout == "dipole")
        {
            // Dipol: Positiv links (-X), Negativ rechts (+X)
            float xOffset = chunk.IsPositive ? -1.5f : 1.5f;
            pos = new Vector3(xOffset, 0, 0) * scale;
        }
        else
        {
            // Chain: Nutze Position aus Chunk-Daten
            pos = chunk.GetPosition() * scale;
        }

        cubeObj.transform.localPosition = pos;
        cubeObj.transform.localScale = Vector3.one * chunkSize * scale;

        // Material und Farbe
        var renderer = cubeObj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material mat = voxelMaterial != null
                ? new Material(voxelMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = chunk.GetUnityColor();
            renderer.material = mat;
        }

        // ChunkInfo Komponente
        var info = cubeObj.AddComponent<ChunkInfo>();
        info.Initialize(chunk);

        // Collider entfernen (Parent hat Collider)
        var col = cubeObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        return cubeObj;
    }

    /// <summary>
    /// Erstellt Verbindungslinien zwischen Chunks
    /// </summary>
    private void BuildConnections(ChunkData[] chunks, Dictionary<int, Vector3> positions, float scale)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.connects_to == null || chunk.connects_to.Length == 0) continue;
            if (!positions.ContainsKey(chunk.chunk_id)) continue;

            Vector3 fromPos = positions[chunk.chunk_id];

            foreach (int targetId in chunk.connects_to)
            {
                if (!positions.ContainsKey(targetId)) continue;
                Vector3 toPos = positions[targetId];

                // Ziel-Chunk für Farbe finden
                ChunkData target = System.Array.Find(chunks, c => c.chunk_id == targetId);
                if (target == null) continue;

                // LineRenderer erstellen
                GameObject lineObj = new GameObject($"Connection_{chunk.chunk_id}_to_{targetId}");
                lineObj.transform.SetParent(transform);

                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, fromPos);
                lr.SetPosition(1, toPos);
                lr.startWidth = connectionWidth * scale;
                lr.endWidth = connectionWidth * scale;
                lr.useWorldSpace = false;

                // Material
                if (connectionMaterial != null)
                {
                    lr.material = new Material(connectionMaterial);
                }
                else
                {
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                }

                // Farbverlauf
                lr.startColor = chunk.GetUnityColor();
                lr.endColor = target.GetUnityColor();

                _chunkObjects.Add(lineObj);
            }
        }
    }

    /// <summary>
    /// Löscht alle Molecule-Objekte
    /// </summary>
    public void ClearMolecule()
    {
        foreach (var obj in _chunkObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _chunkObjects.Clear();
    }

    /// <summary>
    /// Baut Molecule direkt aus Chunks-Array
    /// </summary>
    public void BuildMoleculeFromChunks(ChunkData[] chunks, float scale = 1.0f)
    {
        if (chunks == null || chunks.Length == 0)
        {
            if (showDebugLogs) Debug.LogWarning("[VoxelMesh] Keine Chunks");
            return;
        }

        ClearMolecule();

        Dictionary<int, Vector3> chunkPositions = new Dictionary<int, Vector3>();

        foreach (var chunk in chunks)
        {
            GameObject cubeObj = CreateChunkCube(chunk, scale, "chain");
            cubeObj.transform.SetParent(transform);
            _chunkObjects.Add(cubeObj);

            chunkPositions[chunk.chunk_id] = cubeObj.transform.localPosition;
        }

        BuildConnections(chunks, chunkPositions, scale);
    }

    /// <summary>
    /// Baut aus JSON-String
    /// </summary>
    public void BuildFromJSON(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[VoxelMesh] JSON leer");
            return;
        }

        try
        {
            // Versuche als VoxelGridData
            VoxelGridData data = JsonUtility.FromJson<VoxelGridData>(json);
            if (data != null && data.voxels != null)
            {
                BuildMesh(data);
                return;
            }

            // Versuche als ColoredVoxelData
            ColoredVoxelData colored = JsonUtility.FromJson<ColoredVoxelData>(json);
            if (colored != null && colored.voxels != null)
            {
                BuildColoredMesh(colored);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VoxelMesh] JSON Parse Error: {e.Message}");
        }
    }

    /// <summary>
    /// Löscht das aktuelle Mesh
    /// </summary>
    public void ClearMesh()
    {
        if (_meshFilter != null && _meshFilter.mesh != null)
        {
            if (Application.isPlaying)
                Destroy(_meshFilter.mesh);
            else
                DestroyImmediate(_meshFilter.mesh);
            _meshFilter.mesh = null;
        }
    }

    /// <summary>
    /// Gibt das gecachte Cube-Mesh zurück
    /// </summary>
    private Mesh GetCubeMesh()
    {
        if (_cachedCubeMesh == null)
        {
            _cachedCubeMesh = CreateCubeMesh();
        }
        return _cachedCubeMesh;
    }

    /// <summary>
    /// Erstellt ein einfaches Cube-Mesh
    /// </summary>
    private Mesh CreateCubeMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Mesh copy = Instantiate(mesh);
        DestroyImmediate(temp);
        return copy;
    }

    /// <summary>
    /// Erstellt einen Standard-Gradient
    /// </summary>
    private Gradient CreateDefaultGradient()
    {
        Gradient g = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[3];
        colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.4f, 0.8f), 0.0f);
        colorKeys[1] = new GradientColorKey(new Color(0.4f, 0.8f, 0.4f), 0.5f);
        colorKeys[2] = new GradientColorKey(new Color(0.9f, 0.6f, 0.2f), 1.0f);

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

        g.SetKeys(colorKeys, alphaKeys);
        return g;
    }

    /// <summary>
    /// Gibt die Bounds des Meshes zurück
    /// </summary>
    public Bounds GetMeshBounds()
    {
        if (_meshFilter != null && _meshFilter.mesh != null)
        {
            return _meshFilter.mesh.bounds;
        }
        return new Bounds(Vector3.zero, Vector3.one);
    }

    /// <summary>
    /// Setzt den Material-Typ
    /// </summary>
    public void SetMaterialType(VoxelType type)
    {
        if (_meshRenderer == null) return;

        switch (type)
        {
            case VoxelType.Paper:
                if (paperMaterial != null) _meshRenderer.material = paperMaterial;
                break;
            case VoxelType.Rule:
                if (voxelMaterial != null) _meshRenderer.material = voxelMaterial;
                break;
        }
    }

    public void UsePaperMaterial()
    {
        SetMaterialType(VoxelType.Paper);
    }

    public void UseRuleMaterial()
    {
        SetMaterialType(VoxelType.Rule);
    }

    // === RULE COLORS ===

    [Header("Rule Match Colors")]
    public Color positiveMatchColor = new Color(0.2f, 0.9f, 0.3f);
    public Color negativeMatchColor = new Color(0.9f, 0.2f, 0.2f);
    public Color neutralColor = new Color(0.5f, 0.5f, 0.5f);
    public float colorTransitionDuration = 0.5f;

    private Color _targetColor;
    private Color _startColor;
    private float _colorTransitionTime;
    private bool _isTransitioning;

    public void ApplyRuleColors(float similarity, float threshold, bool animated = true)
    {
        Color targetColor;
        if (similarity >= threshold)
        {
            float t = Mathf.InverseLerp(threshold, 1f, similarity);
            targetColor = Color.Lerp(neutralColor, positiveMatchColor, t);
        }
        else
        {
            float t = Mathf.InverseLerp(0f, threshold, similarity);
            targetColor = Color.Lerp(negativeMatchColor, neutralColor, t);
        }

        SetRuleColor(targetColor, animated);
    }

    public void SetRuleColor(Color color, bool animated = true)
    {
        if (animated && colorTransitionDuration > 0)
        {
            _startColor = _meshRenderer != null && _meshRenderer.material != null
                ? _meshRenderer.material.color
                : neutralColor;
            _targetColor = color;
            _colorTransitionTime = 0;
            _isTransitioning = true;
        }
        else
        {
            ApplyColorToMesh(color);
        }
    }

    public void ResetRuleColors()
    {
        SetRuleColor(neutralColor, true);
    }

    private void ApplyColorToMesh(Color color)
    {
        if (_meshRenderer != null && _meshRenderer.material != null)
        {
            _meshRenderer.material.color = color;
        }

        foreach (var obj in _chunkObjects)
        {
            var rend = obj?.GetComponent<MeshRenderer>();
            if (rend != null && rend.material != null)
            {
                rend.material.color = color;
            }
        }
    }

    void Update()
    {
        if (_isTransitioning)
        {
            _colorTransitionTime += Time.deltaTime;
            float t = _colorTransitionTime / colorTransitionDuration;

            if (t >= 1f)
            {
                ApplyColorToMesh(_targetColor);
                _isTransitioning = false;
            }
            else
            {
                ApplyColorToMesh(Color.Lerp(_startColor, _targetColor, t));
            }
        }
    }

    /// <summary>
    /// Voxel-Selektion per Raycast
    /// </summary>
    public bool TrySelectVoxel(Ray ray, out Vector3 hitPosition, out int voxelIndex)
    {
        hitPosition = Vector3.zero;
        voxelIndex = -1;

        var col = GetComponent<Collider>();
        if (col == null) return false;

        RaycastHit hit;
        if (col.Raycast(ray, out hit, 100f))
        {
            hitPosition = hit.point;
            voxelIndex = PositionToVoxelIndex(transform.InverseTransformPoint(hit.point));
            return true;
        }

        return false;
    }

    private int PositionToVoxelIndex(Vector3 localPos)
    {
        int x = Mathf.FloorToInt(localPos.x / voxelSize);
        int y = Mathf.FloorToInt(localPos.y / voxelSize);
        int z = Mathf.FloorToInt(localPos.z / voxelSize);
        return x + y * 16 + z * 16 * 16;
    }
}

// VoxelType Enum
public enum VoxelType
{
    Paper,
    Rule
}
