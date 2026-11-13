using UnityEngine;
using UnityEngine.Rendering;

// A scene-scoped quality profile: preserve the user's quality selection on exit.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BabyFootVisualProfile : MonoBehaviour
{
    public RenderPipelineAsset pipeline;
    [SerializeField, HideInInspector] RenderPipelineAsset previous;
    [SerializeField, HideInInspector] bool applied;
    void OnEnable()
    {
        if(!pipeline)return;
        if(QualitySettings.renderPipeline==pipeline){applied=true;return;}
        previous=QualitySettings.renderPipeline;
        QualitySettings.renderPipeline=pipeline;applied=true;
    }
    void OnDisable()
    {
        if(applied&&QualitySettings.renderPipeline==pipeline)QualitySettings.renderPipeline=previous;
        applied=false;
    }
}
