// Assets/Scripts/UI/RuleReferenceUI.cs
// Zeigt die aktive Rule als Referenz im UI
// Spieler sieht Pos/Neg Voxel-Shapes während er läuft

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ValidationGame.Data;

public class RuleReferenceUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_Text thresholdText;
    
    [Header("3D Preview Containers")]
    [SerializeField] private Transform positivePreviewContainer;
    [SerializeField] private Transform negativePreviewContainer;
    
    [Header("Preview Settings")]
    [SerializeField] private float previewScale = 0.1f;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private Material positiveMaterial;
    [SerializeField] private Material negativeMaterial;
    
    [Header("Labels")]
    [SerializeField] private TMP_Text positiveLabel;
    [SerializeField] private TMP_Text negativeLabel;

    [Header("References")]
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private VoxelStructureSpawner voxelSpawner;

    private Molecule currentRule;
    private GameObject positivePreview;
    private GameObject negativePreview;

    void Start()
    {
        if (apiClient != null)
        {
            apiClient.OnActiveRuleLoaded += OnRuleLoaded;
        }

        // Initial: UI verstecken
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (apiClient != null)
        {
            apiClient.OnActiveRuleLoaded -= OnRuleLoaded;
        }
    }

    /// <summary>
    /// Wird aufgerufen wenn aktive Rule vom Server geladen wurde
    /// </summary>
    private void OnRuleLoaded(Molecule rule)
    {
        DisplayRule(rule);
    }

    /// <summary>
    /// Zeigt eine Rule im UI an
    /// </summary>
    public void DisplayRule(Molecule rule, float threshold = 0.7f, string question = null)
    {
        if (rule == null)
        {
            SetVisible(false);
            return;
        }

        currentRule = rule;
        rule.DecodeAll();

        // Question Text
        if (questionText != null)
        {
            questionText.text = question ?? rule.title ?? "Is this relevant?";
        }

        // Threshold Text
        if (thresholdText != null)
        {
            thresholdText.text = $"Threshold: {threshold:P0}";
        }

        // Labels
        if (positiveLabel != null) positiveLabel.text = "COLLECT ✓";
        if (negativeLabel != null) negativeLabel.text = "SKIP ✗";

        // 3D Previews erstellen
        CreatePreviews(rule);

        SetVisible(true);
    }

    /// <summary>
    /// Erstellt die 3D Voxel-Previews für Pos/Neg Chunks
    /// </summary>
    private void CreatePreviews(Molecule rule)
    {
        // Alte Previews löschen
        ClearPreviews();

        var posChunk = rule.GetPositiveChunk();
        var negChunk = rule.GetNegativeChunk();

        // Positive Preview (grün)
        if (posChunk != null && positivePreviewContainer != null)
        {
            positivePreview = CreateChunkPreview(posChunk, positivePreviewContainer, 
                positiveMaterial ?? CreateMaterial(Chunk.GetDefaultColor(ChunkType.Positive).ToUnityColor()));
        }

        // Negative Preview (rot)
        if (negChunk != null && negativePreviewContainer != null)
        {
            negativePreview = CreateChunkPreview(negChunk, negativePreviewContainer,
                negativeMaterial ?? CreateMaterial(Chunk.GetDefaultColor(ChunkType.Negative).ToUnityColor()));
        }
    }

    /// <summary>
    /// Erstellt einen einzelnen Chunk-Preview
    /// </summary>
    private GameObject CreateChunkPreview(Chunk chunk, Transform container, Material material)
    {
        if (chunk.voxelGrid == null || chunk.voxelGrid.voxelCount == 0)
        {
            chunk.Decode();
        }

        GameObject preview = new GameObject($"Preview_{chunk.chunk_type}");
        preview.transform.SetParent(container, false);
        preview.transform.localPosition = Vector3.zero;
        preview.transform.localScale = Vector3.one * previewScale;

        // Voxels erstellen
        foreach (var voxel in chunk.voxelGrid.voxels)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(preview.transform, false);
            
            // Position zentrieren (Grid ist 8x8x12)
            cube.transform.localPosition = new Vector3(
                voxel.x - VoxelGrid.SIZE_X / 2f,
                voxel.y - VoxelGrid.SIZE_Y / 2f,
                voxel.z - VoxelGrid.SIZE_Z / 2f
            );
            cube.transform.localScale = Vector3.one * 0.9f;  // Kleiner Gap

            // Farbe mit Intensität
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                Material mat = new Material(material);
                Color baseColor = material.color;
                mat.color = new Color(
                    baseColor.r * voxel.value,
                    baseColor.g * voxel.value,
                    baseColor.b * voxel.value,
                    1f
                );
                renderer.material = mat;
            }

            // Collider entfernen (Performance)
            var collider = cube.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        // Auto-Rotation Component
        preview.AddComponent<RotatePreview>().rotationSpeed = rotationSpeed;

        return preview;
    }

    private Material CreateMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }

    private void ClearPreviews()
    {
        if (positivePreview != null)
        {
            Destroy(positivePreview);
            positivePreview = null;
        }
        if (negativePreview != null)
        {
            Destroy(negativePreview);
            negativePreview = null;
        }
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Lädt aktive Rule vom Server
    /// </summary>
    public void LoadActiveRule()
    {
        if (apiClient != null)
        {
            StartCoroutine(apiClient.FetchActiveRule((rule, threshold, question) =>
            {
                if (rule != null)
                {
                    DisplayRule(rule, threshold, question);
                }
            }));
        }
    }
}

/// <summary>
/// Einfache Auto-Rotation für Preview-Objekte
/// </summary>
public class RotatePreview : MonoBehaviour
{
    public float rotationSpeed = 30f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }
}
