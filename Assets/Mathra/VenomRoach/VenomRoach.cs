using UnityEngine;

namespace Mathra.Enemies
{
    [DisallowMultipleComponent]
    public sealed class VenomRoach : MonoBehaviour
    {
        [Header("Patrol and steering")]
        [Range(.2f,9)] public float walkSpeed=3.6f;
        [Range(1,16)] public float patrolRadius=4;
        [Range(1,45)] public float acceleration=16, braking=23;
        [Range(90,1440)] public float turnSpeed=510;
        [Range(.1f,1.5f)] public float lookAhead=.45f;
        [Range(.05f,1)] public float safeDrop=.32f;
        [Range(0,1)] public float strideVariation=.24f;
        public LayerMask floorMask=~0;
        public LayerMask obstructionMask=Physics.DefaultRaycastLayers;
        [Header("Legs and body")]
        [Range(4,48)] public float walkFramesPerSecond=28;
        [Range(0,.06f)] public float strideBob=.014f;
        [Range(0,9)] public float strideRoll=2;
        [Range(0,15)] public float facingHysteresis=7;
        [Range(0,.4f)] public float recoil=.13f;
        [Range(0,.3f)] public float bodyCompression=.12f;
        [Range(.3f,2)] public float spriteSize=1;
        [Header("Detection and attack timing")]
        public Transform playerRoot;
        [Range(.5f,18)] public float detectionRadius=5;
        [Range(0,2)] public float aimHeight=.45f;
        [Range(.05f,1.2f)] public float windup=.26f;
        [Range(.05f,1.5f)] public float burstDuration=.32f;
        [Range(.05f,.9f)] public float recovery=.19f;
        [Range(.1f,8)] public float cooldown=1.8f;
        [Range(0,.4f)] public float aimLead=.055f;
        [Range(.2f,4)] public float escapeDistance=1.3f;
        [Header("Abdominal sac and impact")]
        [Range(.25f,3f)] public float inflationTime=.92f;
        [Range(.08f,.6f)] public float slamTime=.18f;
        public bool lightAbdomen=true;
        public Color abdomenColor=new Color(.48f,1,.09f);
        [Range(0,12)] public float lightPower=2.1f;
        [Range(.2f,6)] public float lightRadius=1.6f;
        [Header("Fluid and suspended poison")]
        public Color liquidColor=new Color(.43f,.83f,.13f,.84f);
        public Color residueColor=new Color(.73f,.98f,.25f,.60f);
        [Range(0,80)] public int liquidStrands=17;
        [Range(0,120)] public int residueCount=32;
        [Range(.4f,9)] public float residueLife=3.3f;
        [Range(.02f,.3f)] public float strandWidth=.095f;
        [Range(0,2)] public float residueDrift=.45f;
        [Header("Death")]
        [Range(1,1000)] public int hitPoints=30;
        [Range(.3f,3)] public float deathInflation=1.05f;
        [Range(.06f,.7f)] public float ruptureTime=.22f;
        [Range(.25f,3)] public float explosionScale=1.25f;
        [Range(0,180)] public int explosionParticles=80;
        [Header("Venom cloud")]
        public Color gasColor=new Color(.38f,1,.08f,.75f);
        [Range(1,35)] public float gasSpeed=15;
        [Range(0,60)] public float spreadDegrees=18;
        [Range(4,240)] public int density=90;
        [Range(.08f,2)] public float lifetime=.55f;
        [Range(.04f,1)] public float puffSize=.23f;
        [Range(0,2)] public float turbulence=.38f;
        [Range(0,6)] public float cloudGlow=1.35f;
        [Tooltip("Optional walls and scenery that stop venom. Exclude the hero and enemy layers.")]
        public LayerMask gasCollisionLayers=0;
        [Header("Luminous jet and mouth flash")]
        public Color coreColor=new Color(.78f,1,.28f,.95f);
        [Range(0,12)] public float coreGlow=3.4f;
        [Range(0,180)] public int coreRate=65;
        [Range(.015f,.4f)] public float coreWidth=.10f;
        [Range(.05f,1)] public float coreLife=.27f;
        [Range(0,.8f)] public float flashSize=.23f;
        [Header("Venom droplets")]
        public Color dropletColor=new Color(.88f,1,.12f,.95f);
        [Range(0,100)] public int dropletRate=32;
        [Range(.015f,.24f)] public float dropletSize=.067f;
        [Range(0,3)] public float dropletGravity=.8f;
        [Range(0,8)] public float dropletGlow=2.1f;
        [SerializeField,HideInInspector] SpriteRenderer body;
        [SerializeField,HideInInspector] Animator spriteAnimator;
        [SerializeField,HideInInspector] Material gasMaterial;

        enum Phase { Patrol, Brace, Slam, Spit, Recover, Dying, Rupture, Spent }
        Phase phase;
        CharacterController motor;
        RoachPlume fx;
        Transform hero, previousHero, fallback;
        Vector3 home, heading, goal, previousHeroPosition, targetVelocity, shot, escape;
        float speed, verticalSpeed, age, ready, lookup, cadence, gait, traveled, steerTimer;
        int facing=-1, lastState, lastKey=-1;
        Camera view;
        Light sacLight;
        Vector3 lockedAim;
        int health;
        bool IsDying => phase==Phase.Dying || phase==Phase.Rupture || phase==Phase.Spent;
        public void TakeDamage(int amount)
        {
            if(!Application.isPlaying || amount<=0 || IsDying) return;
            health=Mathf.Max(0,health-amount);
            if(health==0)Die();
        }
        [ContextMenu("Preview venom death (Play Mode)")]
        public void Die()
        {
            if(!Application.isPlaying || IsDying || fx==null)return;
            phase=Phase.Dying;age=0;speed=0;traveled=0;health=0;
            if(motor)motor.enabled=false;
        }
        void SacLight(float charge)
        {
            if(!lightAbdomen || charge<=0 || lightPower<=0)
            { if(sacLight)sacLight.enabled=false; return; }
            if(!sacLight)
            {
                var node=new GameObject("Venom sac light");
                node.transform.SetParent(transform,false);
                sacLight=node.AddComponent<Light>();
                sacLight.type=LightType.Point;
                sacLight.shadows=LightShadows.None;
            }
            // Measured in sprite space so the light tracks the sac through camera turns.
            float back=facing<0?0:-Mathf.Sin(facing*Mathf.PI/4);
            sacLight.transform.position=body.transform.TransformPoint(new Vector3(back*.12f,.39f,-.05f));
            sacLight.enabled=true;sacLight.color=abdomenColor;sacLight.range=lightRadius;
            sacLight.intensity=lightPower*charge*(.89f+.11f*Mathf.Sin(age*24));
        }
        static readonly string[] Directions={"N","NE","E","SE","S","SW","W","NW"};
        readonly int[] states=new int[24];
        static readonly Vector2[] MouthPx={new Vector2(31,27),new Vector2(43,26),new Vector2(54,52),new Vector2(45,54),
            new Vector2(31,56),new Vector2(17,55),new Vector2(9,53),new Vector2(20,26)};
        void Awake()
        {
            if (!body || !spriteAnimator || !spriteAnimator.runtimeAnimatorController || !gasMaterial)
            { Debug.LogError("VenomRoach requires its Body, Animator controller and Gas material.",this); enabled=false; return; }
            for(int i=0;i<24;i++)
                states[i]=Animator.StringToHash("Base Layer."+(i<8?"Walk_":i<16?"Spit_":"Death_")+Directions[i%8]);
            spriteAnimator.speed=0;
            motor=GetComponent<CharacterController>();
            if(!motor) motor=gameObject.AddComponent<CharacterController>();
            motor.height=.48f; motor.radius=.19f; motor.center=Vector3.up*.24f;
            motor.skinWidth=.025f; motor.stepOffset=.12f; motor.minMoveDistance=0;
            fx=new RoachPlume(transform,gasMaterial);
            health=hitPoints;
            home=transform.position;
            heading=Vector3.forward;
            PickGoal();
        }
        void PickGoal()
        {
            var p=Random.insideUnitCircle*patrolRadius;
            goal=home+new Vector3(p.x,0,p.y);
            cadence=Random.Range(1-strideVariation*.5f,1+strideVariation*.5f);
        }
        Transform Leader()
        {
            var selected=SelectedRoachTarget.Current;
            if(selected && selected.gameObject.activeInHierarchy) return selected;
            if(playerRoot && playerRoot.gameObject.activeInHierarchy) return playerRoot;
            if(Time.time>=lookup)
            {
                lookup=Time.time+.55f;
                var players=GameObject.FindGameObjectsWithTag("Player");
                fallback=players.Length==1?players[0].transform:null;
                if(players.Length==0)
                {
                    var root=GameObject.Find("PlayerRoot");
                    if(root) fallback=root.transform;
                }
            }
            return fallback && fallback.gameObject.activeInHierarchy?fallback:null;
        }
        static Vector3 Flat(Vector3 v) => new Vector3(v.x,0,v.z);
        bool Visible()
        {
            if(!hero || (hero.position-transform.position).sqrMagnitude>detectionRadius*detectionRadius) return false;
            if(!Physics.Linecast(transform.position+Vector3.up*.34f,hero.position+Vector3.up*aimHeight,out var hit,obstructionMask,QueryTriggerInteraction.Ignore)) return true;
            return hit.transform.IsChildOf(hero)||hero.IsChildOf(hit.transform);
        }
        bool Clear(Vector3 dir,float reach)
        {
            Vector3 pos=transform.position;
            if(Physics.SphereCast(pos+Vector3.up*.25f,.145f,dir,out _,reach,obstructionMask,QueryTriggerInteraction.Ignore)) return false;
            return Physics.Raycast(pos+dir*(reach+.15f)+Vector3.up*.33f,Vector3.down,out var floor,
                .33f+safeDrop,floorMask,QueryTriggerInteraction.Ignore) && floor.normal.y>=Mathf.Cos(motor.slopeLimit*Mathf.Deg2Rad);
        }
        void Turn(Vector3 to,float dt)
        {
            if(to.sqrMagnitude<.0001f) return;
            float angle=Mathf.DeltaAngle(Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg,Mathf.Atan2(to.x,to.z)*Mathf.Rad2Deg);
            heading=Quaternion.AngleAxis(Mathf.Clamp(angle,-turnSpeed*dt,turnSpeed*dt),Vector3.up)*heading;
        }
        float Patrol(float dt)
        {
            var delta=Flat(goal-transform.position);
            if(delta.sqrMagnitude<.09f){PickGoal();delta=Flat(goal-transform.position);}
            steerTimer-=dt;
            float reach=Mathf.Max(lookAhead,dt*speed);
            if(steerTimer<=0)
            {
                steerTimer=.1f;
                escape=Vector3.zero;
                var wanted=delta.normalized;
                if(!Clear(wanted,reach))
                {
                    for(int i=1;i<=6;i++)
                    {
                        float angle=((i+1)/2)*48*(i%2==1?1:-1);
                        var side=Quaternion.AngleAxis(angle,Vector3.up)*wanted;
                        if(Clear(side,reach)){escape=side;goal=transform.position+side*1.15f;break;}
                    }
                    if(escape==Vector3.zero){PickGoal();return 0;}
                }
            }
            Turn(escape!=Vector3.zero?escape:delta,dt);
            return Clear(heading,Mathf.Max(.14f,speed*dt)) ? walkSpeed*cadence : 0;
        }
        void Update()
        {
            float dt=Time.deltaTime;
            if(dt<=0)return;
            if(!view || !view.isActiveAndEnabled)view=Camera.main;
            if(IsDying)
            {
                age+=dt;
                if(phase==Phase.Dying && age>=deathInflation)
                {
                    phase=Phase.Rupture;age=0;
                    fx.Explode(this,transform.position+Vector3.up*.44f*spriteSize);
                }
                else if(phase==Phase.Rupture && age>=ruptureTime)
                {
                    phase=Phase.Spent;age=0;body.enabled=false;
                    SacLight(0);
                }
                else if(phase==Phase.Spent && !fx.HasParticles) Destroy(gameObject);
                return;
            }
            hero=Leader();
            if(hero)
            {
                var estimate=hero==previousHero?Vector3.ClampMagnitude((hero.position-previousHeroPosition)/dt,9):Vector3.zero;
                targetVelocity=Vector3.Lerp(hero==previousHero?targetVelocity:Vector3.zero,estimate,1-Mathf.Exp(-12*dt));
                previousHeroPosition=hero.position;
            }
            previousHero=hero;
            float wanted=0;
            age+=dt;
            if(phase==Phase.Patrol && Time.time>=ready && Visible()){phase=Phase.Brace;age=0;}
            switch(phase)
            {
                case Phase.Patrol: wanted=Patrol(dt);break;
                case Phase.Brace:
                    if(!Visible()){phase=Phase.Patrol;ready=Time.time+.2f;break;}
                    Vector3 aim=hero.position+Vector3.up*aimHeight+targetVelocity*aimLead;
                    var line=Flat(aim-transform.position);
                    Turn(line,dt);
                    if(age>=windup+inflationTime && (line.sqrMagnitude<.001f || Vector3.Angle(heading,line)<13))
                    {
                        lockedAim=aim;phase=Phase.Slam;age=0;
                    }
                    break;
                case Phase.Slam:
                    if(age>=slamTime)
                    {
                        phase=Phase.Spit;age=0;UpdateBody();
                        shot=(lockedAim-Mouth()).normalized;
                        if(shot.sqrMagnitude<.5f)shot=heading;
                        fx.Begin(this,Mouth(),shot);
                        fx.Impact(this,Mouth(),shot);
                    }
                    break;
                case Phase.Spit:
                    if(age>=burstDuration){phase=Phase.Recover;age=0;}
                    break;
                case Phase.Recover:
                    if(age>=recovery)
                    {
                        phase=Phase.Patrol;ready=Time.time+cooldown;age=0;
                        Vector3 side=Quaternion.AngleAxis(Random.value<.5f?105:-105,Vector3.up)*heading;
                        goal=transform.position+side*escapeDistance;
                        if(Flat(goal-home).magnitude>patrolRadius)PickGoal();
                        steerTimer=0;
                    }
                    break;
            }
            speed=Mathf.MoveTowards(speed,wanted,(wanted>speed?acceleration:braking)*dt);
            if(speed>.001f && !Clear(heading,Mathf.Max(.10f,speed*dt))) speed=0;
            verticalSpeed=motor.isGrounded?-2:Mathf.Max(-25,verticalSpeed-20*dt);
            Vector3 before=transform.position;
            var flags=motor.Move((heading*speed+Vector3.up*verticalSpeed)*dt);
            if((flags & CollisionFlags.Sides)!=0)steerTimer=0;
            traveled=Flat(transform.position-before).magnitude;
            gait=Mathf.Repeat(gait+traveled*walkFramesPerSecond/(8*Mathf.Max(.2f,walkSpeed)),1);
        }
        void LateUpdate()
        {
            UpdateBody();
            if(phase==Phase.Spit && Time.deltaTime>0)
                fx.Stream(this,Mouth(),shot,Mathf.Min(Time.deltaTime,burstDuration-age));
        }
        void UpdateBody()
        {
            if(!view || phase==Phase.Spent)return;
            var right=Flat(view.transform.right).normalized;
            float angle=Mathf.Atan2(Vector3.Dot(heading,right),Vector3.Dot(heading,Vector3.Cross(right,Vector3.up)))*Mathf.Rad2Deg;
            if(facing<0 || Mathf.Abs(Mathf.DeltaAngle(facing*45,angle))>22.5f+facingHysteresis)
                facing=(Mathf.RoundToInt(angle/45)+8)%8;
            float tension=phase==Phase.Brace?Mathf.Clamp01(age/(windup+inflationTime)):0;
            float impulse=phase==Phase.Spit?Mathf.Exp(-age*19)*Mathf.Cos(age*31):0;
            float moving=Mathf.Clamp01(traveled/Mathf.Max(.0001f,Time.deltaTime*walkSpeed));
            float cycle=gait*2*Mathf.PI;
            int key,state;
            if(phase==Phase.Patrol){state=facing;key=Mathf.FloorToInt(gait*8);}
            else if(IsDying)
            {
                state=16+facing;
                key=phase==Phase.Dying?Mathf.Min(11,(int)(12*age/deathInflation)):
                    12+Mathf.Min(3,(int)(4*age/ruptureTime));
            }
            else
            {
                state=8+facing;
                if(phase==Phase.Brace)
                    key=age<windup?Mathf.Min(3,(int)(4*age/windup)):
                        4+Mathf.Min(8,(int)(9*(age-windup)/inflationTime));
                else if(phase==Phase.Slam)key=13+Mathf.Min(2,(int)(3*age/slamTime));
                else key=phase==Phase.Spit?16:17;
            }
            if(state!=lastState||key!=lastKey)
            {
                spriteAnimator.Play(states[state],0,(key+.1f)/(state<8?8f:state<16?18f:16f));
                spriteAnimator.Update(0);
                lastState=state;lastKey=key;
            }
            var t=body.transform;
            float k=impulse-.68f*tension;
            t.position=transform.position+heading*(recoil*k)+Vector3.up*(.025f+strideBob*Mathf.Abs(Mathf.Sin(cycle))*moving);
            t.rotation=view.transform.rotation*Quaternion.Euler(0,0,strideRoll*Mathf.Sin(cycle)*moving);
            float side=Mathf.Abs(Vector3.Dot(heading,right));
            t.localScale=spriteSize*new Vector3(1+bodyCompression*k*side,1-bodyCompression*(tension+.4f*impulse),1);
            float charge=tension*tension;
            if(phase==Phase.Slam)charge=1-age/slamTime;
            if(phase==Phase.Spit)charge=Mathf.Exp(-age*22)*.7f;
            if(phase==Phase.Dying)charge=Mathf.Clamp01(age/deathInflation)*1.6f;
            if(phase==Phase.Rupture)charge=2*Mathf.Exp(-age*22);
            SacLight(charge);
        }
        Vector3 Mouth()
        {
            if(facing<0)return transform.position+heading*.25f+Vector3.up*.28f;
            Vector2 px=MouthPx[facing];
            return body.transform.TransformPoint(new Vector3(px.x-32,60-px.y,0)/56f);
        }
        void OnDisable(){SacLight(0);if(fx!=null) fx.Clear();}
        void OnEnable(){if(fx!=null) fx.Resume();}
    }
}
