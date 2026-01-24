// Scripts/UI/ValidationProgressUI.cs
// Validation Progress Bar - zeigt Fortschritt der Paper-Validierung
// P1 Feature: Stimmen-Zähler, Acceptance/Rejection Bar, Animation

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ValidationProgressUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Hintergrund der Progress Bar")]
    public Image backgroundBar;
    
    [Tooltip("Accept Votes Balken (grün)")]
    public Image acceptBar;
    
    [Tooltip("Reject Votes Balken (rot)")]
    public Image rejectBar;
    
    [Tooltip("Threshold Marker")]
    public RectTransform thresholdMarker;
    
    [Tooltip("Paper ID Text")]
    public TextMeshProUGUI paperIdText;
    
    [Tooltip("Vote Count Text")]
    public TextMeshProUGUI voteCountText;
    
    [Tooltip("Status Text (Pending/Accepted/Rejected)")]
    public TextMeshProUGUI statusText;

    [Header("Settings")]
    public Color acceptColor = new Color(0.3f, 0.8f, 0.3f);
    public Color rejectColor = new Color(0.8f, 0.3f, 0.3f);
    public Color pendingColor = new Color(0.8f, 0.8f, 0.3f);
    public float animationSpeed = 5f;

    // Aktuelle Werte
    private int _totalVotes;
    private int _acceptVotes;
    private int _rejectVotes;
    private float _threshold = 0.6f;
    private string _currentPaperId;
    
    // Animation
    private float _targetAcceptFill;
    private float _targetRejectFill;

    void Start()
    {
        // Bei ApiClient für Updates registrieren
        ApiClient apiClient = FindAnyObjectByType<ApiClient>();
        if (apiClient != null)
        {
            apiClient.OnPaperValidated += OnPaperValidated;
            apiClient.OnNewPaper += OnNewPaper;
        }

        // Initial leer
        ResetProgress();
    }

    /// <summary>
    /// SSE Event: Paper wurde validiert (Vote eingegangen)
    /// </summary>
    private void OnPaperValidated(PaperValidatedEvent evt)
    {
        if (evt == null) return;

        // Bei neuem Paper: Reset
        if (evt.paper_id != _currentPaperId)
        {
            ResetProgress();
            _currentPaperId = evt.paper_id;
        }

        // Vote zählen (Accept wenn similarity > threshold)
        _totalVotes++;
        if (evt.result.similarity >= _threshold)
        {
            _acceptVotes++;
        }
        else
        {
            _rejectVotes++;
        }

        UpdateUI();
    }

    /// <summary>
    /// SSE Event: Neues Paper empfangen
    /// </summary>
    private void OnNewPaper(NewPaperEvent evt)
    {
        if (evt == null) return;

        ResetProgress();
        _currentPaperId = evt.paper_id;
        
        if (paperIdText != null)
        {
            paperIdText.text = $"Paper: {evt.paper_id}";
        }
    }

    /// <summary>
    /// Progress für spezifisches Paper setzen (manueller Aufruf)
    /// </summary>
    public void SetProgress(string paperId, int acceptVotes, int rejectVotes, float threshold = 0.6f)
    {
        _currentPaperId = paperId;
        _acceptVotes = acceptVotes;
        _rejectVotes = rejectVotes;
        _totalVotes = acceptVotes + rejectVotes;
        _threshold = threshold;
        
        UpdateUI();
    }

    private void UpdateUI()
    {
        // Prozentwerte berechnen
        float acceptPercent = _totalVotes > 0 ? (float)_acceptVotes / _totalVotes : 0f;
        float rejectPercent = _totalVotes > 0 ? (float)_rejectVotes / _totalVotes : 0f;

        // Ziel-Füllwerte setzen (für Animation)
        _targetAcceptFill = acceptPercent;
        _targetRejectFill = rejectPercent;

        // Paper ID
        if (paperIdText != null)
        {
            paperIdText.text = $"Paper: {_currentPaperId ?? "N/A"}";
        }

        // Vote Count
        if (voteCountText != null)
        {
            voteCountText.text = $"Stimmen: {_totalVotes} ({_acceptVotes}↑ / {_rejectVotes}↓)";
        }

        // Status
        if (statusText != null)
        {
            if (_totalVotes < 3)
            {
                statusText.text = "Ausstehend...";
                statusText.color = pendingColor;
            }
            else if (acceptPercent >= _threshold)
            {
                statusText.text = "AKZEPTIERT";
                statusText.color = acceptColor;
            }
            else
            {
                statusText.text = "ABGELEHNT";
                statusText.color = rejectColor;
            }
        }

        // Threshold Marker positionieren
        if (thresholdMarker != null)
        {
            Vector2 pos = thresholdMarker.anchoredPosition;
            pos.x = _threshold * backgroundBar.rectTransform.rect.width;
            thresholdMarker.anchoredPosition = pos;
        }
    }

    void Update()
    {
        // Smooth Animation für Balken
        if (acceptBar != null)
        {
            acceptBar.fillAmount = Mathf.Lerp(acceptBar.fillAmount, _targetAcceptFill, Time.deltaTime * animationSpeed);
        }

        if (rejectBar != null)
        {
            rejectBar.fillAmount = Mathf.Lerp(rejectBar.fillAmount, _targetRejectFill, Time.deltaTime * animationSpeed);
        }
    }

    public void ResetProgress()
    {
        _totalVotes = 0;
        _acceptVotes = 0;
        _rejectVotes = 0;
        _currentPaperId = null;
        _targetAcceptFill = 0f;
        _targetRejectFill = 0f;

        if (acceptBar != null) acceptBar.fillAmount = 0f;
        if (rejectBar != null) rejectBar.fillAmount = 0f;
        if (paperIdText != null) paperIdText.text = "Paper: -";
        if (voteCountText != null) voteCountText.text = "Stimmen: 0";
        if (statusText != null)
        {
            statusText.text = "Warte auf Paper...";
            statusText.color = pendingColor;
        }
    }

    void OnDestroy()
    {
        ApiClient apiClient = FindAnyObjectByType<ApiClient>();
        if (apiClient != null)
        {
            apiClient.OnPaperValidated -= OnPaperValidated;
            apiClient.OnNewPaper -= OnNewPaper;
        }
    }
}
