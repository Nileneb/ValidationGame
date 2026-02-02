// Assets/Scripts/Core/RuleMatcher.cs
// STARK VEREINFACHT nach DATAMODEL.md
// Kein Cosine Similarity mehr client-side!
// Server macht die Validierung - Client sendet nur Collect/Skip

using UnityEngine;
using ValidationGame.Data;

/// <summary>
/// Spieler-Aktionen die an den Server gesendet werden
/// </summary>
public enum PlayerAction
{
    Collect,  // Spieler hat eingesammelt (implizites JA - "sieht aus wie Referenz")
    Skip      // Spieler hat ignoriert (implizites NEIN)
}

/// <summary>
/// Job Response die an den Server gesendet wird
/// </summary>
[System.Serializable]
public class JobResponse
{
    public string job_id;
    public string device_id;
    public string action;         // "collect" oder "skip"
    public int response_time_ms;  // Reaktionszeit in Millisekunden
}

/// <summary>
/// RuleMatcher - VEREINFACHT
/// 
/// ALTE LOGIK (entfernt):
/// - Kein Cosine Similarity client-side
/// - Kein lokales Matching
/// 
/// NEUE LOGIK:
/// - Spieler sieht Paper-Shape und Rule-Referenz
/// - Spieler entscheidet visuell: "Sieht das aus wie die grüne Referenz?"
/// - Collect = JA, Skip = NEIN
/// - Server aggregiert und validiert
/// </summary>
public class RuleMatcher : MonoBehaviour
{
    [Header("Feedback Settings")]
    [SerializeField] private int collectPoints = 10;
    [SerializeField] private int skipPoints = 5;

    /// <summary>
    /// Aktive Rule (vom Server geladen)
    /// </summary>
    public Molecule ActiveRule { get; private set; }
    public float Threshold { get; private set; } = 0.7f;
    public string Question { get; private set; } = "";

    /// <summary>
    /// Setzt die aktive Rule (von ApiClient geladen)
    /// </summary>
    public void SetActiveRule(Molecule rule, float threshold, string question)
    {
        ActiveRule = rule;
        Threshold = threshold;
        Question = question;

        if (rule != null)
        {
            Debug.Log($"RuleMatcher: Active Rule gesetzt - '{question}' (Threshold: {threshold})");
        }
    }

    /// <summary>
    /// Erstellt eine JobResponse für den Server
    /// </summary>
    public JobResponse CreateResponse(string jobId, string deviceId, PlayerAction action, int responseTimeMs)
    {
        return new JobResponse
        {
            job_id = jobId,
            device_id = deviceId,
            action = action == PlayerAction.Collect ? "collect" : "skip",
            response_time_ms = responseTimeMs
        };
    }

    /// <summary>
    /// Gibt Feedback-Text für UI
    /// </summary>
    public string GetFeedbackText(PlayerAction action)
    {
        return action == PlayerAction.Collect
            ? $"Eingesammelt! (+{collectPoints})"
            : $"Übersprungen (+{skipPoints})";
    }

    /// <summary>
    /// Gibt Punkte für Aktion (lokales Feedback, finale Punkte kommen vom Server)
    /// </summary>
    public int GetLocalPoints(PlayerAction action)
    {
        return action == PlayerAction.Collect ? collectPoints : skipPoints;
    }

    // ============================================================
    // LEGACY SUPPORT - Für Kompatibilität mit GameManager
    // ============================================================
    
    /// <summary>
    /// Match-Ergebnis Struktur (für lokales Feedback)
    /// </summary>
    [System.Serializable]
    public class MatchResult
    {
        public bool is_match;
        public float similarity;
        public int points;
        public string feedback;
    }

    /// <summary>
    /// Prüft Match zwischen Paper-Embedding und Rule
    /// VEREINFACHT: Gibt immer positives Ergebnis, Server validiert später
    /// </summary>
    public MatchResult CheckMatch(VoxelData paperData, RuleData activeRule)
    {
        // Lokales Feedback - Server macht finale Validierung
        return new MatchResult
        {
            is_match = true,  // Optimistisch - Server korrigiert
            similarity = 0.75f,
            points = collectPoints,
            feedback = $"Eingesammelt! (+{collectPoints})"
        };
    }

    /// <summary>
    /// Prüft Match mit raw Embedding
    /// </summary>
    public MatchResult CheckMatch(float[] embedding, RuleData activeRule)
    {
        return new MatchResult
        {
            is_match = true,
            similarity = 0.75f,
            points = collectPoints,
            feedback = $"Eingesammelt! (+{collectPoints})"
        };
    }

    // ============================================================
    // ENTFERNT - Server macht das jetzt
    // ============================================================
    
    // CosineSimilarity() - ENTFERNT
    // Kein lokales Embedding-Matching mehr!
}
