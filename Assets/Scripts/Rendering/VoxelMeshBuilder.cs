using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Baut Mesh-Strukturen aus Voxel-Daten vom Server
/// Unterstützt dynamische Grid-Größen (16x8x16 oder 8x8x12)
/// Optimiert für <2000 Voxels (Combined) oder >2000 (Instanced)
/// </summary>
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

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _cachedCubeMesh;

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
    /// Kombiniertes Mesh für kleine Voxel-Anzahl (<2000)
    /// MIT GREEDY MESHING: Nur sichtbare Faces rendern für bessere Performance
    /// </summary>
    private void BuildCombinedMesh(VoxelGridData data)
    {
        ClearMesh();

        // Voxel-Set für schnelle Nachbar-Prüfung erstellen
        HashSet<Vector3Int> voxelSet = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, float> densityMap = new Dictionary<Vector3Int, float>();

        foreach (float[] voxel in data.voxels)
        {
            if (voxel == null || voxel.Length < 4) continue;
            Vector3Int pos = new Vector3Int((int)voxel[0], (int)voxel[1], (int)voxel[2]);
            voxelSet.Add(pos);
            densityMap[pos] = Mathf.Clamp01(voxel[3]);
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        // Greedy Meshing: Nur sichtbare Faces rendern
        foreach (float[] voxel in data.voxels)
        {
            if (voxel == null || voxel.Length < 4) continue;

            Vector3Int pos = new Vector3Int((int)voxel[0], (int)voxel[1], (int)voxel[2]);
            float density = densityMap[pos];
            Color col = densityGradient.Evaluate(density);
            Vector3 worldPos = new Vector3(pos.x, pos.y, pos.z) * voxelSize;

            // Prüfe alle 6 Nachbarn - nur sichtbare Faces hinzufügen
            AddVisibleFaces(pos, worldPos, voxelSet, col, vertices, triangles, colors);
        }

        // Mesh erstellen
        Mesh mesh = new Mesh();
        mesh.name = "VoxelStructure_Greedy";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _meshFilter.mesh = mesh;
        _meshRenderer.material = voxelMaterial;

        if (showDebugLogs)
        {
            int savedFaces = (data.voxels.Length * 6) - (triangles.Count / 6);
            Debug.Log($"[VoxelMesh] Greedy Mesh: {vertices.Count} Vertices, {triangles.Count / 3} Triangles (saved {savedFaces} faces)");
        }
    }

    /// <summary>
    /// Fügt nur sichtbare Faces hinzu (Greedy Meshing)
    /// </summary>
    private void AddVisibleFaces(Vector3Int pos, Vector3 worldPos, HashSet<Vector3Int> voxelSet,
                                  Color col, List<Vector3> vertices, List<int> triangles, List<Color> colors)
    {
        float halfSize = voxelSize * voxelSpacing * 0.5f;

        // 6 Richtungen: +X, -X, +Y, -Y, +Z, -Z
        Vector3Int[] directions = new Vector3Int[]
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        for (int d = 0; d < 6; d++)
        {
            Vector3Int neighbor = pos + directions[d];

            // Nur Face hinzufügen wenn kein Nachbar-Voxel existiert
            if (!voxelSet.Contains(neighbor))
            {
                AddFace(worldPos, d, halfSize, col, vertices, triangles, colors);
            }
        }
    }

    /// <summary>
    /// Fügt ein einzelnes Face (Quad) hinzu
    /// </summary>
    private void AddFace(Vector3 center, int direction, float halfSize, Color col,
                         List<Vector3> vertices, List<int> triangles, List<Color> colors)
    {
        int startIdx = vertices.Count;

        Vector3[] faceVerts = GetFaceVertices(direction, halfSize);

        foreach (Vector3 v in faceVerts)
        {
            vertices.Add(center + v);
            colors.Add(col);
        }

        // Zwei Triangles für ein Quad
        triangles.Add(startIdx);
        triangles.Add(startIdx + 1);
        triangles.Add(startIdx + 2);
        triangles.Add(startIdx);
        triangles.Add(startIdx + 2);
        triangles.Add(startIdx + 3);
    }

    /// <summary>
    /// Gibt die 4 Vertices für eine Face-Richtung zurück
    /// </summary>
    private Vector3[] GetFaceVertices(int direction, float h)
    {
        switch (direction)
        {
            case 0: // +X (Right)
                return new Vector3[] { new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), new Vector3(h, -h, h) };
            case 1: // -X (Left)
                return new Vector3[] { new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h), new Vector3(-h, -h, -h) };
            case 2: // +Y (Up)
                return new Vector3[] { new Vector3(-h, h, -h), new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h) };
            case 3: // -Y (Down)
                return new Vector3[] { new Vector3(-h, -h, h), new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h) };
            case 4: // +Z (Forward)
                return new Vector3[] { new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, -h, h) };
            case 5: // -Z (Back)
                return new Vector3[] { new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(-h, h, -h), new Vector3(-h, -h, -h) };
            default:
                return new Vector3[4];
        }
    }

    /// <summary>
    /// GPU Instancing für große Voxel-Anzahl (>2000)
    /// Verwendet DrawMeshInstanced für bessere Performance
    /// </summary>
    private void BuildInstancedMesh(VoxelGridData data)
    {
        ClearMesh();

        Mesh cubeMesh = GetCubeMesh();

        // MaterialPropertyBlock für Instancing
        MaterialPropertyBlock props = new MaterialPropertyBlock();

        List<Matrix4x4> matrices = new List<Matrix4x4>();
        List<Vector4> colorData = new List<Vector4>();

        foreach (float[] voxel in data.voxels)
        {
            if (voxel == null || voxel.Length < 4) continue;

            Vector3 pos = new Vector3(voxel[0], voxel[1], voxel[2]) * voxelSize;
            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                Vector3.one * voxelSize * voxelSpacing
            );
            matrices.Add(matrix);

            Color col = densityGradient.Evaluate(Mathf.Clamp01(voxel[3]));
            colorData.Add(col);
        }

        // Unity Instancing Limit: 1023 pro Batch
        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            Matrix4x4[] batch = matrices.GetRange(i, count).ToArray();
            Graphics.DrawMeshInstanced(cubeMesh, 0, voxelMaterial, batch, count, props);
        }

        if (showDebugLogs)
        {
            int batches = Mathf.CeilToInt(matrices.Count / 1023f);
            Debug.Log($"[VoxelMesh] Instanced Mesh: {matrices.Count} Instanzen in {batches} Batches");
        }
    }

    /// <summary>
    /// Für colored=true Modus mit RGB-Daten vom Server
    /// Format: [[x, y, z, density, r, g, b], ...]
    /// </summary>
    public void BuildColoredMesh(ColoredVoxelData data)
    {
        if (data == null || data.voxels == null || data.voxels.Length == 0)
        {
            if (showDebugLogs) Debug.LogWarning("[VoxelMesh] Keine Colored-Voxel-Daten vorhanden");
            return;
        }

        ClearMesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        Mesh cubeMesh = GetCubeMesh();
        Vector3[] cubeVerts = cubeMesh.vertices;
        int[] cubeTris = cubeMesh.triangles;

        int vertOffset = 0;

        foreach (float[] voxel in data.voxels)
        {
            // Colored Format braucht 7 Werte: x, y, z, density, r, g, b
            if (voxel == null || voxel.Length < 7) continue;

            Vector3 pos = new Vector3(voxel[0], voxel[1], voxel[2]) * voxelSize;

            // RGB-Werte (0-255 vom Server → 0-1 für Unity)
            Color col = new Color(
                voxel[4] / 255f,
                voxel[5] / 255f,
                voxel[6] / 255f
            );

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
        _meshRenderer.material = voxelMaterial;

        if (showDebugLogs)
        {
            Debug.Log($"[VoxelMesh] Colored Mesh erstellt: {vertices.Count} Vertices");
        }
    }

    /// <summary>
    /// Baut Mesh direkt aus raw float-Array (alternative API)
    /// </summary>
    public void BuildFromRawArray(float[][] voxels, int[] gridSize)
    {
        VoxelGridData data = new VoxelGridData
        {
            voxels = voxels,
            grid_size = gridSize,
            stats = new VoxelStatsData { total = voxels.Length }
        };
        BuildMesh(data);
    }

    /// <summary>
    /// Baut Mesh aus JSON-String (für SSE Events)
    /// Grid: 8x8x12 (X, Y, Z) = 768 voxels (BioBERT embedding dimension)
    /// - X: horizontal position (8 columns)
    /// - Y: height (8 layers based on text density)
    /// - Z: vertical position (12 rows, top to bottom)
    /// </summary>
    public void BuildFromJSON(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            if (showDebugLogs) Debug.LogWarning("[VoxelMesh] Leerer JSON-String");
            return;
        }

        try
        {
            VoxelGridData data = JsonUtility.FromJson<VoxelGridData>(json);

            // Grid-Size aus JSON lesen (dynamisch!)
            Vector3Int gridSize = new Vector3Int(
                data.GridX,  // Default: 8
                data.GridY,  // Default: 8  
                data.GridZ   // Default: 12
            );

            if (showDebugLogs)
            {
                Debug.Log($"[VoxelMesh] BuildFromJSON: Grid {gridSize.x}x{gridSize.y}x{gridSize.z}");
            }

            BuildMesh(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VoxelMesh] JSON Parse Error: {e.Message}");
        }
    }

    /// <summary>
    /// Aktuelles Mesh leeren
    /// </summary>
    public void ClearMesh()
    {
        if (_meshFilter != null && _meshFilter.mesh != null)
        {
            _meshFilter.mesh.Clear();
        }
    }

    /// <summary>
    /// Cached Cube-Mesh holen oder erstellen
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
    /// Cube-Mesh aus Unity Primitive erstellen
    /// </summary>
    private Mesh CreateCubeMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
        mesh.name = "VoxelCube";
        DestroyImmediate(temp);
        return mesh;
    }

    /// <summary>
    /// Standard Density-Gradient erstellen
    /// Low (Blau) → Mid (Grün) → High (Rot)
    /// </summary>
    private Gradient CreateDefaultGradient()
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[3];
        colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.2f, 0.6f), 0.0f);   // Dunkelblau
        colorKeys[1] = new GradientColorKey(new Color(0.2f, 0.6f, 0.2f), 0.5f);   // Grün
        colorKeys[2] = new GradientColorKey(new Color(0.8f, 0.2f, 0.2f), 1.0f);   // Rot

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    /// <summary>
    /// Bounds des aktuellen Meshes für Kollisionserkennung
    /// </summary>
    public Bounds GetMeshBounds()
    {
        if (_meshFilter != null && _meshFilter.mesh != null)
        {
            return _meshFilter.mesh.bounds;
        }
        return new Bounds(Vector3.zero, Vector3.zero);
    }

    #region Material Selection

    public enum VoxelType
    {
        Paper,
        Rule
    }

    /// <summary>
    /// Wechselt das Material basierend auf Voxel-Typ
    /// </summary>
    public void SetMaterialType(VoxelType type)
    {
        if (_meshRenderer == null) return;

        switch (type)
        {
            case VoxelType.Paper:
                _meshRenderer.material = paperMaterial != null ? paperMaterial : voxelMaterial;
                break;
            case VoxelType.Rule:
                _meshRenderer.material = voxelMaterial;
                break;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[VoxelMesh] Material gewechselt zu: {type}");
        }
    }

    /// <summary>
    /// Paper Material verwenden
    /// </summary>
    public void UsePaperMaterial()
    {
        SetMaterialType(VoxelType.Paper);
    }

    /// <summary>
    /// Rule/Voxel Material verwenden
    /// </summary>
    public void UseRuleMaterial()
    {
        SetMaterialType(VoxelType.Rule);
    }

    #endregion

    #region P1: Rule Color-Coding

    [Header("Rule Color-Coding (P1)")]
    [Tooltip("Farbe für positive Match (Similarity > Threshold)")]
    public Color positiveMatchColor = new Color(0.2f, 0.9f, 0.3f);

    [Tooltip("Farbe für negative Match (Similarity < Threshold)")]
    public Color negativeMatchColor = new Color(0.9f, 0.2f, 0.2f);

    [Tooltip("Farbe für neutralen Zustand")]
    public Color neutralColor = new Color(0.5f, 0.5f, 0.5f);

    [Tooltip("Animation Dauer für Farbwechsel")]
    public float colorTransitionDuration = 0.5f;

    private Color _currentRuleColor;
    private Color _targetRuleColor;
    private float _colorTransitionProgress = 1f;
    private bool _useRuleColors = false;

    /// <summary>
    /// Setzt die Regel-basierte Farbgebung basierend auf Validation Result
    /// P1 Feature: Voxels färben sich je nach Match-Ergebnis
    /// </summary>
    public void ApplyRuleColors(float similarity, float threshold, bool animated = true)
    {
        _useRuleColors = true;

        Color targetColor;
        if (similarity >= threshold)
        {
            // Positive Match - Grün mit Intensität basierend auf Similarity
            float intensity = Mathf.InverseLerp(threshold, 1f, similarity);
            targetColor = Color.Lerp(neutralColor, positiveMatchColor, intensity);
        }
        else
        {
            // Negative Match - Rot mit Intensität basierend auf Distance
            float intensity = Mathf.InverseLerp(threshold, 0f, similarity);
            targetColor = Color.Lerp(neutralColor, negativeMatchColor, intensity);
        }

        if (animated)
        {
            _targetRuleColor = targetColor;
            _colorTransitionProgress = 0f;
        }
        else
        {
            _currentRuleColor = targetColor;
            _targetRuleColor = targetColor;
            ApplyColorToMesh(targetColor);
        }

        if (showDebugLogs)
        {
            string result = similarity >= threshold ? "MATCH" : "NO MATCH";
            Debug.Log($"[VoxelMesh] Rule Colors: {result} (sim={similarity:F2}, thresh={threshold:F2})");
        }
    }

    /// <summary>
    /// Setzt explizit eine Regel-Farbe (für manuelle Steuerung)
    /// </summary>
    public void SetRuleColor(Color color, bool animated = true)
    {
        _useRuleColors = true;

        if (animated)
        {
            _targetRuleColor = color;
            _colorTransitionProgress = 0f;
        }
        else
        {
            _currentRuleColor = color;
            _targetRuleColor = color;
            ApplyColorToMesh(color);
        }
    }

    /// <summary>
    /// Regel-Farben deaktivieren und zu Density-Gradient zurückkehren
    /// </summary>
    public void ResetRuleColors()
    {
        _useRuleColors = false;
        // Mesh neu bauen mit original Gradient-Farben (erfordert VoxelData)
        if (showDebugLogs)
        {
            Debug.Log("[VoxelMesh] Rule Colors deaktiviert");
        }
    }

    /// <summary>
    /// Wendet eine Farbe auf alle Vertices des Meshes an
    /// </summary>
    private void ApplyColorToMesh(Color color)
    {
        if (_meshFilter == null || _meshFilter.mesh == null) return;

        Mesh mesh = _meshFilter.mesh;
        Color[] colors = new Color[mesh.vertexCount];

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = color;
        }

        mesh.SetColors(colors);
    }

    /// <summary>
    /// Blend zwischen aktueller und Ziel-Farbe (für Animation)
    /// </summary>
    private void UpdateColorTransition()
    {
        if (!_useRuleColors || _colorTransitionProgress >= 1f) return;

        _colorTransitionProgress += Time.deltaTime / colorTransitionDuration;
        _colorTransitionProgress = Mathf.Clamp01(_colorTransitionProgress);

        _currentRuleColor = Color.Lerp(_currentRuleColor, _targetRuleColor, _colorTransitionProgress);
        ApplyColorToMesh(_currentRuleColor);
    }

    void Update()
    {
        UpdateColorTransition();
    }

    #endregion

    #region P1: Paper Selection/Interaction

    /// <summary>
    /// Raycast für Voxel-Selektion (wird von externem Controller aufgerufen)
    /// </summary>
    public bool TrySelectVoxel(Ray ray, out Vector3 hitPosition, out int voxelIndex)
    {
        hitPosition = Vector3.zero;
        voxelIndex = -1;

        if (_meshFilter == null || _meshFilter.mesh == null) return false;

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = _meshFilter.mesh;
        }

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit) && hit.collider == collider)
        {
            hitPosition = hit.point;
            // Voxel-Index aus Position berechnen
            Vector3 localPos = transform.InverseTransformPoint(hit.point);
            voxelIndex = PositionToVoxelIndex(localPos);
            return true;
        }

        return false;
    }

    private int PositionToVoxelIndex(Vector3 localPos)
    {
        int x = Mathf.FloorToInt(localPos.x / voxelSize);
        int y = Mathf.FloorToInt(localPos.y / voxelSize);
        int z = Mathf.FloorToInt(localPos.z / voxelSize);

        // Index = x + y * gridX + z * gridX * gridY
        // Vereinfacht für 8x8x12 Grid
        return x + y * 8 + z * 64;
    }

    #endregion

    void OnDestroy()
    {
        // Gecachtes Mesh aufräumen
        if (_cachedCubeMesh != null)
        {
            Destroy(_cachedCubeMesh);
        }
    }
}
