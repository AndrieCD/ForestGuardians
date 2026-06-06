// ConservationStatus.cs
// Defines the IUCN-aligned conservation status tiers used by the Wildlife Almanac.
// Used by SO_WildlifeEntry to tag each species and by Mb_WildlifeDiscoveryManager
// to weight spawn probabilities — Critically Endangered spawns least often.

public enum ConservationStatus
{
    CriticallyEndangered,   // Highest rarity — lowest spawn weight (0.25)
    Endangered,             // Mid rarity — medium spawn weight (0.50)
    NearThreatened          // Lowest rarity — highest spawn weight (1.0)
}