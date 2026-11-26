using UnityEngine;

namespace Mathra.ShorelineStandalone
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ShorelineSwimFoam : MonoBehaviour
    {
        [Header("Walking and Swimming")]
        public bool enableWalkWake = true;
        public bool enableSwimWake = true;
        [UnityEngine.Serialization.FormerlySerializedAs("partRadius")]
        [Range(0, 3)] public float walkContactRadius = .45f;
        [Range(0, 3)] public float swimContactRadius = .28f;
        [Tooltip("Optional separate renderer enabled only during swimming. Overrides automatic depth/name detection.")]
        public SpriteRenderer jynSwimmingRenderer;
        public SpriteRenderer axSwimmingRenderer;
        [Tooltip("With no swim renderer assigned, switch to swimming when the tracked pivot is this far below the foam.")]
        [Min(0)] public float swimDepth = .22f;
        public Transform jyn;
        public Transform ax;
        [Tooltip("Optional. A child Transform at Jyn’s water contact point takes priority over automatic sprite alignment.")]
        public Transform jynWakeAnchor;
        public Transform axWakeAnchor;
        [Header("Waterline")]
        [Min(1)] public float squareSize = 50;
        [Range(64, 512)] public int shoreResolution = 256;
        public LayerMask shoreLayers = ~(1 << 4);
        [Tooltip("Height of the horizontal collision scan above the water surface.")]
        public float scanLift = .045f;
        [Min(.001f)] public float surfaceLift = .035f;
        [Header("Filthy rafts")]
        [Range(.15f, 6)] public float shoreWidth = 1.5f;
        [Range(0, 1)] public float coverage = .66f;
        public Color sludge = new Color(.30f,.23f,.105f);
        public Color froth = new Color(.79f,.69f,.40f);
        [Range(.2f, 4)] public float bubbleScale = 1;
        [Range(0, .4f)] public float rockAmount = .09f;
        [Range(0, 3)] public float rockSpeed = .65f;
        [Range(0, 1)] public float opacity = .92f;
        [Range(2501, 4999)] public int renderQueue = 3050;
        [Header("Characters")]
        [Tooltip("Unassigned slots look for active Ax/Jyn names, then Player tags.")]
        public bool autoFind = true;
        [Range(.2f, 10)] public float reformSeconds = 2.8f;
        [Tooltip("Distance above the water where a character can still part the foam.")]
        [Min(0)] public float verticalReach = 1.4f;
        [Min(0)] public float submergedReach = 5f;
        [Tooltip("Anchor the wake to the visible body in the gameplay camera.")]
        public bool followSwimmingSprite = true;
        [Range(0, 1)] public float spriteWaterline = .20f;
        public Camera gameplayCamera;
        readonly SpriteRenderer[] bodies = new SpriteRenderer[2];
        Camera view;
        [Header("Foam Assets")]
        [SerializeField] Material foamMaterial;
        [SerializeField, HideInInspector] Shader foamShader;
        [SerializeField] Texture2D surfaceDetail;

        Material mat;
        Mesh mesh;
        Texture2D shore, wake;
        byte[] wakePixels;
        const int WakeSize = 384;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Transform[] tracked = new Transform[2];
        readonly Vector2[] last = new Vector2[2];
        readonly bool[] hadLast = new bool[2];
        readonly bool[] lastSwimming = new bool[2];
        float nextFind, lastTick;
        Matrix4x4 oldMatrix;
        float oldSize, oldScan, oldLift;
        int oldLayers, oldResolution;
        bool dirty = true;

        void OnEnable() { dirty = true; nextFind = 0; lastTick = -1; }

        [ContextMenu("Rebuild Shoreline")]
        public void RebuildShoreline() { dirty = true; }

        void Update()
        {
            if (!gameObject.scene.IsValid() || !foamShader || !surfaceDetail) return;
            if (!mat)
            {
                mat = foamMaterial ? new Material(foamMaterial) : new Material(foamShader);
                mat.hideFlags = HideFlags.HideAndDontSave;
                GetComponent<MeshRenderer>().sharedMaterial = mat;
                mesh = new Mesh { name = "Waterline square", hideFlags = HideFlags.HideAndDontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
                wake = MakeMap(WakeSize);
                wakePixels = new byte[WakeSize * WakeSize];
                UploadWake();
                mat.SetTexture("_Wake", wake);
                mat.SetTexture("_Detail", surfaceDetail);
                dirty = true;
            }
            if (dirty || oldMatrix != transform.localToWorldMatrix || oldSize != squareSize ||
                oldScan != scanLift || oldLayers != shoreLayers.value || oldResolution != shoreResolution || oldLift != surfaceLift)
            {
                Bake();
                dirty = false;
                oldMatrix = transform.localToWorldMatrix; oldSize = squareSize;
                oldScan = scanLift; oldLayers = shoreLayers.value; oldResolution = shoreResolution; oldLift = surfaceLift;
            }
            mat.SetColor("_Sludge", sludge); mat.SetColor("_Froth", froth);
            mat.SetVector("_Shape", new Vector4(shoreWidth, coverage, bubbleScale, opacity));
            mat.SetVector("_Motion", new Vector4(rockAmount, rockSpeed, squareSize, swimContactRadius));
            mat.renderQueue = renderQueue;
            if (Application.IsPlaying(gameObject) && Time.time - lastTick >= .05f) TickWake();
        }

        static Texture2D MakeMap(int n) => new Texture2D(n,n,TextureFormat.R8,false,true)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };

        void Bake()
        {
            float size = Mathf.Max(1, squareSize), h = size * .5f;
            mesh.vertices = new[] { new Vector3(-h,surfaceLift,-h), new Vector3(-h,surfaceLift,h),
                                     new Vector3(h,surfaceLift,h), new Vector3(h,surfaceLift,-h) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] {0,1,2,0,2,3};
            mesh.RecalculateBounds();
            int n = Mathf.Clamp(shoreResolution,64,512);
            if (!shore || shore.width != n) { Release(shore); shore = MakeMap(n); }
            float step = size / n;
            var d = new float[n*n];
            for (int i=0; i<d.Length; i++) d[i] = 100000;
            Physics.SyncTransforms();
            // Short opposing sweeps catch both faces of obstacles, including separate pieces of one mesh.
            for (int row=0; row<n; row++)
            {
                float side = -h + (row+.5f)*step;
                for (int k=0; k<n; k+=16)
                {
                    float start = -h + k*step - step*.25f;
                    float span = (Mathf.Min(16,n-k)+.5f)*step;
                    Sweep(new Vector3(start,scanLift,side), Vector3.right, span, n, size, d);
                    Sweep(new Vector3(start+span,scanLift,side), Vector3.left, span, n, size, d);
                    Sweep(new Vector3(side,scanLift,start), Vector3.forward, span, n, size, d);
                    Sweep(new Vector3(side,scanLift,start+span), Vector3.back, span, n, size, d);
                }
            }
            for (int i=0; i<d.Length; i++) Relax(d,n,i,1);
            for (int i=d.Length-1; i>=0; i--) Relax(d,n,i,-1);
            var bytes = new byte[d.Length];
            for (int i=0; i<d.Length; i++) bytes[i]=(byte)Mathf.RoundToInt(Mathf.Clamp01(d[i]*step/12f)*255);
            shore.SetPixelData(bytes,0); shore.Apply(false,false); mat.SetTexture("_Shore",shore);
            System.Array.Clear(wakePixels,0,wakePixels.Length); UploadWake();
            hadLast[0]=hadLast[1]=false;
        }

        void Sweep(Vector3 local, Vector3 dir, float length, int n, float size, float[] d)
        {
            Vector3 delta = transform.TransformVector(dir*length);
            int count = Physics.RaycastNonAlloc(transform.TransformPoint(local),delta.normalized,hits,
                delta.magnitude,shoreLayers,QueryTriggerInteraction.Ignore);
            for (int i=0;i<count;i++)
            {
                Collider c=hits[i].collider;
                if (c is CharacterController || c.attachedRigidbody && !c.attachedRigidbody.isKinematic) continue;
                Vector3 p=transform.InverseTransformPoint(hits[i].point);
                int x=Mathf.Clamp((int)((p.x/size+.5f)*n),0,n-1);
                int z=Mathf.Clamp((int)((p.z/size+.5f)*n),0,n-1);
                d[z*n+x]=0;
            }
        }

        static void Relax(float[] d,int n,int i,int sign)
        {
            int x=i%n, prev=i-sign*n;
            float v=d[i];
            if (x-sign>=0 && x-sign<n) v=Mathf.Min(v,d[i-sign]+1);
            if (prev>=0 && prev<d.Length)
            {
                v=Mathf.Min(v,d[prev]+1);
                if(x>0) v=Mathf.Min(v,d[prev-1]+1.4142f);
                if(x+1<n) v=Mathf.Min(v,d[prev+1]+1.4142f);
            }
            d[i]=v;
        }

        void ResolveActors()
        {
            bool ready=true;
            for(int i=0;i<2;i++)
            {
                var assigned=i==0 ? ax : jyn;
                var body=bodies[i];
                if(!tracked[i] || (assigned && assigned!=tracked[i]) || !body ||
                   !body.enabled || !body.gameObject.activeInHierarchy || !body.sprite) ready=false;
            }
            if(ready && view && (gameplayCamera==null || gameplayCamera==view)) return;
            Transform[] manual = {ax, jyn};
            for(int slot=0;slot<manual.Length;slot++)
            {
                Transform assigned=manual[slot];
                if(!assigned || assigned.gameObject.activeInHierarchy) continue;
                Transform parent=assigned.parent;
                while(parent && !parent.gameObject.activeInHierarchy) parent=parent.parent;
                manual[slot]=parent;
            }
            string[] names = {"Ax","Jyn"};
            var sprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            view = gameplayCamera ? gameplayCamera : Camera.main;
            for(int slot=0;slot<2;slot++)
            {
                Transform root=manual[slot];
                SpriteRenderer best=null;
                float largest=-1;
                foreach(var sprite in sprites)
                {
                    if(!sprite.enabled || !sprite.sprite) continue;
                    Transform owner=sprite.transform;
                    if(manual[slot])
                    {
                        if(!owner.IsChildOf(manual[slot])) continue;
                    }
                    else
                    {
                        if(!autoFind) continue;
                        while(owner && !MatchesHero(owner.name,names[slot])) owner=owner.parent;
                        if(!owner && !MatchesHero(sprite.sprite.name,names[slot])) continue;
                        if(!owner) owner=sprite.transform;
                    }
                    float area=sprite.bounds.size.sqrMagnitude;
                    if(area<=largest) continue;
                    largest=area;
                    best=sprite;
                    root=manual[slot] ? manual[slot] : owner;
                }
                if(!root && autoFind) root=GameObject.Find(names[slot])?.transform;
                if(tracked[slot]!=root || bodies[slot]!=best) hadLast[slot]=false;
                tracked[slot]=root;
                bodies[slot]=best;
            }
            if(autoFind && (!tracked[0] || !tracked[1]))
            {
                foreach(var go in GameObject.FindGameObjectsWithTag("Player"))
                {
                    Transform candidate=go.transform;
                    if((tracked[0] && (candidate.IsChildOf(tracked[0]) || tracked[0].IsChildOf(candidate))) ||
                       (tracked[1] && (candidate.IsChildOf(tracked[1]) || tracked[1].IsChildOf(candidate)))) continue;
                    if(!tracked[0]) tracked[0]=candidate;
                    else if(!tracked[1]) tracked[1]=candidate;
                }
            }
        }

        static bool MatchesHero(string label,string hero)
        {
            // Handles Jyn(Clone), JynSwim, Player_Jyn and AX (PlayerPrefab).
            int at=label.IndexOf(hero,System.StringComparison.OrdinalIgnoreCase);
            if(at<0) return false;
            int end=at+hero.Length;
            return (at==0 || !char.IsLetterOrDigit(label[at-1]) || char.IsUpper(label[at]))
                && (end==label.Length || !char.IsLetter(label[end]) || char.IsUpper(label[end]));
        }

        static bool Visible(SpriteRenderer body)
        {
            return body && body.enabled && body.gameObject.activeInHierarchy && body.sprite;
        }

        bool TryWaterPoint(int slot, out Vector2 point, out float radius, out bool swimming)
        {
            point=default; radius=0; swimming=false;
            Transform actor=tracked[slot];
            SpriteRenderer swimBody=slot==0 ? axSwimmingRenderer : jynSwimmingRenderer;
            if(!actor || !actor.gameObject.activeInHierarchy)
            {
                if(!Visible(swimBody)) return false;
                actor=swimBody.transform;
            }
            Vector3 local=transform.InverseTransformPoint(actor.position);
            SpriteRenderer body=bodies[slot];
            swimming=swimBody ? Visible(swimBody) :
                local.y-surfaceLift < -swimDepth ||
                (Visible(body) && (body.sprite.name.IndexOf("swim", System.StringComparison.OrdinalIgnoreCase)>=0 ||
                                   body.name.IndexOf("swim", System.StringComparison.OrdinalIgnoreCase)>=0));
            if(swimming && swimBody) body=swimBody;
            radius=swimming ? swimContactRadius : walkContactRadius;
            if(radius<=0 || !(swimming ? enableSwimWake : enableWalkWake)) return false;

            Transform anchor=slot==0 ? axWakeAnchor : jynWakeAnchor;
            if(swimming && anchor && anchor.gameObject.activeInHierarchy)
                local=transform.InverseTransformPoint(anchor.position);
            else if(Visible(body) && (swimming ? followSwimmingSprite : actor==body.transform))
            {
                Bounds bounds=body.bounds;
                Vector3 contact=bounds.center;
                contact.y=swimming ? Mathf.Lerp(bounds.min.y,bounds.max.y,spriteWaterline) : bounds.min.y;
                Vector3 visual=transform.InverseTransformPoint(contact);
                if((visual.y>verticalReach && local.y>verticalReach) ||
                   (visual.y < -submergedReach && local.y < -submergedReach)) return false;
                // Project the visible contact onto the water only for swimming.
                local=visual;
                if(swimming && view)
                {
                    Ray ray=view.ViewportPointToRay(view.WorldToViewportPoint(contact));
                    Plane plane=new Plane(transform.up,transform.TransformPoint(Vector3.up*surfaceLift));
                    if(plane.Raycast(ray,out float hit)) local=transform.InverseTransformPoint(ray.GetPoint(hit));
                }
            }
            if(local.y>verticalReach || local.y < -submergedReach) return false;
            point=new Vector2(local.x,local.z);
            return Mathf.Max(Mathf.Abs(point.x),Mathf.Abs(point.y))<=squareSize*.5f+radius;
        }

        void TickWake()
        {
            float now=Time.time;
            if ((enableSwimWake || enableWalkWake) && now>=nextFind) { ResolveActors(); nextFind=now+1.5f; }
            int decay=Mathf.Max(1,Mathf.RoundToInt(255*Mathf.Min(1,(now-lastTick)/reformSeconds)));
            lastTick=now;
            for(int i=0;i<wakePixels.Length;i++) wakePixels[i]=(byte)Mathf.Max(0,wakePixels[i]-decay);
            for(int i=0;i<tracked.Length;i++)
            {
                if(!TryWaterPoint(i,out Vector2 p,out float radius,out bool swimming)) { hadLast[i]=false; continue; }
                Vector2 a=hadLast[i] && lastSwimming[i]==swimming && Vector2.Distance(p,last[i])<radius*4 ? last[i] : p;
                Stamp(a,p,radius); lastSwimming[i]=swimming; hadLast[i]=true; last[i]=p;
            }
            UploadWake();
        }

        void Stamp(Vector2 a,Vector2 b,float radius)
        {
            float s=WakeSize/Mathf.Max(1,squareSize), r=radius*s;
            Vector2 origin=Vector2.one*(WakeSize*.5f-.5f);
            a=a*s+origin; b=b*s+origin;
            Vector2 ab=b-a;
            float len=Mathf.Max(.0001f,ab.sqrMagnitude);
            int left=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)-r));
            int right=Mathf.Min(WakeSize-1,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+r));
            int low=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)-r));
            int high=Mathf.Min(WakeSize-1,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+r));
            for(int y=low;y<=high;y++) for(int x=left;x<=right;x++)
            {
                Vector2 offset=new Vector2(x,y)-a;
                float distance=(offset-ab*Mathf.Clamp01(Vector2.Dot(offset,ab)/len)).magnitude;
                int value=Mathf.RoundToInt(255*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/r))));
                int idx=y*WakeSize+x;
                if(value>wakePixels[idx]) wakePixels[idx]=(byte)value;
            }
        }

        void UploadWake() { wake.SetPixelData(wakePixels,0); wake.Apply(false,false); }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix=transform.localToWorldMatrix; Gizmos.color=new Color(.9f,.72f,.2f);
            if(Application.IsPlaying(gameObject) && (enableSwimWake || enableWalkWake))
            {
                for(int slot=0;slot<2;slot++)
                    if(TryWaterPoint(slot,out Vector2 p,out float radius,out _))
                    {
                        Gizmos.color=slot==1 ? Color.cyan : Color.yellow;
                        Vector3 center=new Vector3(p.x,surfaceLift,p.y);
                        for(int k=0;k<32;k++)
                        {
                            float a=k*Mathf.PI/16, b=(k+1)*Mathf.PI/16;
                            Gizmos.DrawLine(center+radius*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),
                                center+radius*new Vector3(Mathf.Cos(b),0,Mathf.Sin(b)));
                        }
                    }
            }
            Gizmos.color=new Color(.9f,.72f,.2f);
            Gizmos.DrawWireCube(Vector3.up*surfaceLift,new Vector3(squareSize,.015f,squareSize));
        }
        static void Release(Object item)
        {
            if (!item) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
        void OnDisable()
        {
            if(mesh && GetComponent<MeshFilter>().sharedMesh==mesh) GetComponent<MeshFilter>().sharedMesh=null;
            if(mat && GetComponent<MeshRenderer>().sharedMaterial==mat) GetComponent<MeshRenderer>().sharedMaterial=foamMaterial;
            Release(mat); Release(mesh); Release(shore); Release(wake);
        }
    }
}
