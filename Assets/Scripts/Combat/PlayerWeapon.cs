// Assets/Scripts/Combat/PlayerWeapon.cs
// Waffen-System für Spieler
// Nutzt Input System "Attack" Action (linke Maustaste)

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Projectile")]
    [Tooltip("Projektil-Prefab (erstelle in Unity mit Collider + Rigidbody)")]
    [SerializeField] private GameObject projectilePrefab;
    
    [Tooltip("Spawn-Punkt für Projektile (z.B. Schiffs-Nase)")]
    [SerializeField] private Transform firePoint;
    
    [Tooltip("Projektil-Geschwindigkeit")]
    [SerializeField] private float projectileSpeed = 50f;
    
    [Tooltip("Projektil-Lebensdauer in Sekunden")]
    [SerializeField] private float projectileLifetime = 3f;

    [Header("Fire Rate")]
    [Tooltip("Schüsse pro Sekunde")]
    [SerializeField] private float fireRate = 5f;
    
    [Tooltip("Automatisches Feuer bei gehaltenem Button")]
    [SerializeField] private bool autoFire = true;

    [Header("Muzzle Flash")]
    [Tooltip("VFX Prefab für Mündungsfeuer (optional)")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    
    [Tooltip("Dauer des Mündungsfeuers")]
    [SerializeField] private float muzzleFlashDuration = 0.1f;

    [Header("Audio")]
    [Tooltip("Schuss-Sound")]
    [SerializeField] private AudioClip fireSound;
    
    [Tooltip("Lautstärke")]
    [SerializeField] [Range(0f, 1f)] private float volume = 0.5f;

    // Input
    private InputAction fireAction;
    
    // State
    private float nextFireTime = 0f;
    private bool isFiring = false;
    private AudioSource audioSource;
    private GameObject activeMuzzleFlash;

    void Awake()
    {
        SetupInput();
        SetupAudio();
        
        // Auto-create fire point if not set
        if (firePoint == null)
        {
            firePoint = transform;
            Debug.LogWarning("PlayerWeapon: Kein FirePoint gesetzt, nutze Transform");
        }
    }

    void SetupInput()
    {
        // Nutze existierende "Attack" Action aus InputSystem_Actions
        fireAction = new InputAction(
            name: "Fire",
            type: InputActionType.Button
        );
        
        // Bindings wie in InputSystem_Actions.inputactions
        fireAction.AddBinding("<Mouse>/leftButton");
        fireAction.AddBinding("<Gamepad>/buttonWest");
        fireAction.AddBinding("<Gamepad>/rightTrigger");
        fireAction.AddBinding("<Keyboard>/enter");
        fireAction.AddBinding("<Touchscreen>/primaryTouch/tap");
        
        fireAction.Enable();
        
        // Callbacks
        fireAction.started += OnFireStarted;
        fireAction.canceled += OnFireCanceled;
    }

    void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D Sound
    }

    void OnFireStarted(InputAction.CallbackContext context)
    {
        isFiring = true;
        
        // Sofort feuern
        if (Time.time >= nextFireTime)
        {
            Fire();
        }
    }

    void OnFireCanceled(InputAction.CallbackContext context)
    {
        isFiring = false;
    }

    void Update()
    {
        // Auto-fire wenn gehalten
        if (autoFire && isFiring && Time.time >= nextFireTime)
        {
            Fire();
        }
    }

    /// <summary>
    /// Feuert ein Projektil
    /// </summary>
    public void Fire()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("PlayerWeapon: Kein Projektil-Prefab zugewiesen!");
            return;
        }

        // Cooldown setzen
        nextFireTime = Time.time + (1f / fireRate);

        // Projektil spawnen
        GameObject projectile = Instantiate(
            projectilePrefab, 
            firePoint.position, 
            firePoint.rotation
        );

        // Projektil initialisieren
        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(projectileSpeed, projectileLifetime);
        }
        else
        {
            // Fallback: Rigidbody direkt bewegen
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = firePoint.forward * projectileSpeed;
            }
            Destroy(projectile, projectileLifetime);
        }

        // Mündungsfeuer
        ShowMuzzleFlash();

        // Sound
        PlayFireSound();

        Debug.Log($"PlayerWeapon: FIRE! Projektil gespawnt bei {firePoint.position}");
    }

    void ShowMuzzleFlash()
    {
        if (muzzleFlashPrefab == null) return;

        // Altes Mündungsfeuer löschen
        if (activeMuzzleFlash != null)
        {
            Destroy(activeMuzzleFlash);
        }

        // Neues spawnen
        activeMuzzleFlash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
        Destroy(activeMuzzleFlash, muzzleFlashDuration);
    }

    void PlayFireSound()
    {
        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound, volume);
        }
    }

    void OnDestroy()
    {
        if (fireAction != null)
        {
            fireAction.started -= OnFireStarted;
            fireAction.canceled -= OnFireCanceled;
            fireAction.Disable();
        }
    }

    // === Public API ===
    
    public void SetProjectilePrefab(GameObject prefab) => projectilePrefab = prefab;
    public void SetFireRate(float rate) => fireRate = rate;
    public void SetAutoFire(bool auto) => autoFire = auto;
    public bool IsFiring() => isFiring;
}
