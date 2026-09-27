using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM.Editor
{
    public static class TrainGlass
    {
        // glTF's premultiplied clearcoat pass occluded scenery and refrigerated stock
        // in the native URP player. Keep source GLB intact; bind native glass materials.
        public static void Apply()
        {
            const string folder = "Assets/GameVSM/Generated/TrainGlass";
            Directory.CreateDirectory(folder);
            var replacements = new Dictionary<string, Material>();
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) continue;
                    string name = source.name.Replace("Native_", "");
                    bool bottledWater = renderer.name.Contains("Bottle") && name == "Detail_Glass_tint";
                    if (bottledWater) name = "Bottle_water";
                    float alpha = name switch {
                        "Glass_ExteriorGreen" or "Entrance_GlassGreen" => .12f,
                        "Detail_Glass_case" => .055f,
                        "Detail_Glass_tint" => .14f,
                        "Detail_Bottle_amber" or "Detail_Bottle_green" => .82f,
                        "Bottle_water" => .7f,
                        _ => -1
                    };
                    if (alpha < 0) continue;
                    if (!replacements.TryGetValue(name, out var material))
                    {
                        string path = folder + "/" + name + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        bool create = material == null;
                        if (create) material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.name = "Native_" + name;
                        var color = source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : source.GetColor("_BaseColor");
                        if (name is "Glass_ExteriorGreen" or "Entrance_GlassGreen") color = new Color(.45f,.59f,.53f);
                        color.a = alpha; material.SetColor("_BaseColor", color);
                        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                        material.SetFloat("_BlendModePreserveSpecular", 0);
                        material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .76f);
                        material.SetFloat("_Cull", (float)CullMode.Back);
                        BaseShaderGUI.SetupMaterialBlendMode(material);
                        material.enableInstancing = true;
                        if (create) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
                        replacements[name] = material;
                    }
                    materials[i] = material; changed = true;
                }
                if (changed) { renderer.sharedMaterials = materials; EditorUtility.SetDirty(renderer); }
            }
        }
    }
}
