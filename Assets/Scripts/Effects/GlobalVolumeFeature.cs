using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Volumes are scene state, so they must exist before URP evaluates the camera's
// volume stack. They do not need a render pass or a Render Graph texture.
public class GlobalVolumeFeature : ScriptableRendererFeature
{
    public LayerMask _layerMask;
    public VolumeProfile _baseProfile;
    public List<VolumeProfile> _qualityProfiles = new List<VolumeProfile>();

    private GameObject volumeHolder;
    private Volume baseVolume;
    private Volume qualityVolume;

    public override void Create()
    {
        RenderPipelineManager.beginCameraRendering -= UpdateVolumes;
        RenderPipelineManager.beginCameraRendering += UpdateVolumes;
    }

    private void UpdateVolumes(ScriptableRenderContext context, Camera camera)
    {
        if (!isActive) return;
        if (!volumeHolder)
        {
            volumeHolder = new GameObject("[DefaultVolume]") { hideFlags = HideFlags.HideAndDontSave };
            baseVolume = volumeHolder.AddComponent<Volume>();
            baseVolume.isGlobal = true;
            qualityVolume = volumeHolder.AddComponent<Volume>();
            qualityVolume.isGlobal = true;
            qualityVolume.priority = 1;
        }

        baseVolume.sharedProfile = _baseProfile;
        int index = QualitySettings.GetQualityLevel();
        qualityVolume.sharedProfile = _qualityProfiles != null && index >= 0 && index < _qualityProfiles.Count
            ? _qualityProfiles[index]
            : null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }

    protected override void Dispose(bool disposing)
    {
        RenderPipelineManager.beginCameraRendering -= UpdateVolumes;
        CoreUtils.Destroy(volumeHolder);
    }
}