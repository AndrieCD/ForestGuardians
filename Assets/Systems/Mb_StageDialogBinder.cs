// Mb_StageDialogBinder.cs
// Plays a dialog sequence when the stage starts.
// Lives on the Stage GameObject alongside Mb_StageManager.
//
// INSPECTOR SETUP:
//   - StageStartSequence: drag the SO_DialogSequence to play on stage start.
//   - Delay: seconds to wait before enqueuing (gives the player a moment
//     to orient before text appears). Default 1.5f.
//     TODO: Tune this — 1.5f works for a quick fade-in; raise to 3f if
//     there is a longer stage-entry cinematic or camera pan.

using System.Collections;
using UnityEngine;

public class Mb_StageDialogBinder : MonoBehaviour
{
    [Header("Stage Start Dialog")]
    [Tooltip("Sequence to play when the stage begins.")]
    [SerializeField] private SO_DialogSequence StageStartSequence;

    [Tooltip("Seconds to wait after stage start before showing the first line.")]
    [SerializeField] private float Delay = 1.5f;


    private void OnEnable()
    {
        Mb_StageManager.OnStageStart += HandleStageStart;
    }

    private void OnDisable()
    {
        Mb_StageManager.OnStageStart -= HandleStageStart;
    }


    private void HandleStageStart()
    {
        if (StageStartSequence == null) return;
        StartCoroutine(DelayedEnqueue());
    }

    private IEnumerator DelayedEnqueue()
    {
        yield return new WaitForSeconds(Delay);

        if (Mb_DialogManager.Instance == null)
        {
            Debug.LogWarning("[Mb_StageDialogBinder] No Mb_DialogManager instance found.");
            yield break;
        }

        Mb_DialogManager.Instance.EnqueueSequence(StageStartSequence);
    }
}