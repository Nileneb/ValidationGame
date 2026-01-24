// Scripts/Voxel/RulePreview.cs
// Zeigt die aktuelle Regel als 3D Voxel-Figur in einem UI-Bereich an
// Nutzt RenderTexture für Canvas-Anzeige

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RulePreview : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Material voxelMaterial;

    [Header("Preview Settings")]
    [SerializeField] private float cubeSize = 0.3f;
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private float voxelThreshold = 0.3f;

    [Header("Render Settings")]
    [SerializeField] private RawImage targetImage;
    [SerializeField] private int renderTextureSize = 256;
    [SerializeField] private Vector3 previewPosition = new Vector3(100f, 100f, 100f);

    private GameObject previewContainer;
    private Camera previewCamera;
    private RenderTexture renderTexture;
    private string currentRuleId;
    private List<GameObject> voxelCubes = new List<GameObject>();

    void Start()
    {
        SetupPreviewCamera();
    }

    void SetupPreviewCamera()
    {
        // Container für Preview-Objekte (weit weg von der Spielwelt)
        previewContainer = new GameObject("RulePreviewContainer");
        previewContainer.transform.position = previewPosition;

        // Preview-Kamera erstellen
        GameObject camObj = new GameObject("PreviewCamera");
        camObj.transform.SetParent(previewContainer.transform);
        camObj.transform.localPosition = new Vector3(0, 2, -5);
        camObj.transform.LookAt(previewContainer.transform.position);

        previewCamera = camObj.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        previewCamera.cullingMask = LayerMask.GetMask("Default"); // Nur Default Layer
        previewCamera.fieldOfView = 40f;

        // RenderTexture erstellen
        renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16);
        previewCamera.targetTexture = renderTexture;

        // RawImage zuweisen falls vorhanden
        if (targetImage != null)
        {
            targetImage.texture = renderTexture;
        }
    }

    void Update()
    {
        // Preview-Container rotieren
        if (previewContainer != null && voxelCubes.Count > 0)
        {
            previewContainer.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Zeigt eine Regel als Voxel-Preview an
    /// </summary>
    public void ShowRule(string ruleId, float[] posEmbedding)
    {
        // Gleiche Regel? Nicht neu generieren
        if (ruleId == currentRuleId && voxelCubes.Count > 0) return;

        // Alte Preview löschen
        ClearPreview();

        currentRuleId = ruleId;

        // Embedding zu Voxel konvertieren
        List<VoxelPosition> positions = EmbeddingToVoxel.ConvertToPositions(posEmbedding, voxelThreshold);
        positions = EmbeddingToVoxel.CenterPositions(positions);

        // Farbe aus Embedding
        Color color = EmbeddingToVoxel.GetColorFromEmbedding(posEmbedding);

        // Material erstellen
        Material mat = null;
        if (voxelMaterial != null)
        {
            mat = new Material(voxelMaterial);
            mat.color = color;
        }

        // Voxel-Cubes erstellen
        foreach (var pos in positions)
        {
            GameObject cube;

            if (cubePrefab != null)
            {
                // PREFAB NUTZEN!
                cube = Instantiate(cubePrefab, previewContainer.transform);
            }
            else
            {
                // Fallback: Primitive
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(previewContainer.transform);
                Destroy(cube.GetComponent<Collider>());
            }

            cube.name = "PreviewVoxel";
            cube.transform.localPosition = new Vector3(
                pos.x * cubeSize,
                pos.y * cubeSize,
                pos.z * cubeSize
            );
            cube.transform.localScale = Vector3.one * cubeSize * 0.9f;

            // Material zuweisen
            Renderer rend = cube.GetComponent<Renderer>();
            if (rend != null)
            {
                if (mat != null)
                {
                    rend.material = mat;
                }
                else
                {
                    rend.material.color = color;
                }
            }

            voxelCubes.Add(cube);
        }

        Debug.Log($"RulePreview: Regel '{ruleId}' mit {positions.Count} Voxeln angezeigt");
    }

    /// <summary>
    /// Zeigt Regel aus Base64-enkodiertem Embedding
    /// </summary>
    public void ShowRuleFromBase64(string ruleId, string base64Embedding)
    {
        if (string.IsNullOrEmpty(base64Embedding)) return;

        try
        {
            byte[] bytes = System.Convert.FromBase64String(base64Embedding);
            float[] embedding = new float[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
            ShowRule(ruleId, embedding);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RulePreview: Base64 decode error: {e.Message}");
        }
    }

    /// <summary>
    /// Löscht die aktuelle Preview
    /// </summary>
    public void ClearPreview()
    {
        foreach (var cube in voxelCubes)
        {
            if (cube != null) Destroy(cube);
        }
        voxelCubes.Clear();
        currentRuleId = null;
    }

    /// <summary>
    /// Setzt das RawImage für die Canvas-Anzeige
    /// </summary>
    public void SetTargetImage(RawImage image)
    {
        targetImage = image;
        if (renderTexture != null)
        {
            targetImage.texture = renderTexture;
        }
    }

    void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }
}
