// Scripts/Camera/OrbitCameraController.cs
// Orbit/Zoom/Pan Kamera-Steuerung
// P2 Feature: Freie Kamera-Kontrolle für Voxel-Inspektion

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class OrbitCameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Objekt um das die Kamera rotiert")]
    public Transform target;

    [Tooltip("Automatisch zum nächsten Voxel-Mesh wechseln")]
    public bool autoTarget = true;

    [Header("Orbit Settings")]
    [Tooltip("Rotationsgeschwindigkeit")]
    public float rotationSpeed = 5f;

    [Tooltip("Zoom-Geschwindigkeit")]
    public float zoomSpeed = 5f;

    [Tooltip("Pan-Geschwindigkeit")]
    public float panSpeed = 0.5f;

    [Header("Limits")]
    public float minDistance = 2f;
    public float maxDistance = 50f;
    public float minVerticalAngle = -80f;
    public float maxVerticalAngle = 80f;

    [Header("Smoothing")]
    public float smoothTime = 0.1f;

    [Header("Input (Touch/Mouse)")]
    [Tooltip("Touch-Finger für Orbit (0=erster Finger)")]
    public int orbitFinger = 0;

    [Tooltip("Pinch-Zoom aktivieren")]
    public bool enablePinchZoom = true;

    [Tooltip("Two-Finger Pan aktivieren")]
    public bool enableTwoFingerPan = true;

    // Interne Zustände
    private float _distance = 10f;
    private float _horizontalAngle = 0f;
    private float _verticalAngle = 30f;
    private Vector3 _targetOffset = Vector3.zero;

    // Smooth Interpolation
    private float _targetDistance;
    private float _targetHorizontalAngle;
    private float _targetVerticalAngle;
    private Vector3 _velocity = Vector3.zero;

    // Touch-Tracking
    private Vector2 _lastTouchPos;
    private float _lastPinchDistance;
    private bool _isPanning = false;

    // Input System
    private Mouse _mouse;
    private Vector2 _lastMousePos;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        _mouse = Mouse.current;

        if (target == null && autoTarget)
        {
            FindVoxelTarget();
        }

        _targetDistance = _distance;
        _targetHorizontalAngle = _horizontalAngle;
        _targetVerticalAngle = _verticalAngle;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            if (autoTarget)
            {
                FindVoxelTarget();
            }
            return;
        }

        HandleInput();
        UpdateCamera();
    }

    private void HandleInput()
    {
        // Touch Input (Mobile) - New Input System
        if (Touch.activeTouches.Count > 0)
        {
            HandleTouchInput();
        }
        // Mouse Input (Editor/Desktop)
        else if (_mouse != null)
        {
            HandleMouseInput();
        }
    }

    private void HandleTouchInput()
    {
        var touches = Touch.activeTouches;

        // Pinch Zoom (2 Finger)
        if (touches.Count == 2 && enablePinchZoom)
        {
            Touch t0 = touches[0];
            Touch t1 = touches[1];

            float pinchDist = Vector2.Distance(t0.screenPosition, t1.screenPosition);

            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                _lastPinchDistance = pinchDist;
                _isPanning = false;
            }
            else if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
            {
                float delta = pinchDist - _lastPinchDistance;
                _targetDistance -= delta * zoomSpeed * 0.01f;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
                _lastPinchDistance = pinchDist;

                // Two-Finger Pan
                if (enableTwoFingerPan)
                {
                    Vector2 midPoint = (t0.screenPosition + t1.screenPosition) * 0.5f;
                    if (!_isPanning)
                    {
                        _lastTouchPos = midPoint;
                        _isPanning = true;
                    }
                    else
                    {
                        Vector2 panDelta = midPoint - _lastTouchPos;
                        DoPan(panDelta);
                        _lastTouchPos = midPoint;
                    }
                }
            }
        }
        // Orbit (1 Finger)
        else if (touches.Count == 1)
        {
            Touch touch = touches[orbitFinger < touches.Count ? orbitFinger : 0];

            if (touch.phase == TouchPhase.Moved)
            {
                _targetHorizontalAngle += touch.delta.x * rotationSpeed * 0.1f;
                _targetVerticalAngle -= touch.delta.y * rotationSpeed * 0.1f;
                _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, minVerticalAngle, maxVerticalAngle);
            }

            _isPanning = false;
        }
    }

    private void HandleMouseInput()
    {
        Vector2 currentMousePos = _mouse.position.ReadValue();
        Vector2 mouseDelta = _mouse.delta.ReadValue();

        // Orbit (Linke Maustaste)
        if (_mouse.leftButton.isPressed)
        {
            _targetHorizontalAngle += mouseDelta.x * rotationSpeed * 0.1f;
            _targetVerticalAngle -= mouseDelta.y * rotationSpeed * 0.1f;
            _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        // Pan (Rechte Maustaste oder Mittlere Maustaste)
        if (_mouse.rightButton.isPressed || _mouse.middleButton.isPressed)
        {
            DoPan(mouseDelta);
        }

        // Zoom (Mausrad)
        float scroll = _mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetDistance -= scroll * zoomSpeed * 0.01f;
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }

        _lastMousePos = currentMousePos;
    }

    private void DoPan(Vector2 screenDelta)
    {
        Vector3 right = transform.right * screenDelta.x * panSpeed * 0.01f * _distance;
        Vector3 up = transform.up * screenDelta.y * panSpeed * 0.01f * _distance;
        _targetOffset -= (right + up);
    }

    private void UpdateCamera()
    {
        // Smooth Interpolation
        _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _velocity.x, smoothTime);
        _horizontalAngle = Mathf.LerpAngle(_horizontalAngle, _targetHorizontalAngle, Time.deltaTime / smoothTime);
        _verticalAngle = Mathf.Lerp(_verticalAngle, _targetVerticalAngle, Time.deltaTime / smoothTime);

        // Position berechnen
        float radH = _horizontalAngle * Mathf.Deg2Rad;
        float radV = _verticalAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radV) * Mathf.Sin(radH),
            Mathf.Sin(radV),
            Mathf.Cos(radV) * Mathf.Cos(radH)
        ) * _distance;

        Vector3 targetPos = target.position + _targetOffset;
        transform.position = targetPos + offset;
        transform.LookAt(targetPos);
    }

    /// <summary>
    /// Findet das nächste Voxel-Mesh als Target
    /// </summary>
    public void FindVoxelTarget()
    {
        // Suche nach VoxelMeshBuilder oder Tag "VoxelStructure"
        VoxelMeshBuilder voxelBuilder = FindAnyObjectByType<VoxelMeshBuilder>();
        if (voxelBuilder != null)
        {
            target = voxelBuilder.transform;
            return;
        }

        GameObject voxelObj = GameObject.FindGameObjectWithTag("VoxelStructure");
        if (voxelObj != null)
        {
            target = voxelObj.transform;
            return;
        }

        // Fallback: Erstes Objekt mit MeshRenderer und "Voxel" im Namen
        MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r.gameObject.name.Contains("Voxel"))
            {
                target = r.transform;
                return;
            }
        }
    }

    /// <summary>
    /// Target wechseln
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _targetOffset = Vector3.zero;
    }

    /// <summary>
    /// Kamera auf Target zentrieren
    /// </summary>
    public void CenterOnTarget()
    {
        _targetOffset = Vector3.zero;
    }

    /// <summary>
    /// Kamera zurücksetzen
    /// </summary>
    public void ResetCamera()
    {
        _targetDistance = 10f;
        _targetHorizontalAngle = 0f;
        _targetVerticalAngle = 30f;
        _targetOffset = Vector3.zero;
    }

    /// <summary>
    /// Zoom auf Objekt anpassen (Bounds-basiert)
    /// </summary>
    public void FitToBounds(Bounds bounds)
    {
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        _targetDistance = maxExtent * 3f;
        _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        _targetOffset = bounds.center - target.position;
    }
}
