using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI Panel zur Anzeige von Paper-Informationen
/// Zeigt Title, ID, Voxel-Stats, Thumbnail und Validierungsergebnisse
/// </summary>
public class PaperInfoPanel : MonoBehaviour
{
    [Header("Text Elements")]
    [Tooltip("Titel des Papers")]
    public TextMeshProUGUI titleText;

    [Tooltip("Paper-ID Anzeige")]
    public TextMeshProUGUI paperIdText;

    [Tooltip("Voxel-Statistiken (Anzahl, Density, Fill)")]
    public TextMeshProUGUI statsText;

    [Tooltip("Zeitstempel der Validierung")]
    public TextMeshProUGUI timestampText;

    [Header("Images")]
    [Tooltip("Thumbnail-Vorschau des Papers")]
    public Image thumbnailImage;

    [Tooltip("Fallback wenn kein Thumbnail vorhanden")]
    public Sprite defaultThumbnail;

    [Header("Rules Display")]
    [Tooltip("Container für Rule-Ergebnisse")]
    public Transform rulesContainer;

    [Tooltip("Prefab für einzelnes Rule-Result Item")]
    public GameObject ruleItemPrefab;

    [Header("Animation")]
    [Tooltip("Animator für Panel-Animation")]
    public Animator panelAnimator;

    [Tooltip("Animation Trigger Name für neues Paper")]
    public string showAnimTrigger = "Show";

    [Header("Settings")]
    [Tooltip("Panel automatisch ausblenden nach X Sekunden (0 = nie)")]
    public float autoHideDelay = 0f;

    private CanvasGroup _canvasGroup;
    private Coroutine _hideCoroutine;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        // Anfangs ausgeblendet
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// Paper-Informationen im Panel anzeigen
    /// </summary>
    public void DisplayPaper(PaperValidatedEvent paper, RuleResultSSE[] rules = null)
    {
        if (paper == null) return;

        // Auto-Hide Timer zurücksetzen
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
        }

        // Titel
        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(paper.title) ? "Unbekanntes Paper" : paper.title;
        }

        // Paper ID
        if (paperIdText != null)
        {
            paperIdText.text = paper.paper_id ?? "";
        }

        // Timestamp
        if (timestampText != null && !string.IsNullOrEmpty(paper.timestamp))
        {
            timestampText.text = paper.timestamp;
        }

        // Voxel-Statistiken
        DisplayStats(paper.voxel_data);

        // Thumbnail (Base64 → Sprite)
        DisplayThumbnail(paper.thumbnail_base64);

        // Rules Results
        DisplayRules(rules);

        // Panel einblenden
        ShowPanel();

        // Auto-Hide starten
        if (autoHideDelay > 0)
        {
            _hideCoroutine = StartCoroutine(AutoHide());
        }
    }

    /// <summary>
    /// Voxel-Statistiken formatiert anzeigen
    /// </summary>
    private void DisplayStats(VoxelGridData voxelData)
    {
        if (statsText == null) return;

        if (voxelData != null && voxelData.stats != null)
        {
            var stats = voxelData.stats;
            statsText.text = $"<b>Voxels:</b> {stats.total:N0}\n" +
                           $"<b>Density:</b> {stats.density_avg:F2}\n" +
                           $"<b>Fill:</b> {stats.fill_ratio:P0}\n" +
                           $"<b>Grid:</b> {voxelData.GridX}×{voxelData.GridY}×{voxelData.GridZ}";
        }
        else
        {
            statsText.text = "Keine Voxel-Daten";
        }
    }

    /// <summary>
    /// Base64 Thumbnail dekodieren und anzeigen
    /// </summary>
    private void DisplayThumbnail(string base64Data)
    {
        if (thumbnailImage == null) return;

        if (!string.IsNullOrEmpty(base64Data))
        {
            try
            {
                // Base64 dekodieren
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(imageBytes))
                {
                    // Sprite aus Texture erstellen
                    thumbnailImage.sprite = Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    thumbnailImage.color = Color.white;
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaperInfoPanel] Thumbnail dekodieren fehlgeschlagen: {e.Message}");
            }
        }

        // Fallback auf Default
        if (defaultThumbnail != null)
        {
            thumbnailImage.sprite = defaultThumbnail;
            thumbnailImage.color = Color.white;
        }
        else
        {
            thumbnailImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        }
    }

    /// <summary>
    /// Validierungsergebnisse als Liste anzeigen
    /// </summary>
    private void DisplayRules(RuleResultSSE[] rules)
    {
        if (rulesContainer == null) return;

        // Alte Items entfernen
        foreach (Transform child in rulesContainer)
        {
            Destroy(child.gameObject);
        }

        if (rules == null || rules.Length == 0) return;

        // Neue Items erstellen
        foreach (var rule in rules)
        {
            if (ruleItemPrefab != null)
            {
                GameObject item = Instantiate(ruleItemPrefab, rulesContainer);
                SetupRuleItem(item, rule);
            }
            else
            {
                // Fallback: Einfaches Text-Item erstellen
                CreateSimpleRuleItem(rule);
            }
        }
    }

    /// <summary>
    /// Rule Item Prefab mit Daten füllen
    /// Erwartet zwei TextMeshProUGUI Kinder: [0]=Name, [1]=Status
    /// </summary>
    private void SetupRuleItem(GameObject item, RuleResultSSE rule)
    {
        var texts = item.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length >= 2)
        {
            // Rule Name
            texts[0].text = rule.rule_name ?? rule.rule_id;

            // Status (✓ oder ✗)
            texts[1].text = rule.passed ? "✓" : "✗";
            texts[1].color = rule.passed ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
        }

        // Optional: Similarity als Tooltip oder dritten Text
        if (texts.Length >= 3)
        {
            texts[2].text = $"{rule.similarity:P0}";
        }
    }

    /// <summary>
    /// Einfaches Rule-Item ohne Prefab erstellen
    /// </summary>
    private void CreateSimpleRuleItem(RuleResultSSE rule)
    {
        GameObject item = new GameObject($"Rule_{rule.rule_id}");
        item.transform.SetParent(rulesContainer, false);

        TextMeshProUGUI text = item.AddComponent<TextMeshProUGUI>();
        text.fontSize = 14;
        text.alignment = TextAlignmentOptions.Left;

        string status = rule.passed ? "<color=#4CAF50>✓</color>" : "<color=#F44336>✗</color>";
        text.text = $"{status} {rule.rule_name ?? rule.rule_id}";
    }

    /// <summary>
    /// Panel einblenden
    /// </summary>
    public void ShowPanel()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        if (panelAnimator != null && !string.IsNullOrEmpty(showAnimTrigger))
        {
            panelAnimator.SetTrigger(showAnimTrigger);
        }
    }

    /// <summary>
    /// Panel ausblenden
    /// </summary>
    public void HidePanel()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Panel nach Delay automatisch ausblenden
    /// </summary>
    private System.Collections.IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(autoHideDelay);
        HidePanel();
    }

    /// <summary>
    /// Panel leeren
    /// </summary>
    public void Clear()
    {
        if (titleText != null) titleText.text = "";
        if (paperIdText != null) paperIdText.text = "";
        if (statsText != null) statsText.text = "";
        if (timestampText != null) timestampText.text = "";

        if (thumbnailImage != null && defaultThumbnail != null)
        {
            thumbnailImage.sprite = defaultThumbnail;
        }

        if (rulesContainer != null)
        {
            foreach (Transform child in rulesContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
