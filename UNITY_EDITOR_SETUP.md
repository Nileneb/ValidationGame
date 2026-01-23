# 🎮 Unity Editor Setup Anleitung - PaperRun

Diese Anleitung beschreibt alle Schritte, die **im Unity Editor** manuell durchgeführt werden müssen, um das Spiel spielbar zu machen.

---

## 📋 Voraussetzungen

✅ Alle Scripts wurden erstellt in:

- `Assets/Scripts/Core/` - GameManager.cs, RuleMatcher.cs
- `Assets/Scripts/Game/` - PlayerController.cs, TrackGenerator.cs
- `Assets/Scripts/Networking/` - ApiClient.cs
- `Assets/Scripts/UI/` - UIManager.cs
- `Assets/Scripts/Voxel/` - VoxelStructureSpawner.cs, CollectiblePaper.cs, VoxelMover.cs

---

## 🎬 Schritt 1: Game Scene erstellen

1. **File → New Scene** (Empty Scene Template)
2. **Speichern als**: `Assets/Scenes/Game.unity`
3. **Lighting Setup**: Window → Rendering → Lighting → Generate Lighting

---

## 🧊 Schritt 2: Prefabs erstellen

### 2.1 Cube Prefab (für Voxel)

1. **GameObject → 3D Object → Cube**
2. **Transform**: Scale = (1, 1, 1)
3. **Collider entfernen** (wird nicht benötigt für Einzelcubes)
4. **In Prefabs-Ordner ziehen**: `Assets/Prefabs/Voxels/VoxelCube.prefab`
5. **Aus Hierarchy löschen**

### 2.2 Track Segment Prefab

1. **GameObject → 3D Object → Plane**
2. **Umbenennen**: `TrackSegment`
3. **Transform**:
   - Position: (0, 0, 0)
   - Scale: (2, 1, 4) → ergibt 20m x 40m
4. **Material erstellen**:
   - `Assets/Materials/TrackMaterial.mat`
   - Shader: URP/Lit
   - Base Color: Dunkelgrau (#333333)
5. **Material zuweisen**
6. **In Prefabs-Ordner ziehen**: `Assets/Prefabs/Environment/TrackSegment.prefab`
7. **Aus Hierarchy löschen**

### 2.3 Player Prefab

1. **GameObject → 3D Object → Capsule**
2. **Umbenennen**: `Player`
3. **Transform**:
   - Position: (0, 1, 0)
   - Scale: (1, 1, 1)
4. **Tag setzen**: `Player` (Inspector → Tag → Add Tag falls nötig)
5. **Material erstellen**:
   - `Assets/Materials/PlayerMaterial.mat`
   - Base Color: Blau (#0066FF)
   - Emission: leicht leuchtend
6. **Rigidbody hinzufügen** (Add Component):
   - Use Gravity: ❌ (aus)
   - Is Kinematic: ✅ (an)
7. **PlayerController Script hinzufügen**
8. **In Prefabs-Ordner ziehen**: `Assets/Prefabs/Environment/Player.prefab`

---

## 🎭 Schritt 3: Manager GameObjects erstellen

### 3.1 GameManager

1. **GameObject → Create Empty**
2. **Umbenennen**: `GameManager`
3. **Scripts hinzufügen**:
   - GameManager.cs
   - ApiClient.cs
   - RuleMatcher.cs
   - VoxelStructureSpawner.cs
4. **Im Inspector konfigurieren**:

   **ApiClient:**
   - Server URL: `http://localhost:8089`
   - Log Requests: ✅

   **GameManager:**
   - Api Client: → GameManager (Self Reference)
   - Voxel Spawner: → GameManager (Self Reference)
   - Rule Matcher: → GameManager (Self Reference)

   **VoxelStructureSpawner:**
   - Cube Prefab: → VoxelCube.prefab
   - Voxel Material: → VoxelMaterial (erstellen, s.u.)
   - Player: → Player (wird später gesetzt)
   - Cube Size: 0.5
   - Spawn Distance: 50
   - Move Speed: 5
   - Lane Distance: 3

### 3.2 TrackGenerator

1. **GameObject → Create Empty**
2. **Umbenennen**: `TrackGenerator`
3. **TrackGenerator.cs hinzufügen**
4. **Im Inspector konfigurieren**:
   - Track Segment Prefab: → TrackSegment.prefab
   - Segment Length: 20
   - Visible Segments: 5
   - Player: → Player (später zuweisen)

### 3.3 UIManager

1. **GameObject → UI → Canvas**
2. **Canvas Settings**:
   - Render Mode: Screen Space - Overlay
   - UI Scale Mode: Scale With Screen Size
   - Reference Resolution: 1080 x 1920
3. **UIManager.cs zum Canvas hinzufügen**

---

## 📺 Schritt 4: UI Elemente erstellen

Alle UI-Elemente als Kinder des Canvas erstellen:

### 4.1 Score Text

1. **Rechtsklick auf Canvas → UI → Text - TextMeshPro**
2. **Umbenennen**: `ScoreText`
3. **Rect Transform**:
   - Anchor: Top Left
   - Position: (20, -20)
   - Width: 300, Height: 50
4. **TextMeshPro Settings**:
   - Text: "Punkte: 0"
   - Font Size: 36
   - Color: Weiß
   - Alignment: Left

### 4.2 Matches Text

1. **UI → Text - TextMeshPro**
2. **Umbenennen**: `MatchesText`
3. **Rect Transform**:
   - Anchor: Top Left
   - Position: (20, -70)
4. **Text**: "Matches: 0"

### 4.3 Feedback Text

1. **UI → Text - TextMeshPro**
2. **Umbenennen**: `FeedbackText`
3. **Rect Transform**:
   - Anchor: Center
   - Position: (0, 100)
   - Width: 600, Height: 80
4. **TextMeshPro Settings**:
   - Font Size: 48
   - Color: Grün (#00FF00)
   - Alignment: Center
5. **GameObject deaktivieren** (wird per Script aktiviert)

### 4.4 Active Rule Text

1. **UI → Text - TextMeshPro**
2. **Umbenennen**: `ActiveRuleText`
3. **Rect Transform**:
   - Anchor: Top Center
   - Position: (0, -20)
   - Width: 800, Height: 60
4. **Text**: "🔍 Suche: ..."
5. **Alignment**: Center

### 4.5 UIManager Referenzen verbinden

Im **Canvas → UIManager (Script)**:

- Score Text: → ScoreText
- Matches Text: → MatchesText
- Feedback Text: → FeedbackText
- Active Rule Text: → ActiveRuleText

---

## 🎨 Schritt 5: Materials erstellen

### 5.1 Voxel Material

1. **Assets → Create → Material**
2. **Name**: `VoxelMaterial`
3. **Speichern in**: `Assets/Materials/`
4. **Shader**: URP/Lit
5. **Settings**:
   - Surface Type: Opaque
   - Emission: ✅ aktivieren (leichtes Glühen)
   - Emission Color: Weiß mit niedriger Intensity (0.2)

### 5.2 Track Material

(Falls noch nicht erstellt)

1. **Assets → Create → Material**
2. **Name**: `TrackMaterial`
3. **Base Color**: Dunkelgrau (#2A2A2A)
4. **Smoothness**: 0.3

---

## 📷 Schritt 6: Kamera einrichten

### Main Camera konfigurieren

1. **Main Camera auswählen**
2. **Transform**:
   - Position: (0, 5, -10)
   - Rotation: (20, 0, 0)
3. **Camera Settings**:
   - Clear Flags: Solid Color
   - Background: Dunkelblau (#1A1A2E)
   - Field of View: 60

### Optional: Cinemachine (empfohlen)

1. **Window → Package Manager → Cinemachine installieren**
2. **GameObject → Cinemachine → Virtual Camera**
3. **Follow**: → Player
4. **Body**: Framing Transposer
   - Tracked Object Offset: (0, 2, 0)
   - Camera Distance: 10
   - Screen Y: 0.4

---

## 🔗 Schritt 7: Referenzen verbinden

Jetzt alle Cross-Referenzen im Inspector setzen:

### GameManager (VoxelStructureSpawner)

- Player: → Player GameObject

### TrackGenerator

- Player: → Player GameObject

### Player (PlayerController)

- (Alle SerializeField-Werte prüfen)

---

## 🏷️ Schritt 8: Tags & Layers

1. **Edit → Project Settings → Tags and Layers**
2. **Tag hinzufügen**: `Player` (falls nicht vorhanden)
3. **Player GameObject**: Tag = `Player`

---

## 💡 Schritt 9: Lighting & Post-Processing

### Directional Light

1. **Directional Light auswählen/erstellen**
2. **Rotation**: (50, -30, 0)
3. **Color**: Warmweiß
4. **Intensity**: 1.0

### Optional: Post-Processing

1. **Window → Package Manager → Post Processing**
2. **Volume hinzufügen** (Global)
3. **Effekte**: Bloom, Color Grading (Sci-Fi Look)

---

## ✅ Schritt 10: Testen

### Quick-Test Checkliste

1. **Play drücken** im Editor
2. **A/D Tasten** → Player wechselt Lanes
3. **Console** → Keine Errors, Log-Meldungen von ApiClient
4. **Track** → Segmente spawnen vor dem Spieler
5. **Falls MCP-Server läuft**: Voxel-Strukturen erscheinen

### Debug ohne Server

Falls kein MCP-Server verfügbar, Test-Voxels manuell spawnen:

```csharp
// Temporär in GameManager.Start() hinzufügen:
void Start()
{
    // ... bestehender Code ...

    // TEST: Manuelles Voxel spawnen
    VoxelData testData = new VoxelData
    {
        paper_id = "test123",
        section = "abstract",
        voxel_positions = new List<VoxelPosition>
        {
            new VoxelPosition { x = 0, y = 0, z = 0 },
            new VoxelPosition { x = 1, y = 0, z = 0 },
            new VoxelPosition { x = 0, y = 1, z = 0 },
            new VoxelPosition { x = 1, y = 1, z = 0 },
        },
        color = new ColorData { r = 1f, g = 0.5f, b = 0f },
        embedding = new float[768] // Dummy
    };
    voxelSpawner.SpawnFromData(testData);
}
```

---

## 📱 Schritt 11: Android Build vorbereiten

1. **File → Build Settings**
2. **Platform**: Android → Switch Platform
3. **Player Settings**:
   - Company Name: `SciDiffReview`
   - Product Name: `PaperRun`
   - Bundle Identifier: `com.scidiffreview.paperrun`
   - Minimum API Level: 24
   - Target API Level: 33
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64

4. **Internet Permission**:
   - Other Settings → Internet Access: Require

---

## 📁 Finale Ordnerstruktur

Nach Abschluss sollte die Struktur so aussehen:

```
Assets/
├── Materials/
│   ├── PlayerMaterial.mat
│   ├── TrackMaterial.mat
│   └── VoxelMaterial.mat
├── Prefabs/
│   ├── Environment/
│   │   ├── Player.prefab
│   │   └── TrackSegment.prefab
│   ├── UI/
│   └── Voxels/
│       └── VoxelCube.prefab
├── Scenes/
│   ├── Game.unity ⭐ (Hauptszene)
│   └── SampleScene.unity
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs ✅
│   │   └── RuleMatcher.cs ✅
│   ├── Game/
│   │   ├── PlayerController.cs ✅
│   │   └── TrackGenerator.cs ✅
│   ├── Networking/
│   │   └── ApiClient.cs ✅
│   ├── UI/
│   │   └── UIManager.cs ✅
│   └── Voxel/
│       ├── CollectiblePaper.cs ✅
│       ├── VoxelMover.cs ✅
│       └── VoxelStructureSpawner.cs ✅
└── Settings/
```

---

## ⚠️ Häufige Fehler

| Fehler                                         | Lösung                                                |
| ---------------------------------------------- | ----------------------------------------------------- |
| `NullReferenceException: GameManager.Instance` | GameManager ist nicht in der Scene                    |
| `NullReferenceException: UIManager.Instance`   | UIManager/Canvas fehlt                                |
| Player bewegt sich nicht                       | PlayerController Script nicht attached                |
| Voxels erscheinen nicht                        | Cube Prefab nicht zugewiesen im VoxelStructureSpawner |
| API Errors                                     | MCP-Server läuft nicht auf localhost:8089             |
| Player-Tag nicht gefunden                      | Tag "Player" nicht erstellt                           |

---

## 🚀 Nächste Schritte (Optional)

Nach dem Grundsetup kannst du erweitern:

1. **Particle Effects**: Collection-Effekt als Prefab erstellen
2. **Audio**: Background Music + Sound Effects
3. **Cinemachine**: Smoothere Kamera-Bewegung
4. **Obstacles**: Hindernisse auf dem Track
5. **Leaderboard Scene**: Highscore-Anzeige
6. **Main Menu Scene**: Startbildschirm

---

**Viel Erfolg beim Setup! 🎮**
