// Scripts/Voxel/EmbeddingToVoxel.cs
// EINFACHE Konvertierung: Embedding → Voxel-Grid
// GLEICHE Methode für Rules UND Papers!

using UnityEngine;
using System.Collections.Generic;

public static class EmbeddingToVoxel
{
    // Grid-Größe: 8x8x12 = 768 (= BioBERT Embedding-Dimension)
    private const int GRID_X = 8;
    private const int GRID_Y = 12;
    private const int GRID_Z = 8;

    /// <summary>
    /// Konvertiert Embedding zu Voxel-Positionen
    /// EINFACH: Embedding[i] > threshold → Voxel an Position i
    /// </summary>
    public static List<VoxelPosition> ConvertToPositions(float[] embedding, float threshold = 0.3f)
    {
        var positions = new List<VoxelPosition>();

        if (embedding == null || embedding.Length == 0)
        {
            return GenerateDefaultCube();
        }

        // Normalisieren (0-1)
        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in embedding)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }
        float range = max - min;
        if (range < 0.0001f) range = 1f;

        // Embedding → 3D Grid
        for (int i = 0; i < embedding.Length && i < GRID_X * GRID_Y * GRID_Z; i++)
        {
            float normalized = (embedding[i] - min) / range;

            if (normalized > threshold)
            {
                // Index → 3D Position
                int x = i % GRID_X;
                int y = (i / GRID_X) % GRID_Y;
                int z = i / (GRID_X * GRID_Y);

                positions.Add(new VoxelPosition { x = x, y = y, z = z });
            }
        }

        // Mindestens ein paar Voxel
        if (positions.Count < 5)
        {
            return GenerateDefaultCube();
        }

        return positions;
    }

    /// <summary>
    /// Farbe aus Embedding - EINFACH: RGB aus 3 Bereichen
    /// </summary>
    public static Color GetColorFromEmbedding(float[] embedding)
    {
        if (embedding == null || embedding.Length < 3)
        {
            return new Color(0.4f, 0.7f, 1f); // Default: Hellblau
        }

        int third = embedding.Length / 3;

        // Durchschnitt der 3 Bereiche
        float r = 0, g = 0, b = 0;
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

        // Aufhellen für bessere Sichtbarkeit
        r = 0.3f + r * 0.7f;
        g = 0.3f + g * 0.7f;
        b = 0.3f + b * 0.7f;

        return new Color(r, g, b);
    }

    /// <summary>
    /// Zentriert Positionen um Ursprung
    /// </summary>
    public static List<VoxelPosition> CenterPositions(List<VoxelPosition> positions)
    {
        if (positions == null || positions.Count == 0) return positions;

        // Mittelpunkt berechnen
        float cx = 0, cy = 0, cz = 0;
        foreach (var p in positions)
        {
            cx += p.x;
            cy += p.y;
            cz += p.z;
        }
        cx /= positions.Count;
        cy /= positions.Count;
        cz /= positions.Count;

        // Zentrieren
        var centered = new List<VoxelPosition>();
        foreach (var p in positions)
        {
            centered.Add(new VoxelPosition
            {
                x = Mathf.RoundToInt(p.x - cx),
                y = Mathf.RoundToInt(p.y - cy),
                z = Mathf.RoundToInt(p.z - cz)
            });
        }

        return centered;
    }

    /// <summary>
    /// Fallback: Einfacher Würfel
    /// </summary>
    private static List<VoxelPosition> GenerateDefaultCube()
    {
        var positions = new List<VoxelPosition>();
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                for (int z = 0; z < 3; z++)
                {
                    positions.Add(new VoxelPosition { x = x, y = y, z = z });
                }
            }
        }
        return positions;
    }
}
