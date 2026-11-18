using UnityEngine;

namespace PotionShop
{
    /// <summary>Feeds per-renderer bubbles to Liquid_Bubble without cloning its material.</summary>
    [ExecuteAlways, DefaultExecutionOrder(100), DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer), typeof(MeshFilter))]
    public sealed class LiquidBubbleController : MonoBehaviour
    {
        [Header("Bubble lifecycle")]
        [Tooltip("Emission at Bubble Amount = 1. Bubble Speed also scales the whole lifecycle.")]
        [SerializeField, Min(0f)] private float emissionRate = 10f;
        [Tooltip("Rise speed in units relative to the renderer's vertical scale.")]
        [SerializeField, Min(0.01f)] private float riseSpeed = 0.22f;
        [SerializeField, Min(0.05f)] private float surfaceLifetime = 0.45f;
        [Tooltip("Vertical flattening before a surface bubble pops. Zero keeps bubbles spherical.")]
        [SerializeField, Range(0f, 0.5f)] private float surfaceFlattening = 0f;
        [SerializeField] private int randomSeed = 6174;

        [Header("Surface ripples")]
        [SerializeField, Min(0.1f)] private float rippleLifetime = 1.25f;
        [SerializeField, Min(0.01f)] private float rippleSpeed = 0.22f;
        [SerializeField, Range(0f, 0.15f)] private float rippleStrength = 0.045f;
        [Tooltip("Visibility of bubbles seen through the opaque liquid (artistic absorption).")]
        [SerializeField, Range(0f, 1f)] private float subsurfaceVisibility = 0.72f;
        [SerializeField] private bool previewInEditMode = true;

        private static readonly int Amount = Shader.PropertyToID("_Amount");
        private static readonly int WobbleX = Shader.PropertyToID("_WobbleX");
        private static readonly int WobbleZ = Shader.PropertyToID("_WobbleZ");
        private static readonly int BubbleAmount = Shader.PropertyToID("_BubbleAmount");
        private static readonly int BubbleSize = Shader.PropertyToID("_BubbleSize");
        private static readonly int BubbleScale = Shader.PropertyToID("_BubbleScale");
        private static readonly int BubbleSpeed = Shader.PropertyToID("_BubbleSpeed");
        private static readonly int BubbleCount = Shader.PropertyToID("_LiquidBubbleCount");
        private static readonly int BubbleData = Shader.PropertyToID("_LiquidBubbleData");
        private static readonly int BubbleShape = Shader.PropertyToID("_LiquidBubbleShape");
        private static readonly int RippleCount = Shader.PropertyToID("_LiquidRippleCount");
        private static readonly int RippleData = Shader.PropertyToID("_LiquidRippleData");
        private static readonly int Clock = Shader.PropertyToID("_LiquidBubbleTime");
        private static readonly int Settings = Shader.PropertyToID("_LiquidBubbleSettings");

        private readonly Vector4[] positions = new Vector4[LiquidBubbleSimulation.Capacity];
        private readonly Vector4[] shapes = new Vector4[LiquidBubbleSimulation.Capacity];
        private readonly Vector4[] events = new Vector4[LiquidBubbleSimulation.RippleCapacity];
        private Renderer liquidRenderer;
        private MeshFilter meshFilter;
        private MaterialPropertyBlock properties;
        private LiquidBubbleSimulation simulation;
        private double previousTime;

        public int ActiveBubbleCount => simulation?.Count ?? 0;
        public int ActiveRippleCount => simulation?.RippleCount ?? 0;
        public int TotalPopped => simulation?.TotalPopped ?? 0;

        private void OnEnable()
        {
            liquidRenderer = GetComponent<Renderer>();
            meshFilter = GetComponent<MeshFilter>();
            properties ??= new MaterialPropertyBlock();
            Restart();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += Preview;
#endif
        }

        [ContextMenu("Restart bubbles")]
        public void Restart()
        {
            simulation = new LiquidBubbleSimulation(randomSeed);
            previousTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            Upload();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying) Tick(UnityEngine.Time.deltaTime);
        }

#if UNITY_EDITOR
        private void Preview()
        {
            if (this == null || Application.isPlaying || !previewInEditMode) return;
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (now - previousTime < 1.0 / 30.0) return;
            float dt = (float)(now - previousTime);
            previousTime = now;
            Tick(Mathf.Min(dt, 0.1f));
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif

        private float Read(Material material, int id) => properties.HasFloat(id)
            ? properties.GetFloat(id) : material.GetFloat(id);

        private void Tick(float dt)
        {
            Material material = liquidRenderer != null ? liquidRenderer.sharedMaterial : null;
            if (material == null || !material.HasProperty(BubbleAmount) || meshFilter.sharedMesh == null)
                return;
            liquidRenderer.GetPropertyBlock(properties);
            float heightScale = Mathf.Max(transform.TransformVector(Vector3.up).magnitude, 0.0001f);
            Matrix4x4 toObject = transform.worldToLocalMatrix
                * Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one * heightScale);
            Vector3 center = (liquidRenderer.bounds.center - transform.position) / heightScale;
            Vector3 extents = liquidRenderer.bounds.extents / heightScale;
            float step = Mathf.Min(dt, 0.1f) * Mathf.Max(0f, Read(material, BubbleSpeed)) * 2f;
            simulation.Step(step, Read(material, BubbleAmount), Read(material, BubbleSize),
                Read(material, BubbleScale), Read(material, Amount) - 0.5f,
                new Vector2(Read(material, WobbleX), Read(material, WobbleZ)),
                meshFilter.sharedMesh.bounds, toObject, center, new Vector2(extents.x, extents.z),
                emissionRate, riseSpeed, surfaceLifetime, rippleLifetime);
            Upload();
        }

        private void Upload()
        {
            if (liquidRenderer == null || simulation == null) return;
            liquidRenderer.GetPropertyBlock(properties);
            simulation.CopyTo(positions, shapes, events, surfaceFlattening);
            properties.SetInt(BubbleCount, simulation.Count);
            properties.SetVectorArray(BubbleData, positions);
            properties.SetVectorArray(BubbleShape, shapes);
            properties.SetInt(RippleCount, simulation.RippleCount);
            properties.SetVectorArray(RippleData, events);
            properties.SetFloat(Clock, simulation.Time);
            properties.SetVector(Settings,
                new Vector4(rippleLifetime, rippleSpeed, rippleStrength, subsurfaceVisibility));
            liquidRenderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= Preview;
#endif
            if (liquidRenderer == null || properties == null) return;
            liquidRenderer.GetPropertyBlock(properties);
            properties.SetInt(BubbleCount, 0);
            properties.SetInt(RippleCount, 0);
            liquidRenderer.SetPropertyBlock(properties);
        }
    }
}
