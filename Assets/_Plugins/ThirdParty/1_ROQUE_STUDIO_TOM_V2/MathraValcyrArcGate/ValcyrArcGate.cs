using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mathra.Valcyr
{
    [DisallowMultipleComponent]
    public sealed class ValcyrArcGate : MonoBehaviour
    {
        [Header("Aperture in local XY")]
        [Min(.05f)] public float horizontalRadius = 2.3f;
        [Min(.05f)] public float verticalRadius = 2.3f;
        [Range(0,360)] public float rimRotation = 90;
        [Range(0,1)] public float depth = .23f;
        [Header("Loop in seconds")]
        [Min(.05f)] public float charge = 3, discharge = 4, blackout = 3;
        [Header("Plasma")]
        [Range(.006f,.12f)] public float filamentRadius = .027f;
        [Range(0,.5f)] public float turbulence = .16f;
        [Range(1,30)] public float restrikesPerSecond = 11;
        [Range(0,18)] public int crossLinks = 12;
        [ColorUsage(false,true)] public Color plasma = new Color(.02f,.31f,1f);
        [Min(0)] public float emission = 3.4f;
        public int seed = 37;
        [Header("Residual particles during blackout")]
        [Range(0,96)] public int residualCount = 42;
        [Range(.1f,3)] public float residualSize = 1;
        [Range(0,2)] public float residualDrift = 1;
        [Header("Hallway spill and spatial audio")]
        [Min(0)] public float lightIntensity = 6;
        [Min(.1f)] public float lightRange = 7;
        [Range(0,1)] public float flicker = .7f;
        [Range(0,1)] public float buzzVolume = .34f;
        [SerializeField] Material plasmaMaterial;
        [SerializeField] AudioClip buzz;
        const int Contacts = 9, Steps = 25, Sides = 6;
        readonly Vector3[,] channels = new Vector3[Contacts,Steps];
        readonly Vector3[] path = new Vector3[Steps];
        readonly List<Vector3> vertices = new List<Vector3>(42000), normals = new List<Vector3>(42000);
        readonly List<Color> colors = new List<Color>(42000);
        readonly List<int> triangles = new List<int>(205000);
        Mesh mesh;
        MeshRenderer skin;
        Light centerLight, contactLight;
        AudioSource sound;
        struct Residue
        {
            public Vector3 origin, velocity;
            public float phase, size;
        }
        readonly Residue[] residue = new Residue[96];
        System.Random residualRandom;
        bool hadGrid;
        int visibleResidue;
        float clock;

        void Awake()
        {
            residualRandom = new System.Random(seed);
            mesh = new Mesh { name = "Arc channels" };
            mesh.MarkDynamic();
            var surface = Child("Plasma channels");
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            skin = surface.AddComponent<MeshRenderer>();
            skin.sharedMaterial = plasmaMaterial;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.receiveShadows = false;
            centerLight = Lamp("Core spill");
            contactLight = Lamp("Rim spill");
            sound = Child("Capacitor buzz").AddComponent<AudioSource>();
            sound.clip = buzz;
            sound.loop = true;
            sound.playOnAwake = false;
            sound.spatialBlend = 1;
            sound.dopplerLevel = 0;
            sound.volume = 0;
        }
        GameObject Child(string label)
        {
            var go = new GameObject(label);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform,false);
            return go;
        }
        Light Lamp(string label)
        {
            var lamp = Child(label).AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.shadows = LightShadows.None;
            return lamp;
        }
        void OnEnable() { clock = 0; hadGrid = false; visibleResidue = 0; }
        void OnDisable()
        {
            if (!skin) return;
            skin.enabled = centerLight.enabled = contactLight.enabled = false;
            sound.Stop();
        }
        void OnDestroy() { if (mesh) Destroy(mesh); }
        Vector3 Rim(int i)
        {
            float a = (rimRotation + 360f*i/Contacts)*Mathf.Deg2Rad;
            return new Vector3(horizontalRadius*Mathf.Cos(a), verticalRadius*Mathf.Sin(a),0);
        }
        float Noise(float x,float y) => 2*Mathf.PerlinNoise(x + (seed % 4096)*.139f + 700, y+280)-1;
        void Update()
        {
            float c = Mathf.Max(.05f,charge), d = Mathf.Max(.05f,discharge);
            float t = clock;
            clock = Mathf.Repeat(clock + Time.deltaTime,c+d+Mathf.Max(.05f,blackout));
            bool live = t >= c, active = t < c+d;
            skin.enabled = centerLight.enabled = active;
            contactLight.enabled = active && live;
            if (!active)
            {
                if (sound.isPlaying) sound.Stop();
                if (hadGrid) ReleaseResidue();
                hadGrid = false;
                DrawResidue(t-c-d);
                return;
            }
            visibleResidue = 0;
            hadGrid = live;
            float ramp = live ? 1 : Mathf.Clamp01(t/c);
            float tick = t*restrikesPerSecond;
            float spike = Mathf.Clamp01(.52f + Noise(t*5.9f,2)*.95f + .33f*Mathf.Cos(t*59));
            float surge = Mathf.Lerp(1,.26f+1.4f*spike,flicker);
            float power = (.10f+.90f*ramp*ramp)*surge;
            Vector3 core = new Vector3(Noise(t*2,4),Noise(t*2,8),Noise(t*2,12))*.07f*ramp;
            vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear();
            float r = filamentRadius*(1+ramp*1.7f);
            // Interlocking, rapidly shifting arcs keep the seed compact as it charges.
            for (int j=0;j<4;j++)
            {
                Quaternion tilt = Quaternion.Euler(35*j + t*45,57*j+t*64,j*31);
                for (int k=0;k<Steps;k++)
                {
                    float a = 2*Mathf.PI*k/(Steps-1);
                    var p = new Vector3(Mathf.Cos(a),Mathf.Sin(a),.19f*Mathf.Sin(3*a+t*8));
                    path[k] = core + tilt*p*(r*(.95f+.24f*Mathf.Sin(3*a+j+t*16)));
                }
                Tube(Steps,filamentRadius*(.32f+.42f*ramp),power+.22f);
            }
            if (live) Grid(t,tick,core,surge,Mathf.Clamp01((t-c)/.17f));
            UploadMesh();
            Illuminate(centerLight,core,power);
            int hot = Mathf.FloorToInt(t*5)%Contacts;
            Illuminate(contactLight,Rim(hot)*.83f,.46f*surge);
            if (buzz && buzzVolume > 0 && !sound.isPlaying) sound.Play();
            sound.volume = buzzVolume*Mathf.Clamp01(power);
            sound.pitch = live ? 1.14f+.09f*spike : .66f+.41f*ramp;
        }
        void UploadMesh()
        {
            mesh.Clear(false);
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetTriangles(triangles,0,true);
        }

        float Sample() => (float)residualRandom.NextDouble();

        void ReleaseResidue()
        {
            visibleResidue = Mathf.Clamp(residualCount, 0, residue.Length);
            for (int i = 0; i < visibleResidue; i++)
            {
                int lane = i % Contacts;
                int k = residualRandom.Next(0, Steps - 1);
                residue[i] = new Residue
                {
                    origin = Vector3.Lerp(channels[lane,k], channels[lane,k+1], Sample()),
                    velocity = new Vector3(Sample()-.5f, .25f+Sample()*.6f, Sample()-.5f),
                    phase = Sample()*Mathf.PI*2,
                    size = .22f + Sample()*.43f
                };
            }
        }

        void DrawResidue(float age)
        {
            float u = Mathf.Clamp01(age / Mathf.Min(3f, Mathf.Max(.05f, blackout)));
            if (u >= 1 || visibleResidue == 0) return;
            skin.enabled = true;
            vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear();
            float fade = 1f - Mathf.SmoothStep(0f, 1f, u);
            for (int i = 0; i < visibleResidue; i++)
            {
                Residue mote = residue[i];
                float angle = mote.phase + 1.9f*age;
                Vector3 swirl = new Vector3(Mathf.Sin(angle)-Mathf.Sin(mote.phase), 0,
                    Mathf.Cos(angle)-Mathf.Cos(mote.phase))*.035f;
                Vector3 center = mote.origin + residualDrift*(mote.velocity*age*.34f + swirl);
                float radius = filamentRadius*residualSize*mote.size*(1f-.45f*u);
                Vector3 tilt = new Vector3(Mathf.Sin(angle)*.45f,1,Mathf.Cos(angle)*.45f);
                path[0] = center - tilt*radius;
                path[1] = center + tilt*radius;
                Tube(2,radius,fade*(.65f+.12f*Mathf.Sin(mote.phase+age*8)));
            }
            UploadMesh();
        }

        void Illuminate(Light lamp,Vector3 point,float power)
        {
            lamp.transform.localPosition = point;
            lamp.color = plasma;
            lamp.range = lightRange;
            lamp.intensity = lightIntensity*power;
        }
        void Grid(float t,float tick,Vector3 core,float surge,float propagation)
        {
            float size = Mathf.Min(horizontalRadius,verticalRadius);
            for (int i=0;i<Contacts;i++)
            {
                var tip = Rim(i);
                Vector3 side = new Vector3(-tip.y,tip.x,0).normalized;
                for (int k=0;k<Steps;k++)
                {
                    float u = k/(Steps-1f), envelope = Mathf.Sin(Mathf.PI*u);
                    float lane = i*17.4f;
                    float frame = Mathf.Floor(tick);
                    float blend = Mathf.SmoothStep(0,1,Mathf.Clamp01((tick-frame)*3));
                    float prev = Noise(u*13+lane,frame);
                    float next = Noise(u*13+lane,frame+1);
                    float bend = Noise(u*3+lane,t*1.7f)+Mathf.Lerp(prev,next,blend);
                    channels[i,k] = Vector3.Lerp(core,tip,u) + envelope*size*
                        (side*turbulence*bend + Vector3.forward*depth*Noise(u*4+lane,t*2));
                }
                int front = Mathf.CeilToInt(propagation*(Steps-1)*.54f);
                if (propagation < 1)
                {
                    DrawSegment(i,0,front, surge);
                    DrawSegment(i,Steps-1-front,Steps-1,surge);
                }
                else DrawSegment(i,0,Steps-1,surge);
            }
            if (propagation < 1) return;
            // Bridges terminate on actual channels. Stagger their reattachments.
            for (int b=0;b<Mathf.Clamp(crossLinks,0,18);b++)
            {
                int lane = b%Contacts, epoch = Mathf.FloorToInt(t*2.6f+b*.37f);
                float pick = Noise(b*4.21f,epoch);
                int other = (lane+1+(pick>.16f ? 1:0))%Contacts;
                int start = 4+(b*7+epoch)%15, end = 6+(b*11+epoch*3)%15;
                Vector3 a = channels[lane,start], z = channels[other,end];
                for (int k=0;k<13;k++)
                {
                    float u = k/12f, amp = Mathf.Sin(Mathf.PI*u)*size*turbulence*.76f;
                    path[k] = Vector3.Lerp(a,z,u)+amp*new Vector3(
                        Noise(u*9+b,t*9),Noise(u*11+b+29,t*9),Noise(u*5+b+32,t*6));
                }
                Tube(13,filamentRadius*(.28f+.13f*(1+pick)),surge*.82f);
            }
        }
        void DrawSegment(int lane,int first,int last,float power)
        {
            if (last<=first) return;
            for (int k=first;k<=last;k++) path[k-first] = channels[lane,k];
            Tube(last-first+1,filamentRadius*(.88f+.17f*Mathf.Sin(lane*5.3f)),power);
        }
        static readonly Vector2[] circle = MakeCircle();
        static Vector2[] MakeCircle()
        {
            var ring = new Vector2[Sides];
            for (int i=0;i<Sides;i++)
            {
                float a = 2*Mathf.PI*i/Sides;
                ring[i] = new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            }
            return ring;
        }
        void Tube(int count,float radius,float power)
        {
            // Three concentric surfaces give the white filament a cobalt corona.
            for (int layer=0;layer<3;layer++)
            {
                float width = radius*(layer==0 ? .54f : layer==1 ? 1.55f : 3.65f);
                Color tint = layer==0 ? Color.Lerp(plasma,Color.white,.87f) : plasma;
                tint.a = emission*power*(layer==0 ? 1.55f : layer==1 ? .65f : .13f);
                int first = vertices.Count;
                Vector3 oldAxis = Vector3.zero;
                for (int k=0;k<count;k++)
                {
                    Vector3 axis = (path[Mathf.Min(count-1,k+1)]-path[Mathf.Max(0,k-1)]).normalized;
                    if (axis.sqrMagnitude<.1f) axis=Vector3.up;
                    Vector3 side = Vector3.ProjectOnPlane(oldAxis,axis);
                    if (side.sqrMagnitude<.001f)
                        side = Vector3.Cross(axis,Mathf.Abs(axis.z)<.9f ? Vector3.forward : Vector3.up);
                    side.Normalize(); oldAxis=side;
                    Vector3 cross = Vector3.Cross(axis,side);
                    for (int s=0;s<Sides;s++)
                    {
                        Vector3 n = side*circle[s].x+cross*circle[s].y;
                        vertices.Add(path[k]+n*width); normals.Add(n); colors.Add(tint);
                        if (k==0) continue;
                        int a=first+(k-1)*Sides+s, b=first+(k-1)*Sides+(s+1)%Sides;
                        triangles.Add(a); triangles.Add(b); triangles.Add(a+Sides);
                        triangles.Add(b); triangles.Add(b+Sides); triangles.Add(a+Sides);
                    }
                }
            }
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.1f,.7f,1);
            Vector3 previous=Rim(0);
            for (int i=1;i<=72;i++)
            {
                float angle=(rimRotation+5*i)*Mathf.Deg2Rad;
                var next=new Vector3(horizontalRadius*Mathf.Cos(angle),verticalRadius*Mathf.Sin(angle),0);
                Gizmos.DrawLine(previous,next); previous=next;
            }
            for (int i=0;i<Contacts;i++) Gizmos.DrawWireSphere(Rim(i),.055f);
        }
    }
}
