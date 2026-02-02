# Neue Architektur: Chunks als Container mit Satz-Embeddings

## 🎯 Das Problem (vorher)

- -_ Ein Chunk = Ein einzelner Cube/Figur -_ Rules sind kurze Phrasen → Embedding -_ Chunk ist ein ganzer Textabschnitt (Abstract, Introduction) → 1 Embedding für alles -_ **Problem**: Wie soll man eine kurze Phrase in einem ganzen Abschnitt finden?
-

## ✅ Die Lösung (jetzt)

```
Paper (vom Server)
├── Chunk "abstract" (TRANSPARENTER CONTAINER)
│   ├── Satz 1 → 768-dim Embedding → kleine 3D-Figur
│   ├── Satz 2 → 768-dim Embedding → kleine 3D-Figur  ← Rule kann HIER matchen!
│   ├── Satz 3 → 768-dim Embedding → kleine 3D-Figur
│   └── ...
├── Chunk "introduction" (TRANSPARENTER CONTAINER)
│   ├── Satz 1 → Figur
│   └── ...
└── ...
```

-

## 📁 Neue Dateien

### 1. `SentenceEmbedding.cs`

-
- ```csharp
  // Datenklasse für einen Satz innerhalb eines Chunks
  public class SentenceEmbedding
  ```
- {
  public int sentence_id;
  public string text; // Der Satztext
  public string embedding_b64; // Base64-kodiertes Embedding
  public float[] embedding; // Dekodiert
  public Vector3 localPosition; // Position im Container
-     public Color color;
- }

// Erweiterte Chunk-Daten mit Sätzen
public class ExtendedChunkData : ChunkData
{
public SentenceEmbedding[] sentences;
}

````
*
* ### 2. `ChunkContainer.cs`

- **ChunkContainer**: Großer transparenter Container
  - Repräsentiert einen Section (abstract, introduction, etc.)
  - Hat einen Trigger-Collider für Spieler-Kollision
  - Enthält viele `SentenceFigure` Kinder
  - Farbe basiert auf Section-Typ (blau=abstract, grün=intro, etc.)

- **SentenceFigure**: Kleine 3D-Figur für einen Satz
  - Generiert aus 768-dim Embedding → 8x8x12 Voxel-Grid
  - Hat eigenes Highlight wenn Rule matcht
  - Speichert Similarity-Score

### 3. `EmbeddingToVoxel.cs`

```csharp
// Konvertiert 768 floats → 3D Cube-Positionen
// Grid: 8 x 8 x 12 = 768 Positionen (passt perfekt zu BioBERT!)
public static List<Vector3> ConvertToPositions(float[] embedding, float threshold)
{
    // Jeder Embedding-Wert ≥ threshold wird ein Cube
    // Position: Index → (x, y, z) im Grid
}///
````

-

### 4. `RulezSceneManager.cs`

- Manager für die Rule-Visualisierungs-Szene
- Lädt alle aktiven Rules vom Server
- Zeigt Rules als 3D-Figuren im Grid an
- Dipol-Rules: Positive + Negative Figur mit Verbindungslinie
- Keine Spielmechanik - nur Anzeige!

## 🔄 Server-API Änderungen (TODO)

### Aktuell (vom Server)

```json
{
  "chunks": [
    {
      "chunk_id": 1,
      "section_name": "abstract",
      "embedding_b64": "...", // Embedding für ganzen Chunk
      "text_preview": "..."
    }
  ]
}
```

### Neu (benötigt)

-

```json
{
  "chunks": [
    {
      "chunk_id": 1,
      "section_name": "abstract",
      "embedding_b64": "...", // Chunk-Embedding (optional, für Fallback)
      "text_preview": "...",
      "sentences": [
        // NEU: Sätze mit eigenen Embeddings!
        {
          "sentence_id": 0,
///           "text": "This paper investigates...",
          "embedding_b64": "..."
        },
        {
          "sentence_id": 1,
          "text": "Our results show...",
          "embedding_b64": "..."
        }
      ]
    }
  ]
* }
```

## 🎮 Gameplay-Flow (neu)

- 1. **Spawn**: ChunkContainer wird gespawnt (groß, transparent)

2. **Sichtbar**: Spieler sieht viele kleine Figuren im Container
3. **Kollision**: Spieler fliegt durch Container
4. **Matching**: Jede SentenceFigure wird gegen aktive Rules geprüft
5. **Highlight**: Matching Sätze leuchten grün auf
6. **Score**: Punkte basierend auf Anzahl & Similarity der Matches

## 📊 Rulez-Szene

- Separate Szene: `Assets/Scenes/Rulez.unity`
- Zeigt alle Rules als 3D-Figuren
- Grid-Layout mit Labels
- Keine Gameplay-Logik
- Für Debug/Preview der Rules

## 🔧 Integration

1. **VoxelStructureSpawner** muss `ChunkContainer` verwenden statt einzelne Cubes
2. **SpawnManager** muss Container-Größe berücksichtigen
3. **PlayerMatcher** muss `SentenceFigure.CheckMatch()` aufrufen
4. **Server** muss Satz-Embeddings liefern (separate Arbeit!)
