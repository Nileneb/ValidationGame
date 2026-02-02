// Scripts/Rendering/ChunkInfo.cs
// Komponente für Chunk-Würfel im Paper-Molecule und Rule-Dipol
// Speichert Chunk-Daten für Hover/Click-Interaktion

using UnityEngine;

/// <summary>
/// Speichert Chunk-Informationen auf einem Würfel-GameObject
/// Wird für UI-Anzeige und Interaktion verwendet
/// Unterstützt Paper-Chunks (section_name) UND Rule-Chunks (chunk_type)
/// </summary>
public class ChunkInfo : MonoBehaviour
{
    [Header("Chunk Data")]
    public int chunkId;
    public string sectionName;    // Paper: abstract, intro, etc.
    public string chunkType;      // Rule: positive, negative
    public string textPreview;
    public Color sectionColor;

    // Volles ChunkData Objekt (für Embedding-Zugriff)
    private ChunkData _chunkData;

    /// <summary>
    /// Initialisiert mit ChunkData vom Server
    /// </summary>
    public void Initialize(ChunkData data)
    {
        if (data == null) return;

        _chunkData = data;
        chunkId = data.chunk_id;
        sectionName = data.section_name ?? "";
        chunkType = data.chunk_type ?? "";
        textPreview = data.text_preview ?? "";
        sectionColor = data.GetUnityColor();
    }

    /// <summary>
    /// Ist dies ein Rule-Dipol-Chunk?
    /// </summary>
    public bool IsRuleChunk => !string.IsNullOrEmpty(chunkType);

    /// <summary>
    /// Ist dies ein positiver Chunk (für Rule-Dipole)?
    /// </summary>
    public bool IsPositive => chunkType?.ToLower() == "positive";

    /// <summary>
    /// Ist dies ein negativer Chunk (für Rule-Dipole)?
    /// </summary>
    public bool IsNegative => chunkType?.ToLower() == "negative";

    /// <summary>
    /// Gibt das Embedding des Chunks zurück (für Matching)
    /// </summary>
    public float[] GetEmbedding()
    {
        return _chunkData?.embedding;
    }

    /// <summary>
    /// Formatierter Text für UI-Anzeige
    /// </summary>
    public string GetDisplayText()
    {
        string preview = textPreview.Length > 100
            ? textPreview.Substring(0, 100) + "..."
            : textPreview;

        // Unterschiedliche Anzeige für Papers vs Rules
        if (IsRuleChunk)
        {
            string type = IsPositive ? "POSITIV" : "NEGATIV";
            return $"[{type}]\n{preview}";
        }
        else
        {
            return $"[{sectionName.ToUpper()}]\n{preview}";
        }
    }

    /// <summary>
    /// Section-Name auf Deutsch (für UI)
    /// </summary>
    public string GetGermanName()
    {
        // Rule-Chunks
        if (IsRuleChunk)
        {
            return IsPositive ? "Positiv (Suchwörter)" : "Negativ (Ausschlusswörter)";
        }

        // Paper-Sections
        switch (sectionName?.ToLower())
        {
            case "abstract": return "Zusammenfassung";
            case "intro":
            case "introduction": return "Einleitung";
            case "methods":
            case "methodology": return "Methoden";
            case "results": return "Ergebnisse";
            case "discussion": return "Diskussion";
            case "conclusion": return "Fazit";
            case "references": return "Referenzen";
            default: return sectionName ?? "Unbekannt";
        }
    }

    /// <summary>
    /// Visuelle Hervorhebung wenn ausgewählt/hover
    /// </summary>
    public void SetHighlight(bool highlighted)
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null) return;

        if (highlighted)
        {
            // Heller machen + pulsieren für Rule-Chunks
            float intensity = IsRuleChunk ? 2.0f : 1.5f;
            renderer.material.color = sectionColor * intensity;
            transform.localScale *= 1.1f;
        }
        else
        {
            // Zurücksetzen
            renderer.material.color = sectionColor;
            transform.localScale /= 1.1f;
        }
    }
}
