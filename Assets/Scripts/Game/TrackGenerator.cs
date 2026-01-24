// Scripts/Game/TrackGenerator.cs
// Generiert endlose Track-Segmente vor dem Spieler
// Recyclet alte Segmente für Performance

using UnityEngine;
using System.Collections.Generic;

public class TrackGenerator : MonoBehaviour
{
    [Header("Track Segments")]
    [SerializeField] private GameObject trackSegmentPrefab;
    [SerializeField] private float segmentLength = 20f;
    [SerializeField] private float segmentWidth = 10f;
    [SerializeField] private int visibleSegments = 5;
    [SerializeField] private bool autoCreateSegments = true;

    [Header("References")]
    [SerializeField] private Transform player;

    private Queue<GameObject> activeSegments = new Queue<GameObject>();
    private float nextSpawnZ = 0f;
    private Material trackMaterial;

    void Start()
    {
        // Material erstellen
        trackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        trackMaterial.color = new Color(0.25f, 0.25f, 0.3f);

        // Initial Segments spawnen
        for (int i = 0; i < visibleSegments; i++)
        {
            SpawnSegment();
        }
    }

    void Update()
    {
        // Wenn Spieler nahe genug am Ende ist → neues Segment spawnen
        if (player != null && player.position.z > nextSpawnZ - (visibleSegments * segmentLength))
        {
            SpawnSegment();
            RecycleSegment();
        }
    }

    void SpawnSegment()
    {
        GameObject segment;

        if (trackSegmentPrefab != null)
        {
            // Prefab verwenden
            segment = Instantiate(
                trackSegmentPrefab,
                new Vector3(0, 0, nextSpawnZ),
                Quaternion.identity
            );
        }
        else if (autoCreateSegments)
        {
            // Automatisch Plane erstellen
            segment = GameObject.CreatePrimitive(PrimitiveType.Plane);
            segment.transform.position = new Vector3(0, 0, nextSpawnZ + segmentLength / 2);
            segment.transform.localScale = new Vector3(segmentWidth / 10f, 1, segmentLength / 10f);

            // Material zuweisen
            Renderer rend = segment.GetComponent<Renderer>();
            if (rend != null && trackMaterial != null)
            {
                rend.material = trackMaterial;
            }
        }
        else
        {
            Debug.LogWarning("TrackGenerator: Kein Track Segment Prefab zugewiesen!");
            return;
        }

        segment.name = $"TrackSegment_{nextSpawnZ}";
        activeSegments.Enqueue(segment);
        nextSpawnZ += segmentLength;
    }

    void RecycleSegment()
    {
        // Ältestes Segment entfernen wenn zu viele aktiv
        if (activeSegments.Count > visibleSegments)
        {
            GameObject oldSegment = activeSegments.Dequeue();
            Destroy(oldSegment);
        }
    }

    // Setter für Player-Referenz (falls dynamisch gesetzt)
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }
}
