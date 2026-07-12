using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "CutsceneVideo_SO", menuName = "Forest Guardians/Cutscene Video")]
public class SO_CutsceneVideo : ScriptableObject
{
    [Header("Identity")]
    public E_CutsceneId CutsceneId;
    public string CutsceneName;

    [Header("Video")]
    public VideoClip VideoClip;
}
