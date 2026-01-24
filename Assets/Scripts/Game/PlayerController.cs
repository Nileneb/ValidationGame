// Scripts/Game/PlayerController.cs
// Spieler-Steuerung für den Endless Runner
// Nutzt neues Input System

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 10f;
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private float laneChangeSpeed = 5f;

    // 0=links, 1=mitte, 2=rechts
    private int currentLane = 1;
    private Vector3 targetPosition;

    [Header("Input")]
    [SerializeField] private float swipeThreshold = 50f;
    private Vector2 touchStartPos;
    private bool isTouching = false;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        // Forward Movement (konstant nach vorne)
        transform.Translate(Vector3.forward * forwardSpeed * Time.deltaTime);

        // Input verarbeiten
        HandleInput();

        // Smooth Lane Movement
        Vector3 newPosition = transform.position;
        float targetX = (currentLane - 1) * laneDistance;
        newPosition.x = Mathf.Lerp(newPosition.x, targetX, laneChangeSpeed * Time.deltaTime);
        transform.position = newPosition;
    }

    void HandleInput()
    {
        // Keyboard Input (neues Input System)
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                MoveLane(-1);
            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                MoveLane(1);
        }

        // Touch Input (neues Input System)
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            Vector2 touchPos = touchscreen.primaryTouch.position.ReadValue();

            if (!isTouching)
            {
                // Touch gestartet
                touchStartPos = touchPos;
                isTouching = true;
            }
        }
        else if (isTouching)
        {
            // Touch beendet - Swipe auswerten
            if (touchscreen != null)
            {
                Vector2 touchEndPos = touchscreen.primaryTouch.position.ReadValue();
                Vector2 swipeDelta = touchEndPos - touchStartPos;

                if (Mathf.Abs(swipeDelta.x) > swipeThreshold)
                {
                    if (swipeDelta.x > 0)
                        MoveLane(1);  // Rechts
                    else
                        MoveLane(-1); // Links
                }
            }
            isTouching = false;
        }
    }

    void MoveLane(int direction)
    {
        currentLane = Mathf.Clamp(currentLane + direction, 0, 2);
        float xPos = (currentLane - 1) * laneDistance;
        targetPosition = new Vector3(xPos, transform.position.y, transform.position.z);
    }

    // Öffentliche Getter für andere Scripts
    public int GetCurrentLane() => currentLane;
    public float GetForwardSpeed() => forwardSpeed;
}
