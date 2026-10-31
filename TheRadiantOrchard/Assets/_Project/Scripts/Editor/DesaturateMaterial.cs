using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// The one shared material for grey ↔ colour sprites and tilemaps (Art/Shaders/M_SpriteDesaturate). Builders that
    /// make visitors or zones use it; one material keeps them batching (ENGINEERING_GUIDE 7, 8).
    /// </summary>
    public static class DesaturateMaterial
    {
        public const string ShaderName = "Radiant Orchard/Sprite Desaturate";
        public const string Path = "Assets/_Project/Art/Shaders/M_SpriteDesaturate.mat";

        /// <summary>Loads the material, creating it on first use. Null (with an error) if the shader is missing.</summary>
        public static Material Ensure()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Path);
            if (material != null) return material;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"DesaturateMaterial: shader '{ShaderName}' not found (Art/Shaders/Sprite-Desaturate.shader).");
                return null;
            }
            material = new Material(shader) { name = "M_SpriteDesaturate", enableInstancing = false };
            AssetDatabase.CreateAsset(material, Path);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
