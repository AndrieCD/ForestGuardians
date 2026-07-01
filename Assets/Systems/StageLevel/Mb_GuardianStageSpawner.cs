using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Spawns the guardian selected in Guardian Selection when a stage scene loads.
/// </summary>
public class Mb_GuardianStageSpawner : MonoBehaviour
{
    private const string CAMERA_ROOT_NAME = "CameraRoot";

    [Header("Guardian Selection")]
    [Tooltip("Fallback guardian used when the stage is launched directly without a run session.")]
    [SerializeField] private SO_Guardian DefaultGuardian;

    [Header("Spawn")]
    [Tooltip("Where the selected guardian prefab should spawn.")]
    [SerializeField] private Transform SpawnPoint;

    [Tooltip("Optional parent for the spawned guardian. Leave null to spawn at scene root.")]
    [SerializeField] private Transform SpawnParent;

    [Header("Camera")]
    [Tooltip("Third Person Aim Camera that should track the spawned guardian's CameraRoot child.")]
    [SerializeField] private CinemachineCamera ThirdPersonAimCamera;

    [Header("Scene Migration")]
    [Tooltip("Existing scene-placed guardian objects to remove before spawning the selected guardian.")]
    [SerializeField] private GameObject[] SceneGuardiansToRemove;

    private GameObject _spawnedGuardianObject;

    public GameObject SpawnedGuardianObject => _spawnedGuardianObject;

    private void Awake()
    {
        RemoveSceneGuardians();
        SpawnSelectedGuardian();
    }

    private void RemoveSceneGuardians()
    {
        if (SceneGuardiansToRemove == null) return;

        for (int i = 0; i < SceneGuardiansToRemove.Length; i++)
        {
            if (SceneGuardiansToRemove[i] == null) continue;

            Destroy(SceneGuardiansToRemove[i]);
        }
    }

    private void SpawnSelectedGuardian()
    {
        SO_Guardian guardian = Sc_RunSession.SelectedGuardian != null
            ? Sc_RunSession.SelectedGuardian
            : DefaultGuardian;

        if (guardian == null)
        {
            Debug.LogError("[Mb_GuardianStageSpawner] No selected guardian and no DefaultGuardian assigned.");
            return;
        }

        if (guardian.GuardianPrefab == null)
        {
            Debug.LogError($"[Mb_GuardianStageSpawner] GuardianPrefab is not assigned on {guardian.name}.");
            return;
        }

        Vector3 spawnPosition = SpawnPoint != null ? SpawnPoint.position : transform.position;
        Quaternion spawnRotation = SpawnPoint != null ? SpawnPoint.rotation : transform.rotation;

        _spawnedGuardianObject = Instantiate(
            guardian.GuardianPrefab,
            spawnPosition,
            spawnRotation,
            SpawnParent
        );

        Mb_GuardianBase spawnedGuardian = _spawnedGuardianObject.GetComponent<Mb_GuardianBase>();
        if (spawnedGuardian == null)
        {
            Debug.LogError($"[Mb_GuardianStageSpawner] Spawned prefab '{guardian.GuardianPrefab.name}' has no Mb_GuardianBase.");
            return;
        }

        if (spawnedGuardian.GuardianTemplate != guardian)
        {
            Debug.LogWarning($"[Mb_GuardianStageSpawner] Spawned prefab '{guardian.GuardianPrefab.name}' uses " +
                             $"template '{spawnedGuardian.GuardianTemplate?.name}', but session selected '{guardian.name}'. " +
                             "Assign the matching SO_Guardian on the prefab.");
        }

        AssignCameraTarget(spawnedGuardian);

        Debug.Log($"[Mb_GuardianStageSpawner] Spawned guardian: {guardian.CharacterName}.");
    }

    private void AssignCameraTarget(Mb_GuardianBase guardian)
    {
        if (ThirdPersonAimCamera == null)
        {
            Debug.LogWarning("[Mb_GuardianStageSpawner] ThirdPersonAimCamera is not assigned.");
            return;
        }

        Transform trackingTarget = ResolveCameraTrackingTarget(guardian);

        ThirdPersonAimCamera.Target.TrackingTarget = trackingTarget;
        ThirdPersonAimCamera.Target.CustomLookAtTarget = false;

        Debug.Log($"[Mb_GuardianStageSpawner] Assigned camera tracking target: {trackingTarget.name}.");
    }

    private Transform ResolveCameraTrackingTarget(Mb_GuardianBase guardian)
    {
        Transform cameraRoot = FindChildByName(guardian.transform, CAMERA_ROOT_NAME);
        if (cameraRoot != null)
            return cameraRoot;

        Mb_Movement movement = guardian.GetComponent<Mb_Movement>();
        if (movement != null && movement.CinemachineCameraTarget != null)
        {
            Debug.LogWarning($"[Mb_GuardianStageSpawner] No '{CAMERA_ROOT_NAME}' child found on {guardian.name}. " +
                             "Using Mb_Movement.CinemachineCameraTarget instead.");
            return movement.CinemachineCameraTarget;
        }

        Debug.LogWarning($"[Mb_GuardianStageSpawner] No '{CAMERA_ROOT_NAME}' child found on {guardian.name}. " +
                         "Using guardian root as camera tracking target.");
        return guardian.transform;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == childName)
                return child;

            Transform nestedChild = FindChildByName(child, childName);
            if (nestedChild != null)
                return nestedChild;
        }

        return null;
    }
}
