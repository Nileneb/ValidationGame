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
    /// Prüft Match zwischen Paper-Embedding und Regel
    /// WICHTIG: Embeddings kommen jetzt aus VoxelData (Job), nicht aus RuleData!
    /// </summary>
    public MatchResult CheckMatch(VoxelData job, RuleData rule)
    {
        // Embeddings aus Job nutzen (nicht aus Rule!)
        float[] paperEmbedding = job?.section_embedding ?? job?.embedding;
        float[] posEmbedding = job?.pos_embedding;

        if (paperEmbedding == null || posEmbedding == null)
        {
            Debug.LogWarning("RuleMatcher: Missing embeddings in job data!");
            return new MatchResult
            {
                is_match = false,
                similarity = 0f,
                points = missPoints,
                feedback = "⚠️ Daten unvollständig (+10)"
            };
        }

        // Cosine Similarity berechnen
        float similarity = CosineSimilarity(paperEmbedding, posEmbedding);

        // Optional: Negative Similarity berücksichtigen
        if (job.neg_embedding != null && job.neg_embedding.Length > 0)
        {
            float negSimilarity = CosineSimilarity(paperEmbedding, job.neg_embedding);
            // Kombinierte Similarity: positiv - negativ
            similarity = (similarity - negSimilarity + 1f) / 2f; // Normalisiert auf 0-1
        }

        // Threshold aus Job oder Rule
        float threshold = job.threshold > 0 ? job.threshold : (rule?.threshold ?? 0.7f);

        bool isMatch = similarity >= threshold;
        int points = isMatch ? Mathf.RoundToInt(matchPoints * similarity) : missPoints;

        // Feedback-Text mit Frage aus Job oder Rule
        string question = !string.IsNullOrEmpty(job.question) ? job.question : (rule?.question ?? "Paper");

        string feedback = isMatch
            ? $"🎉 MATCH! {question} (+{points})"
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
    /// Legacy-Methode für Kompatibilität (falls embedding separat übergeben wird)
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

        // Für Legacy: Dummy-VoxelData mit Embedding erstellen
        VoxelData dummyJob = new VoxelData
        {
            section_embedding = paperEmbedding,
            pos_embedding = rule.pos_embedding,
            neg_embedding = rule.neg_embedding,
            threshold = rule.threshold,
            question = rule.question
        };

        return CheckMatch(dummyJob, rule);
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
