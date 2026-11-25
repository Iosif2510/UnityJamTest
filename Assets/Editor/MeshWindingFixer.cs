using UnityEditor;
using UnityEngine;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// Some generated (Meshy) props have single-sheet panels with some triangles wound the wrong way, so those faces
    /// are culled and you see through the model (the apothecary cabinet's back panel showed the wall behind it as dark
    /// wedges). For the models listed, every triangle of the back panel (the back part of the model, facing straight
    /// forward or back) is turned to face the front, the side the player sees; then the normals are recalculated.
    /// </summary>
    internal sealed class MeshWindingFixer : AssetPostprocessor
    {
        private readonly struct Rule
        {
            public readonly string Path;
            public readonly Vector3 Front;   // the model's front, in its mesh space
            public readonly float BackShare; // how deep the back panel reaches, as a share of the model's depth

            public Rule(string path, Vector3 front, float backShare)
            {
                Path = path;
                Front = front;
                BackShare = backShare;
            }
        }

        private static readonly Rule[] Rules =
        {
            new("Assets/Arts/Models/Cozy/ApothecaryCabinet/ApothecaryCabinet.fbx", Vector3.forward, 0.2f),
        };

        // bump when the rules change, so every copy of the project reimports the listed models
        public override uint GetVersion() => 1;

        private void OnPostprocessModel(GameObject root)
        {
            foreach (var rule in Rules)
            {
                if (rule.Path != assetPath) continue;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null)
                    {
                        int turned = FaceFront(filter.sharedMesh, rule.Front.normalized, rule.BackShare);
                        if (turned > 0) Debug.Log($"[MeshWindingFixer] {assetPath}: turned {turned} back-panel triangles to the front");
                    }
            }
        }

        private static int FaceFront(Mesh mesh, Vector3 front, float backShare)
        {
            var vertices = mesh.vertices;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var p in vertices)
            {
                float d = Vector3.Dot(p, front);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
            float backLimit = min + (max - min) * backShare;

            int turned = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                bool changed = false;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var a = vertices[tris[i]];
                    var b = vertices[tris[i + 1]];
                    var c = vertices[tris[i + 2]];
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-20f) continue;
                    n.Normalize();
                    float along = Vector3.Dot(n, front);
                    if (along > -0.9f) continue; // already faces the front, or isn't part of a flat back panel
                    if (Vector3.Dot((a + b + c) / 3f, front) > backLimit) continue;
                    (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                    turned++;
                    changed = true;
                }
                if (changed) mesh.SetTriangles(tris, s);
            }
            if (turned > 0)
            {
                // the corners were already split at hard edges on import, so this keeps them crisp
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
            }
            return turned;
        }
    }
}
