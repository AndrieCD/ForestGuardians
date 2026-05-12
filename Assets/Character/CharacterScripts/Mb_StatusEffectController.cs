//using System;
//using System.Collections.Generic;
//using UnityEngine;

//public class Mb_StatusEffectController : MonoBehaviour
//{
//    private Dictionary<string, float> _activeStatuses = new();  // name → timeRemaining
//    public event Action<string> OnStatusApplied;
//    public event Action<string> OnStatusRemoved;

//    public void Apply(string statusName, float duration) { ... }
//    public void Remove(string statusName) { ... }
//    public bool HasStatus(string statusName) { ... }
//}