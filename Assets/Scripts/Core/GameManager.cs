// Scripts/Core/GameManager.cs
// Zentrale Spiellogik - Singleton Pattern
// Verwaltet Jobs, Matching, Punkte und Server-Kommunikation

using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // Singleton Instance
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private VoxelStructureSpawner voxelSpawner;
    [SerializeField] private RuleMatcher ruleMatcher;

    [Header("Game State")]
    [SerializeField] private int totalPoints = 0;
    [SerializeField] private int papersValidated = 0;
    [SerializeField] private int matchesFound = 0;

    [Header("Timing")]
    [SerializeField] private float jobFetchInterval = 30f;
    [SerializeField] private float resultSubmitInterval = 15f;
    [SerializeField] private float spawnChancePerSecond = 0.5f;

    // Aktive Regel für Matching
    private RuleData currentActiveRule;

    // Queue für eingehende Jobs
    private Queue<VoxelData> jobQueue = new Queue<VoxelData>();

    // Pending Results für Server-Submit
    private List<ValidationResult> pendingResults = new List<ValidationResult>();

    void Awake()
    {
        // Singleton Setup
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
    }

    void Start()
    {
        // Regeln laden
        if (apiClient != null)
        {
            StartCoroutine(apiClient.FetchActiveRules(OnRulesLoaded));
        }

        // Initiale Jobs holen
        FetchMoreJobs();

        // Periodisch neue Jobs holen
        InvokeRepeating(nameof(FetchMoreJobs), 10f, jobFetchInterval);

        // Periodisch Results submitten
        InvokeRepeating(nameof(SubmitPendingResults), 5f, resultSubmitInterval);
    }

    void OnRulesLoaded(List<RuleData> rules)
    {
        if (rules != null && rules.Count > 0)
        {
            currentActiveRule = rules[0];  // Erste Regel als aktiv setzen
            Debug.Log($"GameManager: Active Rule loaded: {currentActiveRule.question}");

            // UI aktualisieren
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetActiveRule(currentActiveRule.question);
            }
        }
        else
        {
            Debug.LogWarning("GameManager: Keine Regeln vom Server erhalten!");

            // Fallback: Default-Regel erstellen
            currentActiveRule = new RuleData
            {
                rule_id = "fallback",
                question = "Searching for papers...",
                threshold = 0.7f,
                is_active = true
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
            // WICHTIG: Base64-Daten dekodieren!
            job.DecodeData();

            jobQueue.Enqueue(job);
        }

        Debug.Log($"GameManager: Received {jobs.Count} jobs. Queue size: {jobQueue.Count}");
    }

    void Update()
    {
        // Jobs spawnen wenn Queue nicht leer
        // Zufälliges Spawning basierend auf spawnChancePerSecond
        if (jobQueue.Count > 0 && voxelSpawner != null)
        {
            if (Random.value < spawnChancePerSecond * Time.deltaTime)
            {
                VoxelData job = jobQueue.Dequeue();
                voxelSpawner.SpawnFromData(job);
            }
        }
    }

    /// <summary>
    /// Wird aufgerufen wenn Spieler ein Paper einsammelt
    /// </summary>
    public void OnPaperCollected(CollectiblePaper paper)
    {
        if (paper == null) return;

        papersValidated++;

        // Rule Matching durchführen (mit VoxelData statt nur Embedding)
        RuleMatcher.MatchResult result;

        if (ruleMatcher != null && paper.jobData != null)
        {
            // Neue Methode: Embeddings aus jobData nutzen
            result = ruleMatcher.CheckMatch(paper.jobData, currentActiveRule);
        }
        else if (ruleMatcher != null && currentActiveRule != null && paper.embedding != null)
        {
            // Legacy-Fallback
            result = ruleMatcher.CheckMatch(paper.embedding, currentActiveRule);
        }
        else
        {
            // Fallback wenn kein Matching möglich
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
            Debug.Log($"GameManager: MATCH! +{result.points} Punkte (Similarity: {result.similarity:F2})");
        }
        else
        {
            Debug.Log($"GameManager: Kein Match. +{result.points} Punkte");
        }

        // UI Update
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(totalPoints, matchesFound);
            UIManager.Instance.ShowFeedback(result.feedback);
        }

        // Result für Server-Submit speichern
        // Job-ID aus jobData wenn verfügbar
        string jobId = paper.jobId ?? $"job_{paper.paperId}_{paper.section}";
        string ruleId = paper.jobData?.rule_id ?? currentActiveRule?.rule_id ?? "unknown";

        ValidationResult validationResult = new ValidationResult
        {
            job_id = jobId,
            paper_id = paper.paperId,
            rule_id = ruleId,
            is_match = result.is_match,
            similarity = result.similarity,
            confidence = result.similarity,
            points_earned = result.points,
            time_taken_ms = 0
        };

        pendingResults.Add(validationResult);
    }

    void SubmitPendingResults()
    {
        if (pendingResults.Count > 0 && apiClient != null)
        {
            // Kopie erstellen für Submit
            List<ValidationResult> toSubmit = new List<ValidationResult>(pendingResults);
            pendingResults.Clear();

            StartCoroutine(apiClient.SubmitResults(toSubmit, (success) =>
            {
                if (success)
                {
                    Debug.Log($"GameManager: {toSubmit.Count} Results erfolgreich submitted");
                }
                else
                {
                    // Bei Fehler wieder hinzufügen für Retry
                    Debug.LogWarning("GameManager: Submit fehlgeschlagen, Results werden erneut versucht");
                    pendingResults.AddRange(toSubmit);
                }
            }));
        }
    }

    // === Öffentliche Getter ===
    public int GetTotalPoints() => totalPoints;
    public int GetPapersValidated() => papersValidated;
    public int GetMatchesFound() => matchesFound;
    public RuleData GetCurrentRule() => currentActiveRule;
    public int GetJobQueueSize() => jobQueue.Count;
}
