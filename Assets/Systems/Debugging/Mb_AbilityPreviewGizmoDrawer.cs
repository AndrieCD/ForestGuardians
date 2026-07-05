// Mb_AbilityPreviewGizmoDrawer.cs
// Editor-only scene gizmos for ability tuning: dash paths, knockback vectors,
// cones, projectile lines, and AoE ranges.
//
// This is visual-only. It does not run physics checks or change gameplay values.

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_AbilityPreviewGizmoDrawer : MonoBehaviour
{
    [Header("Debug Visibility")]
    [SerializeField] private bool DrawGizmos = true;
    [SerializeField] private bool DrawAlwaysVisiblePreviews = false;

    [Header("Ability Previews")]
    [SerializeField] private List<Sc_AbilityPreviewGizmo> Previews = new List<Sc_AbilityPreviewGizmo>();

    private void OnDrawGizmos()
    {
        if (!DrawGizmos || !DrawAlwaysVisiblePreviews)
            return;

        DrawPreviews(selectedOnly: false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!DrawGizmos)
            return;

        DrawPreviews(selectedOnly: true);
    }

    private void DrawPreviews(bool selectedOnly)
    {
        foreach (Sc_AbilityPreviewGizmo preview in Previews)
        {
            if (preview == null || !preview.Enabled)
                continue;

            if (selectedOnly != preview.DrawOnlyWhenSelected)
                continue;

            DrawPreview(preview);
        }
    }

    private void DrawPreview(Sc_AbilityPreviewGizmo preview)
    {
        Transform origin = preview.OriginOverride != null ? preview.OriginOverride : transform;
        Vector3 start = origin.TransformPoint(preview.LocalOffset);
        Vector3 forward = GetWorldDirection(origin, preview.LocalDirection);

        Gizmos.color = preview.Color;

        switch (preview.PreviewType)
        {
            case AbilityPreviewGizmoType.DashPath:
                DrawDashPath(start, forward, preview);
                break;

            case AbilityPreviewGizmoType.KnockbackVector:
                DrawKnockbackVector(start, forward, preview);
                break;

            case AbilityPreviewGizmoType.Cone:
                DrawCone(start, forward, preview);
                break;

            case AbilityPreviewGizmoType.ProjectileLine:
                DrawProjectileLine(start, forward, preview);
                break;

            case AbilityPreviewGizmoType.AreaRadius:
                Gizmos.DrawWireSphere(start, Mathf.Max(0f, preview.Radius));
                break;

            case AbilityPreviewGizmoType.LandingPoint:
                DrawLandingPoint(start, preview);
                break;
        }
    }

    private void DrawDashPath(Vector3 start, Vector3 forward, Sc_AbilityPreviewGizmo preview)
    {
        float distance = Mathf.Max(0f, preview.Distance);
        float radius = Mathf.Max(0f, preview.Radius);
        Vector3 end = start + forward * distance;

        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, radius);
        Gizmos.DrawWireSphere(end, radius);
        DrawArrowHead(end, forward, preview.ArrowHeadSize);
    }

    private void DrawKnockbackVector(Vector3 start, Vector3 forward, Sc_AbilityPreviewGizmo preview)
    {
        float distance = Mathf.Max(0f, preview.Distance);
        Vector3 end = start + forward * distance;

        Gizmos.DrawLine(start, end);
        DrawArrowHead(end, forward, preview.ArrowHeadSize);

        if (preview.Radius > 0f)
            Gizmos.DrawWireSphere(end, preview.Radius);
    }

    private void DrawCone(Vector3 start, Vector3 forward, Sc_AbilityPreviewGizmo preview)
    {
        float range = Mathf.Max(0f, preview.Distance);
        float halfAngle = Mathf.Max(0f, preview.Angle) * 0.5f;
        int segments = Mathf.Max(4, preview.Segments);

        Quaternion leftRotation = Quaternion.AngleAxis(-halfAngle, Vector3.up);
        Quaternion rightRotation = Quaternion.AngleAxis(halfAngle, Vector3.up);
        Vector3 left = leftRotation * forward;
        Vector3 right = rightRotation * forward;

        Gizmos.DrawLine(start, start + left * range);
        Gizmos.DrawLine(start, start + right * range);

        Vector3 previousPoint = start + left * range;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 point = start + direction * range;

            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }

    private void DrawProjectileLine(Vector3 start, Vector3 forward, Sc_AbilityPreviewGizmo preview)
    {
        float distance = Mathf.Max(0f, preview.Distance);
        Vector3 end = start + forward * distance;

        Gizmos.DrawLine(start, end);
        DrawArrowHead(end, forward, preview.ArrowHeadSize);

        if (preview.Radius > 0f)
            DrawTubeApproximation(start, forward, distance, preview.Radius);
    }

    private void DrawLandingPoint(Vector3 start, Sc_AbilityPreviewGizmo preview)
    {
        Gizmos.DrawWireSphere(start, Mathf.Max(0f, preview.Radius));
        Gizmos.DrawLine(start + Vector3.left * preview.CrossSize, start + Vector3.right * preview.CrossSize);
        Gizmos.DrawLine(start + Vector3.forward * preview.CrossSize, start + Vector3.back * preview.CrossSize);
    }

    private void DrawTubeApproximation(Vector3 start, Vector3 forward, float distance, float radius)
    {
        Vector3 end = start + forward * distance;
        Vector3 right = Vector3.Cross(forward, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(forward, Vector3.right);

        right.Normalize();
        Vector3 up = Vector3.Cross(right, forward).normalized;

        Gizmos.DrawLine(start + right * radius, end + right * radius);
        Gizmos.DrawLine(start - right * radius, end - right * radius);
        Gizmos.DrawLine(start + up * radius, end + up * radius);
        Gizmos.DrawLine(start - up * radius, end - up * radius);
    }

    private void DrawArrowHead(Vector3 tip, Vector3 forward, float size)
    {
        if (size <= 0f)
            return;

        Quaternion leftRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 150f, 0f);
        Quaternion rightRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, -150f, 0f);

        Gizmos.DrawLine(tip, tip + leftRotation * Vector3.forward * size);
        Gizmos.DrawLine(tip, tip + rightRotation * Vector3.forward * size);
    }

    private Vector3 GetWorldDirection(Transform origin, Vector3 localDirection)
    {
        if (localDirection.sqrMagnitude < 0.001f)
            localDirection = Vector3.forward;

        return origin.TransformDirection(localDirection.normalized);
    }
}

[Serializable]
public class Sc_AbilityPreviewGizmo
{
    public string Name = "Ability Preview";
    public bool Enabled = true;
    public bool DrawOnlyWhenSelected = true;
    public AbilityPreviewGizmoType PreviewType = AbilityPreviewGizmoType.AreaRadius;
    public Color Color = Color.cyan;

    [Tooltip("Optional transform used as the preview origin. Leave empty to use this component's transform.")]
    public Transform OriginOverride;

    public Vector3 LocalOffset = Vector3.zero;
    public Vector3 LocalDirection = Vector3.forward;

    [Tooltip("Used as dash length, knockback distance, cone range, or projectile range depending on PreviewType.")]
    public float Distance = 5f;

    [Tooltip("Used as AoE radius, dash/knockback endpoint radius, or projectile width depending on PreviewType.")]
    public float Radius = 1f;

    [Tooltip("Cone angle in degrees.")]
    public float Angle = 45f;

    [Tooltip("Arc resolution for cone previews.")]
    public int Segments = 16;

    public float ArrowHeadSize = 0.35f;
    public float CrossSize = 0.35f;
}

public enum AbilityPreviewGizmoType
{
    DashPath,
    KnockbackVector,
    Cone,
    ProjectileLine,
    AreaRadius,
    LandingPoint
}
