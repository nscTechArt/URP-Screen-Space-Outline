using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class ScreenSpaceOutlineSettings
{
    [Header("Basic Settings"), Space(5)]
    public RenderPassEvent m_RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    public Shader          m_ScreenSpaceOutlineShader;
    
    [Header("Filtering Settings"), Space(5)]
    public RenderQueueRange   m_RenderQueueRange = RenderQueueRange.Opaque;
    public LayerMask          m_LayerMask;
    [Range(0, 32)] public int m_RenderingLayerMask;
    
    // enums
    // -----
    public enum RenderQueueRange
    {
        [UsedImplicitly] All, 
        [UsedImplicitly] Opaque, 
        [UsedImplicitly] Transparent
    }
}

public class ScreenSpaceOutlineRenderFeature : ScriptableRendererFeature
{
    [SerializeField] 
    private ScreenSpaceOutlineSettings   m_Settings = new();
    private OutlineDrawingRenderPass mPass;

    public override void Create()
    {
        if (m_Settings.m_ScreenSpaceOutlineShader == null) return;
        
        mPass = new OutlineDrawingRenderPass(name, m_Settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        ScreenSpaceOutline volumeComponent = VolumeManager.instance.stack.GetComponent<ScreenSpaceOutline>();
        if (!volumeComponent || !volumeComponent.IsActive()) return;
        
        mPass.Setup(volumeComponent);
        renderer.EnqueuePass(mPass);
    }

    protected override void Dispose(bool disposing)
    {
        mPass.Dispose();
    }
}
