// Scripts/UI/UIManager.cs
// HUD-Verwaltung - VEREINFACHT
// Nur Score, Matches, Feedback, aktive Regel - KEIN SCHNICKSCHNACK

using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
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
            activeRuleText.text = $"Suche: {ruleQuestion}";
        }
    }
}
