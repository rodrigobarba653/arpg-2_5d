using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mathra.Visibility
{
    [DisallowMultipleComponent, DefaultExecutionOrder(32000)]
    public sealed class AXVisibility : MonoBehaviour
    {
        public enum RevealMode { Forcefield = 1, CombatSilhouette = 2 }
        public RevealMode mode = RevealMode.Forcefield;
        public SpriteRenderer ax;
        [Tooltip("Optional. Defaults to the camera tagged MainCamera.")] public Camera gameplayCamera;
        [Header("Distance")]
        [Range(0,100), Tooltip("Start revealing at this camera-to-AX distance, in world units.")] public float minimumCameraDistance;
        [Range(0,200), Tooltip("Stop revealing beyond this distance. Zero disables the upper limit.")] public float maximumCameraDistance;
        [Range(0,10), Tooltip("Fade in or out inside each active distance limit.")] public float distanceFadeWidth;
        [Range(0,10), Tooltip("Forcefield only. Keep foreground surfaces within this many world units of AX along camera depth. Disables sprite assist while positive so chairs can still cover AX.")] public float keepNearAX;
        float distanceWeight;
        internal bool UseAssist => fieldSpriteAssist > 0 && keepNearAX <= 0;
        [Header("Capture")]
        [Range(.25f, 1)] public float resolution = 1;
        [Tooltip("Match the Universal Renderer index used by the gameplay camera. -1 uses the pipeline default.")]
        public int rendererIndex = -1;
        [Range(64,1024), Tooltip("Maximum resolution of the longer side of the bubble capture.")] public int captureLimit = 320;
        [Tooltip("Enable if you want real-time shadows in the secondary view.")] public bool captureShadows;
        [Range(1,60)] public int idleCaptureHz = 20;
        [Tooltip("Refresh every frame even when camera and AX are still.")] public bool fullRate;
        [Tooltip("Only capture when a physics ray hits a foreground collider. Leave off for collider-free waterfalls.")] public bool requireBlockerCollider;
        [Tooltip("Environment-only layers for the optional collider check. Exclude AX.")] public LayerMask blockerLayers = ~0;
        Rect crop;
        Matrix4x4 lastCamera, lastProjection;
        Rect lastCrop;
        Sprite lastFrame;
        float nextRefresh;
        Vector3 lastPosition;
        [Header("Forcefield")]
        [Range(0,1)] public float fieldStrength = .92f;
        [Range(.25f,4), Tooltip("Horizontal radius in projected sprite heights.")] public float radiusX = .8f;
        [Range(.25f,4)] public float radiusY = .95f;
        [Range(.01f,1)] public float feather = .5f;
        [Range(-1,1)] public float verticalOffset = 0;
        [Range(0,.5f), Tooltip("World units of space kept in front of AX's nearest bounds.")] public float depthPadding = .015f;
        [Range(0,1), Tooltip("Blend the actual sprite through any residual foreground at AX's depth.")] public float fieldSpriteAssist = 0;
        [Header("Combat silhouette")]
        [ColorUsage(true,true)] public Color silhouetteColor = new Color(.55f,.92f,1,1);
        [Range(0,1)] public float silhouetteOpacity = .9f;
        [Range(0,1), Tooltip("0 gives a flat silhouette. 1 retains the sprite's original colors.")] public float spriteDetail = .35f;
        [Range(0,1)] public float alphaCutoff = .06f;
        [Tooltip("Enable to show only behind depth-writing geometry. Disable for waterfalls and other transparent blockers.")] public bool depthOnlySilhouette;
        internal Camera Source => gameplayCamera ? gameplayCamera : Camera.main;
        internal static readonly List<AXVisibility> Instances = new List<AXVisibility>();
        internal Material composite, silhouette;
        internal Mesh spriteMesh;
        internal Matrix4x4 spriteMatrix;
        internal bool ready;
        Camera capture;
        RenderTexture newView;
        readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        readonly Vector3[] corners = new Vector3[8];

        Sprite cachedSprite;

        RevealMode lastMode;
        bool warned;

        void Reset() { ax = GetComponentInChildren<SpriteRenderer>(); }
        void OnEnable()
        {
            if (!ax) Reset();
            Instances.Add(this);
            composite = CreateMaterial("AXComposite");
            silhouette = CreateMaterial("AXSilhouette");
            spriteMesh = new Mesh { name = "AX visibility pose", hideFlags = HideFlags.HideAndDontSave };
        }

        static Material CreateMaterial(string name)
        {
            var shader = Resources.Load<Shader>("MathraAX/" + name);
            return shader ? new Material(shader) { hideFlags = HideFlags.HideAndDontSave } : null;
        }

        void OnDisable()
        {
            Instances.Remove(this);
            if (capture) Destroy(capture.gameObject);
            Free(ref newView);
            Destroy(composite); Destroy(silhouette); Destroy(spriteMesh);
            cachedSprite = null;
            nextRefresh=0;
            ready = false;
        }
        static void Free(ref RenderTexture rt)
        {
            if (rt) { rt.Release(); Destroy(rt); rt = null; }
        }
        void OnValidate() { if ((int)mode == 0) mode = RevealMode.Forcefield; nextRefresh=0; }
        void LateUpdate()
        {
            ready = false;
            if ((int)mode == 0) mode=RevealMode.Forcefield;
            if (lastMode != mode) { nextRefresh=0; lastMode = mode; Free(ref newView); }
            var cam = Source;
            if (!cam || !cam.isActiveAndEnabled || !ax || !ax.enabled || !ax.gameObject.activeInHierarchy ||
                !ax.sprite || !composite || !silhouette || (cam.cullingMask & (1 << ax.gameObject.layer)) == 0) return;
            var bounds = ax.bounds;
            float distance=Vector3.Distance(cam.transform.position,bounds.center);
            if(distance<minimumCameraDistance || (maximumCameraDistance>0 && distance>maximumCameraDistance))
            {
                nextRefresh=0;
                return;
            }
            distanceWeight=1;
            if(distanceFadeWidth>0)
            {
                if(minimumCameraDistance>0) distanceWeight=Mathf.SmoothStep(0,1,(distance-minimumCameraDistance)/distanceFadeWidth);
                if(maximumCameraDistance>0) distanceWeight=Mathf.Min(distanceWeight,Mathf.SmoothStep(0,1,(maximumCameraDistance-distance)/distanceFadeWidth));
            }
            if(distanceWeight<=0) { nextRefresh=0; return; }
            var center = cam.WorldToViewportPoint(bounds.center);
            float minX = float.PositiveInfinity, minY = minX, minZ = minX;
            float maxX = float.NegativeInfinity, maxY = maxX;
            for (int i = 0; i < 8; i++)
            {
                corners[i] = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var p = cam.WorldToViewportPoint(corners[i]);
                minX=Mathf.Min(minX,p.x); minY=Mathf.Min(minY,p.y); minZ=Mathf.Min(minZ,p.z);
                maxX=Mathf.Max(maxX,p.x); maxY=Mathf.Max(maxY,p.y);
            }
            if (center.z <= cam.nearClipPlane || minX>1 || maxX<0 || minY>1 || maxY<0) return;
            float height = Mathf.Max(.0001f,maxY-minY);
            composite.SetVector("_Field", new Vector4(center.x, center.y + height*verticalOffset,
                height*radiusX*cam.pixelHeight/Mathf.Max(1,cam.pixelWidth),height*radiusY));
            composite.SetVector("_Style", new Vector4(feather,fieldStrength*distanceWeight,0,0));

            if (mode == RevealMode.Forcefield && fieldStrength <= 0) return;
            if (mode == RevealMode.CombatSilhouette || (mode == RevealMode.Forcefield && UseAssist)) UpdatePose();
            if (mode == RevealMode.CombatSilhouette) { ready = true; return; }
            Vector4 field=composite.GetVector("_Field");
            float left=Mathf.Clamp01(field.x-field.z), bottom=Mathf.Clamp01(field.y-field.w);
            float right=Mathf.Clamp01(field.x+field.z), top=Mathf.Clamp01(field.y+field.w);
            crop=Rect.MinMaxRect(left,bottom,right,top);
            if(crop.width<.00001f || crop.height<.00001f) return;
            float cutDepth=minZ-depthPadding-keepNearAX;
            if(keepNearAX>0 && cutDepth<=cam.nearClipPlane) { nextRefresh=0; return; }
            if(requireBlockerCollider && !HasBlocker(cam,center,minX,maxX,minY,maxY,keepNearAX>0?cutDepth:center.z)) { nextRefresh=0; return; }
            bool refresh=fullRate || !newView || Time.unscaledTime>=nextRefresh || lastFrame!=ax.sprite ||
                lastCrop!=crop || lastCamera!=cam.worldToCameraMatrix || lastProjection!=cam.projectionMatrix ||
                lastPosition!=ax.transform.position;
            if(refresh)
            {
                PrepareCamera(cam);
                capture.nearClipPlane=Mathf.Clamp(cutDepth,cam.nearClipPlane,cam.farClipPlane-.001f);
                capture.ResetProjectionMatrix();
                var proj=cam.projectionMatrix;
                var depth=capture.projectionMatrix;
                proj[2,2]=depth[2,2]; proj[2,3]=depth[2,3];
                var zoom=Matrix4x4.identity;
                zoom.m00=1/crop.width; zoom.m11=1/crop.height;
                zoom.m03=(1-2*crop.center.x)/crop.width;
                zoom.m13=(1-2*crop.center.y)/crop.height;
                capture.projectionMatrix=zoom*proj;
                capture.ResetCullingMatrix();
                if(!RenderView(ref newView,cam)) return;
                lastFrame=ax.sprite; lastCrop=crop; lastCamera=cam.worldToCameraMatrix;
                lastProjection=cam.projectionMatrix; lastPosition=ax.transform.position;
                nextRefresh=Time.unscaledTime+1f/Mathf.Max(1,idleCaptureHz);
            }
            composite.SetTexture("_Current",newView);
            composite.SetVector("_Crop",new Vector4(left,bottom,crop.width,crop.height));
            ready=true;
        }
        bool HasBlocker(Camera cam,Vector3 center,float minX,float maxX,float minY,float maxY,float endDepth)
        {
            for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
            {
                var ray=cam.ViewportPointToRay(new Vector3(center.x+x*.45f*(maxX-minX),center.y+y*.45f*(maxY-minY),0));
                float distance=(endDepth-cam.nearClipPlane)/Mathf.Max(.001f,Vector3.Dot(ray.direction,cam.transform.forward));
                if(Physics.Raycast(ray,distance,blockerLayers,QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }
        void PrepareCamera(Camera cam)
        {
            if (!capture)
            {
                var go=new GameObject("AX secondary view"){hideFlags=HideFlags.HideAndDontSave};
                capture=go.AddComponent<Camera>(); capture.enabled=false;
            }
            capture.CopyFrom(cam); capture.enabled=false; capture.targetTexture=null;
            capture.rect=new Rect(0,0,1,1); capture.aspect=cam.aspect;
            capture.transform.SetPositionAndRotation(cam.transform.position,cam.transform.rotation);
            capture.worldToCameraMatrix=cam.worldToCameraMatrix;
            capture.useOcclusionCulling=false;
            var data=capture.GetUniversalAdditionalCameraData();
            data.SetRenderer(rendererIndex); data.renderType=CameraRenderType.Base;
            var source=cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=false; data.renderShadows=captureShadows && source.renderShadows;
            data.volumeLayerMask=source.volumeLayerMask; data.volumeTrigger=source.volumeTrigger;
            data.requiresColorOption=source.requiresColorOption; data.requiresDepthOption=source.requiresDepthOption;
        }
        bool RenderView(ref RenderTexture rt, Camera main)
        {
            float w=main.scaledPixelWidth*crop.width, h=main.scaledPixelHeight*crop.height;
            float scale=Mathf.Min(resolution,captureLimit/Mathf.Max(1,Mathf.Max(w,h)));
            // Quantize to avoid reallocations for small perspective changes.
            int width=Mathf.Max(16,Mathf.CeilToInt(w*scale/16)*16);
            int height=Mathf.Max(16,Mathf.CeilToInt(h*scale/16)*16);
            var format=main.allowHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default;
            if (!rt || rt.width!=width || rt.height!=height)
            {
                Free(ref rt);
                rt=new RenderTexture(width,height,24,format){name="AX visibility capture",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point};
                rt.Create();
            }
            request.destination=rt;
            if (!RenderPipeline.SupportsRenderRequest(capture,request))
            {
                if (!warned) Debug.LogWarning("AX Visibility requires a URP renderer supporting SingleCameraRequest.",this);
                warned=true; return false;
            }
            RenderPipeline.SubmitRenderRequest(capture,request);
            return true;
        }

        void UpdatePose()
        {
            var s=ax.sprite;
            if (cachedSprite!=s)
            {
                cachedSprite=s; spriteMesh.Clear();
                var verts=s.vertices; var points=new Vector3[verts.Length];
                for(int i=0;i<verts.Length;i++) points[i]=verts[i];
                var tris=s.triangles; var indices=new int[tris.Length];
                for(int i=0;i<tris.Length;i++) indices[i]=tris[i];
                spriteMesh.vertices=points; spriteMesh.uv=s.uv; spriteMesh.triangles=indices;
            }
            spriteMatrix=ax.localToWorldMatrix*Matrix4x4.Scale(new Vector3(ax.flipX?-1:1,ax.flipY?-1:1,1));
            bool assist=mode==RevealMode.Forcefield;
            silhouette.SetTexture("_MainTex",s.texture);
            silhouette.SetColor("_Tint",assist?Color.white:silhouetteColor);
            silhouette.SetColor("_ActorColor",ax.color);
            silhouette.SetVector("_Look",new Vector4((assist?fieldSpriteAssist*fieldStrength:silhouetteOpacity)*distanceWeight,assist?1:spriteDetail,alphaCutoff,0));
            silhouette.SetFloat("_DepthOnly",!assist && depthOnlySilhouette?1:0);
        }
    }
}
