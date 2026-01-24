// Scripts/UI/NotificationManager.cs
// Toast Notification System für Events
// P2 Feature: Zeigt temporäre Nachrichten für Paper-Events, Rang-Änderungen, etc.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Container für Notification Prefabs")]
    public Transform notificationContainer;

    [Tooltip("Notification Prefab")]
    public GameObject notificationPrefab;

    [Header("Settings")]
    [Tooltip("Standard-Anzeigedauer in Sekunden")]
    public float defaultDuration = 3f;

    [Tooltip("Maximale gleichzeitige Notifications")]
    public int maxNotifications = 5;

    [Tooltip("Abstand zwischen Notifications")]
    public float spacing = 60f;

    [Header("Colors")]
    public Color infoColor = new Color(0.3f, 0.6f, 1f);
    public Color successColor = new Color(0.3f, 0.8f, 0.3f);
    public Color warningColor = new Color(0.9f, 0.7f, 0.2f);
    public Color errorColor = new Color(0.8f, 0.3f, 0.3f);

    [Header("Animation")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.5f;

    private Queue<NotificationData> _pendingNotifications = new Queue<NotificationData>();
    private List<GameObject> _activeNotifications = new List<GameObject>();

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

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
        // Bei ApiClient für Events registrieren
        ApiClient apiClient = FindAnyObjectByType<ApiClient>();
        if (apiClient != null)
        {
            apiClient.OnPaperValidated += OnPaperValidated;
            apiClient.OnLeaderboardUpdate += OnLeaderboardUpdate;
            apiClient.OnNewPaper += OnNewPaper;
            apiClient.OnSSEConnected += OnSSEConnected;
            apiClient.OnSSEDisconnected += OnSSEDisconnected;
        }
    }

    // SSE Event Handlers
    private void OnPaperValidated(PaperValidatedEvent evt)
    {
        // Null-Check für result Objekt
        if (evt.result != null)
        {
            string message = $"Paper validiert: {evt.paper_id}\nSimilarity: {evt.result.similarity:P0} - {evt.result.points} Punkte";
            NotificationType type = evt.result.is_match ? NotificationType.Success : NotificationType.Info;
            Show(message, type);
        }
        else
        {
            Show($"Paper erhalten: {evt.paper_id}", NotificationType.Info);
        }
    }

    private void OnLeaderboardUpdate(LeaderboardEvent evt)
    {
        if (evt.changed_ranks != null && evt.changed_ranks.Length > 0)
        {
            Show($"Rangliste aktualisiert!\n{evt.changed_ranks.Length} Rang-Änderungen", NotificationType.Info);
        }
    }

    private void OnNewPaper(NewPaperEvent evt)
    {
        Show($"Neues Paper: {evt.paper_id}\n{evt.title ?? "Unbekannter Titel"}", NotificationType.Info);
    }

    private void OnSSEConnected()
    {
        Show("Mit Server verbunden", NotificationType.Success);
    }

    private void OnSSEDisconnected()
    {
        Show("Verbindung zum Server verloren", NotificationType.Warning);
    }

    /// <summary>
    /// Notification anzeigen
    /// </summary>
    public void Show(string message, NotificationType type = NotificationType.Info, float duration = -1f)
    {
        if (duration < 0) duration = defaultDuration;

        NotificationData data = new NotificationData
        {
            message = message,
            type = type,
            duration = duration
        };

        if (_activeNotifications.Count >= maxNotifications)
        {
            _pendingNotifications.Enqueue(data);
        }
        else
        {
            DisplayNotification(data);
        }
    }

    /// <summary>
    /// Kurzform: Info-Notification
    /// </summary>
    public void ShowInfo(string message) => Show(message, NotificationType.Info);

    /// <summary>
    /// Kurzform: Success-Notification
    /// </summary>
    public void ShowSuccess(string message) => Show(message, NotificationType.Success);

    /// <summary>
    /// Kurzform: Warning-Notification
    /// </summary>
    public void ShowWarning(string message) => Show(message, NotificationType.Warning);

    /// <summary>
    /// Kurzform: Error-Notification
    /// </summary>
    public void ShowError(string message) => Show(message, NotificationType.Error);

    private void DisplayNotification(NotificationData data)
    {
        GameObject notifObj;

        if (notificationPrefab != null)
        {
            notifObj = Instantiate(notificationPrefab, notificationContainer);
        }
        else
        {
            notifObj = CreateDefaultNotification();
        }

        // Position setzen (stack von oben)
        RectTransform rect = notifObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = new Vector2(0, -_activeNotifications.Count * spacing);
        }

        // Text und Farbe setzen
        TextMeshProUGUI text = notifObj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = data.message;
        }

        Image bg = notifObj.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = GetColorForType(data.type);
        }

        _activeNotifications.Add(notifObj);

        // Animation & Auto-Dismiss
        StartCoroutine(NotificationLifecycle(notifObj, data.duration));
    }

    private GameObject CreateDefaultNotification()
    {
        GameObject obj = new GameObject("Notification");
        obj.transform.SetParent(notificationContainer, false);

        // RectTransform
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300f, 50f);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = Vector2.zero;

        // Background Image
        Image bg = obj.AddComponent<Image>();
        bg.color = infoColor;

        // CanvasGroup für Fade
        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 12;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        return obj;
    }

    private IEnumerator NotificationLifecycle(GameObject notifObj, float duration)
    {
        CanvasGroup cg = notifObj.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = notifObj.AddComponent<CanvasGroup>();
        }

        // Fade In
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        cg.alpha = 1f;

        // Warten
        yield return new WaitForSeconds(duration);

        // Fade Out
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        // Entfernen
        RemoveNotification(notifObj);
    }

    private void RemoveNotification(GameObject notifObj)
    {
        _activeNotifications.Remove(notifObj);
        Destroy(notifObj);

        // Positionen aktualisieren
        RepositionNotifications();

        // Nächste aus Queue anzeigen
        if (_pendingNotifications.Count > 0 && _activeNotifications.Count < maxNotifications)
        {
            DisplayNotification(_pendingNotifications.Dequeue());
        }
    }

    private void RepositionNotifications()
    {
        for (int i = 0; i < _activeNotifications.Count; i++)
        {
            RectTransform rect = _activeNotifications[i].GetComponent<RectTransform>();
            if (rect != null)
            {
                Vector2 target = new Vector2(0, -i * spacing);
                StartCoroutine(SmoothMove(rect, target, 0.2f));
            }
        }
    }

    private IEnumerator SmoothMove(RectTransform rect, Vector2 target, float duration)
    {
        Vector2 start = rect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rect.anchoredPosition = Vector2.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        rect.anchoredPosition = target;
    }

    private Color GetColorForType(NotificationType type)
    {
        switch (type)
        {
            case NotificationType.Success: return successColor;
            case NotificationType.Warning: return warningColor;
            case NotificationType.Error: return errorColor;
            default: return infoColor;
        }
    }

    /// <summary>
    /// Alle Notifications löschen
    /// </summary>
    public void ClearAll()
    {
        StopAllCoroutines();

        foreach (var notif in _activeNotifications)
        {
            if (notif != null)
            {
                Destroy(notif);
            }
        }

        _activeNotifications.Clear();
        _pendingNotifications.Clear();
    }

    void OnDestroy()
    {
        ApiClient apiClient = FindAnyObjectByType<ApiClient>();
        if (apiClient != null)
        {
            apiClient.OnPaperValidated -= OnPaperValidated;
            apiClient.OnLeaderboardUpdate -= OnLeaderboardUpdate;
            apiClient.OnNewPaper -= OnNewPaper;
            apiClient.OnSSEConnected -= OnSSEConnected;
            apiClient.OnSSEDisconnected -= OnSSEDisconnected;
        }
    }
}

public struct NotificationData
{
    public string message;
    public NotificationManager.NotificationType type;
    public float duration;
}
