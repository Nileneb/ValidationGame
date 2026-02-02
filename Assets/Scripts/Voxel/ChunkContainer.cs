// Scripts/Voxel/ChunkContainer.cs
// Transparenter Container für einen Chunk mit Satz-Embedding-Figuren darin
// Der Container selbst ist groß und transparent, die Sätze sind kleine sichtbare Objekte

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ChunkContainer: Großer transparenter Container für einen Paper-Chunk (Abstract, Introduction, etc.)
/// Enthält viele kleine Embedding-Figuren für Sätze/Paragraphen.
/// </summary>
public class ChunkContainer : MonoBehaviour
{
    [Header("Chunk Info")]
    public string chunkId;
    public string sectionName;        // "abstract", "introduction", etc.
    public string textPreview;        // Erste 200 Zeichen
    
    [Header("Container Settings")]
    [Tooltip("Größe des transparenten Containers")]
    public Vector3 containerSize = new Vector3(10f, 8f, 10f);
    
    [Tooltip("Transparenz des Containers (0-1)")]
    [Range(0f, 0.5f)]
    public float containerAlpha = 0.1f;
    
    [Tooltip("Farbe des Containers basierend auf Section")]
    public Color containerColor = new Color(0.3f, 0.6f, 1f, 0.1f);
    
    [Header("Sentence Figures")]
    [Tooltip("Prefab für Satz-Embedding-Figuren")]
    public GameObject sentenceFigurePrefab;
    
    [Tooltip("Größe einer Satz-Figur")]
    public float figureSize = 0.5f;
    
    [Tooltip("Abstand zwischen Figuren")]
    public float figureSpacing = 1.5f;
    
    [Header("References")]
    public Transform figureContainer;     // Parent für alle Satz-Figuren
    public MeshRenderer containerRenderer; // Der transparente Container
    public Collider containerCollider;     // Trigger für Spieler-Kollision
    
    [Header("Runtime Data")]
    [SerializeField] private List<SentenceFigure> sentenceFigures = new List<SentenceFigure>();
    
    // Das Original-Chunk-Data
    private ChunkData _chunkData;
    private SentenceEmbedding[] _sentences;
    
    /// <summary>
    /// Initialisiert den Container mit Chunk-Daten
    /// </summary>
    public void Initialize(ChunkData chunk, SentenceEmbedding[] sentences = null)
    {
        _chunkData = chunk;
        _sentences = sentences;
        
        chunkId = $"chunk_{chunk.chunk_id}";
        sectionName = chunk.section_name ?? "unknown";
        textPreview = chunk.text_preview?.Substring(0, Mathf.Min(200, chunk.text_preview?.Length ?? 0));
        
        // Container-Farbe basierend auf Section
        containerColor = GetSectionColor(sectionName);
        containerColor.a = containerAlpha;
        
        // Renderer aktualisieren
        if (containerRenderer != null)
        {
            containerRenderer.material.color = containerColor;
        }
        
        gameObject.name = $"Chunk_{sectionName}_{chunk.chunk_id}";
        
        // Falls Satz-Embeddings vorhanden, Figuren erstellen
        if (_sentences != null && _sentences.Length > 0)
        {
            CreateSentenceFigures();
        }
        else
        {
            // Fallback: Eine Figur für das Chunk-Embedding selbst
            CreateChunkFigure();
        }
        
        Debug.Log($"ChunkContainer '{sectionName}' initialisiert: {sentenceFigures.Count} Figuren");
    }
    
    /// <summary>
    /// Erstellt Figuren für alle Satz-Embeddings
    /// </summary>
    private void CreateSentenceFigures()
    {
        if (figureContainer == null)
        {
            figureContainer = new GameObject("Figures").transform;
            figureContainer.SetParent(transform);
            figureContainer.localPosition = Vector3.zero;
        }
        
        int count = _sentences.Length;
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
        
        for (int i = 0; i < count; i++)
        {
            var sentence = _sentences[i];
            if (!sentence.IsValid) continue;
            
            // Grid-Position innerhalb des Containers
            int x = i % gridSize;
            int z = i / gridSize;
            Vector3 localPos = new Vector3(
                (x - gridSize / 2f) * figureSpacing,
                0f,
                (z - gridSize / 2f) * figureSpacing
            );
            
            // Figur erstellen
            GameObject figureObj = CreateFigureFromEmbedding(sentence.embedding, localPos);
            if (figureObj != null)
            {
                SentenceFigure figure = figureObj.AddComponent<SentenceFigure>();
                figure.Initialize(sentence, i);
                sentenceFigures.Add(figure);
            }
        }
    }
    
    /// <summary>
    /// Fallback: Erstellt eine Figur aus dem Chunk-Embedding
    /// </summary>
    private void CreateChunkFigure()
    {
        if (_chunkData.embedding == null || _chunkData.embedding.Length == 0)
        {
            Debug.LogWarning($"ChunkContainer '{sectionName}': Kein Embedding vorhanden!");
            return;
        }
        
        if (figureContainer == null)
        {
            figureContainer = new GameObject("Figures").transform;
            figureContainer.SetParent(transform);
            figureContainer.localPosition = Vector3.zero;
        }
        
        // Eine Figur für das gesamte Chunk-Embedding
        GameObject figureObj = CreateFigureFromEmbedding(_chunkData.embedding, Vector3.zero);
        if (figureObj != null)
        {
            figureObj.name = $"ChunkFigure_{sectionName}";
            // Etwas größer als Satz-Figuren
            figureObj.transform.localScale *= 2f;
        }
    }
    
    /// <summary>
    /// Erstellt eine 3D-Figur aus einem Embedding
    /// </summary>
    private GameObject CreateFigureFromEmbedding(float[] embedding, Vector3 localPosition)
    {
        if (embedding == null || embedding.Length == 0) return null;
        
        // Voxel-Positionen aus Embedding generieren
        List<Vector3> positions = EmbeddingToVoxel.ConvertToPositions(embedding, figureSize);
        
        if (positions == null || positions.Count == 0)
        {
            Debug.LogWarning($"ChunkContainer: Keine Voxel-Positionen für Embedding generiert");
            return null;
        }
        
        // Parent-Objekt für diese Figur
        GameObject figureParent = new GameObject("Figure");
        figureParent.transform.SetParent(figureContainer);
        figureParent.transform.localPosition = localPosition;
        
        // Farbe aus Container-Farbe ableiten (etwas heller)
        Color figureColor = containerColor;
        figureColor.a = 1f;  // Figuren sind nicht transparent
        figureColor = Color.Lerp(figureColor, Color.white, 0.3f);
        
        // Cubes erstellen
        foreach (var pos in positions)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(figureParent.transform);
            cube.transform.localPosition = pos * figureSize;
            cube.transform.localScale = Vector3.one * figureSize * 0.9f;
            
            // Material setzen
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = figureColor;
            }
            
            // Collider entfernen (nur Container hat Collider)
            var collider = cube.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
        
        return figureParent;
    }
    
    /// <summary>
    /// Gibt die Section-Farbe zurück
    /// </summary>
    public static Color GetSectionColor(string sectionName)
    {
        switch (sectionName?.ToLower())
        {
            case "abstract":
                return new Color(0.2f, 0.6f, 1.0f);   // Blau
            case "introduction":
                return new Color(0.3f, 0.8f, 0.3f);   // Grün
            case "methods":
            case "methodology":
                return new Color(1.0f, 0.8f, 0.2f);   // Gelb
            case "results":
                return new Color(1.0f, 0.5f, 0.2f);   // Orange
            case "discussion":
                return new Color(0.8f, 0.3f, 0.8f);   // Lila
            case "conclusion":
                return new Color(0.9f, 0.3f, 0.3f);   // Rot
            case "references":
                return new Color(0.5f, 0.5f, 0.5f);   // Grau
            default:
                return new Color(0.6f, 0.6f, 0.8f);   // Default Blaugrau
        }
    }
    
    /// <summary>
    /// Prüft alle Satz-Figuren gegen eine Rule
    /// </summary>
    public List<SentenceFigure> CheckRule(RuleData rule, float threshold = 0.7f)
    {
        List<SentenceFigure> matches = new List<SentenceFigure>();
        
        if (rule.pos_embedding == null) return matches;
        
        foreach (var figure in sentenceFigures)
        {
            if (figure.CheckMatch(rule.pos_embedding, threshold))
            {
                matches.Add(figure);
            }
        }
        
        return matches;
    }
    
    /// <summary>
    /// Hebt alle Matches hervor
    /// </summary>
    public void HighlightMatches(List<SentenceFigure> matches)
    {
        foreach (var figure in sentenceFigures)
        {
            bool isMatch = matches.Contains(figure);
            figure.SetHighlight(isMatch);
        }
    }
    
    void OnDrawGizmos()
    {
        // Container-Box anzeigen
        Gizmos.color = containerColor;
        Gizmos.DrawWireCube(transform.position, containerSize);
    }
}

/// <summary>
/// SentenceFigure: Eine einzelne Satz-Embedding-Figur innerhalb eines ChunkContainers
/// </summary>
public class SentenceFigure : MonoBehaviour
{
    [Header("Sentence Info")]
    public int sentenceIndex;
    public string textPreview;
    public float[] embedding;
    
    [Header("Match State")]
    public bool isHighlighted;
    public float lastSimilarity;
    public string matchedRuleId;
    
    private SentenceEmbedding _sentenceData;
    private Material _material;
    private Color _baseColor;
    
    public void Initialize(SentenceEmbedding sentence, int index)
    {
        _sentenceData = sentence;
        sentenceIndex = index;
        textPreview = sentence.GetPreview(100);
        embedding = sentence.embedding;
        
        // Material cachen
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            _material = renderer.material;
            _baseColor = _material.color;
        }
    }
    
    /// <summary>
    /// Prüft ob dieses Embedding mit dem Rule-Embedding matcht
    /// </summary>
    public bool CheckMatch(float[] ruleEmbedding, float threshold = 0.7f)
    {
        if (embedding == null || ruleEmbedding == null) return false;
        
        lastSimilarity = EmbeddingUtils.CosineSimilarity(embedding, ruleEmbedding);
        return lastSimilarity >= threshold;
    }
    
    /// <summary>
    /// Setzt visuelles Highlighting
    /// </summary>
    public void SetHighlight(bool highlight)
    {
        isHighlighted = highlight;
        
        if (_material != null)
        {
            if (highlight)
            {
                // Leuchtend Grün für Match
                _material.color = Color.green;
                _material.SetColor("_EmissionColor", Color.green * 0.5f);
            }
            else
            {
                _material.color = _baseColor;
                _material.SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
