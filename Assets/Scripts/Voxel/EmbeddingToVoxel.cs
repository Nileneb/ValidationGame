// Scripts/Voxel/EmbeddingToVoxel.cs
// PYRAMIDEN-ALGORITHMUS: Wuerfel snappen aneinander!
// Embedding bestimmt den Random-Seed fuer reproduzierbare Formen

using UnityEngine;
using System.Collections.Generic;

public static class EmbeddingToVoxel
{
    // 6 moegliche Richtungen zum Snappen
    private static readonly Vector3Int[] DIRECTIONS = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0),   // Rechts
        new Vector3Int(-1, 0, 0),  // Links
        new Vector3Int(0, 1, 0),   // Oben
        new Vector3Int(0, -1, 0),  // Unten
        new Vector3Int(0, 0, 1),   // Vorne
        new Vector3Int(0, 0, -1)   // Hinten
    };

    /// <summary>
    /// PYRAMIDEN-ALGORITHMUS:
    /// 1. Erster Wuerfel bei (0,0,0)
    /// 2. Jeder weitere Wuerfel snappt an eine freie Seite eines existierenden Wuerfels
    /// 3. Embedding wird als Seed fuer Random verwendet - gleiche Embeddings = gleiche Form!
    /// </summary>
    public static List<VoxelPosition> ConvertToPositions(float[] embedding, float cubeCount = 50f)
    {
        var positions = new List<VoxelPosition>();
        var occupied = new HashSet<Vector3Int>();

        if (embedding == null || embedding.Length == 0)
        {
            Debug.LogWarning("EmbeddingToVoxel: Embedding ist null oder leer");
            return GenerateDefaultCube();
        }

        // Embedding als Seed fuer reproduzierbare Zufallsformen
        int seed = GetSeedFromEmbedding(embedding);
        System.Random rng = new System.Random(seed);

        // Anzahl Wuerfel (20-100)
        int targetCount = Mathf.Clamp(Mathf.RoundToInt(cubeCount), 20, 100);
        
        // 1. Erster Wuerfel bei Origin
        Vector3Int start = Vector3Int.zero;
        positions.Add(new VoxelPosition { x = start.x, y = start.y, z = start.z });
        occupied.Add(start);

        // 2. Baue Pyramiden-artig auf
        int attempts = 0;
        int maxAttempts = targetCount * 10;
        
        while (positions.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            
            // Waehle zufaelligen existierenden Wuerfel
            int baseIndex = rng.Next(positions.Count);
            VoxelPosition baseCube = positions[baseIndex];
            Vector3Int basePos = new Vector3Int(baseCube.x, baseCube.y, baseCube.z);

            // Waehle zufaellige Richtung
            int dirIndex = rng.Next(6);
            Vector3Int newPos = basePos + DIRECTIONS[dirIndex];
            
            // Pruefe ob Position frei ist
            if (!occupied.Contains(newPos))
            {
                positions.Add(new VoxelPosition { x = newPos.x, y = newPos.y, z = newPos.z });
                occupied.Add(newPos);
            }
        }

        Debug.Log("EmbeddingToVoxel: Pyramide mit " + positions.Count + " Wuerfeln (Seed: " + seed + ")");
        return positions;
    }

    /// <summary>
    /// Erzeugt einen deterministischen Seed aus dem Embedding
    /// </summary>
    private static int GetSeedFromEmbedding(float[] embedding)
    {
        int seed = 0;
        for (int i = 0; i < Mathf.Min(20, embedding.Length); i++)
        {
            seed ^= (int)(embedding[i] * 100000) << (i % 16);
        }
        return Mathf.Abs(seed);
    }

    /// <summary>
    /// Farbe aus Embedding - RGB aus 3 Bereichen
    /// </summary>
    public static Color GetColorFromEmbedding(float[] embedding)
    {
        if (embedding == null || embedding.Length < 3)
        {
            return new Color(0.4f, 0.7f, 1f);
        }

        int third = embedding.Length / 3;
        float r = 0, g = 0, b = 0;
        
        for (int i = 0; i < third; i++)
        {
            r += Mathf.Abs(embedding[i]);
            g += Mathf.Abs(embedding[i + third]);
            b += Mathf.Abs(embedding[i + third * 2]);
        }
        
        r /= third; g /= third; b /= third;
        float maxVal = Mathf.Max(r, Mathf.Max(g, b));
        if (maxVal > 0.001f) { r /= maxVal; g /= maxVal; b /= maxVal; }
        
        return new Color(0.3f + r * 0.7f, 0.3f + g * 0.7f, 0.3f + b * 0.7f);
    }

    /// <summary>
    /// Fallback: Einfacher 3x3x3 Wuerfel
    /// </summary>
    private static List<VoxelPosition> GenerateDefaultCube()
    {
        var positions = new List<VoxelPosition>();
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                    positions.Add(new VoxelPosition { x = x, y = y, z = z });
        return positions;
    }

    /// <summary>
    /// Zentriert Positionen um Ursprung
    /// </summary>
    public static List<VoxelPosition> CenterPositions(List<VoxelPosition> positions)
    {
        if (positions == null || positions.Count == 0) return positions;

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

        var centered = new List<VoxelPosition>();
        foreach (var p in positions)
        {
            centered.Add(new VoxelPosition { x = p.x - offsetX, y = p.y - offsetY, z = p.z - offsetZ });
        }

        return centered;
    }
}
