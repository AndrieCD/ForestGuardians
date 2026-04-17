// Augment_WingsOfBalance.cs
// +60% Attack Speed, +40% Move Speed.
//
// This is a "simple" augment — all its effects are defined in the SO_Augment
// Inspector fields, so the base class handles everything automatically.
// This class exists only to give the augment a concrete type that can be
// instantiated and identified at runtime.
//
// Inspector setup: assign the Wings of Balance SO_Augment asset.

public class Augment_WingsOfBalance : Sc_AugmentBase
{
    public Augment_WingsOfBalance(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }

    // No overrides needed — base class reads Effects from the SO and applies them.
}