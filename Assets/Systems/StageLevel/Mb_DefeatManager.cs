// Mb_DefeatManager.cs
// Lives on the Stage GameObject alongside Mb_StageManager and Mb_WaveManager.
//
// Listens for two defeat triggers and runs the defeat end sequence via the
// shared RunEndSequence() method inherited from Mb_EndSequenceManager.
//
// DEFEAT TRIGGERS:
//   1. Mb_PanoharraManager.OnPanoharraDestroyed — the tree was destroyed by CuBots
//   2. Guardian's Mb_HealthComponent.OnDeath    — the player character died
//
// DOUBLE-TRIGGER GUARD:
//   Both triggers could fire the same frame (e.g. a boss kills the Guardian and
//   destroys the Panoharra simultaneously). The _isSequenceTriggered flag ensures
//   only the first trigger runs the sequence; the second is silently ignored.
//
// INSPECTOR SETUP:
//   - Add Mb_DefeatManager to the Stage GameObject.
//   - PlayerObject: optional fallback; dynamic stages bind to Mb_GuardianBase.CurrentGuardian.
//   - DefeatScreen: drag the Mb_DefeatScreenUI component.
//   - StageManager: drag the Mb_StageManager component (inherited field).
//   - SequenceDuration: defaults to 4f real seconds (inherited field).
//   - Message arrays are editable in the Inspector without touching code.

using UnityEngine;

public class Mb_DefeatManager : Mb_EndSequenceManager
{

    #region Inspector Fields            //----------------------------------------

    [Header("References")]
    [Tooltip("Drag the Guardian (player character) GameObject here.")]
    [SerializeField] private GameObject _PlayerObject;

    [Tooltip("Drag the Mb_DefeatScreenUI component here.")]
    [SerializeField] private Mb_DefeatScreenUI _DefeatScreen;

    [Header("Defeat Messages — Panoharra Destroyed")]
    [Tooltip("One message is picked at random when the Panoharra Tree is destroyed.")]
    [SerializeField]
    private string[] _PanoharraMessages = new string[]
    {
        "The Panoharra is gone. Without its heart, Talihaya falls to silence.",
        "You fought bravely, but the forest's soul could not be saved. Try again.",
        "The CuBots have claimed the Panoharra. The last forest is no more."
    };

    [Header("Defeat Messages — Guardian Fallen")]
    [Tooltip("One message is picked at random when the Guardian's health reaches zero.")]
    [SerializeField]
    private string[] _GuardianMessages = new string[]
    {
        "Your light fades... but the forest remembers your sacrifice.",
        "The Guardian has fallen. Without you, the forest has no voice.",
        "Talihaya weeps. Rise again and fight for what remains."
    };

    #endregion                          //----------------------------------------


    #region Runtime State               //----------------------------------------

    private bool _isSequenceTriggered = false;
    private Mb_HealthComponent _guardianHealth;

    #endregion                          //----------------------------------------


    #region Unity Lifecycle             //----------------------------------------

    private void Start()
    {
        BindGuardian(Mb_GuardianBase.CurrentGuardian);

        if (_guardianHealth == null && _PlayerObject != null)
            BindGuardian(_PlayerObject.GetComponent<Mb_GuardianBase>());

        if (_guardianHealth == null)
            Debug.LogError("[Mb_DefeatManager] Could not find Mb_HealthComponent on PlayerObject.");

        if (_DefeatScreen == null)
            Debug.LogError("[Mb_DefeatManager] DefeatScreen is not assigned in the Inspector.");

        if (_StageManager == null)
            Debug.LogWarning("[Mb_DefeatManager] StageManager is not assigned. " +
                             "EndStage() will not be called after the defeat sequence.");
    }


    private void OnEnable()
    {
        Mb_PanoharraManager.OnPanoharraDestroyed += HandlePanoharraDestroyed;
        Mb_GuardianBase.OnActiveGuardianChanged += BindGuardian;
    }


    private void OnDisable()
    {
        Mb_PanoharraManager.OnPanoharraDestroyed -= HandlePanoharraDestroyed;
        Mb_GuardianBase.OnActiveGuardianChanged -= BindGuardian;

        if (_guardianHealth != null)
            _guardianHealth.OnDeath -= HandleGuardianDeath;
    }

    #endregion                          //----------------------------------------


    #region Defeat Triggers             //----------------------------------------

    private void HandlePanoharraDestroyed() => TriggerDefeat(PickRandom(_PanoharraMessages));

    private void HandleGuardianDeath() => TriggerDefeat(PickRandom(_GuardianMessages));

    private void BindGuardian(Mb_GuardianBase guardian)
    {
        if (_guardianHealth != null)
            _guardianHealth.OnDeath -= HandleGuardianDeath;

        _guardianHealth = guardian != null
            ? guardian.GetComponent<Mb_HealthComponent>()
            : null;

        if (_guardianHealth != null)
            _guardianHealth.OnDeath += HandleGuardianDeath;
    }


    private void TriggerDefeat(string message)
    {
        if (_isSequenceTriggered) return;
        _isSequenceTriggered = true;

        Sc_EndSequenceConfig config = new Sc_EndSequenceConfig
        {
            TimeScaleMultiplier = 0.5f,
            TargetGameState = GameState.Defeat,
            Screen = _DefeatScreen
        };

        StartCoroutine(RunEndSequence(config, message));
    }

    #endregion                          //----------------------------------------
}
