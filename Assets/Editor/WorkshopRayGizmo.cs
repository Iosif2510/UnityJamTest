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
            if (!workshop.TryGetRayGizmo(out var ray, out var end, out var hit, out var color))
                return;

            CompareFunction previousDepthTest = Handles.zTest;
            try
            {
                Handles.zTest = CompareFunction.Always;
                if (hit.collider != null)
                {
                    using (new Handles.DrawingScope(Color.gray))
                        Handles.DrawLine(hit.point, end);
                }

                using (new Handles.DrawingScope(color))
                {
                    Handles.DrawAAPolyLine(3f, ray.origin, hit.collider != null ? hit.point : end);
                    if (hit.collider != null)
                        Handles.SphereHandleCap(0, hit.point, Quaternion.identity, 0.09f, EventType.Repaint);
                }
            }
            finally
            {
                Handles.zTest = previousDepthTest;
            }
        }
    }
}
