// Assets/Scripts/Combat/PlayerHealth.cs
// Spieler-Gesundheit
// Nimmt Schaden bei Paper-Berührung

using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Invincibility")]
    [Tooltip("Sekunden Unverwundbarkeit nach Schaden")]
    [SerializeField] private float invincibilityDuration = 0.5f;
    
    [Header("Visual")]
    [Tooltip("Blinken bei Schaden")]
    [SerializeField] private bool flashOnDamage = true;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private int flashCount = 3;

    [Header("Audio")]
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;

    // Events
    public event Action<float> OnDamageTaken;  // (damage)
    public event Action<float> OnHealthChanged;  // (current health)
    public event Action OnDeath;

    // State
    private bool isInvincible = false;
    private bool isDead = false;
    private Renderer[] renderers;

    void Awake()
    {
        currentHealth = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        // UI initial updaten
        OnHealthChanged?.Invoke(currentHealth);
    }

    /// <summary>
    /// Fügt Schaden zu
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        if (isInvincible) return;
        if (damage <= 0) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"PlayerHealth: {damage} Schaden! HP: {currentHealth}/{maxHealth}");

        OnDamageTaken?.Invoke(damage);
        OnHealthChanged?.Invoke(currentHealth);

        // Sound
        if (hurtSound != null)
        {
            AudioSource.PlayClipAtPoint(hurtSound, transform.position, 0.7f);
        }

        // Visual Feedback
        if (flashOnDamage)
        {
            StartCoroutine(DamageFlash());
        }

        // Invincibility Frames
        StartCoroutine(InvincibilityCoroutine());

        // Tod?
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    System.Collections.IEnumerator DamageFlash()
    {
        for (int i = 0; i < flashCount; i++)
        {
            SetRenderersVisible(false);
            yield return new WaitForSeconds(flashDuration);
            SetRenderersVisible(true);
            yield return new WaitForSeconds(flashDuration);
        }
    }

    System.Collections.IEnumerator InvincibilityCoroutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    void SetRenderersVisible(bool visible)
    {
        foreach (var rend in renderers)
        {
            rend.enabled = visible;
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("PlayerHealth: Spieler ist gestorben!");

        // Sound
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        OnDeath?.Invoke();

        // Game Over handling via GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDeath();
        }
    }

    // === Public API ===

    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public bool IsDead() => isDead;
    public bool IsInvincible() => isInvincible;

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void SetMaxHealth(float health)
    {
        maxHealth = health;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        isInvincible = false;
        OnHealthChanged?.Invoke(currentHealth);
    }
}
