// Mb_MinimapIconRegistrar.cs
// Singleton registry — tracks all active minimap icons in the scene.
// CuBot pool reuse is handled automatically via Mb_MinimapIcon.OnEnable/OnDisable.
//
// Other systems read ActiveIcons if they need the full list.
// The minimap UI reads it to know which icons are currently alive.
//
// No MonoBehaviour needed — pure static class. This avoids needing a manager
// GameObject in the scene and keeps the pool-safe register/unregister pattern simple.

using System.Collections.Generic;

public static class Mb_MinimapIconRegistrar
{
    // All currently active icons — CuBots register on spawn, unregister on death/pool return
    public static readonly List<Mb_MinimapIcon> ActiveIcons = new List<Mb_MinimapIcon>();

    public static void Register(Mb_MinimapIcon icon)
    {
        if (!ActiveIcons.Contains(icon))
            ActiveIcons.Add(icon);
    }

    public static void Unregister(Mb_MinimapIcon icon)
    {
        ActiveIcons.Remove(icon);
    }
}