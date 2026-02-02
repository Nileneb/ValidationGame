// Assets/Scripts/Data/Molecule.cs
// Molecule = Verbundene Chunks (Paper oder Rule)
// Nach DATAMODEL.md Spezifikation

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValidationGame.Data
{
    /// <summary>
    /// Layout-Typ für Molecule
    /// </summary>
    public enum MoleculeLayout
    {
        Chain,   // Paper: Abstract → Intro → Methods → Results → Discussion
        Dipole   // Rule: Positive ↔ Negative
    }

    /// <summary>
    /// Verbindungstyp zwischen Chunks
    /// </summary>
    public enum ConnectionType
    {
        Sequential,  // Eins nach dem anderen (Paper)
        Polar        // Gegensätze (Rule)
    }

    /// <summary>
    /// Konfiguration für Molecule-Rendering
    /// </summary>
    [Serializable]
    public class MoleculeConfig
    {
        public int embedding_dim = 768;
        public int[] voxel_grid = new int[] { 8, 8, 12 };
        public string layout;
        public string connection_type;
        public float scale = 1.0f;
    }

    /// <summary>
    /// Molecule nach DATAMODEL.md Schema
    /// Paper = Chain von Section-Chunks
    /// Rule = Dipole von Pos/Neg-Chunks
    /// </summary>
    [Serializable]
    public class Molecule
    {
        // Identifikation
        public string molecule_id;
        public string molecule_type;  // "paper" oder "rule"
        public string title;

        // Chunks
        public List<Chunk> chunks;
        public int chunks_count => chunks?.Count ?? 0;

        // Konfiguration
        public MoleculeConfig molecule_config;

        // Runtime-Daten
        [NonSerialized] public MoleculeLayout layout;
        [NonSerialized] public ConnectionType connectionType;
        [NonSerialized] public GameObject rootObject;

        /// <summary>
        /// Alias für Initialize (API-Kompatibilität)
        /// </summary>
        public void DecodeAll(float voxelThreshold = 0.3f) => Initialize(voxelThreshold);

        /// <summary>
        /// Initialisiert Molecule und dekodiert alle Chunks
        /// </summary>
        public void Initialize(float voxelThreshold = 0.3f)
        {
            // Layout parsen
            layout = molecule_config?.layout?.ToLower() switch
            {
                "dipole" => MoleculeLayout.Dipole,
                _ => MoleculeLayout.Chain
            };

            // Connection type parsen
            connectionType = molecule_config?.connection_type?.ToLower() switch
            {
                "polar" => ConnectionType.Polar,
                _ => ConnectionType.Sequential
            };

            // Alle Chunks dekodieren
            if (chunks != null)
            {
                foreach (var chunk in chunks)
                {
                    chunk.Decode(voxelThreshold);
                }
            }
        }

        /// <summary>
        /// Ist das ein Paper?
        /// </summary>
        public bool IsPaper => molecule_type?.ToLower() == "paper";

        /// <summary>
        /// Ist das eine Rule?
        /// </summary>
        public bool IsRule => molecule_type?.ToLower() == "rule";

        /// <summary>
        /// Holt den positiven Chunk (nur für Rules)
        /// </summary>
        public Chunk GetPositiveChunk()
        {
            if (!IsRule || chunks == null) return null;
            return chunks.Find(c => c.chunk_type?.ToLower() == "positive");
        }

        /// <summary>
        /// Holt den negativen Chunk (nur für Rules)
        /// </summary>
        public Chunk GetNegativeChunk()
        {
            if (!IsRule || chunks == null) return null;
            return chunks.Find(c => c.chunk_type?.ToLower() == "negative");
        }

        /// <summary>
        /// Holt alle Chunks in sequentieller Reihenfolge (für Papers)
        /// </summary>
        public List<Chunk> GetChunksInOrder()
        {
            if (chunks == null) return new List<Chunk>();
            
            var ordered = new List<Chunk>(chunks);
            ordered.Sort((a, b) => a.chunk_id.CompareTo(b.chunk_id));
            return ordered;
        }

        /// <summary>
        /// Erstellt leere Paper Molecule
        /// </summary>
        public static Molecule CreatePaper(string paperId, string paperTitle)
        {
            return new Molecule
            {
                molecule_id = paperId,
                molecule_type = "paper",
                title = paperTitle,
                chunks = new List<Chunk>(),
                molecule_config = new MoleculeConfig
                {
                    layout = "chain",
                    connection_type = "sequential"
                }
            };
        }

        /// <summary>
        /// Erstellt Rule Molecule aus Pos/Neg Embeddings
        /// </summary>
        public static Molecule CreateRule(string ruleId, string question, 
            float[] posEmbedding, float[] negEmbedding = null, float threshold = 0.3f)
        {
            var rule = new Molecule
            {
                molecule_id = ruleId,
                molecule_type = "rule",
                title = question,
                chunks = new List<Chunk>(),
                molecule_config = new MoleculeConfig
                {
                    layout = "dipole",
                    connection_type = "polar"
                }
            };

            // Positive Chunk
            var posChunk = Chunk.FromEmbedding(posEmbedding, "positive", 0, threshold);
            posChunk.position = new ChunkPosition(0, 0, 0);
            if (negEmbedding != null)
            {
                posChunk.connects_to = new List<int> { 1 };
            }
            rule.chunks.Add(posChunk);

            // Negative Chunk (optional)
            if (negEmbedding != null)
            {
                var negChunk = Chunk.FromEmbedding(negEmbedding, "negative", 1, threshold);
                negChunk.position = new ChunkPosition(3, 0, 0);  // Abstand
                negChunk.connects_to = new List<int>();
                rule.chunks.Add(negChunk);
            }

            rule.Initialize(threshold);
            return rule;
        }
    }

    /// <summary>
    /// API Response für aktive Rule
    /// </summary>
    [Serializable]
    public class ActiveRuleResponse
    {
        public Molecule rule;
        public float threshold;
        public string question;
    }

    /// <summary>
    /// API Response für Job
    /// </summary>
    [Serializable]
    public class JobResponse
    {
        public string job_id;
        public string paper_id;
        public Chunk chunk;
        public int timeout_ms;
    }

    /// <summary>
    /// Spieler-Aktion für POST /api/jobs/{job_id}/response
    /// </summary>
    [Serializable]
    public class PlayerJobResponse
    {
        public string job_id;
        public string device_id;
        public string action;  // "collect" oder "skip"
        public int response_time_ms;
    }
}
