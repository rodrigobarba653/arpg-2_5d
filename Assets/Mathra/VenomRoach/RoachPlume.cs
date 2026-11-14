using UnityEngine;
using UnityEngine.Rendering;

namespace Mathra.Enemies
{
    internal sealed class RoachPlume
    {
        readonly ParticleSystem[] layers = new ParticleSystem[5];
        readonly ParticleSystemRenderer[] renderers = new ParticleSystemRenderer[5];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly float[] carry = new float[5];
        static readonly int Glow = Shader.PropertyToID("_Glow");
        static readonly int Style = Shader.PropertyToID("_Style");

        public RoachPlume(Transform owner, Material material)
        {
            string[] names = { "Rolling mist", "Luminous jet", "Venom beads", "Liquid filaments", "Suspended residue" };
            for (int i=0;i<layers.Length;i++)
            {
                var child = new GameObject(names[i]);
                child.transform.SetParent(owner,false);
                var ps = child.AddComponent<ParticleSystem>();
                layers[i] = ps;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake=false;
                main.loop=true;
                main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                main.maxParticles=i==0?512:192;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission; emission.enabled=false;
                var shape=ps.shape; shape.enabled=false;
                var size=ps.sizeOverLifetime;
                size.enabled=true;
                size.size=new ParticleSystem.MinMaxCurve(1, i==0?
                    AnimationCurve.EaseInOut(0,.42f,1,2.3f):AnimationCurve.Linear(0,1,1,.38f));
                var fade=ps.colorOverLifetime;
                fade.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.85f,.35f),new GradientAlphaKey(0,1)});
                fade.color=gradient;
                var r=ps.GetComponent<ParticleSystemRenderer>();
                renderers[i]=r;
                r.sharedMaterial=material;
                r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
                if(i==1 || i==3){r.renderMode=ParticleSystemRenderMode.Stretch;r.velocityScale=i==3?.10f:.038f;r.lengthScale=1.25f;}
                ps.Play();
            }
        }
        public bool HasParticles
        {
            get{foreach(var ps in layers) if(ps.particleCount>0)return true; return false;}
        }
        public void Resume(){foreach(var ps in layers)ps.Play();}
        public void Clear(){foreach(var ps in layers)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void Configure(VenomRoach r)
        {
            for(int i=0;i<layers.Length;i++)
            {
                carry[i]=0;
                var collision=layers[i].collision;
                collision.enabled=r.gasCollisionLayers.value!=0;
                if(collision.enabled)
                {
                    collision.type=ParticleSystemCollisionType.World;
                    collision.mode=ParticleSystemCollisionMode.Collision3D;
                    collision.collidesWith=r.gasCollisionLayers;
                    collision.lifetimeLoss=1;
                    collision.enableDynamicColliders=false;
                    collision.quality=ParticleSystemCollisionQuality.Medium;
                }
                properties.SetFloat(Glow,i==0?r.cloudGlow:i==1?r.coreGlow:r.dropletGlow);
                properties.SetFloat(Style,i);
                renderers[i].SetPropertyBlock(properties);
            }
            var noise=layers[0].noise;
            noise.enabled=r.turbulence>.001f;
            noise.strength=r.turbulence;noise.frequency=.8f;noise.scrollSpeed=.85f;
            noise.quality=ParticleSystemNoiseQuality.Low;
            var main=layers[2].main; main.gravityModifier=r.dropletGravity;
            var fluid=layers[3].main;fluid.gravityModifier=r.dropletGravity;
            var drift=layers[4].noise;
            drift.enabled=r.residueDrift>0;drift.strength=r.residueDrift;drift.frequency=.55f;
            drift.quality=ParticleSystemNoiseQuality.Low;
        }
        public void Begin(VenomRoach r,Vector3 origin,Vector3 forward)
        {
            Configure(r);
            if(r.flashSize>0)
                Emit(1,origin,forward*.16f,.095f,r.flashSize,r.coreColor);
        }
        public void Stream(VenomRoach r,Vector3 origin,Vector3 axis,float dt)
        {
            Quaternion basis=Quaternion.LookRotation(axis,Mathf.Abs(axis.y)>.99f?Vector3.forward:Vector3.up);
            for(int i=0;i<3;i++)
            {
                int rate=i==0?r.density:i==1?r.coreRate:r.dropletRate;
                carry[i]+=rate*dt;
                int count=Mathf.FloorToInt(carry[i]);carry[i]-=count;
                float spread=r.spreadDegrees*(i==1?.28f:i==2?1.14f:1);
                for(int j=0;j<count;j++)
                {
                    Vector2 offset=Random.insideUnitCircle*Mathf.Tan(spread*Mathf.Deg2Rad);
                    Vector3 dir=basis*new Vector3(offset.x,offset.y,1).normalized;
                    float speed=r.gasSpeed*Random.Range(.82f,1.14f)*(i==0?.83f:i==1?1.12f:1);
                    float size=(i==0?r.puffSize:i==1?r.coreWidth:r.dropletSize)*Random.Range(.7f,1.25f);
                    float life=(i==1?r.coreLife:r.lifetime)*Random.Range(.84f,1.1f);
                    Color color=i==0?r.gasColor:i==1?r.coreColor:r.dropletColor;
                    Emit(i,origin+dir*(speed*dt*j/Mathf.Max(1,count)),dir*speed,life,size,color);
                }
            }
        }
        public void Impact(VenomRoach r,Vector3 origin,Vector3 axis)
        {
            Quaternion aim=Quaternion.LookRotation(axis,Mathf.Abs(axis.y)>.98f?Vector3.forward:Vector3.up);
            for(int i=0;i<r.liquidStrands;i++)
            {
                var cone=Random.insideUnitCircle*Mathf.Tan(r.spreadDegrees*Mathf.Deg2Rad);
                Vector3 dir=aim*new Vector3(cone.x,cone.y,1).normalized;
                Emit(3,origin,dir*r.gasSpeed*Random.Range(.42f,.84f)+Vector3.up*.65f,
                    r.lifetime*Random.Range(.6f,1.1f),r.strandWidth*Random.Range(.7f,1.5f),r.liquidColor);
            }
            Residue(r,origin,axis,false);
        }
        void Residue(VenomRoach r,Vector3 origin,Vector3 axis,bool radial)
        {
            for(int i=0;i<r.residueCount;i++)
            {
                Vector3 direction=radial?Random.onUnitSphere:(axis+Random.insideUnitSphere*.8f).normalized;
                direction.y=Mathf.Abs(direction.y)*.65f+.09f;
                Emit(4,origin+direction*Random.Range(.02f,.16f),direction*Random.Range(.22f,1.05f),
                    r.residueLife*Random.Range(.65f,1),Random.Range(.04f,.105f),r.residueColor);
            }
        }
        public void Explode(VenomRoach r,Vector3 origin)
        {
            Configure(r);
            float scale=r.explosionScale;
            for(int i=0;i<r.explosionParticles;i++)
            {
                Vector3 dir=Random.onUnitSphere;
                dir.y=Mathf.Abs(dir.y)*.9f-.06f;
                int layer=i%4;
                float size=layer==0?r.puffSize*1.5f:layer==1?r.coreWidth:layer==2?r.dropletSize:r.strandWidth;
                Color color=layer==0?r.gasColor:layer==1?r.coreColor:layer==2?r.dropletColor:r.liquidColor;
                float speed=layer==0?2.2f:5.2f;
                Emit(layer,origin,dir*speed*scale*Random.Range(.46f,1.45f),
                    (layer==1?r.coreLife:r.lifetime)*Random.Range(.85f,1.5f),
                    size*scale*Random.Range(.68f,1.6f),color);
            }
            Residue(r,origin,Vector3.up,true);
        }
        void Emit(int layer,Vector3 pos,Vector3 velocity,float life,float size,Color color)
        {
            var p=new ParticleSystem.EmitParams{position=pos,velocity=velocity,startSize=size,
                startLifetime=life,startColor=color,rotation=(layer==1 || layer==3)?0:Random.Range(0f,360f)};
            layers[layer].Emit(p,1);
        }
    }
}
