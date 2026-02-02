// Scripts/Voxel/CollectiblePaper.cs
// Komponente für einsammelbare Paper-Voxel-Strukturen
// NEUE ARCHITEKTUR: PlayerMatcher auf dem Player übernimmt das Matching!

using UnityEngine;

public class CollectiblePaper : MonoBehaviour
{
    // Paper-Daten
    public string paperId { get; private set; }
    public string section { get; private set; }
    public string jobId { get; private set; }

    // Vollständige Job-Daten für Matching
    public VoxelData jobData { get; private set; }

    // Legacy (für Kompatibilität)
    public float[] embedding => jobData?.section_embedding ?? jobData?.embedding;

    [Header("Effects")]
    [SerializeField] private GameObject collectEffectPrefab;
    [SerializeField] private AudioClip collectSound;

    /// <summary>
    /// Initialisiert die Paper-Daten aus VoxelData
    /// </summary>
    public void Initialize(VoxelData data)
    {
        if (data == null) return;

        jobData = data;
        paperId = data.paper_id;
        section = data.section ?? $"section_{data.section_id}";
        jobId = data.job_id;
    }

    /// <summary>
    /// Getter für VoxelData (für PlayerMatcher)
    /// </summary>
    public VoxelData GetVoxelData()
    {
        return jobData;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"CollectiblePaper: Trigger mit {other.name}, Tag: {other.tag}");

        // Nur auf Player-Tag reagieren
        if (other.CompareTag("Player"))
        {
            Debug.Log("CollectiblePaper: PLAYER COLLISION - Collecting!");

            // NEUE ARCHITEKTUR: PlayerMatcher auf Player übernimmt das Matching
            PlayerMatcher matcher = other.GetComponent<PlayerMatcher>();
            if (matcher != null)
            {
                // PlayerMatcher übernimmt alles (inkl. Destroy)
                matcher.OnCollectPaper(jobData, gameObject);
                return; // Nicht weitermachen, PlayerMatcher zerstört das Objekt
            }

            // LEGACY: Falls kein PlayerMatcher, alte Methode nutzen
            Collect(other.gameObject);
        }
    }

    /// <summary>
    /// LEGACY: Sammelt das Paper ein und informiert GameManager
    /// </summary>
    void Collect(GameObject player)
    {
        // GameManager über Einsammeln informieren (alte Methode)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPaperCollected(this);
        }
        else
        {
            Debug.LogWarning("CollectiblePaper: GameManager.Instance ist null!");
        }

        // Visual Effect spawnen
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }

        // Sound abspielen
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        // Struktur zerstören
        Destroy(gameObject);
    }

    // Debug-Visualisierung im Editor
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(transform.position + col.center, col.size);
        }
    }
}