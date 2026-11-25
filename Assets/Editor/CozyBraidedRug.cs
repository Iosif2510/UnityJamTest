using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotionShop.EditorTools
{
    /// <summary>
    /// The generated round rug had a broken texture (smeared UV islands, dark blotches). This swaps its look for
    /// a flat braided rug: a thin disc mesh with planar UVs and a hand-made braided texture
    /// (RoundRug_braided.png). The Meshy renderer is only disabled, so the prop keeps its place and size.
    /// Called at the end of CozyWorkshopProps.Place(); re-running replaces the previous disc.
    /// </summary>
    public static class CozyBraidedRug
    {
        private const string Folder = "Assets/Arts/Models/Cozy/RoundRug";
        private const string TexturePath = Folder + "/RoundRug_braided.png";
        private const string MeshPath = Folder + "/RoundRug_Braided.asset";
        private const string MaterialPath = "Assets/Arts/Materials/Cozy/Prop_RoundRug_Braided.mat";
        private const float Thickness = 0.018f;
        private const int Segments = 72;

        public static string Apply(Transform props)
        {
            Transform rug = null;
            foreach (Transform t in props)
                if (t.name.StartsWith("RoundRug ")) rug = t;
            if (rug == null) return "no rug";

            var old = rug.Find("BraidedRug");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var renderers = rug.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return "rug has no renderer";
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            foreach (var r in renderers) r.enabled = false;

            var go = new GameObject("BraidedRug", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(rug, false);
            go.transform.position = new Vector3(b.center.x, rug.position.y, b.center.z);
            go.transform.rotation = Quaternion.identity;
            var parentScale = rug.lossyScale;
            go.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            go.GetComponent<MeshFilter>().sharedMesh = DiscMesh();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            float radius = Mathf.Max(b.size.x, b.size.z) * 0.5f;
            // the disc is 1 m across; size it to the rug the layout placed
            go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(radius * 2f, 1f, radius * 2f));
            return $"braided rug r {radius:F2}";
        }

        /// <summary>A 1 m disc, Thickness high, with planar top UVs and a short rounded-off rim.</summary>
        private static Mesh DiscMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "RoundRug_Braided" };
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            mesh.Clear();
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            const float r = 0.5f;

            vertices.Add(new Vector3(0f, Thickness, 0f)); normals.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(d * r + Vector3.up * Thickness); normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f + d.x * 0.495f, 0.5f + d.z * 0.495f));
            }
            for (int i = 0; i < Segments; i++)
                triangles.AddRange(new[] { 0, i + 2, i + 1 });

            // rim: outer edge band using the dark border of the texture
            int rim = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var uv = new Vector2(0.5f + d.x * 0.49f, 0.5f + d.z * 0.49f);
                vertices.Add(d * r + Vector3.up * Thickness); normals.Add(d); uvs.Add(uv);
                vertices.Add(d * (r + 0.004f)); normals.Add(d); uvs.Add(uv);
            }
            for (int i = 0; i < Segments; i++)
            {
                int top = rim + i * 2, bottom = top + 1, nextTop = top + 2, nextBottom = top + 3;
                triangles.AddRange(new[] { top, nextTop, bottom, bottom, nextTop, nextBottom });
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material Material()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer != null && (importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 1024))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
