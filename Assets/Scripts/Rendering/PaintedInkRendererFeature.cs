using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace RetroLOTR.Rendering
{
    /// <summary>
    /// A restrained full-screen treatment that gives the rendered game the uneven
    /// pigment, dark linework, and warm paper response of painted fantasy art.
    /// </summary>
    public sealed class PaintedInkRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            [Range(0f, 1f)] public float intensity = 0.55f;
            [Range(0f, 1f)] public float inkStrength = 0.22f;
            [Range(0.25f, 4f)] public float inkRadius = 1f;
            [Range(0f, 1f)] public float pigmentGranulation = 0.12f;
            [Range(0f, 1f)] public float paperGrain = 0.055f;
            [Range(0f, 2f)] public float grainScale = 0.85f;
            [Range(0f, 1f)] public float warmth = 0.10f;
            [Range(0f, 1f)] public float vignette = 0.12f;
            [Range(0f, 2f)] public float animationSpeed = 0.10f;
            [Tooltip("Also show the treatment in the Scene view.")]
            public bool showInSceneView;
            [Tooltip("Apply to cameras that render into a RenderTexture, such as the minimap.")]
            public bool affectRenderTextures;
        }

        [SerializeField] private Settings settings = new();

        private Material material;
        private PaintedInkPass pass;

        public override void Create()
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Hidden/RetroLOTR/PaintedInk");
                if (shader != null)
                    material = CoreUtils.CreateEngineMaterial(shader);
            }

            pass ??= new PaintedInkPass();
            pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (!ShouldRender(camera) || material == null || settings.intensity <= 0f)
                return;

            UpdateMaterial();
            pass.Setup(material);
            renderer.EnqueuePass(pass);
        }

#if URP_COMPATIBILITY_MODE
#pragma warning disable 618, 672
        [Obsolete("Compatibility-mode setup for URP's non-RenderGraph path.")]
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (pass != null && ShouldRender(renderingData.cameraData.camera))
                pass.SetCameraColorTarget(renderer.cameraColorTargetHandle);
        }
#pragma warning restore 618, 672
#endif

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
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
            material.SetFloat("_EffectIntensity", settings.intensity);
            material.SetFloat("_InkStrength", settings.inkStrength);
            material.SetFloat("_InkRadius", settings.inkRadius);
            material.SetFloat("_PigmentStrength", settings.pigmentGranulation);
            material.SetFloat("_GrainStrength", settings.paperGrain);
            material.SetFloat("_GrainScale", settings.grainScale);
            material.SetFloat("_Warmth", settings.warmth);
            material.SetFloat("_Vignette", settings.vignette);
            material.SetFloat("_AnimationSpeed", settings.animationSpeed);
        }

        private sealed class PaintedInkPass : ScriptableRenderPass
        {
            private const string PassName = "Painted Ink";
            private Material material;
#if URP_COMPATIBILITY_MODE
            private RTHandle cameraColor;
            private RTHandle temporaryColor;
#endif

            public PaintedInkPass()
            {
                requiresIntermediateTexture = true;
            }

            public void Setup(Material effectMaterial) => material = effectMaterial;

#if URP_COMPATIBILITY_MODE
#pragma warning disable 618, 672
            public void SetCameraColorTarget(RTHandle target) => cameraColor = target;

            [Obsolete("Compatibility-mode setup for URP's non-RenderGraph path.")]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref temporaryColor,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_PaintedInkTemporaryColor");
            }

            [Obsolete("Compatibility-mode fallback for URP's non-RenderGraph path.")]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null || cameraColor == null || temporaryColor == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get(PassName);
                Blitter.BlitCameraTexture(cmd, cameraColor, temporaryColor, material, 0);
                Blitter.BlitCameraTexture(cmd, temporaryColor, cameraColor);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore 618, 672
#endif

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                    return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                    return;

                TextureHandle source = resources.activeColorTexture;
                TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "_PaintedInkCameraColor";
                destinationDescriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);

                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, PassName);
                resources.cameraColor = destination;
            }

            public void Dispose()
            {
#if URP_COMPATIBILITY_MODE
                temporaryColor?.Release();
                temporaryColor = null;
#endif
            }
        }
    }
}
