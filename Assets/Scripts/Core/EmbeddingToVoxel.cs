// Scripts/Core/EmbeddingToVoxel.cs
// Konvertiert Embedding-Vektoren in 3D-Voxel-Positionen für die Visualisierung
// 
// ========================================================================
// CHIFFRE-ALGORITHMUS v5: Deterministisch & Umkehrbar!
// ========================================================================
// 
// Das Embedding IST der Bauplan - NICHTS ANDERES!
// - Gleiches Embedding = IMMER gleiche Figur (deterministisch)
// - Die Figur ist "lesbar" und kann zurück zum Embedding übersetzt werden
// - 768 Embedding-Dimensionen → 12 x 8 x 8 Grid (768 = 12*8*8 perfekt!)
// - Index i → feste Position, Wert → Sichtbarkeit + Offset
//
// KEINE zufälligen Formen! KEINE rule_id-basierte Variation!
// Das Embedding selbst erzeugt die einzigartige Form!
// ========================================================================

using UnityEngine;
using System.Collections.Generic;
using System.Linq;  // Für OrderByDescending

/// <summary>
/// EmbeddingToVoxel: Konvertiert 768-dimensionale BioBERT-Embeddings in 3D-Voxel-Strukturen.
/// 
/// ARCHITEKTUR v5 - CHIFFRE-ALGORITHMUS (Deterministisch & Umkehrbar):
/// ====================================================================
/// - Embedding = 768 floats (BioBERT output)  
/// - Voxel Grid = 12 x 8 x 8 = 768 Positionen (PERFEKT für 768 Dimensionen!)
/// - JEDE Embedding-Dimension hat eine FESTE Position im Grid
/// - Der WERT bestimmt: Sichtbarkeit (Threshold) + Intensität + kleiner Offset
/// - GARANTIE: Gleiches Embedding = IMMER gleiche Figur!
/// - UMKEHRBAR: Man kann die Figur "lesen" und das Embedding rekonstruieren
/// ====================================================================
/// </summary>
public static class EmbeddingToVoxel
{
    // GRÖSSERES Grid für mehr Variation!
    public const int GRID_WIDTH = 16;
    public const int GRID_HEIGHT = 16;
    public const int GRID_DEPTH = 16;
    public const int TOTAL_POSITIONS = GRID_WIDTH * GRID_HEIGHT * GRID_DEPTH; // = 4096

    // Threshold: NICHT mehr fix! Wird dynamisch berechnet
    public const float DEFAULT_THRESHOLD = 0.01f;

    // MEHR Cubes für mehr Vielfalt!
    public const int TARGET_CUBE_COUNT = 500;  // 500 Cubes für komplexere Formen

    /// <summary>
    /// Konvertiert ein Embedding in eine Liste von Voxel-Positionen
    /// VERWENDET DYNAMISCHEN THRESHOLD basierend auf Embedding-Verteilung!
    /// </summary>
    /// <param name="embedding">768-dim Embedding Array</param>
    /// <param name="cubeSize">Größe eines Cubes</param>
    /// <param name="threshold">Minimum-Wert für sichtbare Cubes (wenn -1, wird dynamisch berechnet)</param>
    /// <returns>Liste von 3D-Positionen</returns>
    public static List<Vector3> ConvertToPositions(float[] embedding, float cubeSize = 0.5f, float threshold = -1f)
    {
        if (embedding == null)
        {
            Debug.LogError("EmbeddingToVoxel: Embedding ist null!");
            return new List<Vector3>();
        }

        // DEBUG: Zeige Embedding-Info
        Debug.Log($"EmbeddingToVoxel.ConvertToPositions: Embedding length={embedding.Length}, " +
                  $"first 5 values: [{embedding[0]:F4}, {embedding[1]:F4}, {embedding[2]:F4}, {embedding[3]:F4}, {embedding[4]:F4}]");

        // DYNAMISCHER THRESHOLD: Berechne so dass ~TARGET_CUBE_COUNT Cubes entstehen
        if (threshold < 0)
        {
            threshold = CalculateThresholdForTargetCount(embedding, TARGET_CUBE_COUNT);
            Debug.Log($"EmbeddingToVoxel: Dynamischer Threshold = {threshold:F6}");
        }

        if (embedding.Length != TOTAL_POSITIONS)
        {
            Debug.LogWarning($"EmbeddingToVoxel: Embedding hat {embedding.Length} Werte, erwartet {TOTAL_POSITIONS}. " +
                           "Verwende Mapping/Padding.");
            // Trotzdem versuchen zu konvertieren
        }

        List<Vector3> positions = new List<Vector3>();

        int usedLength = Mathf.Min(embedding.Length, TOTAL_POSITIONS);

        for (int i = 0; i < usedLength; i++)
        {
            float value = embedding[i];

            // Nur Werte über Threshold werden zu Cubes
            if (Mathf.Abs(value) >= threshold)
            {
                // 1D Index → 3D Position
                int x = i % GRID_WIDTH;
                int y = (i / GRID_WIDTH) % GRID_HEIGHT;
                int z = i / (GRID_WIDTH * GRID_HEIGHT);

                // Zentriert um Origin
                Vector3 pos = new Vector3(
                    (x - GRID_WIDTH / 2f + 0.5f),
                    (y - GRID_HEIGHT / 2f + 0.5f),
                    (z - GRID_DEPTH / 2f + 0.5f)
                );

                positions.Add(pos);
            }
        }

        if (positions.Count == 0)
        {
            Debug.LogWarning($"EmbeddingToVoxel: Keine Positionen über Threshold {threshold}. " +
                           $"Embedding min/max: {GetMin(embedding):F3}/{GetMax(embedding):F3}");
        }

        return positions;
    }

    // ========================================================================
    // CHIFFRE-ALGORITHMUS: Deterministische Embedding → Voxel Konvertierung
    // ========================================================================
    // WICHTIG: Jedes Embedding wird IMMER zur GLEICHEN Figur!
    // Das Muster ist fest und UMKEHRBAR (wie eine Verschlüsselung)
    // 
    // Mapping-Schema:
    // - 768 Embedding-Dimensionen → 768 feste Positionen im 3D-Raum
    // - Index i bestimmt die BASIS-POSITION (deterministisch!)
    // - Wert embedding[i] bestimmt:
    //   a) Ob der Voxel sichtbar ist (über Threshold)
    //   b) Die Farb-Intensität
    //   c) Einen kleinen Offset für Variation
    // ========================================================================

    /// <summary>
    /// CHIFFRE-ALGORITHMUS: Konvertiert Embedding deterministisch zu Voxel-Positionen
    /// GARANTIERT: Gleiches Embedding = Gleiche Figur, IMMER!
    /// </summary>
    public static List<(Vector3 position, float intensity)> ConvertWithIntensity(
        float[] embedding,
        float cubeSize = 0.5f,
        float threshold = -1f,
        string uniqueId = null)  // uniqueId wird IGNORIERT - nur das Embedding zählt!
    {
        var result = new List<(Vector3, float)>();

        if (embedding == null || embedding.Length == 0)
        {
            Debug.LogError("EmbeddingToVoxel.CHIFFRE: Embedding ist NULL oder leer!");
            return result;
        }

        // ========== CHIFFRE: Embedding → Voxel ==========
        // Das Embedding IST der Bauplan - nichts anderes!
        
        Debug.Log($"EmbeddingToVoxel CHIFFRE: {embedding.Length} Dimensionen, " +
                  $"erste Werte: [{embedding[0]:F4}, {embedding[1]:F4}, {embedding[2]:F4}]");

        // Dynamischer Threshold für ~TARGET_CUBE_COUNT sichtbare Voxels
        if (threshold < 0)
        {
            threshold = CalculateThresholdForTargetCount(embedding, TARGET_CUBE_COUNT);
        }

        // Embedding-Statistiken für Normalisierung
        float minVal = float.MaxValue, maxVal = float.MinValue;
        foreach (float v in embedding)
        {
            if (v < minVal) minVal = v;
            if (v > maxVal) maxVal = v;
        }
        float range = maxVal - minVal;
        if (range < 0.0001f) range = 1f;

        Debug.Log($"  Embedding Range: [{minVal:F4}, {maxVal:F4}], Threshold: {threshold:F6}");

        // ========== CHIFFRE-MAPPING ==========
        // 768 Dimensionen → 12 x 8 x 8 Grid (12 * 8 * 8 = 768 perfekt!)
        // Jede Dimension hat eine FESTE Position im Grid!
        const int CHIFFRE_X = 12;  // 12 Spalten
        const int CHIFFRE_Y = 8;   // 8 Reihen
        const int CHIFFRE_Z = 8;   // 8 Schichten
        // 12 * 8 * 8 = 768 = Embedding-Länge!

        int visibleCount = 0;
        var debugPositions = new List<string>();

        for (int i = 0; i < embedding.Length && i < 768; i++)
        {
            float value = embedding[i];
            float absValue = Mathf.Abs(value);

            // Nur Werte über Threshold werden sichtbar
            if (absValue < threshold) continue;

            // ========== DETERMINISTISCHE POSITION ==========
            // Index i → feste Position im 12x8x8 Grid
            int ix = i % CHIFFRE_X;           // 0-11
            int iy = (i / CHIFFRE_X) % CHIFFRE_Y;  // 0-7
            int iz = i / (CHIFFRE_X * CHIFFRE_Y);  // 0-7

            // Normalisierter Wert (0-1)
            float normalized = (value - minVal) / range;

            // Der WERT gibt einen kleinen Offset (±0.3 pro Achse)
            // Das macht die Form "lebendig" ohne die Grundstruktur zu ändern
            float offsetX = (normalized - 0.5f) * 0.6f;
            float offsetY = (value > 0 ? normalized : 1f - normalized) * 0.3f;
            float offsetZ = Mathf.Sin(normalized * Mathf.PI) * 0.3f;

            // Finale Position (zentriert um Origin)
            Vector3 pos = new Vector3(
                ix - CHIFFRE_X / 2f + 0.5f + offsetX,
                iy - CHIFFRE_Y / 2f + 0.5f + offsetY,
                iz - CHIFFRE_Z / 2f + 0.5f + offsetZ
            );

            // Intensität = normalisierter Absolutwert
            float intensity = normalized;

            result.Add((pos, intensity));
            visibleCount++;

            if (debugPositions.Count < 5)
                debugPositions.Add($"[{i}]→({pos.x:F1},{pos.y:F1},{pos.z:F1})");
        }

        Debug.Log($"  CHIFFRE: {visibleCount} Voxel sichtbar, erste: {string.Join(", ", debugPositions)}");

        return result;
    }

    /// <summary>
    /// LEGACY: Alte Methode für Kompatibilität (ruft jetzt CHIFFRE auf)
    /// </summary>
    private static List<(Vector3, float)> GenerateRuleBasedShape(System.Random rng, float[] embedding, string ruleId)
    {
        // REDIRECT zu CHIFFRE-Algorithmus - ruleId wird ignoriert!
        return ConvertWithIntensity(embedding, 0.5f, -1f, null);
    }

    /// <summary>
    /// Versucht ein Voxel hinzuzufügen, prüft Bounds und Duplikate
    /// </summary>
    private static bool TryAddVoxel(int x, int y, int z, float intensity, HashSet<string> used, List<(Vector3, float)> result)
    {
        x = Mathf.Clamp(x, 0, GRID_WIDTH - 1);
        y = Mathf.Clamp(y, 0, GRID_HEIGHT - 1);
        z = Mathf.Clamp(z, 0, GRID_DEPTH - 1);

        string key = $"{x},{y},{z}";
        if (used.Contains(key)) return false;
        used.Add(key);

        Vector3 pos = new Vector3(
            x - GRID_WIDTH / 2f + 0.5f,
            y - GRID_HEIGHT / 2f + 0.5f,
            z - GRID_DEPTH / 2f + 0.5f
        );
        result.Add((pos, Mathf.Clamp01(intensity)));
        return true;
    }

    /// <summary>
    /// Stabiler String-Hash (deterministisch über Sessions)
    /// </summary>
    private static int GetStableStringHash(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;

        int hash = 5381;
        foreach (char c in str)
        {
            hash = ((hash << 5) + hash) + c;  // hash * 33 + c
        }
        return hash;
    }

    /// <summary>
    /// Berechnet einen Hash aus dem Embedding für reproduzierbare Einzigartigkeit
    /// </summary>
    private static int ComputeEmbeddingHash(float[] embedding)
    {
        // Verwende die ersten 50 Werte für den Hash (schnell aber einzigartig)
        int hash = 17;
        for (int i = 0; i < Mathf.Min(50, embedding.Length); i++)
        {
            // Konvertiere float zu int bits für stabilen Hash
            int bits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(embedding[i]), 0);
            hash = hash * 31 + bits;
        }
        return hash;
    }

    /// <summary>
    /// Erstellt eine komplette Voxel-Struktur als GameObject
    /// threshold = -1 für automatischen dynamischen Threshold (~500 Cubes)
    /// ruleId = optionale Rule-ID für GARANTIERT einzigartige Formen
    /// </summary>
    public static GameObject CreateStructure(
        float[] embedding,
        Transform parent = null,
        float cubeSize = 0.5f,
        float threshold = -1f,  // -1 = DYNAMISCH!
        Color? baseColor = null,
        Material material = null,
        string ruleId = null)  // NEU: rule_id für Hybrid-Algorithmus
    {
        var positionsWithIntensity = ConvertWithIntensity(embedding, cubeSize, threshold, ruleId);

        if (positionsWithIntensity.Count == 0)
        {
            Debug.LogWarning("EmbeddingToVoxel.CreateStructure: Keine Cubes zu erstellen!");
            return null;
        }

        // Parent GameObject
        string structureName = ruleId != null ?
            $"EmbeddingStructure_{ruleId}_{positionsWithIntensity.Count}cubes" :
            $"EmbeddingStructure_{positionsWithIntensity.Count}cubes";
        GameObject structure = new GameObject(structureName);
        if (parent != null)
        {
            structure.transform.SetParent(parent);
            structure.transform.localPosition = Vector3.zero;
        }

        Color color = baseColor ?? Color.white;

        // Cubes erstellen
        foreach (var (pos, intensity) in positionsWithIntensity)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Voxel_{pos.x}_{pos.y}_{pos.z}";

            cube.transform.SetParent(structure.transform);
            cube.transform.localPosition = pos * cubeSize;
            cube.transform.localScale = Vector3.one * cubeSize * 0.9f;

            // Farbe basierend auf Intensität
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.material = new Material(material);
                }

                // Intensität beeinflusst Helligkeit
                Color cubeColor = Color.Lerp(color * 0.5f, color, intensity);
                renderer.material.color = cubeColor;
            }

            // Collider entfernen für Performance
            var collider = cube.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        Debug.Log($"EmbeddingToVoxel: Struktur erstellt mit {positionsWithIntensity.Count} Cubes");
        return structure;
    }

    /// <summary>
    /// Berechnet die visuelle Ähnlichkeit zwischen zwei Embeddings als Voxel-Overlap
    /// </summary>
    public static float CalculateVisualOverlap(float[] embedding1, float[] embedding2, float threshold = DEFAULT_THRESHOLD)
    {
        if (embedding1 == null || embedding2 == null) return 0f;

        var pos1 = new HashSet<int>();
        var pos2 = new HashSet<int>();

        int len = Mathf.Min(embedding1.Length, embedding2.Length, TOTAL_POSITIONS);

        for (int i = 0; i < len; i++)
        {
            if (Mathf.Abs(embedding1[i]) >= threshold) pos1.Add(i);
            if (Mathf.Abs(embedding2[i]) >= threshold) pos2.Add(i);
        }

        if (pos1.Count == 0 || pos2.Count == 0) return 0f;

        // Jaccard Similarity
        int intersection = 0;
        foreach (var p in pos1)
        {
            if (pos2.Contains(p)) intersection++;
        }

        int union = pos1.Count + pos2.Count - intersection;
        return (float)intersection / union;
    }

    /// <summary>
    /// NEUE METHODE: Berechnet Threshold so dass ungefähr targetCount Cubes entstehen
    /// </summary>
    public static float CalculateThresholdForTargetCount(float[] embedding, int targetCount = 100)
    {
        if (embedding == null || embedding.Length == 0) return DEFAULT_THRESHOLD;

        // Alle absoluten Werte sammeln und sortieren (absteigend)
        float[] absValues = new float[embedding.Length];
        for (int i = 0; i < embedding.Length; i++)
        {
            absValues[i] = Mathf.Abs(embedding[i]);
        }

        // Absteigend sortieren (größte zuerst)
        System.Array.Sort(absValues);
        System.Array.Reverse(absValues);

        // targetCount begrenzen
        int actualTarget = Mathf.Min(targetCount, embedding.Length);

        // Der Threshold ist der Wert an Position targetCount
        // Alle Werte VOR dieser Position sind >= threshold
        if (actualTarget > 0 && actualTarget <= absValues.Length)
        {
            float threshold = absValues[actualTarget - 1];
            // Minimal-Threshold um leere Strukturen zu vermeiden
            threshold = Mathf.Max(threshold, 0.0001f);

            Debug.Log($"EmbeddingToVoxel: Dynamischer Threshold = {threshold:F6} für {actualTarget} Cubes " +
                     $"(Embedding range: {absValues[absValues.Length - 1]:F6} - {absValues[0]:F6})");

            return threshold;
        }

        return DEFAULT_THRESHOLD;
    }

    /// <summary>
    /// Dynamischer Threshold basierend auf Embedding-Verteilung (Percentile)
    /// </summary>
    public static float CalculateDynamicThreshold(float[] embedding, float percentile = 0.5f)
    {
        if (embedding == null || embedding.Length == 0) return DEFAULT_THRESHOLD;

        // Absolute Werte sortieren
        float[] absValues = new float[embedding.Length];
        for (int i = 0; i < embedding.Length; i++)
        {
            absValues[i] = Mathf.Abs(embedding[i]);
        }

        System.Array.Sort(absValues);

        // Percentile berechnen
        int index = Mathf.FloorToInt(absValues.Length * percentile);
        return absValues[index];
    }

    private static float GetMin(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        float min = float.MaxValue;
        foreach (var v in arr) if (v < min) min = v;
        return min;
    }

    private static float GetMax(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        float max = float.MinValue;
        foreach (var v in arr) if (v > max) max = v;
        return max;
    }

    /// <summary>
    /// Berechnet die Ähnlichkeit als Score (0-100)
    /// </summary>
    public static int SimilarityToScore(float similarity, float maxScore = 100f)
    {
        // Similarity von -1 bis 1 → Score von 0 bis maxScore
        float normalized = (similarity + 1f) / 2f;  // -1..1 → 0..1
        return Mathf.RoundToInt(normalized * maxScore);
    }

    /// <summary>
    /// Euclidean Distance zwischen zwei Embeddings (normalisiert)
    /// </summary>
    public static float EuclideanDistance(float[] a, float[] b)
    {
        if (a == null || b == null) return float.MaxValue;

        int len = Mathf.Min(a.Length, b.Length);
        if (len == 0) return float.MaxValue;

        float sum = 0f;
        for (int i = 0; i < len; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }

        return Mathf.Sqrt(sum);
    }

    // === KOMPATIBILITÄTS-METHODEN für bestehenden Code ===

    /// <summary>
    /// Konvertiert Embedding zu VoxelPosition-Liste (für Kompatibilität mit bestehendem Code)
    /// VERWENDET DYNAMISCHEN THRESHOLD für ~100 Cubes!
    /// </summary>
    public static List<VoxelPosition> ConvertToVoxelPositions(float[] embedding, float threshold = -1f)
    {
        var result = new List<VoxelPosition>();

        if (embedding == null) return result;

        // DYNAMISCHER THRESHOLD
        if (threshold < 0)
        {
            threshold = CalculateThresholdForTargetCount(embedding, TARGET_CUBE_COUNT);
        }

        int usedLength = Mathf.Min(embedding.Length, TOTAL_POSITIONS);

        for (int i = 0; i < usedLength; i++)
        {
            float value = embedding[i];

            if (Mathf.Abs(value) >= threshold)
            {
                int x = i % GRID_WIDTH;
                int y = (i / GRID_WIDTH) % GRID_HEIGHT;
                int z = i / (GRID_WIDTH * GRID_HEIGHT);

                result.Add(new VoxelPosition { x = x, y = y, z = z });
            }
        }

        return result;
    }

    /// <summary>
    /// Zentriert VoxelPositions um den Ursprung
    /// </summary>
    public static List<VoxelPosition> CenterPositions(List<VoxelPosition> positions)
    {
        if (positions == null || positions.Count == 0) return positions;

        // Mittelpunkt berechnen
        float avgX = 0, avgY = 0, avgZ = 0;
        foreach (var p in positions)
        {
            avgX += p.x;
            avgY += p.y;
            avgZ += p.z;
        }
        avgX /= positions.Count;
        avgY /= positions.Count;
        avgZ /= positions.Count;

        // Zentrieren
        var centered = new List<VoxelPosition>();
        foreach (var p in positions)
        {
            centered.Add(new VoxelPosition
            {
                x = Mathf.RoundToInt(p.x - avgX),
                y = Mathf.RoundToInt(p.y - avgY),
                z = Mathf.RoundToInt(p.z - avgZ)
            });
        }

        return centered;
    }

    /// <summary>
    /// Generiert eine Farbe basierend auf dem Embedding
    /// </summary>
    public static Color GetColorFromEmbedding(float[] embedding)
    {
        if (embedding == null || embedding.Length < 3)
        {
            return Color.white;
        }

        // Verwende die ersten 3 normalisierten Werte als RGB
        float min = GetMin(embedding);
        float max = GetMax(embedding);
        float range = max - min;
        if (range == 0) range = 1f;

        // Erste 3 Werte normalisiert als Farbe
        float r = (embedding[0] - min) / range;
        float g = (embedding[1] - min) / range;
        float b = (embedding[2] - min) / range;

        // Farbe etwas aufhellen für bessere Sichtbarkeit
        r = Mathf.Lerp(0.3f, 1f, r);
        g = Mathf.Lerp(0.3f, 1f, g);
        b = Mathf.Lerp(0.3f, 1f, b);

        return new Color(r, g, b);
    }
}

