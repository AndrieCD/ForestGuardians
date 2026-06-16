//// Mb_TutorialManager.cs
//// Drives dialog and completion tracking for tutorial stages.
//// Lives on the Stage GameObject in tutorial scenes only —
//// not present in normal wave stages.
////
//// HOW IT WORKS:
////   - On stage start, the opening sequence is enqueued automatically.
////   - Each tutorial chapter is a SO_DialogSequence assigned in the Inspector.
////   - Mb_TutorialManager subscribes to game events (ability activations, enemy
////     deaths, movement) and calls two things when they fire:
////       1. Mb_DialogManager.Instance.CompleteInstruction(key) — closes the
////          active tutorial instruction dialog if its key matches.
////       2. Internal chapter advancement — moves to the next chapter sequence
////          once the current chapter's required action has been performed.
////   - The dummy CuBot is identified by the tag "Dummy" — filter OnCuBotDeath
////     by that tag so normal enemy kills don't advance tutorial steps.
////
//// TUTORIAL CHAPTER ORDER (set up in the Inspector):
////   0. OpeningSequence     — plays automatically on stage start
////   1. MovementChapter     — plays after opening; completes on PlayerJumped
////   2. PrimaryChapter      — plays after movement; completes on PlayerUsedPrimary
////   3. SecondaryChapter    — plays after primary; completes on PlayerUsedSecondary
////   4. QChapter            — plays after secondary; completes on PlayerUsedQ
////   5. EChapter            — plays after Q; completes on PlayerUsedE
////   6. DummyChapter        — plays after E; completes on DummyHit
////   7. ClosingSequence     — plays after dummy chapter; no completion condition
////
//// EXTENDING:
////   Add a new [SerializeField] SO_DialogSequence field and a matching chapter
////   entry in _chapters (built in BuildChapterList()). Nothing else changes.
////
//// INSPECTOR SETUP:
////   - Assign each SO_DialogSequence field.
////   - Assign PlayerObject — drag the Guardian GameObject here.
////   - DummyTag: the Unity tag assigned to the target dummy CuBot (default "Dummy").
////     Make sure the dummy GameObject has this tag set in the Inspector.

//using System;
//using System.Collections.Generic;
//using UnityEngine;

//public class Mb_TutorialManager : MonoBehaviour
//{
//    #region Inspector Fields    //----------------------------------------

//    [Header("Tutorial Sequences")]
//    [Tooltip("Plays automatically when the stage starts.")]
//    [SerializeField] private SO_DialogSequence OpeningSequence;

//    [Tooltip("Introduces movement and jumping. Completes on PlayerJumped.")]
//    [SerializeField] private SO_DialogSequence MovementChapter;

//    [Tooltip("Introduces the primary attack. Completes on PlayerUsedPrimary.")]
//    [SerializeField] private SO_DialogSequence PrimaryChapter;

//    [Tooltip("Introduces the secondary attack. Completes on PlayerUsedSecondary.")]
//    [SerializeField] private SO_DialogSequence SecondaryChapter;

//    [Tooltip("Introduces the Q ability. Completes on PlayerUsedQ.")]
//    [SerializeField] private SO_DialogSequence QChapter;

//    [Tooltip("Introduces the E ability. Completes on PlayerUsedE.")]
//    [SerializeField] private SO_DialogSequence EChapter;

//    [Tooltip("Introduces combat on a dummy. Completes on DummyHit.")]
//    [SerializeField] private SO_DialogSequence DummyChapter;

//    [Tooltip("Plays after all chapters are complete. No completion condition.")]
//    [SerializeField] private SO_DialogSequence ClosingSequence;

//    [Header("References")]
//    [Tooltip("Drag the Guardian (Player) GameObject here.")]
//    [SerializeField] private GameObject PlayerObject;

//    [Header("Dummy Settings")]
//    [Tooltip("Unity tag assigned to the target dummy CuBot. " +
//             "Must match the tag set on the dummy GameObject in the scene.")]
//    // TODO: Make sure the dummy CuBot has this tag set in the Inspector.
//    [SerializeField] private string DummyTag = "Dummy";

//    #endregion                  //----------------------------------------


//    #region Private State       //----------------------------------------

//    // Ordered list of chapters built at Start() — drives linear progression
//    private List<TutorialChapter> _chapters;

//    // Index into _chapters — points at the chapter currently in progress
//    private int _currentChapterIndex = -1;

//    // True once all chapters have completed and the closing sequence has played
//    private bool _tutorialComplete = false;

//    // Cached reference to the player's ability controller
//    private Mb_AbilityController _abilityController;

//    #endregion                  //----------------------------------------


//    #region Unity Lifecycle     //----------------------------------------

//    private void Start()
//    {
//        if (PlayerObject != null)
//            _abilityController = PlayerObject.GetComponent<Mb_AbilityController>();

//        if (_abilityController == null)
//            Debug.LogError("[Mb_TutorialManager] Could not find Mb_AbilityController on PlayerObject.");

//        BuildChapterList();

//        // Play the opening sequence immediately on stage start
//        EnqueueAndAdvance(OpeningSequence, autoAdvance: true);
//    }


//    private void OnEnable()
//    {
//        // Ability activations — instance event on Mb_AbilityController.
//        // We subscribe here but _abilityController may not be set yet (OnEnable fires
//        // before Start). SubscribeToAbilityController() guards against null and is
//        // also called from Start() after caching the reference.
//        SubscribeToAbilityController();

//        // CuBot death — static event, safe to subscribe any time
//        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;

//        // Movement — static event on Mb_Movement
//        Mb_Movement.OnLanded += HandlePlayerLanded;
//    }


//    private void OnDisable()
//    {
//        if (_abilityController != null)
//            _abilityController.OnAbilityActivated -= HandleAbilityActivated;

//        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
//        Mb_Movement.OnLanded -= HandlePlayerLanded;
//    }

//    #endregion                  //----------------------------------------


//    #region Chapter Management  //----------------------------------------

//    // Builds the ordered chapter list from the Inspector-assigned sequences.
//    // Each TutorialChapter pairs a sequence with the completion event key that
//    // closes its instruction dialog and advances to the next chapter.
//    private void BuildChapterList()
//    {
//        _chapters = new List<TutorialChapter>
//        {
//            // Movement chapter — completes when the player jumps
//            new TutorialChapter(MovementChapter,   Sc_DialogEvents.PlayerJumped),

//            // Combat input chapters — complete on first use of each input
//            new TutorialChapter(PrimaryChapter,    Sc_DialogEvents.PlayerUsedPrimary),
//            new TutorialChapter(SecondaryChapter,  Sc_DialogEvents.PlayerUsedSecondary),
//            new TutorialChapter(QChapter,          Sc_DialogEvents.PlayerUsedQ),
//            new TutorialChapter(EChapter,          Sc_DialogEvents.PlayerUsedE),

//            // Dummy combat chapter — completes when the dummy takes a hit
//            new TutorialChapter(DummyChapter,      Sc_DialogEvents.DummyHit),
//        };
//        // ClosingSequence is not a chapter — it plays automatically after the last
//        // chapter completes and has no completion condition of its own.
//    }


//    // Enqueues the given sequence and, if autoAdvance is true, moves the chapter
//    // pointer forward so the next chapter is ready to trigger.
//    // autoAdvance = true  : called when progressing normally through chapters.
//    // autoAdvance = false : called for the opening sequence, which is not a chapter.
//    private void EnqueueAndAdvance(SO_DialogSequence sequence, bool autoAdvance)
//    {
//        if (sequence != null)
//            Mb_DialogManager.Instance?.EnqueueSequence(sequence);

//        if (autoAdvance)
//        {
//            _currentChapterIndex++;

//            // If we have advanced past all chapters, play the closing sequence
//            if (_currentChapterIndex >= _chapters.Count)
//            {
//                _tutorialComplete = true;
//                Mb_DialogManager.Instance?.EnqueueSequence(ClosingSequence);
//                Debug.Log("[Mb_TutorialManager] All chapters complete. Tutorial finished.");
//            }
//        }
//    }


//    // Called after a chapter's completion key is matched.
//    // Notifies the dialog manager and advances to the next chapter.
//    private void CompleteCurrentChapter(string eventKey)
//    {
//        if (_tutorialComplete) return;
//        if (_currentChapterIndex < 0 || _currentChapterIndex >= _chapters.Count) return;

//        TutorialChapter current = _chapters[_currentChapterIndex];

//        // Only advance if this event matches the current chapter's expected key
//        if (current.CompletionKey != eventKey) return;

//        // Tell the dialog manager to close the active instruction dialog
//        Mb_DialogManager.Instance?.CompleteInstruction(eventKey);

//        // Advance and enqueue the next chapter sequence
//        EnqueueAndAdvance(_chapters[_currentChapterIndex].Sequence, autoAdvance: true);
//    }

//    #endregion                  //----------------------------------------


//    #region Event Handlers      //----------------------------------------

//    // Fired by Mb_AbilityController.OnAbilityActivated(string slotName).
//    // slotName matches: "Q", "E", "R", "Primary", "Secondary", "Passive"
//    private void HandleAbilityActivated(string slotName)
//    {
//        switch (slotName)
//        {
//            case "Q":
//                CompleteCurrentChapter(Sc_DialogEvents.PlayerUsedQ);
//                break;
//            case "E":
//                CompleteCurrentChapter(Sc_DialogEvents.PlayerUsedE);
//                break;
//            case "R":
//                // R is not a tutorial chapter step — no completion mapped
//                break;
//            case "Primary":
//                CompleteCurrentChapter(Sc_DialogEvents.PlayerUsedPrimary);
//                break;
//            case "Secondary":
//                CompleteCurrentChapter(Sc_DialogEvents.PlayerUsedSecondary);
//                break;
//        }
//    }


//    // Fired by MB_CuBotBase.OnCuBotDeath(GameObject).
//    // We filter by DummyTag so normal enemy kills don't advance tutorial steps.
//    private void HandleCuBotDeath(GameObject deadEnemy)
//    {
//        // The dummy is set to infinite revive — it fires OnCuBotDeath on each
//        // "kill" before reviving. We use that as the DummyHit signal.
//        if (deadEnemy == null) return;

//        if (deadEnemy.CompareTag(DummyTag))
//            CompleteCurrentChapter(Sc_DialogEvents.DummyHit);
//    }


//    // Fired by Mb_Movement.OnLanded — we use landing as a proxy for "jumped",
//    // since a land confirms the jump actually completed.
//    // OnLanded is a static event on Mb_Movement.
//    private void HandlePlayerLanded()
//    {
//        CompleteCurrentChapter(Sc_DialogEvents.PlayerJumped);
//    }

//    #endregion                  //----------------------------------------


//    #region Helpers             //----------------------------------------

//    // Subscribes to the ability controller's instance event.
//    // Called from both OnEnable() and Start() — null guard makes it safe
//    // whichever fires first.
//    private void SubscribeToAbilityController()
//    {
//        if (_abilityController == null) return;

//        // Unsubscribe first to prevent duplicate listeners if called twice
//        _abilityController.OnAbilityActivated -= HandleAbilityActivated;
//        _abilityController.OnAbilityActivated += HandleAbilityActivated;
//    }

//    #endregion                  //----------------------------------------
//}


//// -------------------------------------------------------------------------
//// Supporting Types
//// -------------------------------------------------------------------------

///// <summary>
///// Pairs a dialog sequence with the completion event key that ends it
///// and advances the tutorial to the next chapter.
///// </summary>
//[Serializable]
//public struct TutorialChapter
//{
//    /// <summary>The sequence to enqueue when this chapter begins.</summary>
//    public SO_DialogSequence Sequence;

//    /// <summary>
//    /// The Sc_DialogEvents key that closes the active instruction dialog
//    /// and triggers advancement to the next chapter.
//    /// </summary>
//    public string CompletionKey;

//    public TutorialChapter(SO_DialogSequence sequence, string completionKey)
//    {
//        Sequence = sequence;
//        CompletionKey = completionKey;
//    }
//}