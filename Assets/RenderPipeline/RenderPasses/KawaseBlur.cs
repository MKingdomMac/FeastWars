using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KawaseBlur : ScriptableRendererFeature
{
    [System.Serializable]
    public class KawaseBlurSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material blurMaterial = null;

        [Range(2, 15)]
        public int blurPasses = 1;

        [Range(1, 4)]
        public int downsample = 1;
        public bool copyToFramebuffer;
        public string targetName = "_blurTexture";
    }

    public KawaseBlurSettings settings = new KawaseBlurSettings();
    CustomRenderPass scriptablePass;

    class CustomRenderPass : ScriptableRenderPass
    {
        public Material blurMaterial;
        public int passes;
        public int downsample;
        public bool copyToFramebuffer;
        public string targetName;
        string profilerTag;

        private RTHandle rtHandle1;
        private RTHandle rtHandle2;

        private RenderTargetIdentifier source;

        public void Setup(RenderTargetIdentifier source)
        {
            this.source = source;
        }

        public CustomRenderPass(string profilerTag)
        {
            this.profilerTag = profilerTag;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            int width = cameraTextureDescriptor.width / downsample;
            int height = cameraTextureDescriptor.height / downsample;

            var desc = cameraTextureDescriptor;
            desc.width = width;
            desc.height = height;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.colorFormat = RenderTextureFormat.ARGB32;

            RenderingUtils.ReAllocateIfNeeded(ref rtHandle1, desc, FilterMode.Bilinear, name: "_BlurRT1");
            RenderingUtils.ReAllocateIfNeeded(ref rtHandle2, desc, FilterMode.Bilinear, name: "_BlurRT2");

            ConfigureTarget(rtHandle1);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            rtHandle1?.Release();
            rtHandle2?.Release();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

            RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;

            // first pass
            cmd.SetGlobalFloat("_offset", 1.5f);
            cmd.Blit(source, rtHandle1, blurMaterial);

            for (var i = 1; i < passes - 1; i++)
            {
                cmd.SetGlobalFloat("_offset", 0.5f + i);
                cmd.Blit(rtHandle1, rtHandle2, blurMaterial);

                // pingpong
                var rttmp = rtHandle1;
                rtHandle1 = rtHandle2;
                rtHandle2 = rttmp;
            }

            // final pass
            cmd.SetGlobalFloat("_offset", 0.5f + passes - 1f);
            if (copyToFramebuffer)
            {
                cmd.Blit(rtHandle1, source, blurMaterial);
            }
            else
            {
                cmd.Blit(rtHandle1, rtHandle2, blurMaterial);
                cmd.SetGlobalTexture(targetName, rtHandle2);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }

    public override void Create()
    {
        scriptablePass = new CustomRenderPass("KawaseBlur");
        scriptablePass.blurMaterial = settings.blurMaterial;
        scriptablePass.passes = settings.blurPasses;
        scriptablePass.downsample = settings.downsample;
        scriptablePass.copyToFramebuffer = settings.copyToFramebuffer;
        scriptablePass.targetName = settings.targetName;
        scriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
#if UNITY_2022_1_OR_NEWER
        var src = renderer.cameraColorTargetHandle;
#else
        var src = renderer.cameraColorTarget;
#endif
        scriptablePass.Setup(src);
        renderer.EnqueuePass(scriptablePass);
    }
}
