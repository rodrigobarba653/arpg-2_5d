using UnityEngine;
using UnityEngine.Rendering;

namespace Mathra.Water
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WastewaterBubbles : MonoBehaviour
    {
        [Tooltip("Footprint in local X and Z units. Keep the prefab at unit scale for metres.")]
        public Vector2 poolSize = new Vector2(50,50);
        [Tooltip("Number of independently timed bubble sites, including resting sites.")]
        [Range(1,512)] public int sites = 96;
        [Tooltip("Smallest and largest fully inflated dome diameters, in local units.")]
        public Vector2 diameter = new Vector2(.24f,1.1f);
        [Range(0,3)] public float animationSpeed = 1;
        [Range(0,15)] public float restSeconds = 3.8f;
        [Range(12,64)] public int pixelsPerBubble = 40;
        [Range(0,1)] public float opacity = .88f;
        public float surfaceOffset = .04f;
        public Color membrane = new Color(.36f,.44f,.19f);
        public Color oilyHighlight = new Color(.82f,.78f,.50f);
        public int seed = 27;
        [SerializeField] Material bubbleMaterial;
        Mesh mesh;
        MaterialPropertyBlock properties;
        bool dirty;
        void OnEnable() { dirty=true; }
        void OnValidate() { dirty=true; }
        void Update()
        {
            if (!dirty) return;
            dirty=false;
            Rebuild();
        }
        void Rebuild()
        {
            var filter=GetComponent<MeshFilter>();
            var draw=GetComponent<MeshRenderer>();
            if(!mesh)
                mesh=new Mesh { name="Wastewater bubble sites", hideFlags=HideFlags.DontSave };
            mesh.Clear();
            int count=Mathf.Clamp(sites,1,512);
            var verts=new Vector3[count*4];
            var uv=new Vector2[count*4];
            var keys=new Vector2[count*4];
            var indices=new int[count*6];
            var random=new System.Random(seed);
            for(int i=0;i<count;i++)
            {
                int k=i*4, t=i*6;
                var key=new Vector2((float)random.NextDouble()*120,(float)random.NextDouble()*120);
                for(int corner=0;corner<4;corner++)
                {
                    var u=new Vector2(corner%2,corner/2);
                    uv[k+corner]=u; keys[k+corner]=key;
                    verts[k+corner]=new Vector3(u.x-.5f,0,u.y-.5f);
                }
                indices[t]=k; indices[t+1]=k+2; indices[t+2]=k+1;
                indices[t+3]=k+1; indices[t+4]=k+2; indices[t+5]=k+3;
            }
            mesh.vertices=verts; mesh.uv=uv; mesh.uv2=keys; mesh.triangles=indices;
            var area=new Vector2(Mathf.Max(.01f,poolSize.x),Mathf.Max(.01f,poolSize.y));
            mesh.bounds=new Bounds(new Vector3(0,surfaceOffset,0),new Vector3(area.x,.2f,area.y));
            filter.sharedMesh=mesh;
            draw.sharedMaterial=bubbleMaterial;
            draw.shadowCastingMode=ShadowCastingMode.Off;
            draw.receiveShadows=false;
            properties ??= new MaterialPropertyBlock();
            properties.SetVector("_AreaSize",new Vector4(area.x,area.y,0,0));
            float min=Mathf.Max(.01f,Mathf.Min(diameter.x,diameter.y));
            properties.SetVector("_Diameter",new Vector4(min,Mathf.Max(min,Mathf.Max(diameter.x,diameter.y)),0,0));
            properties.SetFloat("_Speed",animationSpeed);
            properties.SetFloat("_Idle",restSeconds);
            properties.SetFloat("_Height",surfaceOffset);
            properties.SetFloat("_Pixels",pixelsPerBubble);
            properties.SetFloat("_Opacity",opacity);
            properties.SetColor("_Membrane",membrane);
            properties.SetColor("_Glint",oilyHighlight);
            draw.SetPropertyBlock(properties);
        }
        void OnDisable()
        {
            if(!mesh) return;
            var filter=GetComponent<MeshFilter>();
            if(filter && filter.sharedMesh==mesh) filter.sharedMesh=null;
            if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh=null;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=new Color(.67f,.76f,.32f);
            Gizmos.DrawWireCube(Vector3.up*surfaceOffset,new Vector3(poolSize.x,.02f,poolSize.y));
        }
    }
}
