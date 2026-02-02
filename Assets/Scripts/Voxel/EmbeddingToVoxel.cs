// Scripts/Voxel/EmbeddingToVoxel.cs
// DATAMODEL.md Konformer Algorithmus: Embedding → 8x8x12 Voxel Grid
// DETERMINISTISCH: Gleiche Embeddings = Gleiche Voxels (identisch zu Python)

using UnityEngine;
using System;
using System.Collections.Generic;

namespace ValidationGame.Data
{
    /// <summary>
    /// Voxel Position (legacy support)
    /// </summary>
    [Serializable]
    public class VoxelPosition
    {
        public int x;
        public int y;
        public int z;
    }
}

/// <summary>
/// Konvertiert BioBERT Embeddings (768-dim) zu 8x8x12 Voxel Grids
/// MUSS identische Ergebnisse wie Python-Implementation liefern!
/// </summary>
public static class EmbeddingToVoxel
{
    // Grid-Dimensionen nach DATAMODEL.md
    public const int GRID_X = 8;
    public const int GRID_Y = 8;
    public const int GRID_Z = 12;
    public const int TOTAL = GRID_X * GRID_Y * GRID_Z;  // 768

    /// <summary>
    /// Verstärkt visuelle Unterschiede zwischen Embeddings
    /// IDENTISCH zu Python enhance_visual_contrast()
    /// 
    /// Formula:
    /// 1. centered = emb - mean(emb)
    /// 2. amplified = tanh(centered * 2.0)
    /// 3. result = (amplified + 1) / 2
    /// </summary>
    public static float[] EnhanceVisualContrast(float[] embedding)
    {
        if (embedding == null || embedding.Length == 0)
            return new float[TOTAL];

        int len = Mathf.Min(embedding.Length, TOTAL);
        float[] result = new float[TOTAL];

        // 1. Berechne Mean
        float sum = 0f;
        for (int i = 0; i < len; i++)
            sum += embedding[i];
        float mean = sum / len;

        // 2. Center, Amplify (tanh), Rescale
        for (int i = 0; i < len; i++)
        {
            float centered = embedding[i] - mean;
            float amplified = (float)Math.Tanh(centered * 2.0);
            result[i] = (amplified + 1f) / 2f;
        }

        // Padding falls Embedding kürzer als 768
        for (int i = len; i < TOTAL; i++)
            result[i] = 0.5f;

        return result;
    }

    /// <summary>
    /// Normalisiert Array zu [0, 1]
    /// IDENTISCH zu Python-Version
    /// </summary>
    public static float[] Normalize(float[] values)
    {
        if (values == null || values.Length == 0)
            return new float[TOTAL];

        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (float v in values)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }

        float range = max - min;
        if (range < 1e-8f) range = 1e-8f;

        float[] normalized = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            normalized[i] = (values[i] - min) / range;
        }

        return normalized;
    }

    /// <summary>
    /// Konvertiert 768-dim Embedding zu Voxel Positionen
    /// DETERMINISTISCH nach DATAMODEL.md
    /// 
    /// Mapping: embedding[i] → grid[x,y,z] where i = x + y*8 + z*64
    /// </summary>
    public static List<ValidationGame.Data.VoxelPosition> ConvertToPositions(
        float[] embedding, 
        float threshold = 0.3f,
        bool enhanceContrast = true)
    {
        var positions = new List<ValidationGame.Data.VoxelPosition>();

        if (embedding == null || embedding.Length == 0)
        {
            Debug.LogWarning("EmbeddingToVoxel: Embedding ist null oder leer");
            return GenerateDefaultCube();
        }

        // 1. Optional: Kontrast verstärken
        float[] processed = enhanceContrast 
            ? EnhanceVisualContrast(embedding) 
            : embedding;

        // 2. Normalisieren zu [0, 1]
        float[] normalized = Normalize(processed);

        // 3. Mapping: i = x + y*8 + z*64 (DATAMODEL.md)
        for (int z = 0; z < GRID_Z; z++)
        {
            for (int y = 0; y < GRID_Y; y++)
            {
                for (int x = 0; x < GRID_X; x++)
                {
                    int i = x + y * GRID_X + z * (GRID_X * GRID_Y);
                    
                    if (i < normalized.Length && normalized[i] >= threshold)
                    {
                        positions.Add(new ValidationGame.Data.VoxelPosition 
                        { 
                            x = x, 
                            y = y, 
                            z = z 
                        });
                    }
                }
            }
        }

        Debug.Log($"EmbeddingToVoxel: {positions.Count} Voxels (threshold={threshold})");
        return positions;
    }

    /// <summary>
    /// Erstellt VoxelGrid aus Embedding
    /// Kompatibel mit neuer Data-Struktur
    /// </summary>
    public static ValidationGame.Data.VoxelGrid CreateVoxelGrid(
        float[] embedding, 
        float threshold = 0.3f,
        bool enhanceContrast = true)
    {
        if (embedding == null || embedding.Length == 0)
            return new ValidationGame.Data.VoxelGrid();

        // 1. Optional: Kontrast verstärken
        float[] processed = enhanceContrast 
            ? EnhanceVisualContrast(embedding) 
            : embedding;

        // 2. Normalisieren zu [0, 1]
        float[] normalized = Normalize(processed);

        // 3. Auf 768 bringen falls kürzer
        if (normalized.Length < TOTAL)
        {
            float[] padded = new float[TOTAL];
            Array.Copy(normalized, padded, normalized.Length);
            for (int i = normalized.Length; i < TOTAL; i++)
                padded[i] = 0f;
            normalized = padded;
        }

        // 4. VoxelGrid erstellen
        return new ValidationGame.Data.VoxelGrid(normalized, threshold);
    }

    /// <summary>
    /// Farbe aus Embedding-Charakteristik
    /// Drei Bereiche des Embeddings → RGB
    /// </summary>
    public static Color GetColorFromEmbedding(float[] embedding)
    {
        if (embedding == null || embedding.Length < 3)
            return new Color(0.5f, 0.5f, 0.5f);

        int third = embedding.Length / 3;
        float r = 0, g = 0, b = 0;

        // Durchschnitt pro Drittel
        for (int i = 0; i < third; i++)
        {
            r += Mathf.Abs(embedding[i]);
            g += Mathf.Abs(embedding[i + third]);
            b += Mathf.Abs(embedding[i + third * 2]);
        }

        r /= third;
        g /= third;
        b /= third;

        // Normalisieren
        float maxVal = Mathf.Max(r, Mathf.Max(g, b));
        if (maxVal > 0.001f)
        {
            r /= maxVal;
            g /= maxVal;
            b /= maxVal;
        }

        // Aufhellen (0.3 base + 0.7 * value)
        return new Color(
            0.3f + r * 0.7f,
            0.3f + g * 0.7f,
            0.3f + b * 0.7f
        );
    }

    /// <summary>
    /// Fallback: 3x3x3 Würfel für leere/fehlerhafte Embeddings
    /// </summary>
    private static List<ValidationGame.Data.VoxelPosition> GenerateDefaultCube()
    {
        var positions = new List<ValidationGame.Data.VoxelPosition>();
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                    positions.Add(new ValidationGame.Data.VoxelPosition { x = x, y = y, z = z });
        return positions;
    }

    /// <summary>
    /// Zentriert Positionen um Ursprung
    /// </summary>
    public static List<ValidationGame.Data.VoxelPosition> CenterPositions(
        List<ValidationGame.Data.VoxelPosition> positions)
    {
        if (positions == null || positions.Count == 0) 
            return positions;

        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

        foreach (var p in positions)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
        }

        int offsetX = (minX + maxX) / 2;
        int offsetY = (minY + maxY) / 2;
        int offsetZ = (minZ + maxZ) / 2;

        var centered = new List<ValidationGame.Data.VoxelPosition>();
        foreach (var p in positions)
        {
            centered.Add(new ValidationGame.Data.VoxelPosition 
            { 
                x = p.x - offsetX, 
                y = p.y - offsetY, 
                z = p.z - offsetZ 
            });
        }

        return centered;
    }
}
