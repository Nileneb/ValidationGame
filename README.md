# ValidationGame

A Unity game for distributed scientific paper validation through visual voxel comparison.

**Core concept:** BioBERT embeddings (768-dim) become 8×8×12 voxel grids. Players visually compare paper shapes to rule shapes — no LLM needed for classification.

---

## Quick Start

### Prerequisites
- Unity 6.3 LTS (URP)
- Python backend running ([mcp-paperstream](https://github.com/Nileneb/mcp-paperstream))
- Android device or Unity Editor for testing

### Setup
1. Clone the repo
2. Open in Unity Hub
3. Load `Assets/Scenes/Game.unity`
4. Follow Scene Setup below
5. Press Play!

---

## Scene Setup Guide

### Step 1: Create Core GameObjects

Create these empty GameObjects in your scene hierarchy:

```
Hierarchy:
├── Main Camera
├── Directional Light
├── ------- MANAGERS -------
├── GameManager          [Add: GameManager.cs]
├── ApiClient            [Add: ApiClient.cs]
├── UIManager            [Add: UIManager.cs]
├── ------- GAME -------
├── Player               [Add: PlayerController.cs]
├── TrackGenerator       [Add: VoxelStructureSpawner.cs]
├── RuleDisplay          [Add: RuleReferenceUI.cs]
├── ------- UI -------
└── Canvas
    ├── ScoreText        [TextMeshPro]
    ├── RuleQuestionText [TextMeshPro]
    └── ComboText        [TextMeshPro]
```

### Step 2: Create Prefabs

#### Voxel Cube Prefab
1. `GameObject → 3D Object → Cube`
2. Scale: `(0.4, 0.4, 0.4)`
3. Remove Collider (optional, for performance)
4. Drag to `Assets/Prefabs/VoxelCube.prefab`
5. Delete from scene

#### Player Setup
1. `GameObject → 3D Object → Capsule`
2. Rename to "Player"
3. Position: `(0, 1, 0)`
4. Tag: "Player" (create tag if needed)
5. Add `PlayerController` component
6. Add `Rigidbody` (Is Kinematic: ✓)
7. Add `CapsuleCollider` (Is Trigger: ✓)

### Step 3: Configure Components

#### GameManager
```
Inspector:
├── Api Client: [drag ApiClient]
├── Voxel Spawner: [drag TrackGenerator]
├── Rule Matcher: (auto-created)
├── Job Fetch Interval: 30
└── Auto Connect On Start: ✓
```

#### ApiClient
```
Inspector:
├── Server Url: http://127.0.0.1:8089
├── Auto Connect SSE: ✓
├── SSE Reconnect Delay: 5
└── Log Requests: ✓ (for debugging)
```

#### VoxelStructureSpawner (on TrackGenerator)
```
Inspector:
├── Voxel Prefab: [drag VoxelCube prefab]
├── Voxel Material: [create/assign URP Lit material]
├── Player Transform: [drag Player]
├── Voxel Scale: 0.4
├── Spawn Distance: 40
├── Move Speed: 8
├── Lane Distance: 3
└── Lane Count: 3
```

#### PlayerController
```
Inspector:
├── Move Speed: 10
├── Lane Distance: 3
├── Lane Change Speed: 10
└── Swipe Threshold: 50 (for touch)
```

#### RuleReferenceUI (on RuleDisplay)
```
Inspector:
├── Voxel Prefab: [drag VoxelCube prefab]
├── Preview Position: (-10, 5, 20)
├── Rotation Speed: 15
├── Voxel Scale: 0.3
└── Show Negative Chunk: ✓
```

#### UIManager
```
Inspector:
├── Score Text: [drag ScoreText]
├── Rule Question Text: [drag RuleQuestionText]
└── Combo Text: [drag ComboText]
```

### Step 4: Camera Setup

Position the Main Camera to see the track:
```
Position: (0, 8, -10)
Rotation: (30, 0, 0)
```

Or add a simple follow script:
```csharp
public Transform target;
public Vector3 offset = new Vector3(0, 8, -10);
void LateUpdate() {
    transform.position = target.position + offset;
}
```

---

## Offline Testing (No Server)

1. Add `TestDataLoader` component to any GameObject
2. Set `Load On Start: ✓`
3. Play → Sample voxel data loads from `StreamingAssets/TestData/`
4. Check Console for loaded papers/rules

Test data includes:
- `paper_rct_abstract.json` — RCT paper voxels
- `paper_review_abstract.json` — Review paper voxels
- `rule_is_rct.json` — "Is this an RCT?" rule with pos/neg shapes

---

## Game Flow

```
┌─────────────────────────────────────────────────────────────┐
│  1. STARTUP                                                  │
│     GameManager.Start()                                      │
│     └── ApiClient.FetchActiveRule()                          │
│         └── RuleReferenceUI shows 3D rule shapes             │
├─────────────────────────────────────────────────────────────┤
│  2. JOB STREAM                                               │
│     ApiClient.ConnectJobStream() ← SSE                       │
│     └── OnJobReceived(job)                                   │
│         └── VoxelStructureSpawner.SpawnFromChunk()           │
├─────────────────────────────────────────────────────────────┤
│  3. GAMEPLAY                                                 │
│     Voxel structures move toward player                      │
│     Player sees: "Does this look like the rule?"             │
│     └── Collect (collide) = YES vote                         │
│     └── Ignore (let pass) = NO vote                          │
├─────────────────────────────────────────────────────────────┤
│  4. SUBMIT                                                   │
│     OnTriggerEnter → ApiClient.SubmitJobResponse()           │
│     └── Server aggregates consensus                          │
│     └── Returns points + feedback                            │
└─────────────────────────────────────────────────────────────┘
```

---

## Controls

| Input | Action |
|-------|--------|
| A / ← | Move left lane |
| D / → | Move right lane |
| Swipe Left | Move left (touch) |
| Swipe Right | Move right (touch) |

---

## Project Structure

```
Assets/
├── Scenes/
│   └── Game.unity              # Main game scene
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs      # Central game loop
│   │   └── RuleMatcher.cs      # Tracks player decisions
│   ├── Data/
│   │   ├── VoxelGrid.cs        # 8×8×12 grid structure
│   │   ├── Chunk.cs            # Embedding + voxels container
│   │   └── Molecule.cs         # Paper/Rule molecule
│   ├── Voxel/
│   │   ├── EmbeddingToVoxel.cs # ⭐ Core transformation
│   │   └── VoxelStructureSpawner.cs
│   ├── Networking/
│   │   └── ApiClient.cs        # REST + SSE
│   ├── UI/
│   │   ├── UIManager.cs
│   │   └── RuleReferenceUI.cs  # 3D rule preview
│   ├── Game/
│   │   └── PlayerController.cs
│   └── Debug/
│       └── TestDataLoader.cs   # Offline testing
├── Prefabs/
│   └── VoxelCube.prefab
├── Materials/
│   └── VoxelMaterial.mat       # URP Lit
└── StreamingAssets/
    └── TestData/               # Sample JSON files
```

---

## DATAMODEL.md Contract

This game implements the `DATAMODEL.md` contract shared with the Python backend.

**Critical:** `EmbeddingToVoxel.cs` must produce identical voxel grids as `data_model.py`!

### Transformation Formula
```
Embedding[i] → Voxel[x,y,z]
where: i = x + y*8 + z*64
grid: 8×8×12 = 768 positions
```

### Visual Contrast Enhancement
```csharp
centered = value - mean
amplified = tanh(centered * 2.0)
result = (amplified + 1.0) / 2.0
```

---

## Troubleshooting

### Voxels don't spawn
- Check Console for API errors
- Verify server is running on correct port
- Test with `TestDataLoader` first

### Player doesn't move
- Ensure PlayerController is attached
- Check Input settings (Edit → Project Settings → Input Manager)
- Verify Rigidbody is Kinematic

### Rule preview not showing
- Check RuleReferenceUI has voxel prefab assigned
- Verify API returns rule with chunks
- Check preview position is in camera view

### Colors are wrong
- Ensure URP is properly configured
- Check material uses URP/Lit shader
- Verify Chunk.color values are in 0-1 range

---

## Building for Android

1. `File → Build Settings → Android`
2. Switch Platform
3. Player Settings:
   - Minimum API Level: 24
   - Target API Level: 33
   - Scripting Backend: IL2CPP
4. Build & Run

---

## Links

- **Backend:** [mcp-paperstream](https://github.com/Nileneb/mcp-paperstream)
- **Contract:** See `DATAMODEL.md` in repo root
- **Unity Version:** 6.3 LTS with URP
