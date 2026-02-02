// Scripts/Scenes/RulezSceneManager.cs
// Manager für die Rulez-Szene: Zeigt alle aktiven Rules als Embedding-Figuren an
// Keine Spielmechanik - reine Visualisierung für Debug/Preview

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// RulezSceneManager: Verwaltet die Rule-Visualisierungs-Szene
/// 
/// Zeigt alle aktiven Rules als 3D-Figuren an:
/// - Dipol-Rules: Zwei Figuren (pos + neg) mit Verbindung
/// - Einfache Rules: Eine Figur aus pos_embedding
/// 
/// Kein Gameplay - nur Anzeige!
/// </summary>
public class RulezSceneManager : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("Server URL für Rule-Daten")]
    public string serverUrl = "http://127.0.0.1:8089";

    [Tooltip("Automatisches Polling alle X Sekunden (0 = nur initial)")]
    public float pollInterval = 30f;

    [Header("Layout Settings")]
    [Tooltip("Abstand zwischen Rules")]
    public float ruleSpacing = 15f;

    [Tooltip("Spalten im Grid")]
    public int gridColumns = 3;

    [Tooltip("Startposition")]
    public Vector3 gridOrigin = Vector3.zero;

    [Header("Figure Settings")]
    [Tooltip("Größe der Voxel-Cubes")]
    public float voxelSize = 0.5f;

    [Tooltip("Threshold für Voxel-Sichtbarkeit (-1 = automatisch ~100 Cubes)")]
    public float voxelThreshold = -1f;  // -1 = DYNAMISCH!

    [Tooltip("Material für positive Embeddings (grün)")]
    public Material positiveMaterial;

    [Tooltip("Material für negative Embeddings (rot)")]
    public Material negativeMaterial;

    [Header("References")]
    public Transform rulesContainer;

    [Header("UI")]
    public TMPro.TextMeshProUGUI statusText;
    public TMPro.TextMeshProUGUI ruleCountText;

    [Header("Runtime")]
    [SerializeField] private List<RuleVisualization> ruleVisualizations = new List<RuleVisualization>();
    [SerializeField] private bool isLoading;

    private ApiClient _apiClient;
    private Coroutine _pollCoroutine;

    void Start()
    {
        // Container erstellen falls nicht vorhanden
        if (rulesContainer == null)
        {
            GameObject container = new GameObject("RulesContainer");
            rulesContainer = container.transform;
        }

        // ApiClient holen oder erstellen
        _apiClient = FindFirstObjectByType<ApiClient>();
        if (_apiClient == null)
        {
            GameObject apiObj = new GameObject("ApiClient");
            _apiClient = apiObj.AddComponent<ApiClient>();
            _apiClient.ServerUrl = serverUrl;
        }
        else
        {
            // Existierenden ApiClient nutzen, aber serverUrl überschreiben falls gesetzt
            if (!string.IsNullOrEmpty(serverUrl))
            {
                _apiClient.ServerUrl = serverUrl;
            }
        }

        // Standard-Materialien erstellen falls nicht zugewiesen
        CreateDefaultMaterials();

        // Rules laden
        StartCoroutine(LoadRulesInitial());

        // Polling starten falls aktiviert
        if (pollInterval > 0)
        {
            _pollCoroutine = StartCoroutine(PollRulesLoop());
        }

        UpdateStatusText("Initialisierung...");
    }

    void OnDestroy()
    {
        if (_pollCoroutine != null)
        {
            StopCoroutine(_pollCoroutine);
        }
    }

    /// <summary>
    /// Erstellt Standard-Materialien
    /// </summary>
    private void CreateDefaultMaterials()
    {
        if (positiveMaterial == null)
        {
            positiveMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            positiveMaterial.color = new Color(0.2f, 0.8f, 0.2f);  // Grün
            positiveMaterial.SetFloat("_Smoothness", 0.5f);
        }

        if (negativeMaterial == null)
        {
            negativeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            negativeMaterial.color = new Color(0.8f, 0.2f, 0.2f);  // Rot
            negativeMaterial.SetFloat("_Smoothness", 0.5f);
        }
    }

    /// <summary>
    /// Lädt alle Rules initial
    /// </summary>
    private IEnumerator LoadRulesInitial()
    {
        isLoading = true;
        UpdateStatusText("Lade Rules vom Server...");

        yield return StartCoroutine(LoadAndDisplayRules());

        isLoading = false;
    }

    /// <summary>
    /// Polling-Loop für automatische Updates
    /// </summary>
    private IEnumerator PollRulesLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollInterval);

            if (!isLoading)
            {
                yield return StartCoroutine(LoadAndDisplayRules());
            }
        }
    }

    /// <summary>
    /// Lädt und zeigt Rules an
    /// </summary>
    private IEnumerator LoadAndDisplayRules()
    {
        // Rules vom Server abrufen
        yield return _apiClient.FetchRulesCoroutine();

        List<RuleData> rules = _apiClient.GetCachedRules();

        if (rules == null || rules.Count == 0)
        {
            UpdateStatusText("Keine Rules gefunden!");
            UpdateRuleCount(0);
            yield break;
        }

        Debug.Log($"RulezSceneManager: {rules.Count} Rules erhalten");

        // Bestehende Visualisierungen löschen
        ClearVisualizations();

        // Neue Visualisierungen erstellen
        int col = 0;
        int row = 0;

        foreach (var rule in rules)
        {
            if (!rule.is_active)
            {
                Debug.Log($"Rule '{rule.rule_id}' ist nicht aktiv, überspringe.");
                continue;
            }

            // Position im Grid
            Vector3 position = gridOrigin + new Vector3(
                col * ruleSpacing,
                0,
                row * ruleSpacing
            );

            // Visualisierung erstellen
            RuleVisualization viz = CreateRuleVisualization(rule, position);
            if (viz != null)
            {
                ruleVisualizations.Add(viz);
            }

            // Grid-Position weiter
            col++;
            if (col >= gridColumns)
            {
                col = 0;
                row++;
            }

            // Frame-Pause für Performance
            yield return null;
        }

        UpdateStatusText($"Rules geladen!");
        UpdateRuleCount(ruleVisualizations.Count);
    }

    /// <summary>
    /// Erstellt die Visualisierung für eine Rule
    /// </summary>
    private RuleVisualization CreateRuleVisualization(RuleData rule, Vector3 position)
    {
        // Embeddings dekodieren falls nötig
        rule.DecodeData();

        // Parent-Objekt für diese Rule
        GameObject ruleObj = new GameObject($"Rule_{rule.rule_id}");
        ruleObj.transform.SetParent(rulesContainer);
        ruleObj.transform.position = position;

        RuleVisualization viz = ruleObj.AddComponent<RuleVisualization>();
        viz.ruleId = rule.rule_id;
        viz.question = rule.question;
        viz.threshold = rule.threshold;

        // Dipol-Visualisierung (2 Chunks)?
        if (rule.IsDipole && rule.chunks.Length >= 2)
        {
            CreateDipoleVisualization(viz, rule);
        }
        // Standard-Visualisierung (pos_embedding)?
        else if (rule.pos_embedding != null && rule.pos_embedding.Length > 0)
        {
            CreateStandardVisualization(viz, rule);
        }
        else
        {
            Debug.LogWarning($"Rule '{rule.rule_id}' hat keine visualisierbaren Embeddings!");
            Destroy(ruleObj);
            return null;
        }

        // Label erstellen
        CreateRuleLabel(viz, rule);

        return viz;
    }

    /// <summary>
    /// Erstellt eine Dipol-Visualisierung (pos + neg Chunks)
    /// </summary>
    private void CreateDipoleVisualization(RuleVisualization viz, RuleData rule)
    {
        var posChunk = rule.chunks[0];  // Positiver Chunk
        var negChunk = rule.chunks.Length > 1 ? rule.chunks[1] : null;  // Negativer Chunk

        float separation = 4f;  // Abstand zwischen pos und neg

        // Positive Figur (links)
        if (posChunk?.embedding != null)
        {
            Vector3 posOffset = new Vector3(-separation / 2, 0, 0);
            viz.positiveFigure = EmbeddingToVoxel.CreateStructure(
                posChunk.embedding,
                viz.transform,
                voxelSize,
                voxelThreshold,
                Color.green,
                positiveMaterial,
                rule.rule_id + "_pos"  // HYBRID: rule_id für einzigartige Form!
            );
            if (viz.positiveFigure != null)
            {
                viz.positiveFigure.name = "Positive";
                viz.positiveFigure.transform.localPosition = posOffset;
            }
        }

        // Negative Figur (rechts)
        if (negChunk?.embedding != null)
        {
            Vector3 negOffset = new Vector3(separation / 2, 0, 0);
            viz.negativeFigure = EmbeddingToVoxel.CreateStructure(
                negChunk.embedding,
                viz.transform,
                voxelSize,
                voxelThreshold,
                Color.red,
                negativeMaterial,
                rule.rule_id + "_neg"  // HYBRID: rule_id für einzigartige Form!
            );
            if (viz.negativeFigure != null)
            {
                viz.negativeFigure.name = "Negative";
                viz.negativeFigure.transform.localPosition = negOffset;
            }
        }

        // Verbindungslinie zwischen pos und neg
        if (viz.positiveFigure != null && viz.negativeFigure != null)
        {
            CreateDipoleLine(viz);
        }

        viz.isDipole = true;
        Debug.Log($"Dipol-Visualisierung erstellt für Rule '{rule.rule_id}'");
    }

    /// <summary>
    /// Erstellt eine Standard-Visualisierung (nur pos_embedding)
    /// </summary>
    private void CreateStandardVisualization(RuleVisualization viz, RuleData rule)
    {
        // HYBRID ALGORITHMUS: rule_id bestimmt Grundform, Embedding gibt Variation!
        viz.positiveFigure = EmbeddingToVoxel.CreateStructure(
            rule.pos_embedding,
            viz.transform,
            voxelSize,
            voxelThreshold,
            Color.cyan,
            positiveMaterial,
            rule.rule_id  // WICHTIG: rule_id für garantiert einzigartige Formen!
        );

        if (viz.positiveFigure != null)
        {
            viz.positiveFigure.name = "RuleFigure";
        }

        viz.isDipole = false;
        Debug.Log($"Standard-Visualisierung erstellt für Rule '{rule.rule_id}'");
    }

    /// <summary>
    /// Erstellt eine Linie zwischen pos und neg
    /// </summary>
    private void CreateDipoleLine(RuleVisualization viz)
    {
        GameObject lineObj = new GameObject("DipoleLine");
        lineObj.transform.SetParent(viz.transform);

        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, viz.positiveFigure.transform.localPosition);
        line.SetPosition(1, viz.negativeFigure.transform.localPosition);
        line.startWidth = 0.1f;
        line.endWidth = 0.1f;
        line.startColor = Color.green;
        line.endColor = Color.red;
        line.material = new Material(Shader.Find("Sprites/Default"));

        viz.dipoleLine = line;
    }

    /// <summary>
    /// Erstellt ein Text-Label für die Rule
    /// </summary>
    private void CreateRuleLabel(RuleVisualization viz, RuleData rule)
    {
        // 3D Text über der Figur
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(viz.transform);
        labelObj.transform.localPosition = new Vector3(0, 6, 0);

        TMPro.TextMeshPro label = labelObj.AddComponent<TMPro.TextMeshPro>();
        label.text = $"<b>{rule.rule_id}</b>\n<size=70%>{TruncateText(rule.question, 50)}</size>";
        label.fontSize = 2;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = Color.white;

        viz.label = label;
    }

    /// <summary>
    /// Löscht alle bestehenden Visualisierungen
    /// </summary>
    private void ClearVisualizations()
    {
        foreach (var viz in ruleVisualizations)
        {
            if (viz != null && viz.gameObject != null)
            {
                Destroy(viz.gameObject);
            }
        }
        ruleVisualizations.Clear();
    }

    /// <summary>
    /// Aktualisiert den Status-Text
    /// </summary>
    private void UpdateStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
        Debug.Log($"RulezSceneManager: {text}");
    }

    /// <summary>
    /// Aktualisiert die Rule-Anzahl
    /// </summary>
    private void UpdateRuleCount(int count)
    {
        if (ruleCountText != null)
        {
            ruleCountText.text = $"Rules: {count}";
        }
    }

    /// <summary>
    /// Kürzt Text auf maximale Länge
    /// </summary>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
    }

    // === Public Methods ===

    /// <summary>
    /// Manuelles Neuladen
    /// </summary>
    public void RefreshRules()
    {
        if (!isLoading)
        {
            StartCoroutine(LoadAndDisplayRules());
        }
    }

    /// <summary>
    /// Fokussiert auf eine bestimmte Rule
    /// </summary>
    public void FocusRule(string ruleId)
    {
        foreach (var viz in ruleVisualizations)
        {
            if (viz.ruleId == ruleId)
            {
                // Kamera bewegen (falls vorhanden)
                Camera.main.transform.position = viz.transform.position + new Vector3(0, 5, -10);
                Camera.main.transform.LookAt(viz.transform);

                viz.SetHighlight(true);
            }
            else
            {
                viz.SetHighlight(false);
            }
        }
    }
}

/// <summary>
/// Komponente für eine einzelne Rule-Visualisierung
/// </summary>
public class RuleVisualization : MonoBehaviour
{
    [Header("Rule Info")]
    public string ruleId;
    public string question;
    public float threshold;
    public bool isDipole;

    [Header("Figures")]
    public GameObject positiveFigure;
    public GameObject negativeFigure;
    public LineRenderer dipoleLine;
    public TMPro.TextMeshPro label;

    [Header("State")]
    public bool isHighlighted;

    /// <summary>
    /// Setzt Highlight-Status
    /// </summary>
    public void SetHighlight(bool highlight)
    {
        isHighlighted = highlight;

        // Scale-Animation
        float scale = highlight ? 1.2f : 1f;
        transform.localScale = Vector3.one * scale;

        // Label-Farbe
        if (label != null)
        {
            label.color = highlight ? Color.yellow : Color.white;
        }
    }

    void OnDrawGizmos()
    {
        // Bounding Box
        Gizmos.color = isHighlighted ? Color.yellow : Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 8f);
    }
}
