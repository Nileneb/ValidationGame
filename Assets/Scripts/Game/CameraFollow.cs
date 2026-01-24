// Scripts/Game/CameraFollow.cs
// Third-Person Kamera die dem Spieler folgt
// Smooth Follow mit Shoulder-Offset

using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Position")]
    public float distance = 5f;
    public float height = 3f;
    public float shoulderOffset = 1f;
    public bool switchShoulder = false;

    [Header("Smoothing")]
    public float smoothTime = 0.2f;

    private Vector3 lookTarget;
    private Vector3 lookTargetVelocity;
    private Vector3 currentVelocity;

    void Start()
    {
        // Player automatisch finden falls nicht zugewiesen
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // Initiale Position setzen
        if (player != null)
        {
            lookTarget = player.position;
        }
    }

    void LateUpdate()
    {
        if (player == null)
        {
            Debug.LogWarning("CameraFollow: Kein Player zugewiesen!");
            return;
        }

        // Zielposition: HINTER dem Spieler (er läuft nach +Z, Kamera bei -Z)
        Vector3 targetPosition = player.position;
        targetPosition.z -= distance;  // Hinter dem Player
        targetPosition.y += height;    // Über dem Player

        // Shoulder-Offset (links/rechts)
        if (switchShoulder)
            targetPosition.x -= shoulderOffset;
        else
            targetPosition.x += shoulderOffset;

        // Smooth zur Zielposition bewegen
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);

        // Blickziel: leicht über dem Player
        Vector3 lookTargetPosition = player.position + Vector3.up * (height * 0.3f);
        lookTarget = Vector3.SmoothDamp(lookTarget, lookTargetPosition, ref lookTargetVelocity, smoothTime);
        transform.LookAt(lookTarget);
    }

    // Öffentliche Methode zum Setzen des Players
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
        if (player != null)
        {
            lookTarget = player.position;
        }
    }
}
