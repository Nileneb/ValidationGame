// Scripts/Core/GameManager.cs
// Zentrale Spiellogik - VEREINFACHT
// Nur ApiClient, VoxelSpawner, RuleMatcher - KEIN SCHNICKSCHNACK

using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private VoxelStructureSpawner voxelSpawner;
    [SerializeField] private RuleMatcher ruleMatcher;
    [SerializeField] private RulePreview rulePreview;
    [SerializeField] private Transform player;

    [Header("Player Settings")]
    [SerializeField] private bool autoSpawnPlayer = true;
    [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0, 1, 0);

    [Header("Game State")]
    [SerializeField] private int totalPoints = 0;
    [SerializeField] private int matchesFound = 0;

    [Header("Timing")]
    [SerializeField] private float jobFetchInterval = 30f;
    [SerializeField] private float spawnChancePerSecond = 0.5f;

    // Aktive Regel
    private RuleData currentActiveRule;

    // Job Queue
    private Queue<VoxelData> jobQueue = new Queue<VoxelData>();

    // Pending Results
    private List<ValidationResult> pendingResults = new List<ValidationResult>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        FindReferences();
    }

    void Start()
    {
        // Regeln laden
        if (apiClient != null)
        {
            StartCoroutine(apiClient.FetchActiveRules(OnRulesLoaded));
        }

        // Jobs holen
        FetchMoreJobs();
        InvokeRepeating(nameof(FetchMoreJobs), 10f, jobFetchInterval);
        InvokeRepeating(nameof(SubmitPendingResults), 5f, 15f);
    }

    private void FindReferences()
    {
        if (apiClient == null)
            apiClient = FindAnyObjectByType<ApiClient>();
        if (voxelSpawner == null)
            voxelSpawner = FindAnyObjectByType<VoxelStructureSpawner>();
        if (ruleMatcher == null)
            ruleMatcher = FindAnyObjectByType<RuleMatcher>();
        if (rulePreview == null)
            rulePreview = FindAnyObjectByType<RulePreview>();

        // PLAYER SPAWNEN!
        SpawnPlayer();

        // Kamera Setup
        SetupCamera();
    }

    /// <summary>
    /// Richtet die Kamera ein, dem Spieler zu folgen
    /// </summary>
    private void SetupCamera()
    {
        if (player == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // CameraFollow hinzufügen oder finden
        CameraFollow camFollow = mainCam.GetComponent<CameraFollow>();
        if (camFollow == null)
        {
            camFollow = mainCam.gameObject.AddComponent<CameraFollow>();
        }
        camFollow.SetPlayer(player);
    }

    /// <summary>
    /// Spawnt den Player falls nicht vorhanden
    /// </summary>
    private void SpawnPlayer()
    {
        // Existiert Player schon?
        GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player");
        if (existingPlayer != null)
        {
            player = existingPlayer.transform;
            Debug.Log("GameManager: Player gefunden.");
        }
        else if (autoSpawnPlayer)
        {
            // PLAYER ERSTELLEN!
            GameObject playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            playerObj.transform.position = playerSpawnPosition;

            // Capsule Mesh
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "PlayerMesh";
            capsule.transform.SetParent(playerObj.transform);
            capsule.transform.localPosition = new Vector3(0, 1, 0);
            Object.Destroy(capsule.GetComponent<Collider>());

            // Material (blau)
            Renderer rend = capsule.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.6f, 1f);
                rend.material = mat;
            }

            // Collider
            CapsuleCollider col = playerObj.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1, 0);
            col.height = 2f;
            col.radius = 0.5f;

            // Rigidbody
            Rigidbody rb = playerObj.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // PlayerController
            playerObj.AddComponent<PlayerController>();

            player = playerObj.transform;
            Debug.Log("GameManager: PLAYER ERSTELLT!");
        }

        // Player-Referenz an andere Scripts weitergeben
        if (player != null)
        {
            if (voxelSpawner != null)
                voxelSpawner.SetPlayer(player);
        }
    }

    void OnRulesLoaded(List<RuleData> rules)
    {
        if (rules != null && rules.Count > 0)
        {
            currentActiveRule = rules[0];
            Debug.Log($"GameManager: Rule loaded: {currentActiveRule.question}");

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetActiveRule(currentActiveRule.question);
            }
        }
        else
        {
            // Fallback-Regel
            currentActiveRule = new RuleData
            {
                rule_id = "fallback",
                question = "Suche Papers...",
                threshold = 0.7f
            };

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetActiveRule(currentActiveRule.question);
            }
        }
    }

    void FetchMoreJobs()
    {
        if (apiClient != null)
        {
            StartCoroutine(apiClient.FetchJobs(10, OnJobsReceived));
        }
    }

    void OnJobsReceived(List<VoxelData> jobs)
    {
        if (jobs == null) return;

        foreach (var job in jobs)
        {
            // Base64 dekodieren falls vorhanden
            job.DecodeData();
            jobQueue.Enqueue(job);

            // Rule-Preview anzeigen wenn erster Job mit pos_embedding
            if (rulePreview != null && job.pos_embedding != null && currentActiveRule != null)
            {
                rulePreview.ShowRule(job.rule_id ?? currentActiveRule.rule_id, job.pos_embedding);
            }
        }

        Debug.Log($"GameManager: {jobs.Count} Jobs erhalten. Queue: {jobQueue.Count}");
    }

    void Update()
    {
        // Jobs spawnen
        if (jobQueue.Count > 0 && voxelSpawner != null)
        {
            if (Random.value < spawnChancePerSecond * Time.deltaTime)
            {
                VoxelData job = jobQueue.Dequeue();
                Debug.Log($"GameManager: Spawning Job {job.job_id ?? job.paper_id} - Queue: {jobQueue.Count} remaining");
                GameObject spawned = voxelSpawner.SpawnFromData(job);
                if (spawned == null)
                {
                    Debug.LogError($"GameManager: SpawnFromData returned NULL for {job.paper_id}!");
                }
            }
        }
    }

    /// <summary>
    /// Wird aufgerufen wenn Spieler ein Paper einsammelt
    /// </summary>
    public void OnPaperCollected(CollectiblePaper paper)
    {
        if (paper == null) return;

        // Matching prüfen
        RuleMatcher.MatchResult result;

        if (ruleMatcher != null && paper.jobData != null)
        {
            result = ruleMatcher.CheckMatch(paper.jobData, currentActiveRule);
        }
        else if (ruleMatcher != null && currentActiveRule != null && paper.embedding != null)
        {
            result = ruleMatcher.CheckMatch(paper.embedding, currentActiveRule);
        }
        else
        {
            result = new RuleMatcher.MatchResult
            {
                is_match = false,
                similarity = 0f,
                points = 10,
                feedback = "Paper eingesammelt! (+10)"
            };
        }

        // Punkte addieren
        totalPoints += result.points;
        if (result.is_match)
        {
            matchesFound++;
        }

        // UI aktualisieren
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(totalPoints, matchesFound);
            UIManager.Instance.ShowFeedback(result.feedback);
        }

        // Result für Server speichern
        string jobId = paper.jobId ?? $"job_{paper.paperId}_{paper.section}";
        string ruleId = paper.jobData?.rule_id ?? currentActiveRule?.rule_id ?? "unknown";

        ValidationResult validationResult = new ValidationResult
        {
            job_id = jobId,
            paper_id = paper.paperId,
            rule_id = ruleId,
            section = paper.section,
            is_match = result.is_match,
            similarity = result.similarity,
            points_earned = result.points
        };

        pendingResults.Add(validationResult);
        Debug.Log($"GameManager: {result.feedback} - Total: {totalPoints} Punkte, {matchesFound} Matches");
    }

    void SubmitPendingResults()
    {
        if (pendingResults.Count == 0 || apiClient == null) return;

        var toSubmit = new List<ValidationResult>(pendingResults);
        pendingResults.Clear();

        StartCoroutine(apiClient.SubmitResults(toSubmit, (success) =>
        {
            if (!success)
            {
                pendingResults.AddRange(toSubmit);
                Debug.LogWarning("GameManager: Submit fehlgeschlagen, wird erneut versucht");
            }
            else
            {
                Debug.Log($"GameManager: {toSubmit.Count} Results erfolgreich gesendet");
            }
        }));
    }

    /// <summary>
    /// Wird aufgerufen wenn der Spieler stirbt
    /// </summary>
    public void OnPlayerDeath()
    {
        Debug.Log("GameManager: GAME OVER - Spieler gestorben!");
        
        // Pending Results noch senden
        SubmitPendingResults();
        
        // UI aktualisieren
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOver(totalPoints, matchesFound);
        }
        
        // Optional: Spiel pausieren
        // Time.timeScale = 0f;
    }

    // Public Getter
    public int GetTotalPoints() => totalPoints;
    public int GetMatchesFound() => matchesFound;
    public RuleData GetCurrentRule() => currentActiveRule;
}
