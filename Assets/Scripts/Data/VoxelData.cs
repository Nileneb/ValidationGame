// Assets/Scripts/Data/VoxelData.cs
// Datenklassen für Server-Response nach DATAMODEL.md
// Mappt das Server-Format (job mit chunks) auf Unity-Strukturen

using System;
using System.Collections.Generic;
using UnityEngine;
using ValidationGame.Data;

/// <summary>
/// Job-Response vom Server: /api/jobs/next
/// Enthält Paper-Daten mit Chunks
/// </summary>
[Serializable]
public class VoxelData
{
    // Paper-Identifikation
    public string paper_id;
    public string job_id;  // Optional, kann paper_id sein
    public string title;
    public string authors;
    public string journal;
    
    // Paper-Embedding (gesamtes Paper)
    public string paper_embedding_b64;
    public string paper_text;
    
    // Chunks (Sections des Papers)
    public List<ServerChunk> chunks;
    public int chunks_count;
    public int expires_in_seconds;
    
    // === Legacy-Felder für Kompatibilität ===
    // Diese werden aus chunks[0] befüllt
    [NonSerialized] public float[] embedding;
    [NonSerialized] public float[] section_embedding;
    [NonSerialized] public float[] pos_embedding;  // Alias für rule matching
    public int section_id;
    public string section;
    public string section_text;
    public string rule_id;  // Optional: zugehörige Rule
    public VoxelColor color;
    
    // === Decoded Data ===
    [NonSerialized] private bool _decoded = false;
    
    /// <summary>
    /// Dekodiert Base64-Embeddings und befüllt Legacy-Felder
    /// </summary>
    public void DecodeData()
    {
        if (_decoded) return;
        _decoded = true;
        
        // Paper-Embedding dekodieren
        if (!string.IsNullOrEmpty(paper_embedding_b64))
        {
            embedding = DecodeEmbedding(paper_embedding_b64);
            pos_embedding = embedding;  // Alias für rule matching
        }
        
        // Ersten Chunk als "section" verwenden (für Legacy-Kompatibilität)
        if (chunks != null && chunks.Count > 0)
        {
            var firstChunk = chunks[0];
            section_id = firstChunk.chunk_id;
            section = firstChunk.chunk_type;
            section_text = firstChunk.text_preview;
            
            if (!string.IsNullOrEmpty(firstChunk.embedding_b64))
            {
                section_embedding = DecodeEmbedding(firstChunk.embedding_b64);
                // pos_embedding vom ersten Chunk falls kein Paper-Embedding
                if (pos_embedding == null)
                {
                    pos_embedding = section_embedding;
                }
            }
            
            // Farbe aus Chunk
            if (firstChunk.color != null)
            {
                color = new VoxelColor
                {
                    r = firstChunk.color.r,
                    g = firstChunk.color.g,
                    b = firstChunk.color.b
                };
            }
        }
        
        // Job-ID aus paper_id wenn nicht gesetzt
        if (string.IsNullOrEmpty(job_id))
        {
            job_id = paper_id;
        }
    }
    
    private float[] DecodeEmbedding(string base64)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            float[] result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"VoxelData: Failed to decode embedding: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Konvertiert zu ValidationGame.Data.Chunk (für neues System)
    /// </summary>
    public Chunk ToChunk(int chunkIndex = 0)
    {
        DecodeData();
        
        if (chunks == null || chunkIndex >= chunks.Count)
        {
            Debug.LogWarning($"VoxelData: No chunk at index {chunkIndex}");
            return null;
        }
        
        var serverChunk = chunks[chunkIndex];
        return serverChunk.ToGameChunk();
    }
    
    /// <summary>
    /// Alle Chunks als Game-Chunks
    /// </summary>
    public List<Chunk> ToChunks()
    {
        DecodeData();
        var result = new List<Chunk>();
        
        if (chunks != null)
        {
            foreach (var sc in chunks)
            {
                var chunk = sc.ToGameChunk();
                if (chunk != null)
                {
                    result.Add(chunk);
                }
            }
        }
        
        return result;
    }
}

/// <summary>
/// Chunk wie vom Server geliefert
/// </summary>
[Serializable]
public class ServerChunk
{
    public int chunk_id;
    public string chunk_type;
    public string text_preview;
    public string embedding_b64;
    public ServerVoxelGrid voxels;
    public ServerColor color;
    public ServerPosition position;
    public List<int> connects_to;
    
    public Chunk ToGameChunk()
    {
        var chunk = new Chunk
        {
            chunk_id = chunk_id,
            chunk_type = chunk_type,
            text_preview = text_preview,
            embedding_b64 = embedding_b64
        };
        
        // Embedding dekodieren
        if (!string.IsNullOrEmpty(embedding_b64))
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(embedding_b64);
                chunk.embedding = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, chunk.embedding, 0, bytes.Length);
            }
            catch { }
        }
        
        // Farbe
        if (color != null)
        {
            chunk.color = new ChunkColor(color.r, color.g, color.b);
        }
        
        // Position
        if (position != null)
        {
            chunk.position = new ChunkPosition(position.x, position.y, position.z);
        }
        
        // Voxels - bereits vom Server berechnet!
        if (voxels != null && voxels.voxels != null)
        {
            chunk.voxelGrid = new VoxelGrid();
            foreach (var v in voxels.voxels)
            {
                if (v != null && v.Length >= 4)
                {
                    int x = (int)v[0];
                    int y = (int)v[1];
                    int z = (int)v[2];
                    float value = v[3];
                    chunk.voxelGrid.SetVoxel(x, y, z, value);
                }
            }
        }
        
        // Connections
        if (connects_to != null)
        {
            chunk.connects_to = connects_to;
        }
        
        return chunk;
    }
}

[Serializable]
public class ServerVoxelGrid
{
    public int[] grid_size;
    public float[][] voxels;  // [[x,y,z,value], ...]
    public int voxel_count;
    public float fill_ratio;
}

[Serializable]
public class ServerColor
{
    public float r;
    public float g;
    public float b;
}

[Serializable]
public class ServerPosition
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class VoxelColor
{
    public float r;
    public float g;
    public float b;
    
    public Color ToUnityColor() => new Color(r, g, b);
}
