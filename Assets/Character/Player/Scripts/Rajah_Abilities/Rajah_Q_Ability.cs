using System.Collections.Generic;
using UnityEngine;

public class Rajah_Q_Ability : Sc_BaseAbility
{
    private Camera _cam;

    [Header("Dash Settings")]
    public float dashSpeed = 80;       
    public float dashDuration = 0.175f;   // How long the dash lasts in seconds

    public Rajah_Q_Ability(SO_Ability abilityObject, Mb_CharacterBase user)
        : base(abilityObject, user)
    {
        _cam = Camera.main;
        _Cooldown = _AbilityData.Cooldown;
    }

    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} equipped {_AbilityData.AbilityName}.");
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown( )) return;

        // Dash direction: camera forward, flattened to XZ so we don't fly upward
        Vector3 dashDirection = _cam.transform.forward;
        dashDirection.y = 0f;
        dashDirection.Normalize( );

        // If somehow forward is straight up (edge case), fall back to player forward
        if (dashDirection == Vector3.zero)
            dashDirection = user.transform.forward;

        Vector3 dashVelocity = dashDirection * dashSpeed;

        // Drive the dash through CharacterController via Mb_Movement
        user.Movement.StartDash(dashVelocity, dashDuration);

        // TODO: Hit detection on enemies in dash path goes here (Physics.OverlapCapsule, etc.)
        // TODO: Shield calculation based on enemies hit goes here

        StartCooldown(user);

        Debug.Log($"{user.name} activated {_AbilityData.AbilityName} — dashing {dashDirection}.");
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} unequipped {_AbilityData.AbilityName}.");
    }
}