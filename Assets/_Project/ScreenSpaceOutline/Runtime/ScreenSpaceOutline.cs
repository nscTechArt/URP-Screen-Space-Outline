using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenSpaceOutline : VolumeComponent, IPostProcessComponent
{
    [Space(5)]
    public BoolParameter m_Enable = new(false);
    [Space(5)]
    public ColorParameter        m_OutlineColor = new(Color.white);
    public ClampedFloatParameter m_OutlineWidth = new(3.0f, 1.0f, 10.0f);

    public bool IsActive() => m_Enable.value;
    public bool IsTileCompatible() => false;
}
