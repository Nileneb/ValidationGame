// Scripts/Core/EmbeddingUtils.cs
// Zentrale Utility-Klasse für Embedding-Operationen
// Base64-Dekodierung, Normalisierung, etc.

using UnityEngine;
using System;

public static class EmbeddingUtils
{
    /// <summary>
    /// Dekodiert Base64-String zu float[] (little-endian IEEE 754)
    /// Verwendet für BioBERT Embeddings (768 floats = 3072 bytes = 4096 Base64 chars)
    /// </summary>
    public static float[] DecodeBase64ToFloatArray(string base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            float[] floats = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }
        catch (Exception e)
        {
            Debug.LogError($"EmbeddingUtils: Base64 decode failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Enkodiert float[] zu Base64-String
    /// </summary>
    public static string EncodeFloatArrayToBase64(float[] floats)
    {
        if (floats == null || floats.Length == 0)
        {
            return null;
        }

        try
        {
            byte[] bytes = new byte[floats.Length * 4];
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"EmbeddingUtils: Base64 encode failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Berechnet Cosine Similarity zwischen zwei Vektoren
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
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
        
        if (magnitude < 0.0001f)
        {
            return 0f;
        }

        return dot / magnitude;
    }
}
