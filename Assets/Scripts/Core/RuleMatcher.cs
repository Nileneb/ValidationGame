// Scripts/Core/RuleMatcher.cs
// HYBRID-MATCHING: Embeddings + Keywords (BioBERT Baseline ~0.90!)
// Prüft Paper gegen ALLE Rules und erstellt eine "Embedding Map"
// NUTZT: EmbeddingUtils für alle Embedding-Operationen!

using UnityEngine;
using System.Collections.Generic;

public class RuleMatcher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int matchPoints = 100;
    [SerializeField] private int missPoints = 10;

    [Header("Hybrid Matching")]
    [Tooltip("Keyword-Gewicht (0.7 = Keywords zählen 70%, Embedding 30%)")]
    [SerializeField] private float keywordWeight = 0.7f;

    [Tooltip("Minimum Delta (pos-neg) für reines Embedding-Matching")]
    [SerializeField] private float minEmbeddingDelta = 0.015f;

    // Alle geladenen Rules (werden vom GameManager gesetzt)
    private List<RuleData> allRules = new List<RuleData>();

    // Tracking für Warnings (nur einmal pro Rule warnen)
    private HashSet<string> warnedRules = new HashSet<string>();

    // === KEYWORD DEFINITIONS für ALLE Server-Rules ===
    // NEUE API: Rules haben evtl. keine Embeddings, daher KEYWORD-ONLY Matching!
    private static readonly Dictionary<string, RuleKeywords> RULE_KEYWORDS = new Dictionary<string, RuleKeywords>
    {
        // === RCT & Study Design ===
        { "is_rct", new RuleKeywords {
            positive = new[] { "randomized controlled trial", "randomised controlled trial", "RCT", "randomly assigned", "randomization", "randomisation" },
            negative = new[] { "observational study", "retrospective", "non-randomized", "cohort study", "meta-analysis" },
            weight = 0.9f  // KEYWORD-ONLY da keine Embeddings
        }},
        { "has_placebo", new RuleKeywords {
            positive = new[] { "placebo-controlled", "placebo group", "versus placebo", "compared to placebo", "placebo arm" },
            negative = new[] { "no placebo", "open-label", "active control only" },
            weight = 0.9f
        }},
        { "has_control_group", new RuleKeywords {
            positive = new[] { "control group", "controlled trial", "control arm", "comparison group", "controls received" },
            negative = new[] { "uncontrolled", "single-arm", "no control", "without control" },
            weight = 0.9f
        }},
        { "is_blinded", new RuleKeywords {
            positive = new[] { "double-blind", "double blind", "single-blind", "blinded", "masking", "masked" },
            negative = new[] { "open-label", "unblinded", "non-blinded", "no blinding" },
            weight = 0.9f
        }},
        
        // === Outcomes & Analysis ===
        { "reports_primary_outcome", new RuleKeywords {
            positive = new[] { "primary outcome", "primary endpoint", "primary end point", "primary measure", "main outcome" },
            negative = new[] { "no primary outcome", "secondary only" },
            weight = 0.9f
        }},
        { "sample_size_adequate", new RuleKeywords {
            positive = new[] { "sample size", "power calculation", "participants", "patients enrolled", "n =", "n=" },
            negative = new[] { "pilot study", "feasibility study", "case report", "case series" },
            weight = 0.7f
        }},
        { "has_statistical_analysis", new RuleKeywords {
            positive = new[] { "statistical analysis", "statistically significant", "statistical methods", "analyzed using", "ANOVA", "t-test", "chi-square", "regression" },
            negative = new[] { "descriptive only", "no statistical" },
            weight = 0.9f
        }},
        { "reports_p_values", new RuleKeywords {
            positive = new[] { "p <", "p=", "p =", "p-value", "statistical significance", "p<0.05", "p < 0.05" },
            negative = new string[] { },
            weight = 0.95f
        }},
        { "reports_ci", new RuleKeywords {
            positive = new[] { "confidence interval", "95% CI", "CI:", "95%CI", "confidence intervals" },
            negative = new string[] { },
            weight = 0.95f
        }},
        { "itt_analysis", new RuleKeywords {
            positive = new[] { "intention-to-treat", "intention to treat", "ITT", "intent-to-treat", "modified ITT" },
            negative = new[] { "per-protocol only", "no ITT" },
            weight = 0.9f
        }},
        
        // === Ethics & Compliance ===
        { "has_ethics_approval", new RuleKeywords {
            positive = new[] { "ethics committee", "IRB", "institutional review board", "ethics approval", "ethical approval", "approved by" },
            negative = new string[] { },
            weight = 0.95f
        }},
        { "declares_coi", new RuleKeywords {
            positive = new[] { "conflict of interest", "conflicts of interest", "COI", "competing interests", "no conflicts", "declares no" },
            negative = new string[] { },
            weight = 0.9f
        }},
        { "has_informed_consent", new RuleKeywords {
            positive = new[] { "informed consent", "written consent", "consent form", "consented to participate" },
            negative = new[] { "waived consent" },
            weight = 0.9f
        }},
        { "pre_registered", new RuleKeywords {
            positive = new[] { "registered", "registration", "ClinicalTrials.gov", "ISRCTN", "trial registration", "pre-registered", "prospectively registered" },
            negative = new string[] { },
            weight = 0.9f
        }},
        { "is_peer_reviewed", new RuleKeywords {
            positive = new[] { "peer-reviewed", "peer reviewed", "journal", "published in" },
            negative = new[] { "preprint", "not peer-reviewed" },
            weight = 0.7f
        }},
        
        // === Reporting Quality ===
        { "reports_attrition", new RuleKeywords {
            positive = new[] { "dropout", "attrition", "lost to follow-up", "withdrew", "discontinued", "CONSORT flow" },
            negative = new string[] { },
            weight = 0.9f
        }},
        { "reports_adverse_events", new RuleKeywords {
            positive = new[] { "adverse events", "adverse effects", "side effects", "safety", "tolerability", "AE", "SAE", "serious adverse" },
            negative = new string[] { },
            weight = 0.9f
        }},
        { "is_human_study", new RuleKeywords {
            positive = new[] { "human", "patients", "participants", "subjects", "volunteers", "men", "women", "adults", "children" },
            negative = new[] { "animal", "mice", "rats", "in vitro", "cell culture" },
            weight = 0.9f
        }},
        
        // === Domain-Specific ===
        { "is_eparticipation_civictech", new RuleKeywords {
            positive = new[] { "e-participation", "civic tech", "digital democracy", "online participation", "citizen engagement", "civic technology", "digital citizen" },
            negative = new string[] { },
            weight = 0.9f
        }}
    };

    [System.Serializable]
    public class RuleKeywords
    {
        public string[] positive;
        public string[] negative;
        public float weight;  // Keyword-Gewicht für diese Rule (0.9 = KEYWORD-ONLY)
    }

    [System.Serializable]
    public class MatchResult
    {
        public bool is_match;
        public float similarity;
        public int points;
        public string feedback;
    }

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
    /// Setzt alle verfügbaren Rules (aufgerufen vom GameManager)
    /// </summary>
    public void SetAllRules(List<RuleData> rules)
    {
        allRules = rules ?? new List<RuleData>();
        warnedRules.Clear(); // Warnings zurücksetzen bei neuen Rules

        // Zähle Rules mit/ohne Embeddings
        int withEmbedding = 0;
        foreach (var r in allRules)
        {
            if (r.pos_embedding != null && r.pos_embedding.Length > 0) withEmbedding++;
        }
        Debug.Log($"RuleMatcher: {allRules.Count} Rules geladen, {withEmbedding} mit Embedding");
    }
    /// <summary>
    /// HAUPTMETHODE: Prüft Paper gegen ALLE Rules!
    /// Erstellt eine "Embedding Map" mit JA/NEIN für jede Rule
    /// HYBRID: Embedding-Similarity + Keyword-Matching kombiniert!
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

        // Prüfe gegen JEDE Rule (HYBRID oder KEYWORD-ONLY!)
        foreach (var rule in allRules)
        {
            bool hasEmbedding = rule.pos_embedding != null && rule.pos_embedding.Length > 0;
            bool hasKeywords = RULE_KEYWORDS.ContainsKey(rule.rule_id);

            // Wenn weder Embedding noch Keywords: überspringen
            if (!hasEmbedding && !hasKeywords)
            {
                if (!warnedRules.Contains(rule.rule_id))
                {
                    Debug.LogWarning($"RuleMatcher: Rule '{rule.rule_id}' hat weder Embedding noch Keywords - übersprungen");
                    warnedRules.Add(rule.rule_id);
                }
                continue;
            }

            // Info-Log wenn KEYWORD-ONLY (einmalig)
            if (!hasEmbedding && hasKeywords && !warnedRules.Contains(rule.rule_id))
            {
                Debug.Log($"RuleMatcher: Rule '{rule.rule_id}' nutzt KEYWORD-ONLY Matching (kein Embedding)");
                warnedRules.Add(rule.rule_id);
            }

            // === HYBRID / KEYWORD-ONLY MATCHING ===
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

            // Keyword-Matching (wenn Text vorhanden)
            int posKeywords = 0;
            int negKeywords = 0;
            float ruleKeywordWeight = hasEmbedding ? keywordWeight : 1.0f;  // 100% Keywords wenn kein Embedding!

            if (hasText && hasKeywords)
            {
                RuleKeywords keywords = RULE_KEYWORDS[rule.rule_id];
                ruleKeywordWeight = hasEmbedding ? keywords.weight : 1.0f;  // Volle Gewichtung bei KEYWORD-ONLY
                foreach (string kw in keywords.positive)
                {
                    if (textLower.Contains(kw.ToLower())) posKeywords++;
                }
                foreach (string kw in keywords.negative)
                {
                    if (textLower.Contains(kw.ToLower())) negKeywords++;
                }
            }

            // Kombinierter Score
            float keywordScore = 0f;
            if (posKeywords + negKeywords > 0)
            {
                keywordScore = (posKeywords - negKeywords) / (float)(posKeywords + negKeywords);
            }

            float embeddingScore = embeddingDelta * 10f;  // Skalieren auf ähnlichen Bereich
            float combinedScore;

            if (!hasEmbedding)
            {
                // KEYWORD-ONLY: 100% Keywords
                combinedScore = keywordScore;
            }
            else if (hasText && (posKeywords + negKeywords > 0))
            {
                // HYBRID: Keywords + Embedding
                combinedScore = ruleKeywordWeight * keywordScore + (1f - ruleKeywordWeight) * embeddingScore;
            }
            else
            {
                // EMBEDDING-ONLY
                combinedScore = embeddingScore;
            }

            // Entscheidung
            bool isMatch;
            if (!hasEmbedding)
            {
                // KEYWORD-ONLY: Mindestens 1 positive Keyword
                isMatch = posKeywords > 0 && posKeywords > negKeywords;
            }
            else if (hasText && (posKeywords + negKeywords > 0))
            {
                // HYBRID: Keywords + Embedding
                isMatch = combinedScore > 0 && (posKeywords > negKeywords || embeddingDelta > minEmbeddingDelta);
            }
            else
            {
                // DELTA-BASIERT: BioBERT hat ~0.90 Baseline für ALLES!
                // Nur matchen wenn pos_similarity DEUTLICH höher als neg_similarity
                // ODER wenn neg_embedding fehlt: Delta > minEmbeddingDelta erforderlich
                if (rule.neg_embedding != null && rule.neg_embedding.Length > 0)
                {
                    // Mit neg_embedding: pos muss besser sein als neg
                    isMatch = embeddingDelta > minEmbeddingDelta;
                }
                else
                {
                    // Ohne neg_embedding: KEIN MATCH möglich (BioBERT ~0.90 für alles!)
                    // Hier brauchen wir Keywords oder neg_embedding
                    isMatch = false;
                    Debug.LogWarning($"RuleMatcher: Rule '{rule.rule_id}' hat kein neg_embedding - KEIN Embedding-Match möglich ohne Keywords!");
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
                string matchType = !hasEmbedding ? "KEYWORD-ONLY" : (posKeywords > 0 ? "HYBRID" : "EMBEDDING");
                Debug.Log($"RuleMatcher: [OK] {matchType} MATCH '{rule.rule_id}' - Keywords: +{posKeywords}/-{negKeywords}, Similarity: {similarity:F3}");
            }
        }

        // Punkte berechnen: Mehr Matches = Mehr Punkte
        result.points = result.totalMatches > 0
            ? Mathf.RoundToInt(matchPoints * result.totalMatches / (float)result.totalRules * 2f)
            : missPoints;

        // Feedback erstellen
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

        Debug.Log($"RuleMatcher: EmbeddingMap - {result.totalMatches}/{result.totalRules} Matches (Hybrid: {hasText})");
        return result;
    }

    /// <summary>
    /// Legacy-Overload ohne Text (nur Embedding-Matching)
    /// </summary>
    public EmbeddingMapResult CheckAllRules(float[] paperEmbedding)
    {
        return CheckAllRules(paperEmbedding, null);
    }

    /// <summary>
    /// Legacy: Prüft Match zwischen Paper-Embedding und EINER Regel
    /// </summary>
    public MatchResult CheckMatch(VoxelData job, RuleData rule)
    {
        float[] paperEmbedding = job?.section_embedding ?? job?.embedding;
        float[] posEmbedding = job?.pos_embedding;

        // DEBUG: Was haben wir?
        Debug.Log($"RuleMatcher: job.rule_id={job?.rule_id}, job.question={job?.question}");
        Debug.Log($"RuleMatcher: paperEmbedding={(paperEmbedding != null ? paperEmbedding.Length.ToString() : "NULL")}");
        Debug.Log($"RuleMatcher: job.pos_embedding={(posEmbedding != null ? posEmbedding.Length.ToString() : "NULL")}");
        Debug.Log($"RuleMatcher: job.threshold={job?.threshold}");

        if (paperEmbedding == null || posEmbedding == null)
        {
            Debug.LogWarning($"RuleMatcher: FALLBACK weil posEmbedding={posEmbedding} oder paperEmbedding={paperEmbedding}");
            return new MatchResult
            {
                is_match = false,
                similarity = 0f,
                points = missPoints,
                feedback = "Paper eingesammelt! (+10)"
            };
        }

        float threshold = job.threshold > 0 ? job.threshold : (rule?.threshold ?? 0.7f);
        float similarity = EmbeddingUtils.CosineSimilarity(paperEmbedding, posEmbedding);
        bool isMatch = similarity >= threshold;

        int points = isMatch ? Mathf.RoundToInt(matchPoints * similarity) : missPoints;

        string question = !string.IsNullOrEmpty(job.question) ? job.question : (rule?.question ?? "Paper");
        string feedback = isMatch
            ? $"[OK] MATCH! {question} ({similarity:P0}) +{points}"
            : $"Gesammelt (+{missPoints})";

        return new MatchResult
        {
            is_match = isMatch,
            similarity = similarity,
            points = points,
            feedback = feedback
        };
    }

    /// <summary>
    /// Legacy-Methode für direkte Embedding-Übergabe
    /// </summary>
    public MatchResult CheckMatch(float[] paperEmbedding, RuleData rule)
    {
        if (paperEmbedding == null || rule == null)
        {
            return new MatchResult
            {
                is_match = false,
                similarity = 0f,
                points = missPoints,
                feedback = "Eingesammelt! (+10)"
            };
        }

        float[] posEmbedding = rule.pos_embedding;
        if (posEmbedding == null)
        {
            return new MatchResult
            {
                is_match = false,
                similarity = 0f,
                points = missPoints,
                feedback = "Eingesammelt! (+10)"
            };
        }

        float similarity = EmbeddingUtils.CosineSimilarity(paperEmbedding, posEmbedding);
        bool isMatch = similarity >= rule.threshold;
        int points = isMatch ? Mathf.RoundToInt(matchPoints * similarity) : missPoints;

        return new MatchResult
        {
            is_match = isMatch,
            similarity = similarity,
            points = points,
            feedback = isMatch
                ? $"[OK] MATCH! {rule.question} ({similarity:P0}) +{points}"
                : $"Gesammelt (+{missPoints})"
        };
    }
}
