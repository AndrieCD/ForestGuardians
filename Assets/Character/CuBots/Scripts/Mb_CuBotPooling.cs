using UnityEngine;

public class Mb_CuBotPooling : MonoBehaviour
{
    [Header("Chopper Configs")]
    public GameObject ChopperPrefab;
    public int ChopperPoolSize = 30;

    [Header("Minny Configs")]
    public GameObject MinnyPrefab;
    public int MinnyPoolSize = 15;

    [Header("Hunter Configs")]
    public GameObject HunterPrefab;
    public int HunterPoolSize = 15;

    [Header("Sawyer Configs")]
    public GameObject SawyerPrefab;
    public int SawyerPoolSize = 10;

    [Header("Trapper Configs")]
    public GameObject TrapperPrefab;
    public int TrapperPoolSize = 10;

    [Header("Drilly Configs")]
    public GameObject DrillyPrefab;
    public int DrillyPoolSize = 10;

    [Header("Shovy Configs")]
    public GameObject ShovyPrefab;
    public int ShovyPoolSize = 10;

    [Header("Bernie Configs")]
    public GameObject BerniePrefab;
    public int BerniePoolSize = 8;

    [Header("Toxion Configs")]
    public GameObject ToxionPrefab;
    public int ToxionPoolSize = 8;

    [Header("Luxion Configs")]
    public GameObject LuxionPrefab;
    public int LuxionPoolSize = 2;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        // Spawn the prefabs and set them to inactive
        // Skip if prefabs are not assigned to avoid null reference errors.
        // Chopper uses configured pool size; other types use a small default pool.

        if (ChopperPrefab != null && ChopperPoolSize > 0)
            SpawnPool(ChopperPrefab, ChopperPoolSize);

        if (MinnyPrefab != null && MinnyPoolSize > 0)
            SpawnPool(MinnyPrefab, MinnyPoolSize);

        if (HunterPrefab != null && HunterPoolSize > 0)
            SpawnPool(HunterPrefab, HunterPoolSize);

        if (SawyerPrefab != null && SawyerPoolSize > 0)
            SpawnPool(SawyerPrefab, SawyerPoolSize);

        if (TrapperPrefab != null && TrapperPoolSize > 0)
            SpawnPool(TrapperPrefab, TrapperPoolSize);

        if (DrillyPrefab != null && DrillyPoolSize > 0)
            SpawnPool(DrillyPrefab, DrillyPoolSize);

        if (ShovyPrefab != null && ShovyPoolSize > 0)
            SpawnPool(ShovyPrefab, ShovyPoolSize);

        if (BerniePrefab != null && BerniePoolSize > 0)
            SpawnPool(BerniePrefab, BerniePoolSize);

        if (ToxionPrefab != null && ToxionPoolSize > 0)
            SpawnPool(ToxionPrefab, ToxionPoolSize);

        if (LuxionPrefab != null && LuxionPoolSize > 0)
            SpawnPool(LuxionPrefab, LuxionPoolSize);

    }

    private void SpawnPool(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            GameObject instance = Instantiate(prefab, transform);
            // Ensure the child name matches the prefab name so WaveManager can match by name
            instance.name = prefab.name;
            instance.SetActive(false);
        }
    }
}
