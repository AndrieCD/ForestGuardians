using System.Collections.Generic;
using UnityEngine;

public class Sc_MindspikesTickTracker
{
    private readonly Dictionary<MB_CuBotBase, float> _lastTickTimes
        = new Dictionary<MB_CuBotBase, float>();

    public bool TryConsumeTick(MB_CuBotBase enemy, float tickInterval)
    {
        if (enemy == null) return false;

        float now = Time.time;
        if (_lastTickTimes.TryGetValue(enemy, out float lastTickTime))
        {
            if (now - lastTickTime < tickInterval)
                return false;
        }

        _lastTickTimes[enemy] = now;
        return true;
    }
}
