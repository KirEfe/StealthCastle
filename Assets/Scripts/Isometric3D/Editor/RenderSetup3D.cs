using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Силуэт игрока сквозь стены (3D-рендерер UniversalRenderer3D):
    /// слой Player убирается из обычного прохода и рисуется двумя Render Objects —
    /// сначала там, где он за стеной (плоский цвет, тест глубины Greater), затем как обычно поверх.
    /// Так за стеной виден силуэт, а сам игрок не перекрашивается собственными гранями.
    /// </summary>
    public static class RenderSetup3D
    {
        private const string RendererPath = "Assets/Settings/UniversalRenderer3D.asset";
        private const string MaterialPath = "Assets/Art/Generated/Materials/PlayerSilhouette.mat";
        private const string OccludedName = "PlayerOccluded";
        private const string VisibleName = "PlayerVisible";

        [MenuItem("StealthCastle/3D/Art/Setup Player Silhouette")]
        public static void Setup()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                Debug.LogError($"[RenderSetup3D] {RendererPath} not found");
                return;
            }
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0)
            {
                Debug.LogError("[RenderSetup3D] Layer 'Player' not found");
                return;
            }
            int playerMask = 1 << playerLayer;

            renderer.opaqueLayerMask &= ~playerMask;
            renderer.transparentLayerMask &= ~playerMask;

            var occluded = FindOrAdd(renderer, OccludedName);
            occluded.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            occluded.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            occluded.settings.filterSettings.LayerMask = playerMask;
            occluded.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
            occluded.settings.overrideMaterial = SilhouetteMaterial();
            occluded.settings.overrideMaterialPassIndex = 0;
            occluded.settings.overrideDepthState = true;
            occluded.settings.depthCompareFunction = CompareFunction.Greater;
            occluded.settings.enableWrite = false;

            var visible = FindOrAdd(renderer, VisibleName);
            visible.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            visible.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            visible.settings.filterSettings.LayerMask = playerMask;
            visible.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            visible.settings.overrideDepthState = false;

            EditorUtility.SetDirty(occluded);
            EditorUtility.SetDirty(visible);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Debug.Log("[RenderSetup3D] Player silhouette set up");
        }

        private static RenderObjects FindOrAdd(UniversalRendererData renderer, string featureName)
        {
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is RenderObjects existing && feature.name == featureName) return existing;
            }

            var created = ScriptableObject.CreateInstance<RenderObjects>();
            created.name = featureName;
            AssetDatabase.AddObjectToAsset(created, renderer);
            renderer.rendererFeatures.Add(created);

            // Карта id фич: без неё инспектор рендерера считает список повреждённым
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(created, out _, out long localId);
            var so = new SerializedObject(renderer);
            var map = so.FindProperty("m_RendererFeatureMap");
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return created;
        }

        private static Material SilhouetteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            PieceAuthoring3D.EnsureFolder("Assets/Art/Generated/Materials");
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.35f, 0.65f, 1f) };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
