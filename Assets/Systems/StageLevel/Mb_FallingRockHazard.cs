using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime damage component for a falling rock instance.
/// Damages each Guardian or CuBot once, then destroys itself after its lifetime.
/// </summary>
public class Mb_FallingRockHazard : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Damage dealt once to each Guardian or CuBot hit.")]
    [SerializeField] private float Damage = 25f;

    [Tooltip("Seconds before this falling rock is destroyed.")]
    [SerializeField] private float Lifetime = 10f;

    private readonly HashSet<Mb_CharacterBase> _damagedCharacters =
        new HashSet<Mb_CharacterBase>();

    private float _destroyTime;

    private void OnEnable()
    {
        _damagedCharacters.Clear();
        _destroyTime = Time.time + Lifetime;
    }

    private void Update()
    {
        if (Time.time < _destroyTime) return;

        Destroy(gameObject);
    }

    public void Initialize(float damage, float lifetime)
    {
        Damage = damage;
        Lifetime = lifetime;
        _destroyTime = Time.time + Lifetime;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider);
    }

    private void TryDamage(Collider other)
    {
        if (other == null) return;

        Mb_CharacterBase character = other.GetComponent<Mb_CharacterBase>();
        if (character == null)
            character = other.GetComponentInParent<Mb_CharacterBase>();

        if (character == null) return;
        if (character.Health == null) return;
        if (character.Health.IsDead) return;
        if (!IsValidTarget(character)) return;
        if (!_damagedCharacters.Add(character)) return;

        character.Health.TakeDamage(Damage);
    }

    private bool IsValidTarget(Mb_CharacterBase character)
    {
        return character is Mb_GuardianBase || character is MB_CuBotBase;
    }
}
