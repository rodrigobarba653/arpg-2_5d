using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mathra.Visibility
{
    public sealed class AXVisibilityFeature : ScriptableRendererFeature
    {
        Pass pass;
        public override void Create()
        {
            pass=new Pass {renderPassEvent=RenderPassEvent.AfterRenderingPostProcessing};
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            var target=Find(data.cameraData.camera);
            if (!target) return;
            pass.ConfigureInput(target.mode==AXVisibility.RevealMode.CombatSilhouette && target.depthOnlySilhouette
                ?ScriptableRenderPassInput.Depth:ScriptableRenderPassInput.None);
            renderer.EnqueuePass(pass);
        }
        static AXVisibility Find(Camera camera)
        {
            foreach(var target in AXVisibility.Instances)
                if(target && target.ready && target.Source==camera) return target;
            return null;
        }
        sealed class Pass : ScriptableRenderPass
        {
            sealed class Data
            {
                public Material composite, pose;
                public Mesh mesh;
                public Matrix4x4 matrix;
            }
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();
                var target=Find(frame.Get<UniversalCameraData>().camera);
                if (!target) return;
                using(var builder=graph.AddRasterRenderPass<Data>("AX visibility",out var draw))
                {
                    draw.composite=target.mode==AXVisibility.RevealMode.CombatSilhouette?null:target.composite;
                    bool pose=target.mode==AXVisibility.RevealMode.CombatSilhouette ||
                        (target.mode==AXVisibility.RevealMode.Forcefield && target.UseAssist);
                    draw.pose=pose?target.silhouette:null;
                    draw.mesh=pose?target.spriteMesh:null;
                    draw.matrix=target.spriteMatrix;
                    builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                    if(target.mode==AXVisibility.RevealMode.CombatSilhouette && target.depthOnlySilhouette)
                        builder.UseTexture(resources.cameraDepthTexture,AccessFlags.Read);
                    builder.SetRenderFunc(static (Data d,RasterGraphContext ctx)=>{
                        if(d.composite) ctx.cmd.DrawProcedural(Matrix4x4.identity,d.composite,0,MeshTopology.Triangles,3);
                        if(d.pose && d.mesh) ctx.cmd.DrawMesh(d.mesh,d.matrix,d.pose,0,0);
                    });
                }
            }
        }
    }
}
