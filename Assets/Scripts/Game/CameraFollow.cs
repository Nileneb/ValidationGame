// Scripts/Game/CameraFollow.cs
// Third-Person Kamera die dem Spieler folgt
// Sticky Follow mit optionalem Maus-Orbit für Umsehen

using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Position")]
    [Tooltip("Abstand hinter dem Spieler")]
    public float distance = 5f;

    [Tooltip("Höhe über dem Spieler")]
    public float height = 3f;

    [Tooltip("Seitlicher Offset (Schulter-Kamera)")]
    public float shoulderOffset = 1f;

    [Tooltip("Schulter wechseln")]
    public bool switchShoulder = false;

    [Header("Smoothing")]
    [Tooltip("Wie schnell die Kamera folgt (niedriger = smoother)")]
    public float positionSmoothTime = 0.1f;

    [Tooltip("Wie schnell die Rotation folgt")]
    public float rotationSmoothTime = 0.05f;

    [Header("Mouse Look (Optional)")]
    [Tooltip("Maus-Umsehen aktivieren")]
    public bool enableMouseLook = true;

    [Tooltip("Maus-Empfindlichkeit")]
    public float mouseSensitivity = 2f;

    [Tooltip("Maximaler horizontaler Blickwinkel")]
    public float maxHorizontalAngle = 60f;

    [Tooltip("Maximaler vertikaler Blickwinkel")]
    public float maxVerticalAngle = 30f;

    [Tooltip("Wie schnell der Blick zurück zur Mitte geht")]
    public float lookResetSpeed = 3f;

    [Tooltip("Automatisch zurücksetzen wenn keine Mausbewegung")]
    public bool autoResetLook = true;

    [Header("Zoom")]
    [Tooltip("Zoom mit Mausrad aktivieren")]
    public bool enableZoom = true;

    [Tooltip("Minimaler Abstand")]
    public float minDistance = 2f;

    [Tooltip("Maximaler Abstand")]
    public float maxDistance = 15f;

    [Tooltip("Zoom-Geschwindigkeit")]
    public float zoomSpeed = 2f;

    // Interne Zustände
    private Vector3 currentVelocity;
    private float currentRotationVelocity;
    private float targetDistance;

    // Maus-Look Offset
    private float horizontalLookOffset = 0f;
    private float verticalLookOffset = 0f;
    private float timeSinceLastInput = 0f;

    // Input System
    private Mouse _mouse;

    void Start()
    {
        _mouse = Mouse.current;
        targetDistance = distance;

        // Player automatisch finden falls nicht zugewiesen
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                // Fallback: PlayerController suchen
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null)
                {
                    player = pc.transform;
                }
            }
        }
    }

    void LateUpdate()
    {
        if (player == null)
        {
            Debug.LogWarning("CameraFollow: Kein Player zugewiesen!");
            return;
        }

        // Input verarbeiten
        HandleInput();

        // Kamera-Position und Rotation berechnen
        UpdateCameraPosition();
    }

    void HandleInput()
    {
        if (_mouse == null) return;

        // Maus-Look (nur wenn rechte Maustaste oder immer aktiv)
        if (enableMouseLook)
        {
            Vector2 mouseDelta = _mouse.delta.ReadValue();

            if (_mouse.rightButton.isPressed || Mathf.Abs(mouseDelta.x) > 0.1f || Mathf.Abs(mouseDelta.y) > 0.1f)
            {
                // Nur bei Rechtsklick oder signifikanter Bewegung
                if (_mouse.rightButton.isPressed)
                {
                    horizontalLookOffset += mouseDelta.x * mouseSensitivity * 0.1f;
                    verticalLookOffset -= mouseDelta.y * mouseSensitivity * 0.1f;

                    // Limits anwenden
                    horizontalLookOffset = Mathf.Clamp(horizontalLookOffset, -maxHorizontalAngle, maxHorizontalAngle);
                    verticalLookOffset = Mathf.Clamp(verticalLookOffset, -maxVerticalAngle, maxVerticalAngle);

                    timeSinceLastInput = 0f;
                }
            }
        }

        // Auto-Reset: Blick langsam zur Mitte zurückführen
        if (autoResetLook && !_mouse.rightButton.isPressed)
        {
            timeSinceLastInput += Time.deltaTime;

            if (timeSinceLastInput > 0.5f) // Nach 0.5s ohne Input
            {
                horizontalLookOffset = Mathf.Lerp(horizontalLookOffset, 0f, lookResetSpeed * Time.deltaTime);
                verticalLookOffset = Mathf.Lerp(verticalLookOffset, 0f, lookResetSpeed * Time.deltaTime);
            }
        }

        // Zoom mit Mausrad
        if (enableZoom)
        {
            float scroll = _mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetDistance -= scroll * zoomSpeed * 0.01f;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }
        }

        // Smooth Zoom
        distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * 10f);
    }

    void UpdateCameraPosition()
    {
        // Basis-Rotation vom Spieler übernehmen
        Quaternion playerRotation = player.rotation;

        // Zusätzliche Rotation durch Maus-Look
        Quaternion lookRotation = Quaternion.Euler(verticalLookOffset, horizontalLookOffset, 0f);
        Quaternion finalRotation = playerRotation * lookRotation;

        // Position: Hinter und über dem Spieler (in seiner lokalen Richtung)
        Vector3 offset = finalRotation * new Vector3(
            switchShoulder ? -shoulderOffset : shoulderOffset,
            height,
            -distance
        );

        Vector3 targetPosition = player.position + offset;

        // Smooth zur Zielposition
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            positionSmoothTime
        );

        // Blickrichtung: Auf einen Punkt vor dem Spieler
        Vector3 lookTarget = player.position + playerRotation * Vector3.forward * 10f;
        lookTarget += playerRotation * Vector3.up * (height * 0.3f);

        // Smooth Rotation zum Ziel
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime / rotationSmoothTime
        );
    }

    // === Öffentliche Methoden ===

    /// <summary>
    /// Player-Target setzen
    /// </summary>
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    /// <summary>
    /// Schulter wechseln (links/rechts)
    /// </summary>
    public void ToggleShoulder()
    {
        switchShoulder = !switchShoulder;
    }

    /// <summary>
    /// Blick-Offset zurücksetzen
    /// </summary>
    public void ResetLook()
    {
        horizontalLookOffset = 0f;
        verticalLookOffset = 0f;
    }

    /// <summary>
    /// Zoom zurücksetzen
    /// </summary>
    public void ResetZoom()
    {
        targetDistance = 5f;
    }
}
