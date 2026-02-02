// Scripts/Voxel/SentenceEmbedding.cs
// Repräsentiert ein einzelnes Satz-/Paragraph-Embedding innerhalb eines Chunks
// Diese werden mit Rule-Embeddings verglichen!

using UnityEngine;

/// <summary>
/// Ein einzelnes Embedding aus einem Chunk (Satz oder Paragraph).
/// Diese Einheiten werden mit Rule-Embeddings verglichen.
/// </summary>
[System.Serializable]
public class SentenceEmbedding
{
    public int sentence_id;           // Index im Chunk
    public string text;               // Der Text dieses Satzes/Paragraphs
    public string embedding_b64;      // 768-dim Embedding als Base64
    public int char_start;            // Start-Position im Chunk-Text
    public int char_end;              // End-Position im Chunk-Text

    // Zur Laufzeit
    [System.NonSerialized] public float[] embedding;
    [System.NonSerialized] public Vector3 localPosition;  // Position innerhalb des Chunk-Containers
    [System.NonSerialized] public Color color;            // Farbe basierend auf Matches

    /// <summary>
    /// Dekodiert das Base64-Embedding in float[]
    /// </summary>
    public void DecodeEmbedding()
    {
        if (!string.IsNullOrEmpty(embedding_b64))
        {
            embedding = EmbeddingUtils.DecodeBase64ToFloatArray(embedding_b64);
        }
    }

    /// <summary>
    /// Hat dieses Embedding gültige Daten?
    /// </summary>
    public bool IsValid => embedding != null && embedding.Length > 0;

    /// <summary>
    /// Kurze Vorschau des Textes (max 50 Zeichen)
    /// </summary>
    public string GetPreview(int maxLength = 50)
    {
        if (string.IsNullOrEmpty(text)) return "[leer]";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + "...";
    }
}

/// <summary>
/// Erweiterte ChunkData mit Satz-Embeddings für feineres Matching.
/// </summary>
[System.Serializable]
public class ExtendedChunkData : ChunkData
{
    // Array von Satz-Embeddings innerhalb dieses Chunks
    public SentenceEmbedding[] sentences;

    /// <summary>
    /// Hat dieser Chunk Satz-Embeddings?
    /// </summary>
    public bool HasSentences => sentences != null && sentences.Length > 0;

    /// <summary>
    /// Dekodiert alle Satz-Embeddings
    /// </summary>
    public void DecodeSentences()
    {
        if (sentences == null) return;

        foreach (var sentence in sentences)
        {
            sentence.DecodeEmbedding();
        }
    }

    /// <summary>
    /// Anzahl gültiger Satz-Embeddings
    /// </summary>
    public int ValidSentenceCount
    {
        get
        {
            if (sentences == null) return 0;
            int count = 0;
            foreach (var s in sentences)
            {
                if (s.IsValid) count++;
            }
            return count;
        }
    }
}
