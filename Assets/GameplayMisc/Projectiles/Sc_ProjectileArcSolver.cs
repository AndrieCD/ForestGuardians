// Sc_ProjectileArcSolver.cs
// Plain static helper — solves the launch angle needed for a fixed-speed
// projectile to hit a target position under gravity (real ballistic math,
// not a guessed fixed angle).
//
// WHY THIS EXISTS:
//   Sc_ShovyDirtShot and Sc_ToxionSludgeShot previously used a hardcoded
//   upward angle (e.g. 22 degrees) for every shot. A fixed angle only lands
//   correctly at ONE specific distance — closer targets get overshot,
//   farther targets get undershot. This solver instead calculates the
//   exact angle (for a given fixed launch speed) that makes the parabola
//   pass through the target's position.
//
// THE MATH (standard projectile motion, solved for angle):
//   Given: start position, target position, launch speed (v), gravity (g)
//   Let dx = horizontal distance to target, dy = vertical distance to target
//   theta = atan( (v² ± sqrt(v⁴ - g·(g·dx² + 2·dy·v²))) / (g·dx) )
//   The "-" root gives the LOW arc (flatter, faster, usually what you want
//   for enemy shots). The "+" root gives the HIGH arc (lobbed, slower).
//
// SPEED LIMITATION:
//   A fixed speed can only reach so far. If the target is beyond that
//   speed's maximum range, no angle can hit it (this is real physics, not
//   a bug) — TrySolveAngle() returns false and gives you the best-effort
//   45-degree direction (the angle that maximizes range for that speed).
//   If you see this happening often, raise the projectile's LaunchSpeed —
//   don't try to fix it by changing the angle further.
//
// USAGE:
//   bool hit = Sc_ProjectileArcSolver.TrySolveAngle(
//       muzzlePosition, targetPosition, launchSpeed, out Vector3 fireDirection);
//   // fireDirection is already normalized — multiply by speed when launching.

using UnityEngine;

public static class Sc_ProjectileArcSolver
{
    /// <summary>
    /// Solves for the launch direction (normalized) that makes a projectile
    /// fired at 'speed' from 'start' land exactly on 'target', accounting for gravity.
    /// </summary>
    /// <param name="start">Muzzle / launch position.</param>
    /// <param name="target">World position to hit.</param>
    /// <param name="speed">Fixed launch speed of the projectile (units/sec).</param>
    /// <param name="fireDirection">Output — normalized direction to fire in.</param>
    /// <param name="preferLowArc">
    /// True = flatter, faster-arriving shot (usual choice for enemy ranged attacks).
    /// False = high lob (mortar-style). 
    /// </param>
    /// <returns>
    /// True if an exact solution exists at this speed. False if the target is out
    /// of range for the given speed — fireDirection will still be filled with the
    /// best-effort 45-degree shot (maximum possible range at that speed), but it
    /// will fall short of the actual target.
    /// </returns>
    public static bool TrySolveAngle(
        Vector3 start,
        Vector3 target,
        float speed,
        out Vector3 fireDirection,
        bool preferLowArc = true)
    {
        // Use Unity's actual gravity setting so this always matches how the
        // Rigidbody will really fall — never hardcode 9.81 separately from this.
        float gravity = Mathf.Abs(Physics.gravity.y);

        Vector3 diff = target - start;
        Vector3 diffFlat = new Vector3(diff.x, 0f, diff.z);
        float dx = diffFlat.magnitude;
        float dy = diff.y;

        // Degenerate case — target is basically directly above/below the muzzle.
        // There's no meaningful horizontal arc to solve; just aim straight at it.
        if (dx < 0.05f)
        {
            fireDirection = diff.normalized;
            return true;
        }

        float v2 = speed * speed;

        // This is the discriminant of the quadratic — if negative, no angle at
        // this speed can reach the target (it's simply too far away).
        float underSqrt = (v2 * v2) - gravity * (gravity * dx * dx + 2f * dy * v2);

        Vector3 horizontalDir = diffFlat.normalized;

        if (underSqrt < 0f)
        {
            // Out of range for this speed. Best possible shot is the 45-degree
            // angle — it gives maximum range but will still land short.
            // TODO: If this fires often in the console, raise LaunchSpeed /
            //       PROJECTILE_SPEED on the ability rather than adjusting angles.
            float fallbackTheta = 45f * Mathf.Deg2Rad;
            fireDirection = (horizontalDir * Mathf.Cos(fallbackTheta)
                            + Vector3.up * Mathf.Sin(fallbackTheta)).normalized;
            return false;
        }

        float sqrtPart = Mathf.Sqrt(underSqrt);

        // Two valid solutions exist — pick low (flat) or high (lobbed) arc.
        float theta = preferLowArc
            ? Mathf.Atan((v2 - sqrtPart) / (gravity * dx))
            : Mathf.Atan((v2 + sqrtPart) / (gravity * dx));

        fireDirection = (horizontalDir * Mathf.Cos(theta) + Vector3.up * Mathf.Sin(theta)).normalized;
        return true;
    }
}