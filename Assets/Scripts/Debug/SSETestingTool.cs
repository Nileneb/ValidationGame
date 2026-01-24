// Scripts/Debug/SSETestingTool.cs
// SSE Connection Testing und Mock Events
// P2 Feature: Debug-Panel für SSE-Verbindung, Event-Log, Mock-Events

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class SSETestingTool : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Status-Anzeige (Verbunden/Getrennt)")]
    public TextMeshProUGUI statusText;

    [Tooltip("Event-Log Text")]
    public TextMeshProUGUI eventLogText;

    [Tooltip("Verbinden Button")]
    public Button connectButton;

    [Tooltip("Trennen Button")]
    public Button disconnectButton;

    [Tooltip("Mock Paper Event Button")]
    public Button mockPaperButton;

    [Tooltip("Mock Leaderboard Event Button")]
    public Button mockLeaderboardButton;

    [Tooltip("Clear Log Button")]
    public Button clearLogButton;

    [Header("Settings")]
    public int maxLogEntries = 50;
    public Color connectedColor = new Color(0.3f, 0.8f, 0.3f);
    public Color disconnectedColor = new Color(0.8f, 0.3f, 0.3f);

    [Header("Mock Data")]
    [TextArea(3, 5)]
    public string mockPaperId = "test_paper_001";

    public float mockSimilarity = 0.85f;
    public int mockPoints = 85;

    private Queue<string> _logEntries = new Queue<string>();
    private ApiClient _apiClient;
    private bool _isConnected = false;

    void Start()
    {
        _apiClient = FindAnyObjectByType<ApiClient>();

        if (_apiClient != null)
        {
            _apiClient.OnSSEConnected += OnSSEConnected;
            _apiClient.OnSSEDisconnected += OnSSEDisconnected;
            _apiClient.OnPaperValidated += OnPaperValidated;
            _apiClient.OnLeaderboardUpdate += OnLeaderboardUpdate;
            _apiClient.OnNewPaper += OnNewPaper;
        }

        SetupButtons();
        UpdateStatus();
    }

    private void SetupButtons()
    {
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectClicked);
        }

        if (disconnectButton != null)
        {
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }

        if (mockPaperButton != null)
        {
            mockPaperButton.onClick.AddListener(OnMockPaperClicked);
        }

        if (mockLeaderboardButton != null)
        {
            mockLeaderboardButton.onClick.AddListener(OnMockLeaderboardClicked);
        }

        if (clearLogButton != null)
        {
            clearLogButton.onClick.AddListener(OnClearLogClicked);
        }
    }

    // Button Handlers
    private void OnConnectClicked()
    {
        if (_apiClient != null)
        {
            _apiClient.ConnectSSE();
            AddLog("[ACTION] SSE Verbindung gestartet...");
        }
    }

    private void OnDisconnectClicked()
    {
        if (_apiClient != null)
        {
            _apiClient.DisconnectSSE();
            AddLog("[ACTION] SSE Verbindung getrennt");
        }
    }

    private void OnMockPaperClicked()
    {
        AddLog("[MOCK] Generiere Paper Validated Event...");

        // Mock Event erstellen
        PaperValidatedEvent mockEvent = new PaperValidatedEvent
        {
            paper_id = mockPaperId,
            device_id = PlayerPrefs.GetString("device_id", "test_device"),
            result = new ValidationResultSSE
            {
                rule_id = "mock_rule",
                similarity = mockSimilarity,
                is_match = mockSimilarity >= 0.6f,
                points = mockPoints
            }
        };

        // Event auslösen (simuliert SSE)
        if (_apiClient != null)
        {
            // Reflection oder direkte Methode um Event zu triggern
            InvokePrivateEvent(_apiClient, "OnPaperValidated", mockEvent);
        }

        AddLog($"[MOCK] Paper Event gesendet: {mockPaperId}, Similarity: {mockSimilarity:P0}");
    }

    private void OnMockLeaderboardClicked()
    {
        AddLog("[MOCK] Generiere Leaderboard Event...");

        // Mock Leaderboard
        LeaderboardEntry[] mockEntries = new LeaderboardEntry[]
        {
            new LeaderboardEntry { rank = 1, device_id = "player_1", player_name = "TopPlayer", total_points = 10000 },
            new LeaderboardEntry { rank = 2, device_id = "player_2", player_name = "Runner-Up", total_points = 8500 },
            new LeaderboardEntry { rank = 3, device_id = PlayerPrefs.GetString("device_id", "me"), player_name = "Du", total_points = 7200 }
        };

        LeaderboardEvent mockEvent = new LeaderboardEvent
        {
            top_players = mockEntries,
            changed_ranks = new int[] { 2, 3 }
        };

        if (_apiClient != null)
        {
            InvokePrivateEvent(_apiClient, "OnLeaderboardUpdate", mockEvent);
        }

        AddLog("[MOCK] Leaderboard Event gesendet mit 3 Spielern");
    }

    private void OnClearLogClicked()
    {
        _logEntries.Clear();
        UpdateLogUI();
    }

    // SSE Event Handlers
    private void OnSSEConnected()
    {
        _isConnected = true;
        UpdateStatus();
        AddLog("[SSE] Verbindung hergestellt");
    }

    private void OnSSEDisconnected()
    {
        _isConnected = false;
        UpdateStatus();
        AddLog("[SSE] Verbindung getrennt");
    }

    private void OnPaperValidated(PaperValidatedEvent evt)
    {
        if (evt.result != null)
        {
            AddLog($"[EVENT] paper_validated: {evt.paper_id}, sim={evt.result.similarity:F2}, match={evt.result.is_match}");
        }
        else
        {
            AddLog($"[EVENT] paper_validated: {evt.paper_id} (kein Result)");
        }
    }

    private void OnLeaderboardUpdate(LeaderboardEvent evt)
    {
        int playerCount = evt.top_players?.Length ?? 0;
        int changedCount = evt.changed_ranks?.Length ?? 0;
        AddLog($"[EVENT] leaderboard_update: {playerCount} Spieler, {changedCount} Änderungen");
    }

    private void OnNewPaper(NewPaperEvent evt)
    {
        AddLog($"[EVENT] new_paper: {evt.paper_id} - {evt.title ?? "Kein Titel"}");
    }

    // UI Update
    private void UpdateStatus()
    {
        if (statusText != null)
        {
            statusText.text = _isConnected ? "● VERBUNDEN" : "○ GETRENNT";
            statusText.color = _isConnected ? connectedColor : disconnectedColor;
        }

        if (connectButton != null)
        {
            connectButton.interactable = !_isConnected;
        }

        if (disconnectButton != null)
        {
            disconnectButton.interactable = _isConnected;
        }
    }

    private void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string entry = $"[{timestamp}] {message}";

        _logEntries.Enqueue(entry);

        while (_logEntries.Count > maxLogEntries)
        {
            _logEntries.Dequeue();
        }

        UpdateLogUI();
        Debug.Log($"[SSETest] {message}");
    }

    private void UpdateLogUI()
    {
        if (eventLogText != null)
        {
            eventLogText.text = string.Join("\n", _logEntries);
        }
    }

    /// <summary>
    /// Hilfsmethode um private Events zu triggern (für Mock)
    /// </summary>
    private void InvokePrivateEvent<T>(ApiClient client, string eventName, T eventData)
    {
        // Event direkt über Delegate aufrufen
        var eventField = typeof(ApiClient).GetField(eventName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        if (eventField != null)
        {
            var del = eventField.GetValue(client) as Delegate;
            del?.DynamicInvoke(eventData);
        }
        else
        {
            // Fallback: Manuell die Handler aufrufen die wir kennen
            if (eventName == "OnPaperValidated" && eventData is PaperValidatedEvent pve)
            {
                // Alle registrierten Listener benachrichtigen (geht nicht direkt)
                AddLog("[MOCK] Event konnte nicht direkt getriggert werden - verwende UI Update");
            }
        }
    }

    void OnDestroy()
    {
        if (_apiClient != null)
        {
            _apiClient.OnSSEConnected -= OnSSEConnected;
            _apiClient.OnSSEDisconnected -= OnSSEDisconnected;
            _apiClient.OnPaperValidated -= OnPaperValidated;
            _apiClient.OnLeaderboardUpdate -= OnLeaderboardUpdate;
            _apiClient.OnNewPaper -= OnNewPaper;
        }
    }
}
