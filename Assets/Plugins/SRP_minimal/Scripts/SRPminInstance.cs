using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_VR
using UnityEngine.XR;
#endif //ENABLE_VR

namespace Playdead.ScriptableRenderPipeline
{
    public class SRPminInstance : UnityEngine.Rendering.RenderPipeline
    {
        private SRPminAsset asset;
        public SRPminInstance(SRPminAsset asset)
        {
            this.asset = asset;
        }

        private CommandBuffer cmdbuf = null;

        protected override void Dispose( bool disposing )
        {
            base.Dispose( disposing );

            if (cmdbuf != null)
            {
                cmdbuf.Dispose();
                cmdbuf = null;
            }
        }

        //note: main entrypoint
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            if (cameras.Length == 0)
            {
                return; // For cases when an empty scene with no cameras is open
            }

            for ( int camidx=0, camcount=cameras.Length; camidx<camcount; ++camidx )
            {
                Camera camera = cameras[camidx];
                Camera.SetupCurrent(camera);
                RenderGameCamera(context, camera);
            }
        }

        private void RenderGameCamera(ScriptableRenderContext context, Camera camera)
        {
            #if ENABLE_VR
            if ( camera.stereoEnabled )
            {
                Debug.Assert( ( XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced) || (camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono) );

                context.SetupCameraProperties(camera, stereoSetup: true);
                context.StartMultiEye(camera);
            }
            else
            {
                context.SetupCameraProperties(camera);
            }
            #else //ENABLE_VR
            context.SetupCameraProperties(camera);
            #endif //ENABLE_VR


            if ( cmdbuf == null )
            {
                cmdbuf = new CommandBuffer();
                cmdbuf.name = "cmdbuf";
            }

            int rtid_color = Shader.PropertyToID("main_col");
            int rtid_depth = Shader.PropertyToID("main_depth");
            {
                #if ENABLE_VR
                RenderTextureDescriptor rtdesc = camera.stereoEnabled ? XRGraphics.eyeTextureDesc : new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
                #else
                RenderTextureDescriptor rtdesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
                #endif

                RenderTextureDescriptor rtDesc_Color;
                rtDesc_Color = rtdesc;
                rtDesc_Color.colorFormat = RenderTextureFormat.ARGB32;
                rtDesc_Color.depthBufferBits = 0;

                RenderTextureDescriptor rtDesc_Depth;
                rtDesc_Depth = rtdesc;
                rtDesc_Depth.colorFormat = RenderTextureFormat.Depth;
                rtDesc_Depth.depthBufferBits = 32; //note: D32S8

                cmdbuf.GetTemporaryRT( rtid_color, rtDesc_Color, FilterMode.Bilinear );
                cmdbuf.GetTemporaryRT( rtid_depth, rtDesc_Depth, FilterMode.Point );
                context.ExecuteCommandBuffer(cmdbuf);
                cmdbuf.Clear();
            }

            //note: draw objects
            {
                CullingResults cull;
                {
                    ScriptableCullingParameters cullParams;
                    camera.TryGetCullingParameters( stereoAware:camera.stereoEnabled, out cullParams);
                    Debug.Assert(cullParams != null);
                    cull = context.Cull(ref cullParams);
                }

                cmdbuf.SetRenderTarget(color: new RenderTargetIdentifier(rtid_color), depth: new RenderTargetIdentifier(rtid_depth), mipLevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: -1);
                cmdbuf.ClearRenderTarget(clearDepth: true, clearColor: true, backgroundColor: camera.backgroundColor);
                context.ExecuteCommandBuffer(cmdbuf);
                cmdbuf.Clear();

                var sortingSettings = new SortingSettings(camera);
                sortingSettings.criteria = SortingCriteria.CommonOpaque;
                var drawingSettings = new DrawingSettings(new ShaderTagId("Always"), sortingSettings);
                var filterSettings = new FilteringSettings( renderQueueRange: RenderQueueRange.opaque);
                context.DrawRenderers(cull, ref drawingSettings, ref filterSettings);
            }

            //note: blit color -> screen
            {
                cmdbuf.Blit( rtid_color, BuiltinRenderTextureType.CameraTarget, asset.Material_Blit);
                context.ExecuteCommandBuffer(cmdbuf);
                cmdbuf.Clear();
            }

            #if UNITY_EDITOR
            if ( camera.cameraType == CameraType.SceneView )
            {
                if (UnityEditor.Handles.ShouldRenderGizmos())
                {
                    context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
                }
            }
            #endif //UNITY_EDITOR

            #if ENABLE_VR
            if ( camera.stereoEnabled )
            {
                context.StopMultiEye(camera);
                context.StereoEndRender(camera);
            }
            #endif //ENABLE_VR

            //cleanup
            {
                cmdbuf.ReleaseTemporaryRT(rtid_color);
                cmdbuf.ReleaseTemporaryRT(rtid_depth);
                context.ExecuteCommandBuffer(cmdbuf);
                cmdbuf.Clear();
            }
            context.Submit();
        } //RenderGameCamera
    } //class
} //namespace
