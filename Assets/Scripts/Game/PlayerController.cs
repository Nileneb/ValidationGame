// Scripts/Game/PlayerController.cs
// Spieler-Steuerung für Weltraum-Flieger
// Freie 3D-Bewegung, keine Gravitation
// Nutzt neues Input System

using UnityEngine;
using UnityEngine.InputSystem;
using InputSystemGyroscope = UnityEngine.InputSystem.Gyroscope;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Konstante Vorwärtsgeschwindigkeit")]
    [SerializeField] private float forwardSpeed = 10f;

    [Tooltip("Maximale Geschwindigkeit (mit Boost)")]
    [SerializeField] private float maxSpeed = 30f;

    [Tooltip("Beschleunigung pro Sekunde")]
    [SerializeField] private float acceleration = 15f;

    [Tooltip("Abbremsung pro Sekunde (wenn kein Boost)")]
    [SerializeField] private float deceleration = 10f;

    [Tooltip("Rotationsgeschwindigkeit für Links/Rechts-Drehung")]
    [SerializeField] private float horizontalRotationSpeed = 120f;

    [Tooltip("Rotationsgeschwindigkeit für Hoch/Runter-Drehung")]
    [SerializeField] private float verticalRotationSpeed = 120f;

    [Header("Vertical Movement")]
    [Tooltip("Aktiviere/Deaktiviere Bewegung auf der Y-Achse (Hoch/Runter)")]
    [SerializeField] private bool allowVerticalMovement = true;

    [Tooltip("Invertiere die Hoch/Runter-Steuerung")]
    [SerializeField] private bool invertVerticalControl = false;

    [Header("Gyroscope (Mobile)")]
    [Tooltip("Aktiviere/Deaktiviere Gyrosensor-Steuerung für Mobilgeräte")]
    [SerializeField] private bool useGyroscope = false;

    [Tooltip("Empfindlichkeit der Gyrosensor-Steuerung")]
    [SerializeField] private float gyroSensitivity = 2.5f;

    [Tooltip("Invertiere die Gyro-Steuerung links/rechts")]
    [SerializeField] private bool invertGyroHorizontal = false;

    [Tooltip("Invertiere die Gyro-Steuerung oben/unten")]
    [SerializeField] private bool invertGyroVertical = false;

    // Input Actions
    private InputAction moveAction;
    private InputAction boostAction;
    private InputAction gyroAttitudeAction;
    private InputAction gyroRotationRateAction;

    // Gyroscope State
    private bool gyroAvailable = false;
    private Quaternion initialGyroAttitude;
    private Quaternion calibrationQuaternion;

    // Speed State
    private float currentSpeed;
    private bool isBoosting = false;

    // Rigidbody für Physik (optional)
    private Rigidbody rb;

    void Awake()
    {
        // Rigidbody Setup (falls vorhanden)
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;  // Keine Gravitation im Weltraum!
            rb.linearDamping = 0f;        // Kein Luftwiderstand
            rb.angularDamping = 0.5f;
        }

        currentSpeed = forwardSpeed;
        SetupInputActions();
        CheckControlTypeSettings();
    }

    void SetupInputActions()
    {
        // Neues InputAction-Objekt für Bewegung (Vector2)
        moveAction = new InputAction(
            name: "Move",
            type: InputActionType.Value,
            expectedControlType: "Vector2"
        );

        // Composite für WASD + Pfeiltasten
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");

        // Gamepad Support
        moveAction.AddBinding("<Gamepad>/leftStick");

        moveAction.Enable();

        // Boost Action (Shift, Space, Gamepad-Trigger, oder Touch-Hold)
        boostAction = new InputAction(
            name: "Boost",
            type: InputActionType.Button
        );
        boostAction.AddBinding("<Keyboard>/leftShift");
        boostAction.AddBinding("<Keyboard>/rightShift");
        boostAction.AddBinding("<Keyboard>/space");
        boostAction.AddBinding("<Gamepad>/rightTrigger");
        boostAction.AddBinding("<Touchscreen>/primaryTouch/press");

        boostAction.Enable();

        // Gyroscope Actions einrichten
        gyroAttitudeAction = new InputAction(
            name: "GyroAttitude",
            type: InputActionType.Value,
            expectedControlType: "Quaternion"
        );
        gyroAttitudeAction.AddBinding("<Gyroscope>/attitude");

        gyroRotationRateAction = new InputAction(
            name: "GyroRotationRate",
            type: InputActionType.Value,
            expectedControlType: "Vector3"
        );
        gyroRotationRateAction.AddBinding("<Gyroscope>/angularVelocity");
    }

    void CheckControlTypeSettings()
    {
        // Standard: Tastatursteuerung für Desktop, Gyro für Mobile
#if UNITY_ANDROID || UNITY_IOS
        useGyroscope = true;
#else
        useGyroscope = false;
#endif

        // Gyrosensor initialisieren falls benötigt
        if (useGyroscope)
        {
            InitializeGyroscope();
        }
    }

    /// <summary>
    /// Öffentliche Methode zum Ändern der Steuerungsmethode
    /// </summary>
    public void SetUseGyroscope(bool useGyro)
    {
        if (useGyroscope == useGyro) return;

        useGyroscope = useGyro;

        if (useGyroscope)
        {
            InitializeGyroscope();
        }
        else
        {
            DisableGyroscope();
        }

        Debug.Log($"[PlayerController] Steuerung: {(useGyroscope ? "Gyrosensor" : "Tastatur/Gamepad")}");
    }

    void InitializeGyroscope()
    {
        // Prüfen ob Gyrosensor verfügbar ist
        if (Accelerometer.current != null && InputSystemGyroscope.current != null)
        {
            gyroAvailable = true;
            gyroAttitudeAction.Enable();
            gyroRotationRateAction.Enable();

            StartCoroutine(CalibrateGyroscopeAfterDelay(0.2f));
            Debug.Log("[PlayerController] Gyrosensor initialisiert");
        }
        else
        {
            Debug.LogWarning("[PlayerController] Gyrosensor nicht verfügbar - nutze Tastatur");
            useGyroscope = false;
            gyroAvailable = false;
        }
    }

    void DisableGyroscope()
    {
        if (gyroAvailable)
        {
            gyroAttitudeAction.Disable();
            gyroRotationRateAction.Disable();
            gyroAvailable = false;
        }
    }

    System.Collections.IEnumerator CalibrateGyroscopeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        initialGyroAttitude = gyroAttitudeAction.ReadValue<Quaternion>();
        calibrationQuaternion = Quaternion.Inverse(initialGyroAttitude);

        Debug.Log("[PlayerController] Gyrosensor kalibriert");
    }

    /// <summary>
    /// Gyrosensor neu kalibrieren (z.B. wenn Spieler Gerät neu ausrichtet)
    /// </summary>
    public void RecalibrateGyroscope()
    {
        if (gyroAvailable)
        {
            StartCoroutine(CalibrateGyroscopeAfterDelay(0f));
        }
    }

    void Update()
    {
        // Boost verarbeiten
        ProcessBoost();

        // Input verarbeiten
        if (useGyroscope && gyroAvailable)
        {
            ProcessGyroInput();
        }
        else
        {
            ProcessKeyboardInput();
        }

        // Kontinuierliche Vorwärtsbewegung in lokaler Z-Richtung
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime, Space.Self);
    }

    void ProcessBoost()
    {
        isBoosting = boostAction.IsPressed();

        if (isBoosting)
        {
            // Beschleunigen bis maxSpeed
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            // Abbremsen bis forwardSpeed (Basisgeschwindigkeit)
            currentSpeed = Mathf.MoveTowards(currentSpeed, forwardSpeed, deceleration * Time.deltaTime);
        }
    }

    void ProcessGyroInput()
    {
        if (!gyroAvailable) return;

        Vector3 gyroRates = gyroRotationRateAction.ReadValue<Vector3>();

        // Horizontale Rotation (Links/Rechts)
        float xRate = invertGyroHorizontal ? -gyroRates.y : gyroRates.y;
        float yRotation = xRate * horizontalRotationSpeed * gyroSensitivity * Time.deltaTime;
        transform.Rotate(0, yRotation, 0);

        // Vertikale Rotation (Hoch/Runter)
        if (allowVerticalMovement)
        {
            float zRate = invertGyroVertical ? -gyroRates.x : gyroRates.x;
            float xRotation = zRate * verticalRotationSpeed * gyroSensitivity * Time.deltaTime;
            transform.Rotate(xRotation, 0, 0);
        }
    }

    void ProcessKeyboardInput()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        // Y-Rotation (Links/Rechts drehen)
        float yRotation = input.x * horizontalRotationSpeed * Time.deltaTime;
        transform.Rotate(0, yRotation, 0);

        // X-Rotation (Hoch/Runter)
        if (allowVerticalMovement)
        {
            float verticalInput = invertVerticalControl ? -input.y : input.y;
            float xRotation = -verticalInput * verticalRotationSpeed * Time.deltaTime;
            transform.Rotate(xRotation, 0, 0);
        }
    }

    void OnDestroy()
    {
        moveAction?.Disable();
        boostAction?.Disable();

        if (gyroAvailable)
        {
            gyroAttitudeAction?.Disable();
            gyroRotationRateAction?.Disable();
        }
    }

    // === Öffentliche Getter/Setter ===

    public float GetForwardSpeed() => forwardSpeed;
    public float GetCurrentSpeed() => currentSpeed;
    public float GetMaxSpeed() => maxSpeed;
    public bool IsBoosting() => isBoosting;
    public float GetSpeedPercent() => (currentSpeed - forwardSpeed) / (maxSpeed - forwardSpeed);

    public void SetForwardSpeed(float speed) => forwardSpeed = speed;
    public void SetMaxSpeed(float speed) => maxSpeed = speed;
    public bool IsUsingGyroscope() => useGyroscope && gyroAvailable;
}
