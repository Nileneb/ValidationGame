// Scripts/Game/PlayerMatcher.cs
// RuleMatcher-Logik auf dem Player - vergleicht bei Kontakt mit Papers
// Ersetzt die Logik aus dem alten RuleMatcher auf dem GameManager

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerMatcher: Komponente auf dem Player-Prefab
/// Bei Kollision mit CollectiblePaper wird das Matching durchgeführt.
/// </summary>
public class PlayerMatcher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int matchPoints = 100;
    [SerializeField] private int missPoints = 10;

    [Header("Hybrid Matching")]
    [Tooltip("Keyword-Gewicht (0.7 = Keywords zählen 70%, Embedding 30%)")]
    [SerializeField] private float keywordWeight = 0.7f;

    [Tooltip("Minimum Delta (pos-neg) für reines Embedding-Matching")]
    [SerializeField] private float minEmbeddingDelta = 0.015f;

    [Header("Visual Feedback")]
    [Tooltip("Particle System für Match-Effekt")]
    [SerializeField] private ParticleSystem matchParticles;

    [Tooltip("Particle System für Miss-Effekt")]
    [SerializeField] private ParticleSystem missParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip matchSound;
    [SerializeField] private AudioClip missSound;

    [Header("Statistics")]
    [SerializeField] private int totalCollected = 0;
    [SerializeField] private int totalMatches = 0;
    [SerializeField] private int totalPoints = 0;

    // Alle geladenen Rules
    private List<RuleData> allRules = new List<RuleData>();
    private HashSet<string> warnedRules = new HashSet<string>();

    // Guard gegen doppelte Kollisionen (Paper-IDs die gerade verarbeitet werden)
    private HashSet<string> _processingPapers = new HashSet<string>();

    // Events
    public System.Action<VoxelData, EmbeddingMapResult> OnPaperMatched;
    public System.Action<int> OnPointsEarned;
    public System.Action<VoxelData> OnPaperCollected;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    /// <summary>
    /// Setzt alle verfügbaren Rules
    /// </summary>
    public void SetAllRules(List<RuleData> rules)
    {
        allRules = rules ?? new List<RuleData>();
        warnedRules.Clear();

        int withEmbedding = 0;
        foreach (var r in allRules)
        {
            if (r.pos_embedding != null && r.pos_embedding.Length > 0) withEmbedding++;
        }
        Debug.Log($"[PlayerMatcher] {allRules.Count} Rules geladen, {withEmbedding} mit Embedding");
    }

    /// <summary>
    /// Wird aufgerufen wenn Spieler Paper einsammelt (von CollectiblePaper)
    /// </summary>
    public void OnCollectPaper(VoxelData paper, GameObject paperObject)
    {
        if (paper == null)
        {
            Debug.LogWarning("[PlayerMatcher] Paper ist NULL!");
            return;
        }

        // Guard: Verhindere doppelte Verarbeitung desselben Papers
        string paperId = paper.paper_id ?? paper.job_id ?? paperObject.GetInstanceID().ToString();
        if (_processingPapers.Contains(paperId))
        {
            Debug.Log($"[PlayerMatcher] Paper '{paperId}' wird bereits verarbeitet, ignoriere doppelten Trigger");
            return;
        }
        _processingPapers.Add(paperId);

        totalCollected++;
        OnPaperCollected?.Invoke(paper);

        // Embedding dekodieren falls nötig
        if (paper.section_embedding == null && !string.IsNullOrEmpty(paper.paper_embedding_b64))
        {
            paper.DecodeData();
        }

        float[] embedding = paper.section_embedding ?? paper.embedding;

        // Alle Rules prüfen
        EmbeddingMapResult result = CheckAllRules(embedding, null);

        // Punkte gutschreiben
        totalPoints += result.points;
        if (result.totalMatches > 0) totalMatches++;

        OnPointsEarned?.Invoke(result.points);
        OnPaperMatched?.Invoke(paper, result);

        // Visual Feedback
        PlayFeedback(result.totalMatches > 0);

        // An GameManager melden (falls vorhanden)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPaperCollectedByPlayer(paper, result);
        }

        // An SpawnManager melden
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnStructureCollected(paperObject, paper);
        }

        // Paper zerstören
        if (paperObject != null)
        {
            Destroy(paperObject);
        }

        // Guard freigeben (nach kurzem Delay falls noch Trigger kommen)
        StartCoroutine(RemoveFromProcessing(paperId, 0.5f));

        Debug.Log($"[PlayerMatcher] Paper '{paper.paper_id}' eingesammelt: {result.totalMatches}/{result.totalRules} Matches, +{result.points} Punkte");
    }

    /// <summary>
    /// Entfernt Paper-ID nach Delay aus dem Processing-Guard
    /// </summary>
    private System.Collections.IEnumerator RemoveFromProcessing(string paperId, float delay)
    {
        yield return new WaitForSeconds(delay);
        _processingPapers.Remove(paperId);
    }

    /// <summary>
    /// Spielt visuelles und akustisches Feedback
    /// </summary>
    private void PlayFeedback(bool isMatch)
    {
        if (isMatch)
        {
            if (matchParticles != null) matchParticles.Play();
            if (audioSource != null && matchSound != null) audioSource.PlayOneShot(matchSound);
        }
        else
        {
            if (missParticles != null) missParticles.Play();
            if (audioSource != null && missSound != null) audioSource.PlayOneShot(missSound);
        }
    }

    // === COLLISION HANDLING ===

    void OnTriggerEnter(Collider other)
    {
        // Prüfen ob es ein CollectiblePaper ist
        CollectiblePaper paper = other.GetComponent<CollectiblePaper>();
        if (paper != null)
        {
            OnCollectPaper(paper.GetVoxelData(), other.gameObject);
            return;
        }

        // Oder Parent prüfen (falls Collider auf Child)
        paper = other.GetComponentInParent<CollectiblePaper>();
        if (paper != null)
        {
            OnCollectPaper(paper.GetVoxelData(), paper.gameObject);
        }
    }

    // === MATCHING LOGIC (from RuleMatcher) ===

    /// <summary>
    /// Ergebnis für eine einzelne Rule
    /// </summary>
    [System.Serializable]
    public class RuleMatchResult
    {
        public string rule_id;
        public string question;
        public bool is_match;
        public float similarity;
        public float threshold;
    }

    /// <summary>
    /// "Embedding Map" - Ergebnis für ALLE Rules
    /// </summary>
    [System.Serializable]
    public class EmbeddingMapResult
    {
        public List<RuleMatchResult> matches = new List<RuleMatchResult>();
        public int totalMatches;
        public int totalRules;
        public int points;
        public string feedback;
    }

    /// <summary>
    /// HAUPTMETHODE: Prüft Paper gegen ALLE Rules
    /// </summary>
    public EmbeddingMapResult CheckAllRules(float[] paperEmbedding, string paperText = null)
    {
        var result = new EmbeddingMapResult();
        result.totalRules = allRules.Count;

        if (paperEmbedding == null || paperEmbedding.Length == 0)
        {
            result.feedback = "Kein Embedding!";
            result.points = missPoints;
            return result;
        }

        if (allRules.Count == 0)
        {
            result.feedback = "Keine Rules geladen!";
            result.points = missPoints;
            return result;
        }

        string textLower = paperText?.ToLower() ?? "";
        bool hasText = !string.IsNullOrEmpty(textLower);

        foreach (var rule in allRules)
        {
            bool hasEmbedding = rule.pos_embedding != null && rule.pos_embedding.Length > 0;
            bool hasKeywords = RuleKeywordDatabase.HasKeywords(rule.rule_id);

            if (!hasEmbedding && !hasKeywords)
            {
                if (!warnedRules.Contains(rule.rule_id))
                {
                    Debug.LogWarning($"[PlayerMatcher] Rule '{rule.rule_id}' hat weder Embedding noch Keywords");
                    warnedRules.Add(rule.rule_id);
                }
                continue;
            }

            // Similarity berechnen
            float similarity = 0f;
            float negSimilarity = 0f;
            float embeddingDelta = 0f;

            if (hasEmbedding)
            {
                similarity = EmbeddingUtils.CosineSimilarity(paperEmbedding, rule.pos_embedding);
                if (rule.neg_embedding != null && rule.neg_embedding.Length > 0)
                {
                    negSimilarity = EmbeddingUtils.CosineSimilarity(paperEmbedding, rule.neg_embedding);
                }
                embeddingDelta = similarity - negSimilarity;
            }

            // Keyword-Matching
            int posKeywords = 0;
            int negKeywords = 0;
            float ruleKeywordWeight = hasEmbedding ? keywordWeight : 1.0f;

            if (hasText && hasKeywords)
            {
                var keywords = RuleKeywordDatabase.GetKeywords(rule.rule_id);
                if (keywords != null)
                {
                    ruleKeywordWeight = hasEmbedding ? keywords.weight : 1.0f;
                    foreach (string kw in keywords.positive)
                    {
                        if (textLower.Contains(kw.ToLower())) posKeywords++;
                    }
                    foreach (string kw in keywords.negative)
                    {
                        if (textLower.Contains(kw.ToLower())) negKeywords++;
                    }
                }
            }

            // Kombinierter Score
            float keywordScore = 0f;
            if (posKeywords + negKeywords > 0)
            {
                keywordScore = (posKeywords - negKeywords) / (float)(posKeywords + negKeywords);
            }

            float embeddingScore = embeddingDelta * 10f;
            float combinedScore;

            if (!hasEmbedding)
            {
                combinedScore = keywordScore;
            }
            else if (hasText && (posKeywords + negKeywords > 0))
            {
                combinedScore = ruleKeywordWeight * keywordScore + (1f - ruleKeywordWeight) * embeddingScore;
            }
            else
            {
                combinedScore = embeddingScore;
            }

            // Entscheidung
            bool isMatch;
            if (!hasEmbedding)
            {
                isMatch = posKeywords > 0 && posKeywords > negKeywords;
            }
            else if (hasText && (posKeywords + negKeywords > 0))
            {
                isMatch = combinedScore > 0 && (posKeywords > negKeywords || embeddingDelta > minEmbeddingDelta);
            }
            else
            {
                if (rule.neg_embedding != null && rule.neg_embedding.Length > 0)
                {
                    isMatch = embeddingDelta > minEmbeddingDelta;
                }
                else
                {
                    isMatch = false;
                }
            }

            result.matches.Add(new RuleMatchResult
            {
                rule_id = rule.rule_id,
                question = rule.question,
                is_match = isMatch,
                similarity = similarity,
                threshold = rule.threshold
            });

            if (isMatch)
            {
                result.totalMatches++;
            }
        }

        // Punkte berechnen
        result.points = result.totalMatches > 0
            ? Mathf.RoundToInt(matchPoints * result.totalMatches / (float)result.totalRules * 2f)
            : missPoints;

        // Feedback
        if (result.totalMatches > 0)
        {
            var matchedRules = result.matches.FindAll(m => m.is_match);
            string ruleNames = string.Join(", ", matchedRules.ConvertAll(m => m.rule_id));
            result.feedback = $"[OK] {result.totalMatches}/{result.totalRules} Rules! [{ruleNames}] +{result.points}";
        }
        else
        {
            result.feedback = $"Paper gesammelt (+{result.points})";
        }

        return result;
    }

    // === PUBLIC GETTERS ===

    public int GetTotalCollected() => totalCollected;
    public int GetTotalMatches() => totalMatches;
    public int GetTotalPoints() => totalPoints;
    public List<RuleData> GetAllRules() => allRules;
}
