// Scripts/UI/LeaderboardPanel.cs
// Leaderboard UI - Top Players mit Echtzeit-Updates via SSE
// P1 Feature: Zeigt Rangliste, Points, Rank-Changes

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LeaderboardPanel : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Container für Leaderboard-Einträge")]
    public Transform entriesContainer;
    
    [Tooltip("Prefab für einen Leaderboard-Eintrag")]
    public GameObject entryPrefab;
    
    [Tooltip("Panel Titel")]
    public TextMeshProUGUI titleText;
    
    [Tooltip("Eigener Rang Text")]
    public TextMeshProUGUI myRankText;

    [Header("Settings")]
    [Tooltip("Maximale angezeigte Einträge")]
    public int maxEntries = 100;
    
    [Tooltip("Highlight-Farbe für Rang-Änderungen")]
    public Color rankUpColor = new Color(0.3f, 0.8f, 0.3f);
    public Color rankDownColor = new Color(0.8f, 0.3f, 0.3f);
    public Color myRankColor = new Color(0.3f, 0.6f, 1f);

    [Header("Animation")]
    public float rankChangeAnimDuration = 1f;

    private List<LeaderboardEntryUI> _entryUIs = new List<LeaderboardEntryUI>();
    private string _myDeviceId;
    private CanvasGroup _canvasGroup;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _myDeviceId = PlayerPrefs.GetString("device_id", "");
    }

    void Start()
    {
        // Bei ApiClient für Updates registrieren
        ApiClient apiClient = FindAnyObjectByType<ApiClient>();
        if (apiClient != null)
        {
            apiClient.OnLeaderboardUpdate += OnLeaderboardUpdate;
        }

        // Initial ausgeblendet
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// SSE Event: Leaderboard wurde aktualisiert
    /// </summary>
    private void OnLeaderboardUpdate(LeaderboardEvent evt)
    {
        if (evt == null || evt.top_players == null) return;

        UpdateLeaderboard(evt.top_players, evt.changed_ranks);
    }

    /// <summary>
    /// Leaderboard mit neuen Daten aktualisieren
    /// </summary>
    public void UpdateLeaderboard(LeaderboardEntry[] entries, int[] changedRanks)
    {
        // Alte Einträge entfernen
        ClearEntries();

        HashSet<int> changedSet = new HashSet<int>();
        if (changedRanks != null)
        {
            foreach (int r in changedRanks)
            {
                changedSet.Add(r);
            }
        }

        int myRank = -1;
        int displayCount = Mathf.Min(entries.Length, maxEntries);

        for (int i = 0; i < displayCount; i++)
        {
            LeaderboardEntry entry = entries[i];
            
            GameObject entryObj;
            if (entryPrefab != null)
            {
                entryObj = Instantiate(entryPrefab, entriesContainer);
            }
            else
            {
                entryObj = CreateDefaultEntry();
            }

            LeaderboardEntryUI entryUI = SetupEntryUI(entryObj, entry, i + 1);
            _entryUIs.Add(entryUI);

            // Highlight bei Rang-Änderung
            if (changedSet.Contains(entry.rank))
            {
                AnimateRankChange(entryUI, entry.rank);
            }

            // Eigenen Rang markieren
            if (entry.device_id == _myDeviceId)
            {
                myRank = entry.rank;
                HighlightMyRank(entryUI);
            }
        }

        // Eigener Rang anzeigen
        if (myRankText != null)
        {
            myRankText.text = myRank > 0 ? $"Dein Rang: #{myRank}" : "Nicht platziert";
        }

        // Panel einblenden
        ShowPanel();
    }

    private LeaderboardEntryUI SetupEntryUI(GameObject obj, LeaderboardEntry entry, int displayRank)
    {
        LeaderboardEntryUI ui = obj.GetComponent<LeaderboardEntryUI>();
        if (ui == null)
        {
            ui = obj.AddComponent<LeaderboardEntryUI>();
        }

        // TextMeshPro Komponenten finden
        TextMeshProUGUI[] texts = obj.GetComponentsInChildren<TextMeshProUGUI>();
        
        if (texts.Length >= 3)
        {
            texts[0].text = $"#{displayRank}";
            texts[1].text = string.IsNullOrEmpty(entry.player_name) ? "Anonym" : entry.player_name;
            texts[2].text = $"{entry.total_points:N0} Pts";
        }
        else if (texts.Length >= 1)
        {
            texts[0].text = $"#{displayRank} {entry.player_name ?? "Anonym"} - {entry.total_points:N0} Pts";
        }

        ui.entry = entry;
        return ui;
    }

    private GameObject CreateDefaultEntry()
    {
        GameObject obj = new GameObject("LeaderboardEntry");
        obj.transform.SetParent(entriesContainer, false);

        // Horizontal Layout
        HorizontalLayoutGroup hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // Rank Text
        CreateEntryText(obj, "Rank", 50f);
        // Name Text
        CreateEntryText(obj, "Name", 150f);
        // Points Text
        CreateEntryText(obj, "Points", 100f);

        return obj;
    }

    private void CreateEntryText(GameObject parent, string name, float width)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Left;
        
        LayoutElement le = textObj.AddComponent<LayoutElement>();
        le.preferredWidth = width;
    }

    private void AnimateRankChange(LeaderboardEntryUI entryUI, int newRank)
    {
        // Einfache Farb-Animation für Rang-Änderung
        Image bg = entryUI.GetComponent<Image>();
        if (bg != null)
        {
            // Grün für Aufstieg, Rot für Abstieg (vereinfacht)
            bg.color = rankUpColor;
            StartCoroutine(FadeColor(bg, Color.clear, rankChangeAnimDuration));
        }
    }

    private void HighlightMyRank(LeaderboardEntryUI entryUI)
    {
        Image bg = entryUI.GetComponent<Image>();
        if (bg == null)
        {
            bg = entryUI.gameObject.AddComponent<Image>();
        }
        bg.color = new Color(myRankColor.r, myRankColor.g, myRankColor.b, 0.3f);
    }

    private System.Collections.IEnumerator FadeColor(Image img, Color target, float duration)
    {
        Color start = img.color;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            img.color = Color.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        
        img.color = target;
    }

    private void ClearEntries()
    {
        foreach (var ui in _entryUIs)
        {
            if (ui != null && ui.gameObject != null)
            {
                Destroy(ui.gameObject);
            }
        }
        _entryUIs.Clear();
    }

    public void ShowPanel()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }
        gameObject.SetActive(true);
    }

    public void HidePanel()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    public void TogglePanel()
    {
        if (_canvasGroup != null && _canvasGroup.alpha > 0)
        {
            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }

    void OnDestroy()
    {
        ApiClient apiClient = FindAnyObjectByType<ApiClient>();
        if (apiClient != null)
        {
            apiClient.OnLeaderboardUpdate -= OnLeaderboardUpdate;
        }
    }
}

/// <summary>
/// Hilfskomponente für einzelne Leaderboard-Einträge
/// </summary>
public class LeaderboardEntryUI : MonoBehaviour
{
    public LeaderboardEntry entry;
}
