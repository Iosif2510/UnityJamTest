using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Idempotent setup for the existing Liquid_Bubble graph and open demo scene.</summary>
public static class ApplyLiquidBubbles
{
    public static object Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply the effect in Edit mode.");
        const string includePath = "Assets/Arts/Shaders/LiquidBubbleEffects.hlsl";
        const string graphPath = "Assets/Arts/Shaders/Liquid_Bubble.shadergraph";
        AssetDatabase.ImportAsset(includePath, ImportAssetOptions.ForceSynchronousImport);
        string guid = AssetDatabase.AssetPathToGUID(includePath);
        if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("HLSL include was not imported.");

        // Shader Graph's serialized nodes are separate JSON documents. Replace
        // only this function node; all Blackboard properties and edges survive.
        string graph = File.ReadAllText(graphPath);
        string changed = Regex.Replace(graph, @"\{\s*""m_SGVersion""[\s\S]*?\n\}", match =>
        {
            string node = match.Value;
            if (!node.Contains("\"m_FunctionName\": \"LiquidFoam\"")) return node;
            node = Regex.Replace(node, @"""m_SourceType"":\s*\d+", "\"m_SourceType\": 0");
            node = Regex.Replace(node, @"""m_FunctionSource"":\s*""[^""]*""",
                "\"m_FunctionSource\": \"" + guid + "\"");
            node = Regex.Replace(node, @"""m_FunctionBody"":\s*""(?:\\.|[^""\\])*""",
                "\"m_FunctionBody\": \"\"");
            return node;
        });
        if (graph != changed)
        {
            File.WriteAllText(graphPath, changed, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceSynchronousImport);
        }

        var componentType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("PotionShop.LiquidBubbleController")).FirstOrDefault(t => t != null);
        if (componentType == null) throw new InvalidOperationException("Compile LiquidBubbleController first.");
        var scene = SceneManager.GetActiveScene();
        var targets = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
            .Where(r => AssetDatabase.GetAssetPath(r.sharedMaterial) == "Assets/Arts/Shaders/M_Liquid_Bubble.mat")
            .ToArray();
        if (targets.Length == 0) throw new InvalidOperationException("No M_Liquid_Bubble renderer in the open scene.");
        Undo.SetCurrentGroupName("Apply volumetric liquid bubbles");
        foreach (var renderer in targets)
        {
            if (renderer.GetComponent(componentType) == null)
                Undo.AddComponent(renderer.gameObject, componentType);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return new { scene = scene.path, targets = targets.Select(r => r.name).ToArray(), includeGuid = guid };
    }
}
