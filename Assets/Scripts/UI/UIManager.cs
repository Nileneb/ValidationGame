// Scripts/UI/UIManager.cs
// HUD-Verwaltung - Singleton Pattern
// Zeigt Score, Matches, Feedback und aktive Regel an

using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    // Singleton Instance
    public static UIManager Instance { get; private set; }

    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI matchesText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private TextMeshProUGUI activeRuleText;

    [Header("Feedback Settings")]
    [SerializeField] private float feedbackDuration = 2f;
    [SerializeField] private Color matchColor = Color.green;
    [SerializeField] private Color missColor = Color.white;

    void Awake()
    {
        // Singleton Setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Initial UI Setup
        UpdateScore(0, 0);

        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Aktualisiert Punkte und Match-Anzeige
    /// </summary>
    public void UpdateScore(int points, int matches)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Punkte: {points}";
        }

        if (matchesText != null)
        {
            matchesText.text = $"Matches: {matches}";
        }
    }

    /// <summary>
    /// Zeigt temporäres Feedback an
    /// </summary>
    public void ShowFeedback(string message)
    {
        if (feedbackText == null) return;

        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);

        // Farbe basierend auf Match/Miss
        if (message.Contains("MATCH"))
        {
            feedbackText.color = matchColor;
        }
        else
        {
            feedbackText.color = missColor;
        }

        // Vorherigen Hide-Aufruf abbrechen
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), feedbackDuration);
    }

    void HideFeedback()
    {
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Setzt die aktive Regel-Anzeige
    /// </summary>
    public void SetActiveRule(string ruleQuestion)
    {
        if (activeRuleText != null)
        {
            activeRuleText.text = $"🔍 Suche: {ruleQuestion}";
        }
    }

    /// <summary>
    /// Zeigt Game Over Screen (für spätere Implementierung)
    /// </summary>
    public void ShowGameOver(int finalScore, int totalMatches)
    {
        Debug.Log($"Game Over! Score: {finalScore}, Matches: {totalMatches}");
        // TODO: Game Over UI Panel aktivieren
    }
}
