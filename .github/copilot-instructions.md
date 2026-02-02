# Copilot Instructions: ValidationGame (PaperRun) × mcp-paperstream

## 🎮 Project Overview

**ValidationGame** is an Android endless runner game gamifying scientific paper validation through 3D voxel structures. Players collect chunk-based voxel representations of paper sections and match them against semantic rules using BioBERT embedding similarity.

**Backend Architecture**: mcp-paperstream (Docker) runs distributed BioBERT embedding computation across IoT devices (ESP32, RPi, smartphones) using SSE + REST coordination. The server creates rule preview containers first, then paper embeddings, mapping 768-dim vectors to 8×8×12 voxel grids for visualization.

### Key Data Flow

```
N8N (paper-search-mcp)
  ↓ [finds PDF, saves to /shared/papers]
paperstream-MCP (Docker)
  ├─ load_default_rules() → 17 rule-embeddings (Preview Chunks)
  ├─ process_paper() → PDF→Sections→Embeddings→Voxel-Grid
  └─ send Jobs (Rule-B64 + Paper-B64 embeddings) via REST
Android Devices
  ├─ receive Jobs (Base64 embeddings)
  ├─ compute cosine-similarity (Paper vs Rules)
  └─ send results: "Rule 1,3,5 matched in Section abstract"
GameManager
  └─ spawn validated chunks → player collects → matches UI feedback
```

---

## 🏗️ Core Data Model: Text → Embedding → Chunk → Voxels

### CRITICAL Hierarchy

```
TEXT (Papers & Rules)
  ↓ BioBERT 768-dim
EMBEDDING (Base64 encoded for transport: embedding_b64)
  ↓ Container
CHUNK (invisible cube; holds embedding + voxels)
  └─ connects_to: [] (wire connections within chunk ONLY)
VOXELS (8×8×12 grid = 768 values, visible as cubes)
  └─ wires: connect voxels showing embedding structure
```

### Unified Chunk Structure (Papers AND Rules)

Both paper sections and rules are represented as **chunks** with identical structure:

```json
{
  "chunk_id": 0,
  "chunk_type": "abstract" | "methods" | "results" | "positive" | "negative",
  "text_preview": "First 500 chars...",
  "embedding_b64": "base64-encoded-768-float32",
  "voxels": {
    "grid_size": [8, 8, 12],
    "voxels": [[x, y, z, value], ...],
    "voxel_count": 384,
    "fill_ratio": 0.5
  },
  "color": {"r": 0.2, "g": 0.9, "b": 0.3},
  "position": {"x": 0.0, "y": 0.0, "z": 0.0},
  "connects_to": [1, 2]
}
```

**KEY DISTINCTIONS:**

- **Paper Chunks**: One per PDF section (abstract, methods, results, etc.) — `chunk_type` matches section name
- **Rule Chunks**: Two per rule — `chunk_type` = "positive" (green) or "negative" (red)
- **Wires**: Connect VOXELS within a chunk (showing embedding dimension relationships), NOT chunks to each other
- **Position**: Defines local origin (0,0,0) for voxels spawned inside that chunk

### Serializable Data Classes for Unity

```csharp
[System.Serializable]
public class Chunk
{
    public int chunk_id;
    public string chunk_type;
    public string text_preview;
    public string embedding_b64;        // Base64-encoded 768-float32 array
    public VoxelGrid voxels;
    public ColorData color;
    public Vector3Data position;
    public int[] connects_to;
}

[System.Serializable]
public class VoxelGrid
{
    public int[] grid_size;             // [8, 8, 12]
    public int[][] voxels;             // [[x, y, z, value], ...]
    public int voxel_count;
    public float fill_ratio;
}

[System.Serializable]
public class ValidationResult
{
    public string paper_id;
    public string paper_section;
    public string rule_id;
    public string rule_type;            // "positive" or "negative"
    public bool is_match;
    public float cosine_similarity;
    public int points_earned;
    public string device_id;
}
```

---

## ⚙️ Critical Patterns & Workflows

### 1. Rule Preview vs Paper Validation

**Rule Preview (Static, Loaded First):**

- paperstream loads 17 predefined rules on startup → `load_default_rules()`
- Each rule = 2 chunks (positive phrase embedding + negative phrase embedding)
- Color: green (positive), red (negative)
- RulePreview UI shows static 3D visualization of expected chunk structure
- Used as comparison target for matching

**Paper Validation (Dynamic, Per Job):**

- N8N downloads PDF → paperstream extracts sections → `process_paper()`
- Each section becomes a chunk (abstract, methods, results, discussion, conclusion, etc.)
- Paper embeddings generated via BioBERT on distributed IoT workers
- Each paper chunk spawns as collectible in game
- Player collision triggers similarity comparison against all active rules

### 2. BioBERT Distributed Embedding Pipeline

**BioBERT Configuration** (`paperstream/config.yaml`):

- Model: `dmis-lab/biobert-base-uncased-v1.2` (768 dimensions)
- Tokenization via HuggingFace transformers
- Partial layer extraction for IoT capability levels:
  ```
  LOW    (ESP32):     layers [0]        # Embedding layer only
  MEDIUM (RPi4):      layers [0,1,2]    # First 3 transformer layers
  HIGH   (modern):    layers [0-5]      # All 6 layers
  ```

**Handler Usage** (when extending embedding logic):

```python
from paperstream.handlers import get_biobert_handler
handler = get_biobert_handler()
tokens, token_ids = handler.tokenize("scientific text")
embedding_768 = handler.embed("text", layer_range=(0, 6))  # Full embedding
# Returns float32 array, length 768
```

**Base64 Transport**:

- Embeddings encoded as Base64 UTF-8 strings for JSON safety
- Decode in Unity: `Convert.FromBase64String(embedding_b64) → float[]` via BinaryReader

### 3. Cosine Similarity Matching (Game Core Mechanic)

When player collects a paper chunk:

1. Decode chunk's `embedding_b64` → float[768]
2. Compare against all active rules' embeddings using cosine similarity:
   ```
   similarity = (a·b) / (|a| × |b|)
   ```
3. If `similarity ≥ rule.threshold`:
   - Match type: positive rule match → +100 × similarity points
   - Match type: negative rule match → penalize (shows conflict)
4. If no match: +10 consolation points

**Critical**: Do NOT modify the cosine similarity formula. Adjust thresholds via server rule config (`config.yaml`).

### 4. Chunk Spawning Pipeline

**In GameManager:**

1. `ApiClient.FetchJobs()` requests batch of 10 chunks from paperstream
2. Chunks enqueued in `jobQueue` (prevents spawn lag)
3. Each frame: `Update()` probabilistically spawns chunk via `ChunkSpawner.SpawnFromJson()`
4. `ChunkSpawner.SpawnFromJson()`:
   - Instantiate parent cube (invisible, at `chunk.position`)
   - Decode `embedding_b64` → float[768]
   - Reshape to 8×8×12 grid
   - Instantiate visible cube per voxel at `parent.position + voxel[x,y,z]`
   - Apply voxel color (from `chunk.color`)
   - Draw LineRenderer wires between adjacent voxels (showing embedding connectivity)
5. `VoxelMover` translates chunk toward player at `-moveSpeed`
6. `CollectibleChunk` trigger on player contact → `OnChunkCollected()` callback

**Wires (Visual Enhancement):**

- Use LineRenderer component on parent chunk
- Connect voxels [x,y,z] to adjacent voxels [x±1,y,z], [x,y±1,z], [x,y,z±1]
- Color: semi-transparent white (~0.3 alpha)
- Purpose: visual clustering showing embedding structure within chunk

### 5. Input Handling (Android-First)

- Primary: Touch swipe detection (`Input.touchCount` → TouchPhase)
- Fallback: Keyboard A/D for editor testing
- Swipe threshold: 50 pixels (configurable via `[SerializeField]`)
- Three lanes: 0 (left), 1 (center), 2 (right) mapped via `(lane - 1) * laneDistance`

### 6. Networking (REST to paperstream Docker)

**BaseUrl**: `http://localhost:8089` (default paperstream port)

**Endpoints**:

- `GET /api/jobs/next?device_id=...&limit=10` → returns array of Chunks
- `POST /api/validation/submit` → submits ValidationResult batch
- `GET /api/rules/active` → returns preview rule chunks (static, cached client-side)

**Coroutine Pattern** (no async/await):

```csharp
StartCoroutine(apiClient.FetchJobs(10, (chunks) =>
{
    foreach (var chunk in chunks)
        jobQueue.Enqueue(chunk);
}));
```

**Error Recovery**: Failed submits re-queue results for retry on next batch.

---

## 🎯 Common Development Tasks

### Adding a New Chunk Type

1. Extend `chunk_type` enum in paperstream's `config.yaml`
2. Assign new color in rule/section mapping
3. Update `ChunkSpawner.GetColorForChunkType()` in Unity
4. Test spawn via Editor console with A/D keyboard input

### Modifying Voxel Appearance

- **Color**: Comes from `Chunk.color` (RGB 0-1 normalized)
- **Material**: Shared `voxelMaterial` assigned in Inspector
- **Size**: `cubeSize` parameter (default 0.5) scales grid layout
- **Glow**: Shader Graph material with emission channel (multiply by voxel value for gradient effect)

### Debugging Chunk Matching

- Check `Debug.Log()` in GameManager for job queue status
- ApiClient logs all network errors with HTTP status codes
- Enable Console Profiler to monitor voxel instantiation spikes
- Manual test: Set rule threshold to 0.1 to force matches

### Server Integration: Extending BioBERT Handler

If adding new text preprocessing:

1. Edit `paperstream/handlers/biobert_handler.py`
2. Modify `tokenize()` or `embed()` method
3. Ensure output remains float32 array of length 768
4. Redeploy Docker container: `docker-compose up --build`

---

## 🔧 Build & Testing Specifics

### Docker Stack (Critical!)

mcp-paperstream runs in Docker Compose with:

- **paperstream-mcp**: Main Python server (port 8089)
- **biobert-worker**: Optional distributed embedding (SSE subscriber)
- **volumes**: `/shared/papers/` — PDF storage from N8N downloads

**Start server:**

```bash
cd /path/to/paperstream
docker-compose up -d
```

**Verify running:**

```bash
curl http://localhost:8089/api/rules/active
```

### Android Build Prerequisites

- **Unity Version**: 6.3 LTS
- **Minimum API**: Level 24 (Android 7.0)
- **Target API**: Level 33 (Android 13)
- **Graphics**: URP v17.3.0
- **IL2CPP Scripting**: Required for ARM support
- **Key Packages**: Input System (1.17), Timeline (1.8.9)

### Performance Constraints

- Voxel grids: 8×8×12 = max 384 cubes per chunk (manageable)
- Chunks destroyed when Z < player.Z - 20
- Material instantiation batched if spawning >50 voxels/frame
- Use Object Pooling for track segments (TODO)

### Testing Entry Points

- **Editor Test**: Run SampleScene, press A/D to move lanes, watch voxel spawn
- **Network Debug**: Start Docker, watch ApiClient logs for job fetches
- **Matching Debug**: Set rule threshold = 0.3, collect chunks, verify similarity calculation

---

## ⚠️ Project-Specific Conventions

### Naming

- **Lanes**: Always 3 (0, 1, 2); mapped to X axis: `(lane - 1) * laneDistance`
- **Forward Direction**: +Z axis (player always moves forward)
- **Scoring**: Match (positive rule) = 100 × similarity; no match = 10 points
- **Chunks**: Named `Chunk_{paper_id}_{section}` or `Preview_Rule_{rule_id}_{type}`

### Code Style

- **[SerializeField]** for all configurable values
- **German comments** (project team German-speaking)
- **Flat component design** — Singletons for GameManager, UIManager, ApiClient
- **JSON serialization**: Use C# [System.Serializable] classes only; no Newtonsoft

### Known Limitations

- **UI**: TextMeshPro only; UGUI Canvas partial
- **VFX**: Particle system scaffolding exists; not implemented
- **Audio**: Stubbed (references exist, no audio files)
- **Leaderboard**: Scene not created (TODO)
- **RulePreview UI**: Static 3D model reference not yet integrated

---

## 📋 Quick Checklist for New Contributors

Before implementing features:

- [ ] Verify paperstream Docker running: `curl http://localhost:8089/api/rules/active`
- [ ] Create script in correct `Scripts/` subfolder
- [ ] Use `[SerializeField]` for tunables
- [ ] Add German comments for logic
- [ ] Register with GameManager/UIManager/ApiClient Singleton
- [ ] Test keyboard fallback (A/D) in Editor before mobile build
- [ ] Check ApiClient console logs for network errors
- [ ] Ensure cosine similarity logic preserved
- [ ] Test chunk cleanup when player passes (no memory leak)

---

## 🔗 Key Entry Points

- **Backend Docs**: paperstream `README.md` in docker container
- **Game Flow**: `GameManager.Start()` → loads rules, fetches jobs, manages scoring
- **Chunk Spawning**: `ChunkSpawner.SpawnFromJson()` → decodes embedding, instantiates voxels
- **Matching Logic**: `RuleMatcher.CheckMatch()` → cosine similarity, scoring
- **Networking**: `ApiClient.FetchJobs()` + `ApiClient.SubmitResults()` → REST coordination
- **Input**: `PlayerController.HandleInput()` → swipe + keyboard lanes
