# Unity Editor Setup - VEREINFACHT

## 🚀 AUTOMATISCHES SETUP

**Player wird AUTOMATISCH vom GameManager erstellt!**

### Setup in Unity:

1. **Game.unity** öffnen
2. Stelle sicher dass ein **GameManager** GameObject existiert mit `GameManager` Component
3. **Play drücken** → Player erscheint automatisch!

**Steuerung:** `A/D` oder `Pfeiltasten` für Spurwechsel

---

## Voxel-System Workflow

### So funktioniert es:

1. **Server sendet Jobs** mit `section_embedding_b64` (768-dim Embedding als Base64)
2. **EmbeddingToVoxel** konvertiert Embedding zu 3D Voxel-Positionen (8x8x12 Grid)
3. **VoxelStructureSpawner** spawnt die Figur vor dem Spieler
4. **RulePreview** zeigt die Ziel-Regel als rotierende 3D-Figur neben dem Spielfeld
5. **Spieler sammelt** → **RuleMatcher** vergleicht Embeddings via Cosine Similarity

### RulePreview hinzufügen:

1. **GameObject > Create Empty** → Umbenennen zu **"RulePreview"**
2. **Add Component** → `RulePreview`
3. Einstellungen:
   - Cube Size: `0.3`
   - Rotation Speed: `20`
   - Display Offset: `(-8, 3, 10)` (links vom Spieler)
   - Voxel Threshold: `0.3` (je höher, desto weniger Voxel)

---

## Cube Prefab erstellen

1. **GameObject > 3D Object > Cube**
2. Scale: `(0.5, 0.5, 0.5)`
3. In `Assets/Prefabs/` ziehen (Ordner erstellen falls nötig)
4. Original aus Hierarchy löschen

---

## GameManager Einstellungen

### Inspector prüfen:

- Api Client: `ApiClient` (Auto-Find)
- Voxel Spawner: `VoxelStructureSpawner` (Auto-Find)
- Rule Matcher: `RuleMatcher` (Auto-Find)
- Rule Preview: `RulePreview` (Auto-Find)
- Auto Spawn Player: ✅ ON

### Wichtige Werte:

- Job Fetch Interval: `30`
- Spawn Chance Per Second: `0.5`

---

## VoxelStructureSpawner Setup

Auf dem **TrackGenerator** oder einem separaten GameObject:

1. Cube Prefab: → `Assets/Prefabs/Cube`
2. Voxel Material: → Erstelle URP Lit Material (beliebige Farbe)
3. **Player**: → **Player GameObject aus Hierarchy ziehen!**

### Einstellungen:

- Cube Size: `0.5`
- Spawn Distance: `50`
- Move Speed: `5`
- Lane Distance: `3`
- Lane Count: `3`

---

## 5. UIManager Setup

Canvas sollte bereits existieren.

### UI Elemente verlinken:

- Score Text: → `ScoreText` (TextMeshPro)
- Matches Text: → (erstellen falls nötig)
- Feedback Text: → (erstellen falls nötig)
- Active Rule Text: → `ActiveRuleText` (TextMeshPro)

---

## 6. Server starten

```bash
cd mcp_server
python main.py
```

Server läuft auf `http://localhost:8089`

---

## 7. Test-Checkliste

- [ ] Player existiert in Scene (mit Capsule Mesh)
- [ ] Player hat PlayerController Component
- [ ] Player hat Tag "Player"
- [ ] Cube Prefab erstellt und in VoxelStructureSpawner verlinkt
- [ ] VoxelStructureSpawner hat Player-Referenz
- [ ] MCP Server läuft
- [ ] Play Mode → A/D Tasten funktionieren
- [ ] Voxel-Strukturen spawnen vor dem Player

---

## Vereinfachte Script-Struktur

```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs      # Zentrale Logik
│   └── RuleMatcher.cs      # Cosine Similarity
├── Game/
│   └── PlayerController.cs # Steuerung (A/D, Swipe)
├── Networking/
│   └── ApiClient.cs        # REST API
├── UI/
│   └── UIManager.cs        # HUD (Score, Feedback)
└── Voxel/
    ├── CollectiblePaper.cs # Einsammelbar
    ├── VoxelMover.cs       # Bewegung
    └── VoxelStructureSpawner.cs # Spawning
```

**KEINE** redundanten Skripte mehr:

- ~~SSEClient.cs~~ (gelöscht)
- ~~MatchValidator.cs~~ (gelöscht)
- ~~RuleFigureDisplay.cs~~ (gelöscht)
- ~~Embedding/~~ Ordner (gelöscht)
