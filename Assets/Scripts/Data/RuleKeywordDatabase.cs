// Scripts/Data/RuleKeywordDatabase.cs
// Statische Keyword-Datenbank für Rule-Matching
// Ausgelagert aus RuleMatcher für Wiederverwendung

using System.Collections.Generic;

/// <summary>
/// Statische Datenbank mit Keywords für Rule-Matching.
/// Ermöglicht KEYWORD-ONLY Matching wenn Server keine Embeddings liefert.
/// </summary>
public static class RuleKeywordDatabase
{
    [System.Serializable]
    public class RuleKeywords
    {
        public string[] positive;
        public string[] negative;
        public float weight;
    }

    private static readonly Dictionary<string, RuleKeywords> KEYWORDS = new Dictionary<string, RuleKeywords>
    {
        // === RCT & Study Design ===
        { "is_rct", new RuleKeywords {
            positive = new[] { "randomized controlled trial", "randomised controlled trial", "RCT", "randomly assigned", "randomization", "randomisation" },
            negative = new[] { "observational study", "retrospective", "non-randomized", "cohort study", "meta-analysis" },
            weight = 0.9f
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

    /// <summary>
    /// Prüft ob Keywords für eine Rule existieren
    /// </summary>
    public static bool HasKeywords(string ruleId)
    {
        return KEYWORDS.ContainsKey(ruleId);
    }

    /// <summary>
    /// Holt Keywords für eine Rule
    /// </summary>
    public static RuleKeywords GetKeywords(string ruleId)
    {
        if (KEYWORDS.TryGetValue(ruleId, out RuleKeywords keywords))
        {
            return keywords;
        }
        return null;
    }

    /// <summary>
    /// Alle verfügbaren Rule-IDs
    /// </summary>
    public static IEnumerable<string> GetAllRuleIds()
    {
        return KEYWORDS.Keys;
    }

    /// <summary>
    /// Anzahl der definierten Rules
    /// </summary>
    public static int Count => KEYWORDS.Count;
}
