# Copilot Instructions: ValidationGame

## Project Overview

**ValidationGame** is a Unity game where players visually validate scientific papers by comparing 3D voxel structures. Papers and validation rules are represented as voxel shapes — players collect papers that "look like" the target rule.

**Core mechanic:** Visual pattern matching replaces expensive LLM classification.

---

## DATAMODEL.md Contract (CRITICAL!)

The `DATAMODEL.md` file (in workspace root) defines the shared contract between Python backend and Unity frontend.

### Key Principle
**The embedding → voxel transformation MUST be identical in Python and C#!**

### Voxel Grid
- **Dimensions:** 8×8×12 = 768 voxels (matches BioBERT embedding size)
- **Mapping:** `i = x + y*8 + z*64`
- **Threshold:** Default 0.3 (value >= threshold → voxel visible)

### Visual Contrast Enhancement (must match Python exactly!)
```csharp
float centered = value - mean;
float amplified = (float)Math.Tanh(centered * 2.0);
float result = (amplified + 1.0f) / 2.0f;
```

---

## Project Structure

```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs       # Main game loop, job queue
│   └── RuleMatcher.cs       # Simplified: just tracks player actions
├── Data/
│   ├── VoxelGrid.cs         # 8×8×12 grid data structure
│   ├── Chunk.cs             # Container: embedding + voxels + color
│   └── Molecule.cs          # Paper (chain) or Rule (dipole)
├── Voxel/
│   ├── EmbeddingToVoxel.cs  # ⭐ DATAMODEL.md transformation
│   └── VoxelStructureSpawner.cs  # Instantiates 3D voxel cubes
├── Networking/
│   └── ApiClient.cs         # REST + SSE communication
├── UI/
│   ├── UIManager.cs         # Score, combo, UI updates
│   └── RuleReferenceUI.cs   # Shows target rule as 3D preview
├── Game/
│   └── PlayerController.cs  # Lane switching, touch input
└── Debug/
    └── TestDataLoader.cs    # Loads offline test data

Assets/StreamingAssets/
└── TestData/                # Sample JSON for offline testing
```

---

## Data Classes

### Molecule (Paper or Rule)
```csharp
public class Molecule {
    public string molecule_id;
    public string molecule_type;  // "paper" or "rule"
    public string title;
    public List<Chunk> chunks;
    public MoleculeConfig molecule_config;
}
```

### Chunk (Section container)
```csharp
public class Chunk {
    public int chunk_id;
    public string chunk_type;     // "abstract", "methods", "positive", "negative"
    public string embedding_b64;  // Base64 encoded float32 array
    public ChunkVoxels voxels;    // Pre-computed voxel positions
    public ChunkColor color;
    public ChunkPosition position;
    public List<int> connects_to;
}
```

### VoxelGrid
```csharp
public class VoxelGrid {
    public int[] gridSize;  // [8, 8, 12]
    public List<Voxel> voxels;
    public int voxelCount;
    public float fillRatio;
}
```

---

## Game Flow

1. **Startup:** `GameManager` fetches active Rule via `ApiClient.FetchActiveRule()`
2. **Rule Display:** `RuleReferenceUI` shows Rule's pos/neg voxel shapes as 3D reference
3. **Job Stream:** `ApiClient.ConnectJobStream()` receives paper Chunks via SSE
4. **Spawning:** `VoxelStructureSpawner.SpawnFromChunk()` creates 3D voxel objects
5. **Player Action:** Collects (touch/collision) or ignores paper
6. **Submit:** `ApiClient.SubmitJobResponse()` sends player decision to server
7. **Scoring:** Server responds with points, consensus status

**NO client-side similarity calculation! Server validates via human consensus.**

---

## API Integration

### GET /api/rule/active
```csharp
StartCoroutine(apiClient.FetchActiveRule((molecule, threshold, question) => {
    ruleMatcher.SetActiveRule(molecule, threshold, question);
}));
```

### SSE /api/jobs/stream
```csharp
apiClient.OnJobReceived += (job) => {
    // job.chunk contains the paper Chunk to spawn
    voxelSpawner.SpawnFromChunk(job.chunk);
};
apiClient.ConnectJobStream();
```

### POST /api/jobs/{job_id}/response
```csharp
var response = new PlayerJobResponse {
    job_id = currentJob.job_id,
    device_id = apiClient.DeviceId,
    action = "collect",  // or "skip"
    response_time_ms = elapsedMs
};
StartCoroutine(apiClient.SubmitJobResponse(response, (success, result) => {
    if (result != null) AddScore(result.points);
}));
```

---

## Key Scripts

### EmbeddingToVoxel.cs
Converts 768-dim embedding to 3D voxel positions. **Must match Python exactly!**

```csharp
public static VoxelGrid CreateVoxelGrid(float[] embedding, float threshold = 0.3f, bool enhanceContrast = true)
```

### VoxelStructureSpawner.cs
Instantiates GameObjects from Chunk data.

```csharp
public GameObject SpawnFromChunk(Chunk chunk)
public GameObject SpawnFromMolecule(Molecule molecule)
```

### RuleMatcher.cs
Tracks current rule and player actions. **No similarity math — humans decide!**

```csharp
public void SetActiveRule(Molecule rule, float threshold, string question)
public void RecordPlayerAction(string jobId, PlayerAction action)
```

---

## Testing

### Offline Testing
1. Add `TestDataLoader` component to a GameObject
2. Set `loadOnStart = true`
3. Play → loads JSON from `StreamingAssets/TestData/`
4. Check Console for voxel counts

### Determinism Test
```csharp
[ContextMenu("Test Voxel Determinism")]
public void TestVoxelDeterminism()
// Compares loaded voxels vs regenerated from embedding
```

---

## Code Conventions

- **German comments OK:** Team is German-speaking
- **SerializeField:** All inspector-configurable values
- **Singleton pattern:** GameManager, UIManager, ApiClient
- **Coroutines:** All network calls (no async/await)

---

## Common Tasks

### Adding a new Chunk type
1. Add color to `SECTION_COLORS` in `EmbeddingToVoxel.cs`
2. Handle in `VoxelStructureSpawner.GetChunkColor()`
3. Update Python `data_model.py` to match

### Changing voxel appearance
1. Modify `VoxelStructureSpawner.SpawnVoxel()`
2. Adjust `voxelMaterial` or add shader effects
3. Scale via `voxelScale` parameter

### Debugging spawning issues
1. Check `TestDataLoader` Console output for voxel counts
2. Verify JSON structure matches `Chunk` class
3. Ensure `Initialize()` called after JSON deserialization

---

## Build Settings

- **Unity Version:** 6.3 LTS
- **Target:** Android (API 24+)
- **Render Pipeline:** URP
- **Input:** Legacy Input Manager (touch + keyboard fallback)
