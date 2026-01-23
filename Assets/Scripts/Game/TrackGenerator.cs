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
    [SerializeField] private int visibleSegments = 5;

    [Header("References")]
    [SerializeField] private Transform player;

    private Queue<GameObject> activeSegments = new Queue<GameObject>();
    private float nextSpawnZ = 0f;

    void Start()
    {
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
        if (trackSegmentPrefab == null)
        {
            Debug.LogWarning("TrackGenerator: Kein Track Segment Prefab zugewiesen!");
            return;
        }

        GameObject segment = Instantiate(
            trackSegmentPrefab,
            new Vector3(0, 0, nextSpawnZ),
            Quaternion.identity
        );
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
