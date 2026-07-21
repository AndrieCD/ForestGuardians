using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives Luxion-specific boss animation parameters.
/// Shared CuBot locomotion, hit, and death animation handling remains on Mb_CuBotAnimator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class Mb_LuxionAnimator : MonoBehaviour
{
    private static readonly int _AxeSlashHash = Animator.StringToHash("Luxion_AxeSlash");
    private static readonly int _SpinHarvestHash = Animator.StringToHash("Luxion_SpinHarvest");
    private static readonly int _RifleShotHash = Animator.StringToHash("Luxion_RifleShot");
    private static readonly int _BulletRainHash = Animator.StringToHash("Luxion_BulletRain");
    private static readonly int _PickaxeSlamHash = Animator.StringToHash("Luxion_PickaxeSlam");
    private static readonly int _EarthBreakerHash = Animator.StringToHash("Luxion_EarthBreaker");
    private static readonly int _PhaseAcquisitionHash = Animator.StringToHash("Luxion_PhaseAcquisition");
    private static readonly int _PhaseConsumptionHash = Animator.StringToHash("Luxion_PhaseConsumption");
    private static readonly int _PhaseHash = Animator.StringToHash("Luxion_Phase");
    private static readonly int _IsPhaseTransitioningHash = Animator.StringToHash("Luxion_IsPhaseTransitioning");

    [Header("References")]
    [SerializeField] private Animator _Animator;

    private readonly HashSet<int> _availableParameters = new HashSet<int>();

    private void Awake()
    {
        if (_Animator == null)
            _Animator = GetComponent<Animator>();

        CacheAvailableParameters();
    }

    public void TriggerAxeSlash()
    {
        SetTriggerIfPresent(_AxeSlashHash);
    }

    public void TriggerSpinHarvest()
    {
        SetTriggerIfPresent(_SpinHarvestHash);
    }

    public void TriggerRifleShot()
    {
        SetTriggerIfPresent(_RifleShotHash);
    }

    public void TriggerBulletRain()
    {
        SetTriggerIfPresent(_BulletRainHash);
    }

    public void TriggerPickaxeSlam()
    {
        SetTriggerIfPresent(_PickaxeSlamHash);
    }

    public void TriggerEarthBreaker()
    {
        SetTriggerIfPresent(_EarthBreakerHash);
    }

    public void TriggerPhaseTransition(LuxionPhase nextPhase)
    {
        SetBoolIfPresent(_IsPhaseTransitioningHash, true);

        if (nextPhase == LuxionPhase.Acquisition)
            SetTriggerIfPresent(_PhaseAcquisitionHash);
        else if (nextPhase == LuxionPhase.Consumption)
            SetTriggerIfPresent(_PhaseConsumptionHash);
    }

    public void SetPhase(LuxionPhase phase)
    {
        SetIntegerIfPresent(_PhaseHash, (int)phase);
        SetBoolIfPresent(_IsPhaseTransitioningHash, false);
    }

    public void CancelLuxionTriggers()
    {
        ResetTriggerIfPresent(_AxeSlashHash);
        ResetTriggerIfPresent(_SpinHarvestHash);
        ResetTriggerIfPresent(_RifleShotHash);
        ResetTriggerIfPresent(_BulletRainHash);
        ResetTriggerIfPresent(_PickaxeSlamHash);
        ResetTriggerIfPresent(_EarthBreakerHash);
        ResetTriggerIfPresent(_PhaseAcquisitionHash);
        ResetTriggerIfPresent(_PhaseConsumptionHash);
    }

    private void CacheAvailableParameters()
    {
        _availableParameters.Clear();

        if (_Animator == null) return;

        foreach (AnimatorControllerParameter parameter in _Animator.parameters)
            _availableParameters.Add(parameter.nameHash);
    }

    private void SetTriggerIfPresent(int parameterHash)
    {
        if (!HasParameter(parameterHash)) return;

        _Animator.SetTrigger(parameterHash);
    }

    private void ResetTriggerIfPresent(int parameterHash)
    {
        if (!HasParameter(parameterHash)) return;

        _Animator.ResetTrigger(parameterHash);
    }

    private void SetIntegerIfPresent(int parameterHash, int value)
    {
        if (!HasParameter(parameterHash)) return;

        _Animator.SetInteger(parameterHash, value);
    }

    private void SetBoolIfPresent(int parameterHash, bool value)
    {
        if (!HasParameter(parameterHash)) return;

        _Animator.SetBool(parameterHash, value);
    }

    private bool HasParameter(int parameterHash)
    {
        return _Animator != null && _availableParameters.Contains(parameterHash);
    }
}
