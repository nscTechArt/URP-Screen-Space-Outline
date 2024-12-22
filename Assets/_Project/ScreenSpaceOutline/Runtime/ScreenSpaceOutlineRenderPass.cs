using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineDrawingRenderPass : ScriptableRenderPass
{
    public OutlineDrawingRenderPass(string featureName, ScreenSpaceOutlineSettings settings)
    {
        // initialization
        // --------------
        mProfilingSampler = new ProfilingSampler(featureName);
        renderPassEvent = settings.m_RenderPassEvent;
        mPassMaterial = CoreUtils.CreateEngineMaterial(settings.m_ScreenSpaceOutlineShader);
        
        // create FilteringSettings
        // ------------------------
        RenderQueueRange renderQueueRange;
        switch (settings.m_RenderQueueRange)
        {
            case ScreenSpaceOutlineSettings.RenderQueueRange.All :
                renderQueueRange = RenderQueueRange.all; break;
            case ScreenSpaceOutlineSettings.RenderQueueRange.Opaque:
                renderQueueRange = RenderQueueRange.opaque; break;
            case ScreenSpaceOutlineSettings.RenderQueueRange.Transparent:
                renderQueueRange = RenderQueueRange.transparent; break;
            default: throw new ArgumentOutOfRangeException();
        }
        uint renderingLayerMask = (uint) 1 << settings.m_RenderingLayerMask;
        mFilteringSettings = new FilteringSettings(renderQueueRange, settings.m_LayerMask, renderingLayerMask);
    }
    
    public void Setup(ScreenSpaceOutline volumeComponent)
    {
        // pass shader properties
        // ----------------------
        mPassMaterial.SetColor(_OutlineColor, volumeComponent.m_OutlineColor.value);
        mPassMaterial.SetFloat(_OutlineWidth, volumeComponent.m_OutlineWidth.value);
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // setup temporary render texture
        // ------------------------------
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.width /= 2;
        descriptor.height /= 2;
        descriptor.colorFormat = RenderTextureFormat.R8;
        descriptor.depthBufferBits = 0;
        RenderingUtils.ReAllocateIfNeeded(ref mRendererDrawingTexture, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: kRenderedObjectTextureName);
        RenderingUtils.ReAllocateIfNeeded(ref mOutlineTexture, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: kOutlineTextureName);
        
        // setup color render target
        // -------------------------
        mCameraColorTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, mProfilingSampler))
        {
            // setup temporary texture as render target
            // ----------------------------------------
            CoreUtils.SetRenderTarget(cmd, mRendererDrawingTexture, ClearFlag.Color, Color.clear);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // Step 1: render certain objects to temporary render texture
            // ----------------------------------------------------------
            var drawingSettings = CreateDrawingSettings(mShaderTagIDs, ref renderingData, kSortingCriteria);
            drawingSettings.overrideMaterial = mPassMaterial;
            drawingSettings.overrideMaterialPassIndex = kRendererDrawingPassIndex;
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref mFilteringSettings);
            
            // Step 2: edge detection to create outline
            // ----------------------------------------
            Blitter.BlitCameraTexture(cmd, mRendererDrawingTexture, mOutlineTexture, mPassMaterial, kEdgeDetectionPassIndex);
                
            // Step 3: composite
            // -----------------
            CoreUtils.SetRenderTarget(cmd, mCameraColorTexture);
            Blitter.BlitCameraTexture(cmd, mOutlineTexture, mCameraColorTexture, mPassMaterial, kCompositePassIndex);
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(mPassMaterial);
        
        mRendererDrawingTexture?.Release();
        mOutlineTexture?.Release();
    }
    
    // basic variables
    // ---------------
    private ProfilingSampler           mProfilingSampler;
    private Material                   mPassMaterial;
    // render texture handles
    // ----------------------
    private RTHandle mRendererDrawingTexture;
    private RTHandle mOutlineTexture;
    private RTHandle mCameraColorTexture;
    // renderer drawing related
    // ------------------------
    private FilteringSettings          mFilteringSettings;
    private readonly List<ShaderTagId> mShaderTagIDs = new()
    {
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly")
    };
    // constants
    // ---------
    private const SortingCriteria kSortingCriteria = SortingCriteria.CommonTransparent | SortingCriteria.CommonOpaque;
    private const string kRenderedObjectTextureName = "_RendererDrawingTexture";
    private const string kOutlineTextureName = "_OutlineTexture";
    private const int    kRendererDrawingPassIndex = 0;
    private const int    kEdgeDetectionPassIndex = 1;
    private const int    kCompositePassIndex = 2;
    // cached shader property IDs
    // --------------------------
    private static readonly int _OutlineWidth = Shader.PropertyToID("_OutlineWidth");
    private static readonly int _OutlineColor = Shader.PropertyToID("_OutlineColor");

}

