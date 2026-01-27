using UnityEngine;

public class Mb_AugmentManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void ApplyWingsOfBalance( )
    {
        Sc_WingsOfBalance wingsOfBalance = new Sc_WingsOfBalance( );
        wingsOfBalance.ApplyAugment( );
    }
    public void ApplyHeartOfTheForest( )
    {
        Sc_HeartOfTheForest heartOfTheForest= new Sc_HeartOfTheForest( );
        heartOfTheForest.ApplyAugment( );
    }
}
