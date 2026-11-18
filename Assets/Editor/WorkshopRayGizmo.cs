using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotionShop.Editor
{
    internal static class WorkshopRayGizmo
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.InSelectionHierarchy)]
        private static void Draw(WorkshopInteraction workshop, GizmoType gizmoType)
        {
            if (!workshop.TryGetRayGizmo(out var ray, out var end, out var target, out var hitPoint, out var color))
                return;

            CompareFunction previousDepthTest = Handles.zTest;
            try
            {
                Handles.zTest = CompareFunction.Always;
                if (target != null)
                {
                    using (new Handles.DrawingScope(Color.gray))
                        Handles.DrawLine(hitPoint, end);
                }

                using (new Handles.DrawingScope(color))
                {
                    Handles.DrawAAPolyLine(3f, ray.origin, target != null ? hitPoint : end);
                    if (target != null)
                        Handles.SphereHandleCap(0, hitPoint, Quaternion.identity, 0.09f, EventType.Repaint);
                }
            }
            finally
            {
                Handles.zTest = previousDepthTest;
            }
        }
    }
}
