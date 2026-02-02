// Scripts/Core/GameManager.cs
// Zentrale Spiellogik - NEUE ARCHITEKTUR
// ApiClient holt Jobs → SpawnManager spawnt → PlayerMatcher vergleicht

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References - NEUE ARCHITEKTUR")]
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private RulePreview rulePreview;
    [SerializeField] private Transform player;

    [Header("Legacy References (für Rückwärtskompatibilität)")]
    [SerializeField] private VoxelStructureSpawner voxelSpawner;
    [SerializeField] private RuleMatcher ruleMatcher;

    [Header("Player Settings")]
    [SerializeField] private bool autoSpawnPlayer = true;
    [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0, 1, 0);

    [Header("Game State")]
    [SerializeField] private int totalPoints = 0;
    [SerializeField] private int matchesFound = 0;

    [Header("Timing")]
    [SerializeField] private float jobFetchInterval = 30f;
    [SerializeField] private float spawnChancePerSecond = 0.5f;

    // Aktive Regel (für Kompatibilität)
    private RuleData currentActiveRule;

    // ALLE geladenen Rules für Embedding Map
    private List<RuleData> allLoadedRules = new List<RuleData>();

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

        // Rule Cache initialisieren
        InitializeRuleCache();

        FindReferences();
    }

    /// <summary>
    /// Initialisiert den Rule Prefab Cache (einmalig)
    /// </summary>
    private void InitializeRuleCache()
    {
        if (RulePrefabCache.Instance == null)
        {
            GameObject cacheObj = new GameObject("RulePrefabCache");
            cacheObj.AddComponent<RulePrefabCache>();
            DontDestroyOnLoad(cacheObj);
            Debug.Log("GameManager: RulePrefabCache initialisiert");
        }
    }

    void Start()
    {
        // Regeln laden
        if (apiClient != null)
        {
            StartCoroutine(apiClient.FetchActiveRules(OnRulesLoaded));
        }

        // WICHTIG: Erst verfügbare Papers prüfen, dann Jobs holen!
        StartCoroutine(InitialPaperCheck());

        InvokeRepeating(nameof(FetchMoreJobs), 15f, jobFetchInterval);
        InvokeRepeating(nameof(SubmitPendingResults), 5f, 15f);
    }

    /// <summary>
    /// Prüft verfügbare Papers und holt dann Jobs
    /// Löst das "no_jobs" Problem wenn Paper vorhanden aber noch nicht zugewiesen
    /// </summary>
    private IEnumerator InitialPaperCheck()
    {
        if (apiClient == null) yield break;

        Debug.Log("GameManager: Prüfe verfügbare Papers...");

        // Erst verfügbare Papers abrufen
        List<PaperInfo> papers = null;
        yield return apiClient.FetchAvailablePapers((result) => papers = result);

        if (papers != null && papers.Count > 0)
        {
            Debug.Log($"GameManager: {papers.Count} Papers gefunden!");

            // Finde Papers mit Jobs
            foreach (var paper in papers)
            {
                if (paper.HasJobs && paper.IsReady)
                {
                    Debug.Log($"GameManager: Paper '{paper.paper_id}' hat {paper.jobs_count} Jobs (Status: {paper.status})");

                    // Versuche Jobs für dieses Paper zu holen
                    yield return apiClient.FetchJobsForPaper(paper.paper_id, OnJobsReceived);

                    // Wenn Jobs erhalten, fertig
                    if (jobQueue.Count > 0)
                    {
                        Debug.Log($"GameManager: Jobs erfolgreich für Paper '{paper.paper_id}' geladen!");
                        yield break;
                    }
                }
            }
        }

        // Fallback: Standard Job-Abruf
        Debug.Log("GameManager: Fallback zu /api/jobs/next...");
        FetchMoreJobs();
    }

    private void FindReferences()
    {
        if (apiClient == null)
            apiClient = FindAnyObjectByType<ApiClient>();
        if (spawnManager == null)
            spawnManager = FindAnyObjectByType<SpawnManager>();
        if (rulePreview == null)
            rulePreview = FindAnyObjectByType<RulePreview>();

        // Legacy (für Rückwärtskompatibilität)
        if (voxelSpawner == null)
            voxelSpawner = FindAnyObjectByType<VoxelStructureSpawner>();
        if (ruleMatcher == null)
            ruleMatcher = FindAnyObjectByType<RuleMatcher>();

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

            // PlayerMatcher - WICHTIG für die neue Architektur!
            PlayerMatcher matcher = playerObj.AddComponent<PlayerMatcher>();
            if (allLoadedRules.Count > 0)
            {
                matcher.SetAllRules(allLoadedRules);
            }

            player = playerObj.transform;
            Debug.Log("GameManager: PLAYER ERSTELLT (mit PlayerMatcher)!");
        }

        // Player-Referenz an andere Scripts weitergeben
        if (player != null)
        {
            // SpawnManager bekommt Player als Ziel
            if (spawnManager != null)
            {
                spawnManager.targetTransform = player;
            }

            // Legacy: VoxelSpawner (falls noch verwendet)
            if (voxelSpawner != null)
                voxelSpawner.SetPlayer(player);
        }
    }

    void OnRulesLoaded(List<RuleData> rules)
    {
        if (rules != null && rules.Count > 0)
        {
            // Base64 Embeddings dekodieren für JEDE Rule
            int rulesWithEmbedding = 0;
            foreach (var rule in rules)
            {
                rule.DecodeData();
                if (rule.pos_embedding != null && rule.pos_embedding.Length > 0)
                {
                    rulesWithEmbedding++;
                }
            }

            // ALLE Rules speichern für Matching
            allLoadedRules = rules;
            currentActiveRule = rules[0];
            Debug.Log($"GameManager: {rules.Count} Rules geladen, {rulesWithEmbedding} mit Embedding!");

            // RuleMatcher bekommt ALLE Rules (jetzt MIT Embeddings!)
            // Legacy RuleMatcher
            if (ruleMatcher != null)
            {
                ruleMatcher.SetAllRules(rules);
            }

            // NEUE Architektur: PlayerMatcher auf dem Player
            PlayerMatcher playerMatcher = player?.GetComponent<PlayerMatcher>();
            if (playerMatcher != null)
            {
                playerMatcher.SetAllRules(rules);
                Debug.Log("GameManager: PlayerMatcher mit Rules aktualisiert!");
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetActiveRule($"{rules.Count} Rules aktiv");
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

        bool firstJobWithRule = true;  // Nur beim ersten Job die Preview setzen

        // DEBUG: Duplikat-Erkennung für section_embeddings
        CheckForDuplicateEmbeddings(jobs);

        foreach (var job in jobs)
        {
            // Base64 dekodieren falls vorhanden
            job.DecodeData();

            // DEBUG: Was hat der Job nach Dekodierung? (NEUE API Felder!)
            Debug.Log($"GameManager: Job empfangen - paper_id={job.paper_id}, title='{job.title ?? "N/A"}'");
            Debug.Log($"  paper_embedding_b64={(string.IsNullOrEmpty(job.paper_embedding_b64) ? "LEER" : $"{job.paper_embedding_b64.Length} chars")}");
            Debug.Log($"  paper_text={(string.IsNullOrEmpty(job.paper_text) ? "LEER" : $"{job.paper_text.Length} chars")}");
            Debug.Log($"  section_embedding={(job.section_embedding != null ? $"{job.section_embedding.Length} floats" : "NULL")}");

            // === WICHTIG: Rule-Embedding aus Job extrahieren und in Rules speichern ===
            if (!string.IsNullOrEmpty(job.rule_id) && job.pos_embedding != null)
            {
                UpdateRuleEmbedding(job.rule_id, job.pos_embedding, job.neg_embedding, job.threshold);
            }

            jobQueue.Enqueue(job);

            // Rule-Preview NUR EINMAL setzen (für den ersten Job mit pos_embedding)
            if (firstJobWithRule && rulePreview != null && job.pos_embedding != null)
            {
                rulePreview.ShowRule(job.rule_id ?? currentActiveRule?.rule_id ?? "unknown", job.pos_embedding);
                firstJobWithRule = false;

                // Aktive Rule aktualisieren für UI
                if (UIManager.Instance != null && !string.IsNullOrEmpty(job.question))
                {
                    UIManager.Instance.SetActiveRule(job.question);
                }
            }

            // NEUE API: UI mit Paper-Info aktualisieren
            if (UIManager.Instance != null && !string.IsNullOrEmpty(job.title))
            {
                UIManager.Instance.ShowFeedback($"Neues Paper: {job.title}");
            }
        }

        Debug.Log($"GameManager: {jobs.Count} Jobs erhalten. Queue: {jobQueue.Count}");
    }

    /// <summary>
    /// Aktualisiert das Embedding einer Rule aus Job-Daten.
    /// Jobs liefern pos_embedding - Rules brauchen das zum Matching!
    /// </summary>
    void UpdateRuleEmbedding(string ruleId, float[] posEmbedding, float[] negEmbedding, float threshold)
    {
        // Suche Rule in allLoadedRules
        var rule = allLoadedRules.Find(r => r.rule_id == ruleId);

        if (rule != null)
        {
            // Embedding nur setzen wenn noch nicht vorhanden
            if (rule.pos_embedding == null || rule.pos_embedding.Length == 0)
            {
                rule.pos_embedding = posEmbedding;
                rule.neg_embedding = negEmbedding;
                if (threshold > 0) rule.threshold = threshold;

                Debug.Log($"GameManager: Rule '{ruleId}' mit Embedding aktualisiert! ({posEmbedding.Length} floats)");

                // RuleMatcher über Update informieren
                if (ruleMatcher != null)
                {
                    ruleMatcher.SetAllRules(allLoadedRules);
                }
            }
        }
        else
        {
            // Rule existiert noch nicht - neu anlegen!
            var newRule = new RuleData
            {
                rule_id = ruleId,
                pos_embedding = posEmbedding,
                neg_embedding = negEmbedding,
                threshold = threshold > 0 ? threshold : 0.5f,
                is_active = true
            };
            allLoadedRules.Add(newRule);

            Debug.Log($"GameManager: NEUE Rule '{ruleId}' aus Job erstellt! ({posEmbedding.Length} floats)");

            // RuleMatcher über neue Rule informieren (Legacy)
            if (ruleMatcher != null)
            {
                ruleMatcher.SetAllRules(allLoadedRules);
            }

            // PlayerMatcher über neue Rule informieren (NEUE Architektur)
            PlayerMatcher playerMatcher = player?.GetComponent<PlayerMatcher>();
            if (playerMatcher != null)
            {
                playerMatcher.SetAllRules(allLoadedRules);
            }
        }
    }

    void Update()
    {
        // NEUE ARCHITEKTUR: SpawnManager übernimmt das Spawning!
        // Jobs an SpawnManager weiterleiten
        if (jobQueue.Count > 0 && spawnManager != null)
        {
            // Jobs an SpawnManager übergeben
            while (jobQueue.Count > 0 && spawnManager.GetAvailableCount() > 0)
            {
                VoxelData job = jobQueue.Dequeue();
                spawnManager.QueueJob(job);
                Debug.Log($"GameManager: Job '{job.job_id ?? job.paper_id}' an SpawnManager übergeben");
            }
        }

        // LEGACY: Falls SpawnManager nicht vorhanden, alten Code nutzen
        else if (jobQueue.Count > 0 && voxelSpawner != null && spawnManager == null)
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
    /// NEUE ARCHITEKTUR: Wird vom PlayerMatcher aufgerufen wenn ein Paper eingesammelt wird
    /// </summary>
    public void OnPaperCollectedByPlayer(VoxelData paper, PlayerMatcher.EmbeddingMapResult mapResult)
    {
        if (paper == null || mapResult == null) return;

        // Punkte addieren
        totalPoints += mapResult.points;
        matchesFound += mapResult.totalMatches;

        // UI aktualisieren
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(totalPoints, matchesFound);
            UIManager.Instance.ShowFeedback(mapResult.feedback);
        }

        // Results für Server speichern
        string jobId = paper.job_id ?? $"job_{paper.paper_id}_{paper.section}";
        int processingTime = 100 * mapResult.totalRules;

        foreach (var match in mapResult.matches)
        {
            if (match.is_match)
            {
                ValidationResult validationResult = new ValidationResult
                {
                    job_id = jobId,
                    matched = match.is_match,  // API-Feld (serialisiert)
                    confidence = match.similarity,
                    time_taken_ms = processingTime / Mathf.Max(1, mapResult.totalMatches),
                    paper_id = paper.paper_id,
                    rule_id = match.rule_id,
                    section = paper.section,
                    points_earned = mapResult.points / Mathf.Max(1, mapResult.totalMatches),
                    regions = new int[0][]  // Leere Regions erstmal
                };
                validationResult.is_match = match.is_match; // Intern
                validationResult.similarity = match.similarity; // Intern
                pendingResults.Add(validationResult);
            }
        }

        Debug.Log($"GameManager: {mapResult.feedback} - Total: {totalPoints} Punkte");
    }

    /// <summary>
    /// Wird aufgerufen wenn Spieler ein Paper einsammelt
    /// Prüft Paper gegen ALLE Rules und erstellt Embedding Map!
    /// HYBRID: Nutzt paper_text für Keyword-Matching (BioBERT Baseline ~0.90!)
    /// </summary>
    public void OnPaperCollected(CollectiblePaper paper)
    {
        if (paper == null) return;

        // Paper-Embedding holen
        float[] paperEmbedding = paper.jobData?.section_embedding ?? paper.embedding;

        // Paper-Text für Hybrid-Matching (NEUE API!)
        string paperText = paper.jobData?.paper_text ?? paper.jobData?.section_text;

        // Gegen ALLE Rules prüfen (HYBRID: Embedding + Keywords!)
        RuleMatcher.EmbeddingMapResult mapResult = null;

        if (ruleMatcher != null && paperEmbedding != null)
        {
            // HYBRID: Mit Text wenn vorhanden
            mapResult = ruleMatcher.CheckAllRules(paperEmbedding, paperText);
        }

        if (mapResult == null)
        {
            mapResult = new RuleMatcher.EmbeddingMapResult
            {
                totalMatches = 0,
                totalRules = 0,
                points = 10,
                feedback = "Paper eingesammelt! (+10)"
            };
        }

        // Punkte addieren
        totalPoints += mapResult.points;
        matchesFound += mapResult.totalMatches;

        // UI aktualisieren
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(totalPoints, matchesFound);
            UIManager.Instance.ShowFeedback(mapResult.feedback);
        }

        // Results für Server speichern - für JEDE gematchte Rule
        string jobId = paper.jobId ?? $"job_{paper.paperId}_{paper.section}";

        // Startzeit für time_taken_ms (vereinfacht: wir nehmen an, Verarbeitung dauert ~100ms pro Rule)
        int processingTime = 100 * mapResult.totalRules;

        foreach (var match in mapResult.matches)
        {
            if (match.is_match)
            {
                ValidationResult validationResult = new ValidationResult
                {
                    // API Felder (serialisiert)
                    rule_id = match.rule_id,
                    matched = match.is_match,
                    confidence = match.similarity,  // Confidence = Similarity für jetzt
                    regions = new int[0][],  // Leere Regions erstmal

                    // Intern (NonSerialized)
                    job_id = jobId,
                    time_taken_ms = processingTime / Mathf.Max(1, mapResult.totalMatches),
                    paper_id = paper.paperId,
                    section = paper.section,
                    points_earned = mapResult.points / Mathf.Max(1, mapResult.totalMatches)
                };
                validationResult.is_match = match.is_match;
                validationResult.similarity = match.similarity;
                pendingResults.Add(validationResult);
            }
        }

        Debug.Log($"GameManager: {mapResult.feedback} - Total: {totalPoints} Punkte, {matchesFound} Rule-Matches");
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
    /// DEBUG: Prüft ob alle Jobs identische Embeddings haben.
    /// SERVER-BUG wenn alle Embeddings gleich sind!
    /// Unterstützt NEUE API (paper_embedding_b64) UND Legacy (section_embedding_b64)
    /// </summary>
    void CheckForDuplicateEmbeddings(List<VoxelData> jobs)
    {
        if (jobs == null || jobs.Count < 2) return;

        // Sammle alle einzigartigen Embeddings (NEUE API hat Priorität)
        HashSet<string> uniqueEmbeddings = new HashSet<string>();
        HashSet<string> uniquePapers = new HashSet<string>();
        HashSet<string> uniqueTitles = new HashSet<string>();

        foreach (var job in jobs)
        {
            // NEUE API: paper_embedding_b64 hat Priorität
            string embedding = !string.IsNullOrEmpty(job.paper_embedding_b64)
                ? job.paper_embedding_b64
                : job.section_embedding_b64;

            if (!string.IsNullOrEmpty(embedding))
                uniqueEmbeddings.Add(embedding);

            if (!string.IsNullOrEmpty(job.paper_id))
                uniquePapers.Add(job.paper_id);

            if (!string.IsNullOrEmpty(job.title))
                uniqueTitles.Add(job.title);
        }

        // Warnung wenn alle Embeddings identisch sind
        if (uniqueEmbeddings.Count == 1 && jobs.Count > 1)
        {
            Debug.LogError($"⚠️ SERVER-BUG: Alle {jobs.Count} Jobs haben IDENTISCHE Embeddings!");
            Debug.LogError($"   → Unique Papers: {uniquePapers.Count} ({string.Join(", ", uniquePapers)})");
            Debug.LogError($"   → Unique Titles: {uniqueTitles.Count}");
            Debug.LogError($"   → Der Server sendet immer dasselbe Embedding → Voxel-Visualisierungen werden alle gleich aussehen!");
        }
        else if (uniqueEmbeddings.Count < jobs.Count)
        {
            Debug.LogWarning($"GameManager: Nur {uniqueEmbeddings.Count} einzigartige Embeddings bei {jobs.Count} Jobs. Einige Jobs teilen sich Embeddings.");
        }
        else if (uniqueEmbeddings.Count > 0)
        {
            Debug.Log($"GameManager: [OK] {uniqueEmbeddings.Count} einzigartige Embeddings bei {jobs.Count} Jobs - alles OK!");
        }
    }

    // Public Getter
    public int GetTotalPoints() => totalPoints;
    public int GetMatchesFound() => matchesFound;
    public RuleData GetCurrentRule() => currentActiveRule;

    // === DEBUG METHODEN (über Inspector Context Menu aufrufbar) ===

    /// <summary>
    /// DEBUG: Lädt Jobs für ein bestimmtes Paper
    /// </summary>
    [ContextMenu("DEBUG: Fetch Jobs for PMID:39629493")]
    public void DebugFetchJobsForTestPaper()
    {
        if (apiClient == null)
        {
            Debug.LogError("ApiClient nicht gefunden!");
            return;
        }

        StartCoroutine(apiClient.FetchJobsForPaper("semantic_PMID:39629493", OnJobsReceived));
    }

    /// <summary>
    /// DEBUG: Zeigt alle verfügbaren Papers
    /// </summary>
    [ContextMenu("DEBUG: Show Available Papers")]
    public void DebugShowAvailablePapers()
    {
        if (apiClient == null)
        {
            Debug.LogError("ApiClient nicht gefunden!");
            return;
        }

        StartCoroutine(apiClient.FetchAvailablePapers((papers) =>
        {
            if (papers == null || papers.Count == 0)
            {
                Debug.Log("Keine Papers gefunden!");
                return;
            }

            Debug.Log($"=== {papers.Count} PAPERS VERFÜGBAR ===");
            foreach (var p in papers)
            {
                Debug.Log($"  [{p.status}] {p.paper_id}: {p.title ?? "N/A"} - {p.jobs_count} Jobs ({p.jobs_completed} fertig)");
            }
        }));
    }

    /// <summary>
    /// DEBUG: Server-Status abrufen
    /// </summary>
    [ContextMenu("DEBUG: Server Status")]
    public void DebugServerStatus()
    {
        if (apiClient == null)
        {
            Debug.LogError("ApiClient nicht gefunden!");
            return;
        }

        StartCoroutine(apiClient.FetchServerStatus((status) =>
        {
            Debug.Log($"Server Status: {status ?? "N/A"}");
        }));
    }
}
