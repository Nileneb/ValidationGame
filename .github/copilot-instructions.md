# Copilot Instructions: ValidationGame (PaperRun)

## 🎮 Project Overview

**ValidationGame** is an Android endless runner game that gamifies scientific paper validation through 3D voxel structures. Players collect procedurally-generated voxel representations of paper sections and match them against semantic rules using embedding-based similarity matching. The game bridges an MCP server backend (running research validation tasks) with Unity gameplay.

### Key Architecture

- **Backend**: MCP server providing JSON voxel data (`VoxelData`), validation rules (`RuleData`), and scoring
- **Game Loop**: Player moves forward automatically → collects voxels → matches embeddings → earns points
- **Data Flow**: Server → ApiClient → GameManager → VoxelSpawner/RuleMatcher → UIManager

---

## 🏗️ Essential Project Structure

### Data Classes (Serializable JSON contracts)

- **`VoxelData`**: `paper_id`, `section`, `voxel_positions[]`, `color`, `embedding` (float array)
- **`RuleData`**: `rule_id`, `question`, `pos_embedding`, `neg_embedding`, `threshold`
- **`ValidationResult`**: Captured when paper collected; includes similarity score and points earned

### Script Organization (follow this when creating new scripts)

```
Assets/Scripts/
├── Core/          # GameManager, RuleMatcher, Singletons
├── Game/          # PlayerController, TrackGenerator
├── Networking/    # ApiClient (REST via UnityWebRequest)
├── UI/            # UIManager
└── Voxel/         # VoxelStructureSpawner, CollectiblePaper, VoxelMover
```

### Critical Scenes

- Only **SampleScene.unity** exists (starter template); must create **Game.unity** as main playable scene

---

## ⚙️ Critical Patterns & Workflows

### Embedding-Based Matching

**This is the game's core mechanic:**

1. Voxel data arrives with `float[] embedding` (semantic vector from server)
2. Current `RuleData` provides `pos_embedding` and optional `neg_embedding`
3. `RuleMatcher.CosineSimilarity()` computes: similarity = (a·b) / (|a||b|)
4. If similarity ≥ `rule.threshold` → match → points = `100 × similarity`
5. If no match → still 10 consolation points

**When modifying matching logic:** Preserve cosine similarity formula; adjust thresholds via server rules, not code.

### Voxel Spawning Pipeline

1. **GameManager** periodically calls `ApiClient.FetchJobs()` (batches of 10 voxels)
2. Jobs enqueued in `jobQueue` (prevents spawn lag spikes)
3. **Update()** probabilistically spawns: `Random.value < 0.1f * deltaTime`
4. `VoxelStructureSpawner.SpawnFromJson()` instantiates parent + child cubes
5. Each cube gets material with voxel's RGB color
6. **VoxelMover** translates structure toward player at `-moveSpeed`
7. **CollectiblePaper** trigger on player contact → `OnPaperCollected()` callback

**When adding obstacles/variants:** Don't instantiate new copies; use object pooling on the Track prefab.

### Input Handling (Android-first design)

- Primary: **Touch swipe detection** (`Input.touchCount` → TouchPhase)
- Fallback: **Keyboard A/D** for editor testing
- Swipe threshold default: **50 pixels** (configurable via inspector)
- Three lanes: 0 (left), 1 (center), 2 (right) with `laneDistance` spacing

**When adding input:** Use `Input` module (legacy) since new InputSystem package present but not enforced; swipes fire `MoveLane(direction)`.

### Networking (No Offline Support Yet)

- **BaseUrl**: `http://localhost:8765` (dev MCP server)
- **Device ID**: Generated from `SystemInfo.deviceUniqueIdentifier`, cached in `PlayerPrefs`
- **Endpoints**: `/api/jobs/next`, `/api/validation/submit`, `/api/rules/active`
- **Coroutines**: All requests use `IEnumerator` (no async/await)
- **Error recovery**: Failed submits re-queue results for retry

**When extending networking:** Match existing JSON contracts; add retry backoff if production needs it.

---

## 🎯 Common Development Tasks

### Adding a New Game Feature

1. Create script in appropriate `Scripts/` subfolder
2. If it manages state: use Singleton pattern like `GameManager`/`UIManager`
3. If it's procedural: attach to scene via `[SerializeField]` reference (no dynamic loading)
4. Register callbacks through `GameManager.Instance` for cross-system communication

### Modifying Voxel Appearance

- **Color**: Comes from `VoxelData.color` (RGB 0-1 normalized)
- **Material**: Shared `voxelMaterial` (assign in Inspector)
- **Size**: `cubeSize` parameter (default 0.5) scales all voxel positions
- **Glow effect**: Add shader to `voxelMaterial` (use Shader Graph; no custom shaders in repo yet)

### Debugging Gameplay

- Check `Debug.Log()` calls in GameManager for job queue status
- ApiClient logs all network failures with error codes
- Use `PlayerController` keyboard fallback (A/D keys) in Editor to test lanes without touch
- Enable Console Profiler to monitor GameObject instantiation spikes

### Server Integration Changes

If MCP server changes API response format:

1. Update `[System.Serializable]` class in ApiClient.cs
2. Ensure field names match server JSON exactly (case-sensitive)
3. Test with `JsonUtility.FromJson<T>()` on sample payload

---

## 🔧 Build & Testing Specifics

### Android Build Prerequisites

- **Unity Version**: 6.3 LTS (already set; validates in `ProjectSettings/`)
- **Minimum API**: Level 24 (Android 7.0)
- **Target API**: Level 33 (Android 13)
- **Graphics**: URP (Universal Render Pipeline v17.3.0)
- **IL2CPP Scripting**: Required for ARMv7/ARM64 support
- **Key Packages**: Input System (1.17), Timeline (1.8.9), AI Navigation (2.0.9)

### Performance Constraints

- Avoid dynamic physics queries in Update(); use trigger colliders only
- Voxel structures destroyed when Z < player.Z - 20; don't increase this range significantly
- Material instantiation happens per-voxel; batch material creation if spawning >50 voxels/frame
- Use Object Pooling instead of Instantiate/Destroy for track segments (not yet implemented; TODO)

### Testing Entry Points

- **Game Scene**: Run SampleScene; verify PlayerController responds to A/D
- **Network**: Start local MCP server; watch ApiClient console logs for job fetches
- **Matching**: Manually set `currentActiveRule` threshold to 0.5 to force matches

---

## ⚠️ Project-Specific Conventions

### Naming & Defaults

- **Lanes**: Always 3; stored as int 0-2, mapped to X via `(lane - 1) * laneDistance`
- **Forward Direction**: +Z axis (common 3D game convention; player always moves forward)
- **Scoring**: Match=100×similarity; miss=10 points
- **Device Tracking**: All validation results tagged with `SystemInfo.deviceUniqueIdentifier`

### Code Style (Observed)

- **Serialized fields** for all configurable values (speeds, distances, thresholds)
- **Comments in German** (project team is German-speaking); match this in new code
- **Simple inheritance**: No complex hierarchies; mostly flat component design
- **No external packages**: Uses only built-in Unity + URP; Newtonsoft JSON mentioned in TODO but not imported yet

### Known Limitations

- **UI System**: TextMeshPro only; no UGUI Canvas yet (see UIManager TODO)
- **Voxel Effects**: Placeholder particle system referenced; not implemented
- **Audio**: All audio stubbed (reference objects but no .wav/.mp3 files)
- **Leaderboard Scene**: Referenced in TODO but not created
- **3D Template**: Project uses 6.3 LTS 3D template, but only SampleScene present (Game.unity TODO)

---

## 📋 Quick Checklist for New Contributors

When implementing a feature from the TODO list:

- [ ] Create script in correct `Scripts/` subfolder
- [ ] Use `[SerializeField]` for tunables; expose via Inspector
- [ ] Add German comments for non-obvious logic
- [ ] Register with relevant Manager (GameManager/UIManager/ApiClient)
- [ ] Test with keyboard fallback in Editor before mobile build
- [ ] Check console logs for network/instantiation errors
- [ ] Don't break the cosine similarity matching pipeline
- [ ] Ensure voxel cleanup when player passes (avoid memory leaks)

---

## 🔗 Key Entry Points for Exploration

- **Start Here**: [unity_TODO.md](../../unity_TODO.md) — comprehensive feature spec with C# pseudocode
- **Game Flow**: `GameManager.Start()` → initializes rules, fetches jobs, spawns, submits results
- **Matching Logic**: `RuleMatcher.CheckMatch()` → implements embedding similarity decision
- **Networking Layer**: `ApiClient.FetchJobs()` and `ApiClient.SubmitResults()` — all server communication
- **Player Input**: `PlayerController.HandleInput()` → swipe + keyboard lane changes
