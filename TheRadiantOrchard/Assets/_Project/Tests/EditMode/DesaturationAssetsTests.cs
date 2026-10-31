using NUnit.Framework;
using RadiantOrchard.Presentation;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// The desaturation shader and its one shared material (ENGINEERING_GUIDE 8): the shader compiles, the material
    /// uses it, and the visitor's body and head are on it with a Desaturation driving them.
    /// </summary>
    public class DesaturationAssetsTests
    {
        private const string ShaderName = "Radiant Orchard/Sprite Desaturate";
        private const string MaterialPath = "Assets/_Project/Art/Shaders/M_SpriteDesaturate.mat";
        private const string VisitorPrefabPath = "Assets/_Project/Prefabs/Visitors/Visitor.prefab";

        [Test]
        public void TheShaderCompilesWithItsProperties()
        {
            Shader shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "The shader has compile errors.");
            foreach (string property in new[] { "_Saturation", "_Darken", "_SweepOrigin", "_SweepDirection", "_SweepHeight", "_SweepSoftness" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(property), 0, property);
        }

        [Test]
        public void TheSharedMaterialUsesTheShader()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, MaterialPath);
            Assert.AreEqual(ShaderName, material.shader.name);
        }

        [Test]
        public void TheVisitorsBodyAndHeadAreOnTheMaterialAndStartGrey()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorPrefabPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var colour = prefab.GetComponent<Desaturation>();
            Assert.IsNotNull(colour, "The visitor has a Desaturation.");
            Assert.AreEqual(0f, colour.Saturation, "Grey until healed.");

            SerializedProperty renderers = new SerializedObject(colour).FindProperty("_renderers");
            Assert.AreEqual(2, renderers.arraySize, "Body and head.");
            for (int i = 0; i < renderers.arraySize; i++)
                Assert.AreSame(material, ((Renderer)renderers.GetArrayElementAtIndex(i).objectReferenceValue).sharedMaterial);

            Assert.AreSame(colour, new SerializedObject(prefab.GetComponent<VisitorView>()).FindProperty("_colour").objectReferenceValue);
        }
    }
}
