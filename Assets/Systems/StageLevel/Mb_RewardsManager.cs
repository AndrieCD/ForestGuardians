using UnityEngine;

public class Mb_RewardsManager : MonoBehaviour
{
    // WaveRewards SO


    // LeftReward augment/upgrade data
    // RightReward augment/upgrade data


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void FetchWaveRewards(int waveNumber)
    {
        GameManager.Instance.ChangeState(GameState.RewardsPanel);

        // Get the rewards for the current wave from the WaveRewards SO, then send to the RewardsPanel UI to display.
    }


    // Called when player clicks the left reward panel. 
    void GetLeftReward()
    {
        // Call the activate or apply method on either an augment or upgrade, depending on the type of reward. Then close the rewards panel and return to the stage.
    }

    void GetRightReward( )
    {
        // Call the activate or apply method on either an augment or upgrade, depending on the type of reward. Then close the rewards panel and return to the stage.
    }


}
