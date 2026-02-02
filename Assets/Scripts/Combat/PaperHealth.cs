// Assets/Scripts/Combat/PaperHealth.cs
// Gesundheits-System für Paper-Voxel-Strukturen
// Ermöglicht Schaden durch Projektile und Kollision

using UnityEngine;
using System;

public class PaperHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Contact Damage (Spieler berührt Paper)")]
    [Tooltip("Schaden den der Spieler bei Berührung nimmt")]
    [SerializeField] private float contactDamageToPlayer = 10f;
    
    [Tooltip("Schaden den das Paper bei Spieler-Berührung nimmt")]
    [SerializeField] private float contactDamageToSelf = 25f;

    [Header("Visual Feedback")]
    [Tooltip("Farbe bei niedrigem HP")]
    [SerializeField] private Color lowHealthColor = Color.red;
    
    [Tooltip("HP-Prozent ab dem Farbe wechselt")]
    [SerializeField] private float lowHealthThreshold = 0.3f;
    
    [Tooltip("Blinken bei Schaden")]
    [SerializeField] private bool flashOnDamage = true;
    [SerializeField] private float flashDuration = 0.1f;

    [Header("Death")]
    [Tooltip("VFX bei Zerstörung")]
    [SerializeField] private GameObject deathEffectPrefab;
    
    [Tooltip("Sound bei Zerstörung")]
    [SerializeField] private AudioClip deathSound;

    // Events
    public event Action<float> OnDamageTaken;  // (damage amount)
    public event Action OnDeath;

    // Components
    private CollectiblePaper paper;
    private Renderer[] renderers;
    private Color[] originalColors;
    private bool isDead = false;

    void Awake()
    {
        currentHealth = maxHealth;
        paper = GetComponent<CollectiblePaper>();
        
        // Renderer für Visual Feedback cachen
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material != null)
            {
                originalColors[i] = renderers[i].material.color;
            }
        }
    }

    /// <summary>
    /// Fügt Schaden zu
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        if (damage <= 0) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"PaperHealth: {gameObject.name} nimmt {damage} Schaden. HP: {currentHealth}/{maxHealth}");

        OnDamageTaken?.Invoke(damage);

        // Visual Feedback
        if (flashOnDamage)
        {
            StartCoroutine(FlashDamage());
        }
        
        UpdateVisualHealth();

        // Tod?
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Spieler-Kollision behandeln
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        
        if (other.CompareTag("Player"))
        {
            // Schaden an Spieler
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(contactDamageToPlayer);
                Debug.Log($"PaperHealth: Spieler berührt! {contactDamageToPlayer} Schaden an Spieler");
            }

            // Selbstschaden
            if (contactDamageToSelf > 0)
            {
                TakeDamage(contactDamageToSelf);
            }
        }
    }

    System.Collections.IEnumerator FlashDamage()
    {
        // Weiß blinken
        SetAllRenderersColor(Color.white);
        yield return new WaitForSeconds(flashDuration);
        
        // Zurück zur aktuellen Farbe
        UpdateVisualHealth();
    }

    void UpdateVisualHealth()
    {
        float healthPercent = currentHealth / maxHealth;
        
        if (healthPercent <= lowHealthThreshold)
        {
            // Rötlich färben basierend auf HP
            float t = healthPercent / lowHealthThreshold;
            Color targetColor = Color.Lerp(lowHealthColor, Color.white, t);
            
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material != null)
                {
                    Color original = originalColors[i];
                    renderers[i].material.color = original * targetColor;
                }
            }
        }
        else
        {
            // Originalfarben wiederherstellen
            RestoreOriginalColors();
        }
    }

    void SetAllRenderersColor(Color color)
    {
        foreach (var rend in renderers)
        {
            if (rend.material != null)
            {
                rend.material.color = color;
            }
        }
    }

    void RestoreOriginalColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material != null)
            {
                renderers[i].material.color = originalColors[i];
            }
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"PaperHealth: {gameObject.name} zerstört!");

        OnDeath?.Invoke();

        // Death Effect
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // Sound
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        // GameManager informieren (als "skip" werten?)
        // Optional: Punkte-Abzug für Zerstörung statt Einsammeln

        // Zerstören
        Destroy(gameObject);
    }

    // === Public API ===
    
    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public bool IsDead() => isDead;
    
    public void SetMaxHealth(float health)
    {
        maxHealth = health;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateVisualHealth();
    }
}
