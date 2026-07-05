// Mb_DebugShapeGizmoDrawer.cs
// Inspector-configurable editor gizmos for tuning hitboxes, ranges, raycasts,
// and overlap shapes without hardcoding OnDrawGizmos in gameplay scripts.
//
// Attach this to the character, ability helper, projectile origin, or debug
// anchor you want to visualize. Each entry is drawn in local space relative to
// this component's transform, unless an OriginOverride is assigned.

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_DebugShapeGizmoDrawer : MonoBehaviour
{
    [Header("Debug Visibility")]
    [SerializeField] private bool DrawGizmos = true;

    [Tooltip("When true, non-selected shapes are drawn even when this GameObject is not selected.")]
    [SerializeField] private bool DrawAlwaysVisibleShapes = true;

    [Header("Shapes")]
    [SerializeField] private List<Sc_DebugGizmoShape> Shapes = new List<Sc_DebugGizmoShape>();

    private void OnDrawGizmos()
    {
        if (!DrawGizmos || !DrawAlwaysVisibleShapes)
            return;

        DrawShapes(selectedOnly: false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!DrawGizmos)
            return;

        DrawShapes(selectedOnly: true);
    }

    private void DrawShapes(bool selectedOnly)
    {
        foreach (Sc_DebugGizmoShape shape in Shapes)
        {
            if (shape == null || !shape.Enabled)
                continue;

            if (selectedOnly != shape.DrawOnlyWhenSelected)
                continue;

            DrawShape(shape);
        }
    }

    private void DrawShape(Sc_DebugGizmoShape shape)
    {
        Transform origin = shape.OriginOverride != null ? shape.OriginOverride : transform;
        Vector3 center = origin.TransformPoint(shape.LocalOffset);
        Vector3 direction = GetWorldDirection(origin, shape.LocalDirection);

        Gizmos.color = shape.Color;

        switch (shape.ShapeType)
        {
            case DebugGizmoShapeType.Sphere:
                Gizmos.DrawWireSphere(center, Mathf.Max(0f, shape.Radius));
                break;

            case DebugGizmoShapeType.Box:
                DrawBox(origin, shape, center);
                break;

            case DebugGizmoShapeType.Ray:
                Gizmos.DrawRay(center, direction * Mathf.Max(0f, shape.Distance));
                break;

            case DebugGizmoShapeType.Line:
                Gizmos.DrawLine(center, center + direction * Mathf.Max(0f, shape.Distance));
                break;

            case DebugGizmoShapeType.Capsule:
                DrawCapsule(center, direction, shape);
                break;
        }
    }

    private void DrawBox(Transform origin, Sc_DebugGizmoShape shape, Vector3 center)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, shape.Size);
        Gizmos.matrix = previousMatrix;
    }

    private void DrawCapsule(Vector3 center, Vector3 direction, Sc_DebugGizmoShape shape)
    {
        float radius = Mathf.Max(0f, shape.Radius);
        float distance = Mathf.Max(0f, shape.Distance);
        Vector3 halfOffset = direction * (distance * 0.5f);
        Vector3 start = center - halfOffset;
        Vector3 end = center + halfOffset;

        Gizmos.DrawWireSphere(start, radius);
        Gizmos.DrawWireSphere(end, radius);

        Vector3 right = Vector3.Cross(direction, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(direction, Vector3.right);

        right.Normalize();
        Vector3 up = Vector3.Cross(right, direction).normalized;

        Gizmos.DrawLine(start + right * radius, end + right * radius);
        Gizmos.DrawLine(start - right * radius, end - right * radius);
        Gizmos.DrawLine(start + up * radius, end + up * radius);
        Gizmos.DrawLine(start - up * radius, end - up * radius);
    }

    private Vector3 GetWorldDirection(Transform origin, Vector3 localDirection)
    {
        if (localDirection.sqrMagnitude < 0.001f)
            localDirection = Vector3.forward;

        return origin.TransformDirection(localDirection.normalized);
    }
}

[Serializable]
public class Sc_DebugGizmoShape
{
    public string Name = "Debug Shape";
    public bool Enabled = true;
    public bool DrawOnlyWhenSelected = true;
    public DebugGizmoShapeType ShapeType = DebugGizmoShapeType.Sphere;
    public Color Color = Color.red;

    [Tooltip("Optional transform used as the shape origin. Leave empty to use this component's transform.")]
    public Transform OriginOverride;

    public Vector3 LocalOffset = Vector3.zero;
    public Vector3 LocalDirection = Vector3.forward;
    public Vector3 Size = Vector3.one;
    public float Radius = 1f;
    public float Distance = 1f;
}

public enum DebugGizmoShapeType
{
    Sphere,
    Box,
    Ray,
    Line,
    Capsule
}
