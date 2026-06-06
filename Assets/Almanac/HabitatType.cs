// HabitatType.cs
// [Flags] enum defining the habitat categories used to match wildlife species
// to compatible spawn points in the stage.
//
// WHY [Flags]:
//   Some species are comfortable in more than one habitat type
//   (e.g. Tamaraw: Ground | Aquatic). [Flags] lets a single SO_WildlifeEntry
//   field hold multiple types via bitwise OR without a List.
//
// SPAWN POINT MATCHING RULE:
//   A spawn point is valid for a species if the spawn point's single HabitatType
//   matches ANY of the species' AcceptedHabitats flags (bitwise AND check).
//
// HABITAT ASSIGNMENTS (reference):
//   Ground   — Tamaraw, McGregor's Pit Viper, Mount Data Forest Frog*
//   Arboreal — Philippine Eagle*, Philippine Tarsier, Panay Cloudrunner,
//              Visayan Spotted Deer, Sulu Hornbill*, Mindanao Flying Dragon*
//   Aerial   — Golden-crowned Flying Fox
//   Aquatic  — Philippine Forest Turtle, Philippine Crocodile,
//              Malatgan River Caecilian, Mount Data Forest Frog*,
//              Tamaraw* (* = multi-habitat, listed in both)
//
// TODO: Confirm final habitat assignments with the level design team
//       once stage environment layouts are decided.

[System.Flags]
public enum HabitatType
{
    None = 0,
    Ground = 1 << 0,   // Terrestrial — forest floor, grassland, open terrain
    Arboreal = 1 << 1,   // Tree-dwelling — canopy, branches, elevated forest
    Aerial = 1 << 2,   // Airborne — open sky, cliff edges, tall canopy tops
    Aquatic = 1 << 3,   // Water-adjacent — rivers, ponds, wetland edges
}