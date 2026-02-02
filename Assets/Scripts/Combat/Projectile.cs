// Assets/Scripts/Combat/Projectile.cs
// Projektil-Verhalten
// Fliegt vorwärts, trifft Paper-Voxel-Strukturen

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float speed = 50f;
    [SerializeField] private float lifetime = 3f;

    [Header("Visual")]
    [Tooltip("Trail Renderer (optional)")]
    [SerializeField] private TrailRenderer trail;
    
    [Tooltip("VFX bei Treffer (optional)")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] [Range(0f, 1f)] private float hitVolume = 0.5f;

    private Rigidbody rb;
    private bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Collider als Trigger für Paper-Strukturen
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// Initialisiert das Projektil (von PlayerWeapon aufgerufen)
    /// </summary>
    public void Initialize(float projectileSpeed, float projectileLifetime)
    {
        speed = projectileSpeed;
        lifetime = projectileLifetime;
        
        // Geschwindigkeit setzen
        rb.linearVelocity = transform.forward * speed;
        
        // Auto-Destroy
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // Paper-Struktur getroffen?
        CollectiblePaper paper = other.GetComponent<CollectiblePaper>();
        if (paper == null)
        {
            paper = other.GetComponentInParent<CollectiblePaper>();
        }

        if (paper != null)
        {
            hasHit = true;
            
            // Schaden an Paper
            PaperHealth health = paper.GetComponent<PaperHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
                Debug.Log($"Projectile: Treffer auf {paper.paperId} - {damage} Schaden");
            }
            else
            {
                Debug.Log($"Projectile: Treffer auf {paper.paperId} (kein PaperHealth)");
            }

            // Hit Effect
            SpawnHitEffect();
            
            // Sound
            PlayHitSound();

            // Projektil zerstören
            DestroyProjectile();
        }
        else if (!other.CompareTag("Player"))
        {
            // Andere Objekte (außer Spieler) - einfach zerstören
            SpawnHitEffect();
            DestroyProjectile();
        }
    }

    void SpawnHitEffect()
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    void PlayHitSound()
    {
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position, hitVolume);
        }
    }

    void DestroyProjectile()
    {
        // Trail detachen damit er ausfadet
        if (trail != null)
        {
            trail.transform.SetParent(null);
            trail.autodestruct = true;
        }

        Destroy(gameObject);
    }

    // === Public API ===
    
    public void SetDamage(float dmg) => damage = dmg;
    public float GetDamage() => damage;
}
