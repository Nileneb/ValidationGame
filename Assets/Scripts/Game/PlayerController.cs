// Scripts/Game/PlayerController.cs
// Spieler-Steuerung für den Endless Runner
// Touch-Swipe auf Android, Keyboard-Fallback für Editor-Testing

using UnityEngine;

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
        // Swipe Detection für Android
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                Vector2 swipeDelta = touch.position - touchStartPos;

                // Horizontal Swipe
                if (Mathf.Abs(swipeDelta.x) > swipeThreshold)
                {
                    if (swipeDelta.x > 0)
                        MoveLane(1);  // Rechts
                    else
                        MoveLane(-1); // Links
                }
            }
        }

        // Fallback: Keyboard für Editor-Testing
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            MoveLane(-1);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            MoveLane(1);
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
