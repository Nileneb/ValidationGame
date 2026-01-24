// Scripts/Core/RuleMatcher.cs
// Embedding-basiertes Matching mit Cosine Similarity - VEREINFACHT
// Keine externen Abhängigkeiten

using UnityEngine;

public class RuleMatcher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int matchPoints = 100;
    [SerializeField] private int missPoints = 10;

    [System.Serializable]
    public class MatchResult
    {
        public bool is_match;
        public float similarity;
        public int points;
        public string feedback;
    }

    /// <summary>
    /// Prüft Match zwischen Paper-Embedding und Regel
    /// </summary>
    public MatchResult CheckMatch(VoxelData job, RuleData rule)
    {
        float[] paperEmbedding = job?.section_embedding ?? job?.embedding;
        float[] posEmbedding = job?.pos_embedding;

        if (paperEmbedding == null || posEmbedding == null)
        {
            return new MatchResult
            {
                is_match = false,
                similarity = 0f,
                points = missPoints,
                feedback = "Paper eingesammelt! (+10)"
            };
        }

        float threshold = job.threshold > 0 ? job.threshold : (rule?.threshold ?? 0.7f);
        float similarity = CosineSimilarity(paperEmbedding, posEmbedding);
        bool isMatch = similarity >= threshold;

        int points = isMatch ? Mathf.RoundToInt(matchPoints * similarity) : missPoints;

        string question = !string.IsNullOrEmpty(job.question) ? job.question : (rule?.question ?? "Paper");
        string feedback = isMatch
            ? $"✓ MATCH! {question} ({similarity:P0}) +{points}"
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

        float similarity = CosineSimilarity(paperEmbedding, posEmbedding);
        bool isMatch = similarity >= rule.threshold;
        int points = isMatch ? Mathf.RoundToInt(matchPoints * similarity) : missPoints;

        return new MatchResult
        {
            is_match = isMatch,
            similarity = similarity,
            points = points,
            feedback = isMatch
                ? $"✓ MATCH! {rule.question} ({similarity:P0}) +{points}"
                : $"Gesammelt (+{missPoints})"
        };
    }

    /// <summary>
    /// Cosine Similarity: (a·b) / (|a||b|)
    /// </summary>
    public float CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length || a.Length == 0)
        {
            return 0f;
        }

        float dot = 0f;
        float magA = 0f;
        float magB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        float magnitude = Mathf.Sqrt(magA) * Mathf.Sqrt(magB);
        return magnitude > 0 ? dot / magnitude : 0f;
    }
}
