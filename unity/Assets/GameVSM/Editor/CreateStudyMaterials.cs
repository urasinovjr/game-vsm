// Import preparation only. Must be compiled and visually checked in the chosen Unity Editor.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameVSM.Editor
{
    public static class CreateStudyMaterials
    {
        private const string Root = "Assets/GameVSM/Art/Materials";

        [MenuItem("GameVSM/Create URP study materials")]
        public static void Create()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Create/open a URP project before importing GameVSM materials.");
            if (!Directory.Exists(Root))
                throw new DirectoryNotFoundException("Copy the prepared Assets/GameVSM folder first.");

            int created = 0;
            foreach (string folder in Directory.GetDirectories(Root))
            {
                string directory = folder.Replace('\\', '/');
                string destination = directory + "/" + Path.GetFileName(directory) + ".mat";
                // Do not overwrite later artistic adjustments to a generated material.
                if (File.Exists(destination)) continue;

                Texture2D baseColor = ImportTexture(directory + "/BaseColor.jpg", true, false);
                Texture2D normal = ImportTexture(directory + "/NormalGL.jpg", false, true);
                Texture2D metallic = ImportTexture(directory + "/MetallicSmoothness.png", false, false);
                Texture2D occlusion = ImportTexture(directory + "/Occlusion.png", false, false);
                var material = new Material(shader) { name = Path.GetFileName(directory) };
                material.SetFloat("_WorkflowMode", 1f);
                material.SetTexture("_BaseMap", baseColor);
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.SetTexture("_OcclusionMap", occlusion);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, destination);
                created++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"GameVSM: created {created} URP study materials; existing materials preserved.");
        }

        private static Texture2D ImportTexture(string path, bool srgb, bool normal)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Missing prepared material map", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture importer unavailable: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.convertToNormalmap = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Texture import failed: " + path);
            return texture;
        }
    }
}
