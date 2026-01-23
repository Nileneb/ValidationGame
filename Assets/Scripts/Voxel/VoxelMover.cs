// Scripts/Voxel/VoxelMover.cs
// Bewegt Voxel-Strukturen auf den Spieler zu
// Optional mit langsamer Rotation für visuellen Effekt

using UnityEngine;

public class VoxelMover : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = -5f;  // Negativ = auf Spieler zu

    [Header("Rotation")]
    [SerializeField] private bool rotateSlowly = true;
    [SerializeField] private float rotationSpeed = 30f;

    [Header("Optional Bobbing")]
    [SerializeField] private bool enableBobbing = false;
    [SerializeField] private float bobbingAmplitude = 0.3f;
    [SerializeField] private float bobbingSpeed = 2f;

    private float initialY;
    private float bobbingOffset;

    void Start()
    {
        initialY = transform.position.y;
        bobbingOffset = Random.Range(0f, Mathf.PI * 2f); // Zufälliger Start-Offset
    }

    void Update()
    {
        // Nach vorne bewegen (in World Space)
        transform.Translate(0, 0, moveSpeed * Time.deltaTime, Space.World);

        // Optional: Langsam rotieren für visuellen Effekt
        if (rotateSlowly)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }

        // Optional: Bobbing-Effekt (auf und ab)
        if (enableBobbing)
        {
            float newY = initialY + Mathf.Sin((Time.time + bobbingOffset) * bobbingSpeed) * bobbingAmplitude;
            Vector3 pos = transform.position;
            pos.y = newY;
            transform.position = pos;
        }
    }
}
