// Assets/Scripts/Debug/TestDataLoader.cs
// Lädt Test-Daten aus StreamingAssets für Offline-Testing
// DATAMODEL.md Contract Test

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ValidationGame.Data;

public class TestDataLoader : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private string testDataFolder = "TestData";
    
    [Header("Loaded Data")]
    [SerializeField] private List<string> loadedPapers = new List<string>();
    [SerializeField] private List<string> loadedRules = new List<string>();
    
    [Header("Debug")]
    [SerializeField] private bool logVoxelCounts = true;
    
    // Parsed data
    private Dictionary<string, Molecule> papers = new Dictionary<string, Molecule>();
    private Dictionary<string, Molecule> rules = new Dictionary<string, Molecule>();
    
    void Start()
    {
        if (loadOnStart)
        {
            StartCoroutine(LoadAllTestData());
        }
    }
    
    public IEnumerator LoadAllTestData()
    {
        string basePath = Path.Combine(Application.streamingAssetsPath, testDataFolder);
        
        if (!Directory.Exists(basePath))
        {
            Debug.LogError($"TestDataLoader: Directory not found: {basePath}");
            yield break;
        }
        
        Debug.Log($"TestDataLoader: Loading from {basePath}");
        
        // Load all JSON files
        string[] files = Directory.GetFiles(basePath, "*.json");
        
        foreach (string file in files)
        {
            string filename = Path.GetFileNameWithoutExtension(file);
            
            if (filename == "sample_voxels")
            {
                // Skip summary file
                continue;
            }
            
            yield return LoadFile(file, filename);
        }
        
        Debug.Log($"TestDataLoader: Loaded {papers.Count} papers, {rules.Count} rules");
        
        // Update inspector lists
        loadedPapers = new List<string>(papers.Keys);
        loadedRules = new List<string>(rules.Keys);
    }
    
    private IEnumerator LoadFile(string path, string filename)
    {
        string json = File.ReadAllText(path);
        
        try
        {
            // Parse as Molecule
            var molecule = JsonUtility.FromJson<Molecule>(json);
            
            if (molecule == null)
            {
                Debug.LogWarning($"TestDataLoader: Failed to parse {filename}");
                yield break;
            }
            
            // Initialize (decode embeddings, create voxel grids)
            molecule.Initialize();
            
            // Store based on type
            if (filename.StartsWith("paper_"))
            {
                string key = filename.Substring(6);
                papers[key] = molecule;
                
                if (logVoxelCounts && molecule.chunks != null)
                {
                    foreach (var chunk in molecule.chunks)
                    {
                        Debug.Log($"  Paper '{key}' chunk '{chunk.chunk_type}': {chunk.voxelGrid?.voxelCount ?? 0} voxels");
                    }
                }
            }
            else if (filename.StartsWith("rule_"))
            {
                string key = filename.Substring(5);
                rules[key] = molecule;
                
                if (logVoxelCounts && molecule.chunks != null)
                {
                    var pos = molecule.GetPositiveChunk();
                    var neg = molecule.GetNegativeChunk();
                    Debug.Log($"  Rule '{key}': pos={pos?.voxelGrid?.voxelCount ?? 0}, neg={neg?.voxelGrid?.voxelCount ?? 0} voxels");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"TestDataLoader: Error parsing {filename}: {e.Message}");
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Holt ein geladenes Paper
    /// </summary>
    public Molecule GetPaper(string key)
    {
        papers.TryGetValue(key, out var mol);
        return mol;
    }
    
    /// <summary>
    /// Holt eine geladene Rule
    /// </summary>
    public Molecule GetRule(string key)
    {
        rules.TryGetValue(key, out var mol);
        return mol;
    }
    
    /// <summary>
    /// Holt alle Paper-Keys
    /// </summary>
    public IEnumerable<string> GetPaperKeys() => papers.Keys;
    
    /// <summary>
    /// Holt alle Rule-Keys
    /// </summary>
    public IEnumerable<string> GetRuleKeys() => rules.Keys;
    
    /// <summary>
    /// Test: Vergleiche Python vs C# Voxel-Generierung
    /// </summary>
    [ContextMenu("Test Voxel Determinism")]
    public void TestVoxelDeterminism()
    {
        foreach (var kvp in papers)
        {
            var mol = kvp.Value;
            if (mol.chunks == null || mol.chunks.Count == 0) continue;
            
            var chunk = mol.chunks[0];
            if (chunk.embedding == null) continue;
            
            // Regeneriere Voxels aus dem Embedding
            var regenerated = EmbeddingToVoxel.CreateVoxelGrid(chunk.embedding, 0.3f, true);
            
            // Vergleiche mit geladenem Grid
            int originalCount = chunk.voxelGrid?.voxelCount ?? 0;
            int regeneratedCount = regenerated.voxelCount;
            
            if (originalCount != regeneratedCount)
            {
                Debug.LogError($"MISMATCH: {kvp.Key} - Python={originalCount}, C#={regeneratedCount}");
            }
            else
            {
                Debug.Log($"OK: {kvp.Key} - {originalCount} voxels match");
            }
        }
    }
}
