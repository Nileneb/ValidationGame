// Scripts/Spawning/SpawnManager.cs
// Zentraler Manager für alle SpawnPoints
// Verteilt Jobs an freie SpawnPoints und verwaltet das Spawning

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SpawnManager: Verwaltet alle SpawnPoints und verteilt Jobs.
/// Ersetzt die verteilte Logik aus VoxelStructureSpawner.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("SpawnPoints")]
    [Tooltip("Liste aller SpawnPoints (automatisch gefunden wenn leer)")]
    public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    [Header("Spawn Configuration")]
    [Tooltip("Automatisch SpawnPoints finden")]
    public bool autoFindSpawnPoints = true;

    [Tooltip("Material für Voxels (wird an SpawnPoints weitergegeben)")]
    public Material voxelMaterial;

    [Tooltip("Standard Voxel-Größe")]
    public float defaultVoxelSize = 0.3f;

    [Header("Job Queue")]
    [Tooltip("Jobs die noch gespawnt werden müssen")]
    [SerializeField] private List<VoxelData> jobQueue = new List<VoxelData>();

    [Tooltip("Maximale Jobs in Queue")]
    public int maxQueueSize = 20;

    [Header("Bewegung")]
    [Tooltip("Bewegungsgeschwindigkeit der Strukturen zum Spieler")]
    public float moveSpeed = 5f;

    [Tooltip("Ziel-Transform (Player)")]
    public Transform targetTransform;

    [Header("3D Boundaries (Weltraum-Modus)")]
    [Tooltip("Maximale 3D-Distanz zum Spieler bevor Struktur despawnt")]
    public float maxDistanceFromPlayer = 150f;

    [Tooltip("Spawn-Radius um SpawnPoints (Spieler muss innerhalb sein)")]
    public float spawnPointActivationRadius = 200f;

    [Tooltip("Minimale Spawn-Distanz zum Spieler")]
    public float minSpawnDistance = 30f;

    [Tooltip("Maximale Spawn-Distanz zum Spieler")]
    public float maxSpawnDistance = 80f;

    [Header("Statistics")]
    [SerializeField] private int totalJobsProcessed = 0;
    [SerializeField] private int activeStructures = 0;

    // Aktive Strukturen die sich bewegen
    private List<MovingStructure> _movingStructures = new List<MovingStructure>();

    // Events
    public System.Action<VoxelData> OnJobQueued;
    public System.Action<SpawnPoint, VoxelData> OnJobStarted;
    public System.Action<SpawnPoint, VoxelData> OnJobCompleted;
    public System.Action<GameObject, VoxelData> OnStructureReachedPlayer;

    void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SpawnManager: Duplikat gefunden, zerstöre dieses Objekt");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // SpawnPoints finden
        if (autoFindSpawnPoints || spawnPoints.Count == 0)
        {
            FindSpawnPoints();
        }

        // SpawnPoints konfigurieren
        ConfigureSpawnPoints();

        // Queue-Processing starten
        StartCoroutine(ProcessJobQueue());

        Debug.Log($"SpawnManager: Initialisiert mit {spawnPoints.Count} SpawnPoints");
    }

    /// <summary>
    /// Findet alle SpawnPoints in Children
    /// </summary>
    private void FindSpawnPoints()
    {
        spawnPoints.Clear();

        // In Children suchen
        SpawnPoint[] found = GetComponentsInChildren<SpawnPoint>(true);
        spawnPoints.AddRange(found);

        // Falls keine gefunden, Children ohne SpawnPoint-Komponente ausstatten
        if (spawnPoints.Count == 0)
        {
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Spawn") || child.name.Contains("Point") || child.name.Contains("Paper"))
                {
                    SpawnPoint sp = child.gameObject.AddComponent<SpawnPoint>();
                    spawnPoints.Add(sp);
                    Debug.Log($"SpawnManager: SpawnPoint automatisch hinzugefügt zu '{child.name}'");
                }
            }
        }

        Debug.Log($"SpawnManager: {spawnPoints.Count} SpawnPoints gefunden");
    }

    /// <summary>
    /// Konfiguriert alle SpawnPoints mit den Manager-Einstellungen
    /// </summary>
    private void ConfigureSpawnPoints()
    {
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;

            // Material zuweisen
            if (sp.voxelMaterial == null && voxelMaterial != null)
            {
                sp.voxelMaterial = voxelMaterial;
            }

            // Voxel-Größe
            if (sp.voxelSize <= 0)
            {
                sp.voxelSize = defaultVoxelSize;
            }

            // Events registrieren
            sp.OnJobStarted += HandleJobStarted;
            sp.OnJobCompleted += HandleJobCompleted;
            sp.OnStructureReady += HandleStructureReady;
        }
    }

    /// <summary>
    /// Fügt einen Job zur Queue hinzu
    /// </summary>
    public void QueueJob(VoxelData job)
    {
        if (job == null)
        {
            Debug.LogWarning("SpawnManager: Versuch NULL-Job hinzuzufügen");
            return;
        }

        if (jobQueue.Count >= maxQueueSize)
        {
            Debug.LogWarning($"SpawnManager: Queue voll ({maxQueueSize}), Job verworfen");
            return;
        }

        jobQueue.Add(job);
        OnJobQueued?.Invoke(job);

        Debug.Log($"SpawnManager: Job '{job.job_id ?? job.paper_id}' zur Queue hinzugefügt ({jobQueue.Count} in Queue)");
    }

    /// <summary>
    /// Fügt mehrere Jobs zur Queue hinzu
    /// </summary>
    public void QueueJobs(IEnumerable<VoxelData> jobs)
    {
        foreach (var job in jobs)
        {
            QueueJob(job);
        }
    }

    /// <summary>
    /// Verarbeitet die Job-Queue kontinuierlich
    /// </summary>
    private IEnumerator ProcessJobQueue()
    {
        while (true)
        {
            // Warten falls Queue leer
            if (jobQueue.Count == 0)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Freien SpawnPoint suchen
            SpawnPoint available = GetAvailableSpawnPoint();
            if (available == null)
            {
                yield return new WaitForSeconds(0.2f);
                continue;
            }

            // Job aus Queue nehmen
            VoxelData job = jobQueue[0];
            jobQueue.RemoveAt(0);

            // Job starten
            available.StartJob(job);

            // Kurz warten bevor nächster Job
            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// Findet einen freien SpawnPoint der in Reichweite des Spielers ist (3D)
    /// </summary>
    public SpawnPoint GetAvailableSpawnPoint()
    {
        if (targetTransform == null)
        {
            // Fallback: Ersten freien nehmen
            foreach (var sp in spawnPoints)
            {
                if (sp != null && sp.IsAvailable) return sp;
            }
            return null;
        }

        Vector3 playerPos = targetTransform.position;
        SpawnPoint bestPoint = null;
        float bestDistance = float.MaxValue;

        foreach (var sp in spawnPoints)
        {
            if (sp == null || !sp.IsAvailable) continue;

            // 3D Distanz-Check: Nur SpawnPoints in Reichweite aktivieren
            float distance = Vector3.Distance(playerPos, sp.transform.position);

            if (distance <= spawnPointActivationRadius && distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = sp;
            }
        }

        return bestPoint;
    }

    /// <summary>
    /// Prüft ob ein SpawnPoint in Reichweite des Spielers ist
    /// </summary>
    public bool IsSpawnPointInRange(SpawnPoint sp)
    {
        if (targetTransform == null || sp == null) return true; // Fallback
        return Vector3.Distance(targetTransform.position, sp.transform.position) <= spawnPointActivationRadius;
    }

    /// <summary>
    /// Anzahl freier SpawnPoints
    /// </summary>
    public int GetAvailableCount()
    {
        int count = 0;
        foreach (var sp in spawnPoints)
        {
            if (sp != null && sp.IsAvailable) count++;
        }
        return count;
    }

    // === EVENT HANDLERS ===

    private void HandleJobStarted(SpawnPoint sp, VoxelData job)
    {
        totalJobsProcessed++;
        OnJobStarted?.Invoke(sp, job);
    }

    private void HandleJobCompleted(SpawnPoint sp, VoxelData job)
    {
        OnJobCompleted?.Invoke(sp, job);
    }

    private void HandleStructureReady(SpawnPoint sp, GameObject structure)
    {
        if (structure == null) return;

        // WICHTIG: Struktur von Parent lösen damit sie unabhängig bewegt werden kann!
        structure.transform.SetParent(null);

        // DEBUG: Position loggen
        Debug.Log($"SpawnManager: Struktur '{structure.name}' gespawnt bei {structure.transform.position}");

        if (targetTransform != null)
        {
            Debug.Log($"SpawnManager: Player Position = {targetTransform.position}");

            // 3D WELTRAUM-MODUS: Position relativ zum Spieler berechnen
            Vector3 playerPos = targetTransform.position;
            Vector3 structurePos = structure.transform.position;
            float currentDistance = Vector3.Distance(playerPos, structurePos);

            // Wenn Struktur zu nah oder zu weit, optimal positionieren
            if (currentDistance < minSpawnDistance || currentDistance > maxSpawnDistance)
            {
                // Richtung vom Spieler zur Struktur (oder Vorwärts wenn zu nah)
                Vector3 direction = (structurePos - playerPos).normalized;
                if (direction == Vector3.zero)
                {
                    direction = targetTransform.forward; // Fallback: Blickrichtung des Spielers
                }

                // Neue Position in optimaler Distanz VOR dem Spieler
                float targetDistance = (minSpawnDistance + maxSpawnDistance) / 2f; // Mitte
                Vector3 newPos = playerPos + direction * targetDistance;
                structure.transform.position = newPos;

                Debug.Log($"SpawnManager: Struktur repositioniert (war {currentDistance:F1}m) → neu bei {targetDistance:F1}m vom Spieler");
            }
        }
        else
        {
            Debug.LogWarning("SpawnManager: targetTransform ist NULL! Strukturen können nicht bewegt werden!");
        }

        // Struktur zur Bewegungsliste hinzufügen
        MovingStructure ms = new MovingStructure
        {
            gameObject = structure,
            spawnPoint = sp,
            voxelData = sp.CurrentJob,
            startPosition = structure.transform.position
        };

        _movingStructures.Add(ms);
        activeStructures = _movingStructures.Count;

        Debug.Log($"SpawnManager: Struktur '{structure.name}' bereit zur Bewegung (aktive: {activeStructures})");
    }

    void Update()
    {
        // Strukturen zum Spieler bewegen
        MoveStructures();
    }

    /// <summary>
    /// Bewegt alle fertigen Strukturen zum Spieler (3D Weltraum-Modus)
    /// </summary>
    private void MoveStructures()
    {
        if (targetTransform == null) return;

        List<MovingStructure> toRemove = new List<MovingStructure>();
        Vector3 playerPos = targetTransform.position;

        foreach (var ms in _movingStructures)
        {
            if (ms.gameObject == null)
            {
                toRemove.Add(ms);
                continue;
            }

            // 3D WELTRAUM-MODUS: Strukturen bewegen sich ZUM Spieler (nicht nur -Z!)
            Vector3 structurePos = ms.gameObject.transform.position;
            Vector3 directionToPlayer = (playerPos - structurePos).normalized;

            // Bewege Struktur Richtung Spieler
            ms.gameObject.transform.position += directionToPlayer * moveSpeed * Time.deltaTime;

            // 3D Distanz-Check: Entferne wenn zu weit vom Spieler entfernt
            float distance = Vector3.Distance(playerPos, ms.gameObject.transform.position);

            if (distance > maxDistanceFromPlayer)
            {
                Debug.Log($"SpawnManager: Struktur '{ms.gameObject.name}' zu weit weg ({distance:F1}m > {maxDistanceFromPlayer}m), wird entfernt");
                Destroy(ms.gameObject);
                toRemove.Add(ms);
            }
        }

        // Entfernte Strukturen aus Liste nehmen
        foreach (var ms in toRemove)
        {
            _movingStructures.Remove(ms);
        }

        activeStructures = _movingStructures.Count;
    }

    /// <summary>
    /// Wird aufgerufen wenn Spieler Struktur einsammelt
    /// </summary>
    public void OnStructureCollected(GameObject structure, VoxelData data)
    {
        // Aus Bewegungsliste entfernen
        _movingStructures.RemoveAll(ms => ms.gameObject == structure);
        activeStructures = _movingStructures.Count;

        OnStructureReachedPlayer?.Invoke(structure, data);

        Debug.Log($"SpawnManager: Struktur eingesammelt: {data?.paper_id ?? "unknown"}");
    }

    /// <summary>
    /// Alle laufenden Jobs abbrechen
    /// </summary>
    public void ClearAllJobs()
    {
        // Queue leeren
        jobQueue.Clear();

        // SpawnPoints stoppen
        foreach (var sp in spawnPoints)
        {
            if (sp != null) sp.ClearCurrentJob();
        }

        // Bewegende Strukturen zerstören
        foreach (var ms in _movingStructures)
        {
            if (ms.gameObject != null) Destroy(ms.gameObject);
        }
        _movingStructures.Clear();

        activeStructures = 0;
        Debug.Log("SpawnManager: Alle Jobs gelöscht");
    }

    void OnDestroy()
    {
        // Events abmelden
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;
            sp.OnJobStarted -= HandleJobStarted;
            sp.OnJobCompleted -= HandleJobCompleted;
            sp.OnStructureReady -= HandleStructureReady;
        }
    }

    // === EDITOR GIZMOS ===

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Despawn-Radius um Spieler (rot)
        if (targetTransform != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.2f);
            Gizmos.DrawWireSphere(targetTransform.position, maxDistanceFromPlayer);

            // Spawn-Bereich (grün)
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(targetTransform.position, maxSpawnDistance);
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.1f);
            Gizmos.DrawWireSphere(targetTransform.position, minSpawnDistance);
        }

        // SpawnPoint Aktivierungsradien (blau)
        Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.2f);
        foreach (var sp in spawnPoints)
        {
            if (sp != null)
            {
                Gizmos.DrawWireSphere(sp.transform.position, spawnPointActivationRadius);

                // SpawnPoint Marker
                Gizmos.color = IsSpawnPointInRange(sp) ? Color.green : Color.yellow;
                Gizmos.DrawSphere(sp.transform.position, 2f);
                Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.2f);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Detailliertere Ansicht wenn ausgewählt
        if (targetTransform != null)
        {
            // Bewegende Strukturen markieren
            Gizmos.color = Color.cyan;
            foreach (var ms in _movingStructures)
            {
                if (ms.gameObject != null)
                {
                    Gizmos.DrawLine(targetTransform.position, ms.gameObject.transform.position);
                    Gizmos.DrawWireCube(ms.gameObject.transform.position, Vector3.one * 3f);
                }
            }
        }
    }
#endif

    // === HELPER CLASS ===

    [System.Serializable]
    private class MovingStructure
    {
        public GameObject gameObject;
        public SpawnPoint spawnPoint;
        public VoxelData voxelData;
        public Vector3 startPosition;
    }
}
