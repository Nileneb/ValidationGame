// Assets/Scripts/Data/Chunk.cs
// Chunk Container nach DATAMODEL.md Spezifikation
// Ein Chunk = Ein Embedding + seine Voxels

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValidationGame.Data
{
    /// <summary>
    /// Chunk-Typen mit zugehörigen Farben (DATAMODEL.md Tabelle)
    /// </summary>
    public enum ChunkType
    {
        Abstract,
        Introduction,
        Methods,
        Results,
        Discussion,
        Positive,
        Negative,
        Unknown
    }

    /// <summary>
    /// RGB Farbe für JSON-Serialisierung
    /// </summary>
    [Serializable]
    public class ChunkColor
    {
        public float r;
        public float g;
        public float b;

        public ChunkColor() { }
        
        public ChunkColor(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public Color ToUnityColor() => new Color(r, g, b);
        
        public static ChunkColor FromUnityColor(Color c) => new ChunkColor(c.r, c.g, c.b);
    }

    /// <summary>
    /// Position für JSON-Serialisierung
    /// </summary>
    [Serializable]
    public class ChunkPosition
    {
        public float x;
        public float y;
        public float z;

        public ChunkPosition() { }
        
        public ChunkPosition(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
        
        public static ChunkPosition FromVector3(Vector3 v) => new ChunkPosition(v.x, v.y, v.z);
    }

    /// <summary>
    /// Chunk nach DATAMODEL.md Schema
    /// Container für Embedding + Voxels
    /// </summary>
    [Serializable]
    public class Chunk
    {
        // Identifikation
        public int chunk_id;
        public string chunk_type;
        public string text_preview;

        // Embedding (Base64 encoded)
        public string embedding_b64;
        
        // Voxel Grid (nach DATAMODEL.md)
        public VoxelGridJson voxels;

        // Darstellung
        public ChunkColor color;
        public ChunkPosition position;
        
        // Verbindungen zu anderen Chunks
        public List<int> connects_to;

        // Runtime-Daten (nicht serialisiert)
        [NonSerialized] public float[] embedding;
        [NonSerialized] public VoxelGrid voxelGrid;
        [NonSerialized] public GameObject gameObject;

        /// <summary>
        /// Dekodiert Base64 Embedding und erstellt VoxelGrid
        /// </summary>
        public void Decode(float threshold = 0.3f)
        {
            // Base64 → float[]
            if (!string.IsNullOrEmpty(embedding_b64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(embedding_b64);
                    embedding = new float[bytes.Length / 4];
                    Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Chunk.Decode: Base64 error: {e.Message}");
                    embedding = new float[768];
                }
            }

            // VoxelGrid aus JSON oder Embedding erstellen
            if (voxels != null && voxels.voxels != null && voxels.voxels.Count > 0)
            {
                voxelGrid = VoxelGrid.FromJson(voxels);
            }
            else if (embedding != null && embedding.Length >= 768)
            {
                // Aus Embedding generieren
                float[] enhanced = EmbeddingToVoxel.EnhanceVisualContrast(embedding);
                float[] normalized = EmbeddingToVoxel.Normalize(enhanced);
                voxelGrid = new VoxelGrid(normalized, threshold);
            }

            // Default Farbe wenn nicht gesetzt
            if (color == null)
            {
                color = GetDefaultColor(GetChunkType());
            }

            // Default Position wenn nicht gesetzt
            if (position == null)
            {
                position = new ChunkPosition(0, 0, 0);
            }

            // connects_to initialisieren
            if (connects_to == null)
            {
                connects_to = new List<int>();
            }
        }

        /// <summary>
        /// Parst chunk_type String zu Enum
        /// </summary>
        public ChunkType GetChunkType()
        {
            if (string.IsNullOrEmpty(chunk_type)) return ChunkType.Unknown;
            
            return chunk_type.ToLower() switch
            {
                "abstract" => ChunkType.Abstract,
                "introduction" => ChunkType.Introduction,
                "methods" => ChunkType.Methods,
                "results" => ChunkType.Results,
                "discussion" => ChunkType.Discussion,
                "positive" => ChunkType.Positive,
                "negative" => ChunkType.Negative,
                _ => ChunkType.Unknown
            };
        }

        /// <summary>
        /// Standard-Farben nach DATAMODEL.md Tabelle
        /// </summary>
        public static ChunkColor GetDefaultColor(ChunkType type)
        {
            return type switch
            {
                ChunkType.Abstract     => new ChunkColor(0.2f, 0.6f, 0.9f),  // #3399E6
                ChunkType.Introduction => new ChunkColor(0.3f, 0.8f, 0.3f),  // #4DCC4D
                ChunkType.Methods      => new ChunkColor(0.9f, 0.7f, 0.2f),  // #E6B233
                ChunkType.Results      => new ChunkColor(0.8f, 0.3f, 0.3f),  // #CC4D4D
                ChunkType.Discussion   => new ChunkColor(0.7f, 0.4f, 0.9f),  // #B266E6
                ChunkType.Positive     => new ChunkColor(0.2f, 0.9f, 0.3f),  // #33E64D (grün)
                ChunkType.Negative     => new ChunkColor(0.9f, 0.2f, 0.2f),  // #E63333 (rot)
                _                      => new ChunkColor(0.5f, 0.5f, 0.5f),  // Grau
            };
        }

        /// <summary>
        /// Erstellt Chunk aus Embedding
        /// </summary>
        public static Chunk FromEmbedding(float[] emb, string chunkType, int chunkId = 0, float threshold = 0.3f)
        {
            var chunk = new Chunk
            {
                chunk_id = chunkId,
                chunk_type = chunkType,
                embedding = emb
            };

            // Base64 encodieren
            if (emb != null)
            {
                byte[] bytes = new byte[emb.Length * 4];
                Buffer.BlockCopy(emb, 0, bytes, 0, bytes.Length);
                chunk.embedding_b64 = Convert.ToBase64String(bytes);
            }

            chunk.Decode(threshold);
            return chunk;
        }
    }
}
