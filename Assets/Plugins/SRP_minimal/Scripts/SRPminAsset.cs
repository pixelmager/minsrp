using UnityEngine;
using UnityEngine.Rendering;

namespace Playdead.ScriptableRenderPipeline
{
    [ExecuteInEditMode]
    [CreateAssetMenu(menuName = "Playdead/Create SRPmin")]
    public class SRPminAsset : RenderPipelineAsset
    {
        public Material Material_Blit = null;

        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
        {
            return new SRPminInstance(this);
        }
    }
}
