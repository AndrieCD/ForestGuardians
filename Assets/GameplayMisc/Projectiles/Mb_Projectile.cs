using UnityEngine;

/// <summary>
/// A generic projectile fired by ranged abilities or CuBots.
///
/// USAGE:
///   1. Instantiate or fetch from pool
///   2. Call SetDamageAmount(), SetOwnerTag(), and SetOwner() before enabling
///   3. The projectile launches itself forward on OnEnable using the Rigidbody
///   4. It destroys itself on hitting a valid target or after MaxRange is exceeded
///
/// Inspector setup:
///   - Rigidbody must be attached (isKinematic = false at runtime)
///   - Assign launch speed and max range in the Inspector
/// </summary>
public class Mb_Projectile : MonoBehaviour
{
    [SerializeField] float LaunchSpeed = 20f;   // Units per second
    [SerializeField] float MaxRange = 40f;       // Self-destructs after travelling this far

    private Rigidbody _rb;
    private float _damageAmount;

    // Tag of the GameObject that fired this — used to skip friendly-fire collisions
    private string _ownerTag;

    // The character who fired this projectile — passed to SetLastAttacker() on hit
    // so kill-credit events (OnCuBotKill) correctly identify the attacker.
    // May be null if the projectile was fired by something that isn't a character
    // (e.g. an environmental hazard) — hit handling guards against null.
    private Mb_CharacterBase _owner;

    private Vector3 _spawnPosition;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            Debug.LogError($"[Mb_Projectile] No Rigidbody found on {gameObject.name}.");
    }


    private void OnEnable()
    {
        // Record spawn position so we can measure travel distance
        _spawnPosition = transform.position;

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = transform.forward * LaunchSpeed;
        }
    }


    private void Update()
    {
        // Self-destruct if the projectile has flown past its maximum range
        if (Vector3.Distance(_spawnPosition, transform.position) >= MaxRange)
            Destroy(gameObject); // TODO: return to pool once projectile pooling is added
    }


    // -------------------------------------------------------------------------
    // Setup API — call these after instantiation, before enabling
    // -------------------------------------------------------------------------

    /// <summary>Sets the damage this projectile will deal on hit.</summary>
    public void SetDamageAmount(float amount) => _damageAmount = amount;

    /// <summary>
    /// Sets the tag of the firing character so the projectile skips
    /// friendly-fire collisions. Example: pass "CuBot" for CuBot projectiles.
    /// </summary>
    public void SetOwnerTag(string tag) => _ownerTag = tag;

    /// <summary>
    /// Sets the character who fired this projectile.
    /// This is used to credit the kill to the correct character when the
    /// projectile kills an enemy — passed to MB_CuBotBase.SetLastAttacker()
    /// before TakeDamage() is called.
    /// Call this alongside SetOwnerTag() when firing from an ability.
    /// </summary>
    public void SetOwner(Mb_CharacterBase owner) => _owner = owner;

    /// <summary>Returns the damage amount — read externally if needed.</summary>
    public float GetDamageAmount() => _damageAmount;


    // -------------------------------------------------------------------------
    // Hit Detection
    // -------------------------------------------------------------------------

    private void OnCollisionEnter(Collision collision)
    {
        // Skip if the hit object belongs to the same faction as the firer
        if (!string.IsNullOrEmpty(_ownerTag) && collision.gameObject.CompareTag(_ownerTag))
            return;

        // If we hit a CuBot, register the attacker BEFORE calling TakeDamage().
        // This ensures that if this hit kills the CuBot, OnCuBotKill fires with
        // the correct attacker reference — Cycle of Life depends on this.
        MB_CuBotBase cuBot = collision.gameObject.GetComponent<MB_CuBotBase>();
        if (cuBot != null && _owner != null)
            cuBot.SetLastAttacker(_owner);

        // Deal damage through the interface — works for both Guardians and CuBots
        I_Damageable damageable = collision.gameObject.GetComponent<I_Damageable>();
        if (damageable != null)
            damageable.TakeDamage(_damageAmount);

        Destroy(gameObject); // TODO: return to pool once projectile pooling is added
    }
}