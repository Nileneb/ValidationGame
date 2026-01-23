// Scripts/Voxel/CollectiblePaper.cs
// Komponente für einsammelbare Paper-Voxel-Strukturen
// Triggert GameManager bei Kollision mit Spieler

using UnityEngine;

public class CollectiblePaper : MonoBehaviour
{
    // Paper-Daten
    public string paperId { get; private set; }
    public string section { get; private set; }
    public float[] embedding { get; private set; }

    [Header("Effects")]
    [SerializeField] private GameObject collectEffectPrefab;
    [SerializeField] private AudioClip collectSound;

    /// <summary>
    /// Initialisiert die Paper-Daten aus VoxelData
    /// </summary>
    public void Initialize(VoxelData data)
    {
        if (data == null) return;

        paperId = data.paper_id;
        section = data.section;
        embedding = data.embedding;
    }

    void OnTriggerEnter(Collider other)
    {
        // Nur auf Player-Tag reagieren
        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    void Collect(GameObject player)
    {
        // GameManager über Einsammeln informieren
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
