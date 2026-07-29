using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace RetroLOTR.Rendering
{
    /// <summary>
    /// Hosts independently attachable camera-art treatments. Add this feature more
    /// than once to a renderer and select a different style on each instance.
    /// </summary>
    public sealed class CardArtStyleRendererFeature : ScriptableRendererFeature
    {
        public enum ArtStyle
        {
            LivingIllustration,
            VintagePrint,
            PainterlySimplification,
            EightiesCartoon
        }

        [Serializable]
        public sealed class Settings
        {
            public ArtStyle style;
            [Range(0f, 1f)] public float intensity = 0.15f;

            [Header("Living Illustration")]
            [Range(0f, 3f)] public float motionPixels = 0.7f;
            [Range(1f, 20f)] public float motionScale = 7f;
            [Range(0f, 1f)] public float motionSpeed = 0.08f;
            [Range(0f, 1f)] public float warmHalation = 0.12f;

            [Header("Vintage Print")]
            [Range(0f, 3f)] public float colorMisregistrationPixels = 0.75f;
            [Range(0f, 1f)] public float printVariation = 0.06f;

            [Header("Painterly Simplification")]
            [Range(1f, 5f)] public float brushRadius = 2f;

            [Header("80s Cartoon")]
            [Range(2f, 12f)] public float celBands = 6f;
            [Range(0f, 2f)] public float cartoonOutline = 0.8f;
            [Range(0.5f, 3f)] public float cartoonOutlineRadius = 1.25f;
            [Range(0f, 1f)] public float colorSimplification = 0.55f;
            [Range(0f, 1f)] public float broadcastTexture = 0.035f;
            [Range(0.5f, 1.5f)] public float cartoonSaturation = 1.08f;

            [Header("Camera Filtering")]
            [Tooltip("Also show this treatment in the Scene view.")]
            public bool showInSceneView;
            [Tooltip("Apply to cameras rendering into a texture, such as the minimap.")]
            public bool affectRenderTextures;
        }

        [SerializeField] private Settings settings = new();

        private Material material;
        private CardArtStylePass pass;

        public override void Create()
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Hidden/RetroLOTR/CardArtStyles");
                if (shader != null)
                    material = CoreUtils.CreateEngineMaterial(shader);
            }

            pass ??= new CardArtStylePass();
            pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (material == null || settings.intensity <= 0f || !ShouldRender(camera))
                return;

            UpdateMaterial();
            pass.Setup(material, settings.style.ToString());
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private bool ShouldRender(Camera camera)
        {
            if (camera == null || camera.cameraType == CameraType.Preview ||
                camera.cameraType == CameraType.Reflection)
                return false;

            if (camera.cameraType == CameraType.SceneView && !settings.showInSceneView)
                return false;

            return settings.affectRenderTextures || camera.targetTexture == null;
        }

        private void UpdateMaterial()
        {
            material.SetFloat("_Style", (float)settings.style);
            material.SetFloat("_EffectIntensity", settings.intensity);
            material.SetFloat("_MotionPixels", settings.motionPixels);
            material.SetFloat("_MotionScale", settings.motionScale);
            material.SetFloat("_MotionSpeed", settings.motionSpeed);
            material.SetFloat("_WarmHalation", settings.warmHalation);
            material.SetFloat("_MisregistrationPixels", settings.colorMisregistrationPixels);
            material.SetFloat("_PrintVariation", settings.printVariation);
            material.SetFloat("_BrushRadius", settings.brushRadius);
            material.SetFloat("_CelBands", settings.celBands);
            material.SetFloat("_CartoonOutline", settings.cartoonOutline);
            material.SetFloat("_CartoonOutlineRadius", settings.cartoonOutlineRadius);
            material.SetFloat("_ColorSimplification", settings.colorSimplification);
            material.SetFloat("_BroadcastTexture", settings.broadcastTexture);
            material.SetFloat("_CartoonSaturation", settings.cartoonSaturation);
        }

        private sealed class CardArtStylePass : ScriptableRenderPass
        {
            private Material material;
            private string styleName;

            public CardArtStylePass()
            {
                requiresIntermediateTexture = true;
            }

            public void Setup(Material effectMaterial, string selectedStyle)
            {
                material = effectMaterial;
                styleName = selectedStyle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                    return;

                TextureHandle source = resources.activeColorTexture;
                TextureDesc descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = $"_CardArtStyle_{styleName}";
                descriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(descriptor);

                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, $"Card Art Style: {styleName}");
                resources.cameraColor = destination;
            }
        }
    }
}
