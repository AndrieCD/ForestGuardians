using System.Collections.Generic;
using UnityEngine;

public abstract class Sc_AugmentBase
{
    protected string _AugmentName = "New Augment";
    protected List<Sc_StatEffect> _StatEffects = new List<Sc_StatEffect>( );    // List of stat effects this augment provides

    abstract public void ApplyAugment();
}
