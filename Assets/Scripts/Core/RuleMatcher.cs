// Scripts/Core/RuleMatcher.cs
// Embedding-basiertes Matching mit Cosine Similarity
// Vergleicht Paper-Embeddings mit Rule-Embeddings

using UnityEngine;

public class RuleMatcher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int matchPoints = 100;
    [SerializeField] private int missPoints = 10;

    /// <summary>
    /// Ergebnis eines Matching-Vorgangs
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
    /// Prüft ob ein Paper-Embedding zur aktiven Regel passt
    /// </summary>
    public MatchResult CheckMatch(float[] paperEmbedding, RuleData rule)
    {
        if (paperEmbedding == null || rule == null || rule.pos_embedding == null)
        {
            return new MatchResult
            {
                is_match = false,
                similarity = 0f,
                points = missPoints,
                feedback = "Eingesammelt! (+10)"
            };
        }

        // Cosine Similarity mit positivem Embedding berechnen
        float similarity = CosineSimilarity(paperEmbedding, rule.pos_embedding);

        // Optional: Negative Similarity berücksichtigen
        if (rule.neg_embedding != null && rule.neg_embedding.Length > 0)
        {
            float negSimilarity = CosineSimilarity(paperEmbedding, rule.neg_embedding);
            // Kombinierte Similarity: positiv - negativ
            similarity = (similarity - negSimilarity + 1f) / 2f; // Normalisiert auf 0-1
        }

        // Threshold-Check
        bool isMatch = similarity >= rule.threshold;
        int points = isMatch ? Mathf.RoundToInt(matchPoints * similarity) : missPoints;

        // Feedback-Text generieren
        string feedback = isMatch
            ? $"🎉 MATCH! {rule.question} (+{points})"
            : $"Weiter suchen... (Similarity: {similarity:F2}) (+{missPoints})";

        return new MatchResult
        {
            is_match = isMatch,
            similarity = similarity,
            points = points,
            feedback = feedback
        };
    }

    /// <summary>
    /// Berechnet die Cosine Similarity zwischen zwei Vektoren
    /// </summary>
    public float CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0)
        {
            return 0f;
        }

        // Bei unterschiedlicher Länge: kürzeren Vektor verwenden
        int length = Mathf.Min(a.Length, b.Length);

        if (a.Length != b.Length)
        {
            Debug.LogWarning($"RuleMatcher: Embedding-Längen unterschiedlich ({a.Length} vs {b.Length}), verwende {length}");
        }

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Mathf.Sqrt(magnitudeA);
        magnitudeB = Mathf.Sqrt(magnitudeB);

        // Division durch Null verhindern
        if (magnitudeA < float.Epsilon || magnitudeB < float.Epsilon)
        {
            return 0f;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Berechnet die euklidische Distanz zwischen zwei Vektoren
    /// </summary>
    public float EuclideanDistance(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return float.MaxValue;
        }

        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }

        return Mathf.Sqrt(sum);
    }
}
