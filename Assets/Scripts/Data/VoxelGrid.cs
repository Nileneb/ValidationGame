// Assets/Scripts/Data/VoxelGrid.cs
// 8x8x12 Voxel Grid nach DATAMODEL.md Spezifikation
// Deterministisch identisch zu Python-Implementation

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValidationGame.Data
{
    /// <summary>
    /// Ein einzelner Voxel mit Position und Intensitätswert
    /// </summary>
    [Serializable]
    public struct Voxel
    {
        public int x;
        public int y;
        public int z;
        public float value;

        public Voxel(int x, int y, int z, float value)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.value = value;
        }

        /// <summary>
        /// Konvertiert zu Array-Format [x, y, z, value]
        /// </summary>
        public float[] ToArray() => new float[] { x, y, z, value };
    }

    /// <summary>
    /// 8×8×12 Voxel Grid Container
    /// Dimension = 768 (BioBERT Embedding Größe)
    /// </summary>
    [Serializable]
    public class VoxelGrid
    {
        public const int SIZE_X = 8;
        public const int SIZE_Y = 8;
        public const int SIZE_Z = 12;
        public const int TOTAL_VOXELS = SIZE_X * SIZE_Y * SIZE_Z; // 768

        public int[] gridSize = new int[] { SIZE_X, SIZE_Y, SIZE_Z };
        public List<Voxel> voxels = new List<Voxel>();
        public int voxelCount => voxels.Count;

        /// <summary>
        /// 3D Array der Rohdaten (alle Werte, auch unter Threshold)
        /// </summary>
        private float[,,] rawGrid;

        /// <summary>
        /// Erstellt ein leeres Grid
        /// </summary>
        public VoxelGrid()
        {
            rawGrid = new float[SIZE_X, SIZE_Y, SIZE_Z];
        }

        /// <summary>
        /// Erstellt Grid aus normalisierten Werten
        /// </summary>
        public VoxelGrid(float[] normalizedValues, float threshold = 0.3f) : this()
        {
            if (normalizedValues == null || normalizedValues.Length < TOTAL_VOXELS)
            {
                Debug.LogWarning($"VoxelGrid: Invalid input array length {normalizedValues?.Length}, expected {TOTAL_VOXELS}");
                return;
            }

            // Mapping: i = x + y*8 + z*64 (DATAMODEL.md Spezifikation)
            for (int z = 0; z < SIZE_Z; z++)
            {
                for (int y = 0; y < SIZE_Y; y++)
                {
                    for (int x = 0; x < SIZE_X; x++)
                    {
                        int i = x + y * SIZE_X + z * (SIZE_X * SIZE_Y);
                        float value = normalizedValues[i];
                        rawGrid[x, y, z] = value;

                        if (value >= threshold)
                        {
                            voxels.Add(new Voxel(x, y, z, value));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Holt den Wert an einer Position (auch unter Threshold)
        /// </summary>
        public float GetValue(int x, int y, int z)
        {
            if (x < 0 || x >= SIZE_X || y < 0 || y >= SIZE_Y || z < 0 || z >= SIZE_Z)
                return 0f;
            return rawGrid[x, y, z];
        }

        /// <summary>
        /// Konvertiert zu JSON-kompatiblem Format nach DATAMODEL.md
        /// </summary>
        public VoxelGridJson ToJson()
        {
            var json = new VoxelGridJson
            {
                grid_size = gridSize,
                voxel_count = voxelCount,
                voxels = new List<float[]>()
            };

            foreach (var v in voxels)
            {
                json.voxels.Add(v.ToArray());
            }

            return json;
        }

        /// <summary>
        /// Erstellt Grid aus JSON-Daten
        /// </summary>
        public static VoxelGrid FromJson(VoxelGridJson json)
        {
            var grid = new VoxelGrid();

            if (json?.voxels == null) return grid;

            foreach (var arr in json.voxels)
            {
                if (arr.Length >= 4)
                {
                    int x = Mathf.RoundToInt(arr[0]);
                    int y = Mathf.RoundToInt(arr[1]);
                    int z = Mathf.RoundToInt(arr[2]);
                    float value = arr[3];

                    grid.voxels.Add(new Voxel(x, y, z, value));

                    if (x >= 0 && x < SIZE_X && y >= 0 && y < SIZE_Y && z >= 0 && z < SIZE_Z)
                    {
                        grid.rawGrid[x, y, z] = value;
                    }
                }
            }

            return grid;
        }
    }

    /// <summary>
    /// JSON-Serialisierbare Version nach DATAMODEL.md
    /// </summary>
    [Serializable]
    public class VoxelGridJson
    {
        public int[] grid_size;
        public List<float[]> voxels;
        public int voxel_count;
    }
}
