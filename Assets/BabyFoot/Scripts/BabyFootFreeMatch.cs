using System;
using System.Collections.Generic;
using UnityEngine;

// Endless casual match: continuous XZ ball, real player collider rebounds and swept foot strikes.
// Independent of training/session scripts; no participant file output.
public class BabyFootFreeMatch : MonoBehaviour
{
    public Transform ball;
    public Transform[] rods;
    public ParticleSystem goalSparks;
    public bool Paused {get;private set;}=true;
    public int BlueScore {get;private set;}
    public int CoralScore {get;private set;}
    public Vector3 Velocity {get;private set;}
    public int PlayerContacts {get;private set;}
    public int WallContacts {get;private set;}
    public int AIShots {get;private set;}
    public string Message {get;private set;}="Blue vs Coral AI";
    public bool aiEnabled=true;
    public int difficulty=1;
    public readonly BabyFootBallControl BallControl=new BabyFootBallControl();
    public bool ControlWarmup {get;private set;}
    public int ControlledRod=>BallControl.Rod;
    public bool PossessionSecured=>BallControl.Secured;
    BabyFootFreeMatchHands hands;
    float[] angularVelocity;
    float[] rodAngle;
    public float RodAngle(int index)=>rodAngle[index];
    float feedDelay;
    public bool Spectator {get;private set;}
    public float MatchTime {get;private set;}
    public BabyFootMatchAI AI {get;private set;}
    bool savedSpectatorAI;
    float deadTime;
    Vector3 lastMotionPosition;
    public int DeadBallResets {get;private set;}
    public bool IsAutomatedRod(int index)=>aiEnabled&&(Spectator||!IsBlue(index));
    bool savedAI;
    public const float Radius=.0175f,HalfWidth=.34f,GoalZ=.5565f,GoalHalf=.1025f;
    BabyFootFootContact[] feet;
    readonly HashSet<Collider> players=new HashSet<Collider>();
    readonly Dictionary<Collider,int> colliderRod=new Dictionary<Collider,int>();
    readonly RaycastHit[] hits=new RaycastHit[64];
    readonly Collider[] overlaps=new Collider[32];
    SphereCollider ballCollider;
    float[] previousAngle,previousX,cooldown;
    Vector3[] rodVelocity;
    Vector2[] rodTravel;
    public Vector2 TravelRange(int index)=>rodTravel[index];
    float serveAt=-1,serveSign=-1;
    int ignoreRod=-1;
    float ignoreUntil;
    AudioSource audioSource;AudioClip contactClip,goalClip;
    void Awake()
    {
        hands=GetComponent<BabyFootFreeMatchHands>();angularVelocity=new float[rods.Length];rodAngle=new float[rods.Length];
        feet=new BabyFootFootContact[rods.Length];previousAngle=new float[rods.Length];previousX=new float[rods.Length];cooldown=new float[rods.Length];rodVelocity=new Vector3[rods.Length];rodTravel=new Vector2[rods.Length];
        for(int i=0;i<rods.Length;i++)
        {
            rods[i].gameObject.SetActive(true);rods[i].localRotation=Quaternion.identity;rodTravel[i]=BabyFootRodTravel.Configure(rods[i],HalfWidth);feet[i]=new BabyFootFootContact(rods[i]);
            foreach(var c in rods[i].GetComponentsInChildren<Collider>(true)){if(c.enabled&&!c.isTrigger){players.Add(c);colliderRod[c]=i;}}
        }
        AI=new BabyFootMatchAI(this);
        var rb=ball.GetComponent<Rigidbody>();if(rb)rb.isKinematic=true;
        ballCollider=ball.GetComponent<SphereCollider>();
        var audioObject=new GameObject("Match Audio");audioObject.transform.SetParent(transform,false);audioSource=audioObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;audioSource.minDistance=.5f;audioSource.maxDistance=5;
        contactClip=Resources.Load<AudioClip>("BabyFootAudio/contact");goalClip=Resources.Load<AudioClip>("BabyFootAudio/goal");
    }
    public static bool IsBlue(int index)=>index==0||index==1||index==3||index==5;
    public static bool IsKeeper(int index)=>index==0||index==7;
    public void SetRodPose(int index,float slide,float angle)
    {
        var range=rodTravel[index];var p=rods[index].localPosition;p.x=Mathf.Clamp(slide,range.x,range.y);rods[index].localPosition=p;
        rodAngle[index]=angle;rods[index].localRotation=Quaternion.AngleAxis(angle,Vector3.right);
    }
    public void SetPaused(bool value){Paused=value;if(value)BallControl.Release();Message=value?"Match paused":"Play on";}
    public void EndControlWarmup()
    {
        if(!ControlWarmup)return;ControlWarmup=false;aiEnabled=savedAI;
        for(int i=0;i<rods.Length;i++){SetRodPose(i,rods[i].localPosition.x,0);previousAngle[i]=0;}
        Serve();
    }
    public void SetSpectatorMode(bool value)
    {
        if(Spectator==value)return;
        EndControlWarmup();hands?.ReleaseAll();BallControl.Release();
        if(value){savedSpectatorAI=aiEnabled;aiEnabled=true;}else aiEnabled=savedSpectatorAI;
        Spectator=value;AI.Reset();deadTime=0;
        for(int i=0;i<rods.Length;i++){previousX[i]=rods[i].localPosition.x;previousAngle[i]=rodAngle[i];}
    }
    public void StartSpectator(){SetSpectatorMode(true);StartMatch();}
    public void StartMatch()
    {
        EndControlWarmup();hands?.ReleaseAll();BlueScore=CoralScore=0;DeadBallResets=0;
        for(int i=0;i<rods.Length;i++)
        {SetRodPose(i,0,0);previousX[i]=previousAngle[i]=angularVelocity[i]=0;rodVelocity[i]=Vector3.zero;cooldown[i]=MatchTime;}
        AI.Reset(true);serveSign=UnityEngine.Random.value<.5f?-1:1;SetPaused(false);Serve();
    }
    public void BeginControlWarmup()
    {
        SetSpectatorMode(false);
        if(!ControlWarmup){savedAI=aiEnabled;ControlWarmup=true;}
        aiEnabled=false;SetPaused(false);
        for(int i=0;i<rods.Length;i++){SetRodPose(i,0,i==1?0:75);previousAngle[i]=Mathf.DeltaAngle(0,rods[i].localEulerAngles.x);previousX[i]=0;}
        Serve();
    }
    public void Serve()
    {
        BallControl.Release(0);feedDelay=0;AI.Reset();deadTime=0;
        if(ControlWarmup)
        {
            serveAt=-1;ignoreRod=-1;feedDelay=1.2f;
            float x=rods[1].localPosition.x; x+=x>0?-.1175f:.1175f;
            PlaceBall(new Vector3(x,.775f,-.13f),Vector3.zero);
            Message="Receive on the blue two-player rod / A or X repeats";return;
        }
        serveAt=-1;PlaceBall(new Vector3(0,.775f,0),new Vector3(UnityEngine.Random.Range(-.1f,.1f),0,.65f*serveSign));
        ignoreRod=-1;Message="Ball in play";
    }
    public void PlaceBall(Vector3 localPosition,Vector3 velocity)
    {
        BallControl.Release(0);
        localPosition.y=.775f;velocity.y=0;ball.localPosition=localPosition;Velocity=Vector3.ClampMagnitude(velocity,3.5f);
    }
    void Play(AudioClip clip){if(clip){audioSource.transform.position=ball.position;audioSource.PlayOneShot(clip,.6f);}}
    public bool TryStrike(int index,float from,float to,float dt)
    {
        float speed=(to-from)/Mathf.Max(dt,.001f);
        if(IsAutomatedRod(index)&&speed*(IsBlue(index)?-1:1)<0)return false; // Powered AI strokes only face the opponent goal.
        if(Mathf.Abs(speed)<180||MatchTime<cooldown[index]||serveAt>=0)return false;
        // Keep the retained friction contact within the solver's millimetre contact skin.
        float contactRadius=Radius+(ControlledRod==index?.0015f:0);
        if(!feet[index].Sweep(ball.position,contactRadius,from,to,out float offset,out _))return false;
        var direction=BabyFootFootContact.Direction(offset,speed>0);
        BallControl.Release();
        Velocity=Vector3.ClampMagnitude(direction*Mathf.Clamp(Mathf.Abs(speed)*Mathf.Deg2Rad*.065f*1.5f,.35f,3.5f)+Vector3.right*(rodVelocity[index].x*.8f),3.5f);
        cooldown[index]=MatchTime+.14f;ignoreRod=index;ignoreUntil=MatchTime+.09f;PlayerContacts++;
        if(IsAutomatedRod(index)){AIShots++;AI.OnStrike(index);}else AI.OnPlayerContact(index);Play(contactClip);return true;
    }
    public void Simulate(float delta)
    {
        if(Paused||delta<=0)return;
        MatchTime+=delta;
        if(feedDelay>0){feedDelay-=delta;if(feedDelay<=0)Velocity=new Vector3(0,0,-.55f);}
        if(serveAt>=0){if(MatchTime>=serveAt)Serve();return;}
        AI.Step(delta);
        for(int i=0;i<rods.Length;i++)
        {
            float angle=rodAngle[i]+Mathf.DeltaAngle(rodAngle[i],BabyFootFootContact.Angle(rods[i]));rodAngle[i]=angle;
            rodVelocity[i]=new Vector3((rods[i].localPosition.x-previousX[i])/Mathf.Max(delta,.001f),0,0);
            angularVelocity[i]=(angle-previousAngle[i])/Mathf.Max(delta,.001f);
            TryStrike(i,previousAngle[i],angle,delta);previousAngle[i]=angle;previousX[i]=rods[i].localPosition.x;
        }
        Physics.SyncTransforms();
        int steps=Mathf.Max(1,Mathf.CeilToInt(delta*240));float dt=delta/steps;
        for(int n=0;n<steps&&serveAt<0;n++)AdvanceBall(dt);
        if(Spectator&&serveAt<0)
        {
            if(ControlledRod<0&&Vector3.Distance(ball.localPosition,lastMotionPosition)<.004f)deadTime+=delta;
            else{deadTime=0;lastMotionPosition=ball.localPosition;}
            if(deadTime>2.5f){DeadBallResets++;Serve();Message="Dead ball / new serve";}
        }
    }
    void FixedUpdate(){Simulate(Time.fixedDeltaTime);}
    void AdvanceBall(float dt)
    {
        var p=ball.localPosition;
        UpdateControl(p,dt);
        // Casts do not report an already-overlapping sphere. Resolve moving-rod overlaps first.
        if(ballCollider)
        {
            Vector3 world=ball.parent.TransformPoint(p);int count=Physics.OverlapSphereNonAlloc(world,Radius,overlaps,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=overlaps[i];if(!players.Contains(c)||colliderRod[c]==ControlledRod||colliderRod[c]==ignoreRod&&MatchTime<ignoreUntil)continue;
                if(Physics.ComputePenetration(ballCollider,world,ball.rotation,c,c.transform.position,c.transform.rotation,out var normal,out float distance))
                {
                    if(TryReceiveAt(colliderRod[c],p)){PlayerContacts++;continue;}
                    normal=ball.parent.InverseTransformDirection(normal);normal.y=0;float planar=normal.magnitude;if(planar<.1f)continue;normal/=planar;
                    p+=normal*Mathf.Min(.04f,distance/planar+.0002f);world=ball.parent.TransformPoint(p);
                    Respond(c,p,normal);
                    PlayerContacts++;
                }
            }
        }
        float remaining=dt;
        for(int bounce=0;bounce<4&&remaining>0;bounce++)
        {
            float speed=Velocity.magnitude;if(speed<.0001f)break;
            var direction=Velocity/speed;
            int count=Physics.SphereCastNonAlloc(ball.parent.TransformPoint(p),Radius,ball.parent.TransformDirection(direction),hits,speed*remaining,~0,QueryTriggerInteraction.Ignore);
            RaycastHit closest=default;bool found=false;float nearest=speed*remaining;
            for(int i=0;i<count;i++)if(players.Contains(hits[i].collider)&&colliderRod[hits[i].collider]!=ControlledRod&&!(colliderRod[hits[i].collider]==ignoreRod&&MatchTime<ignoreUntil)&&hits[i].distance<nearest)
            {closest=hits[i];nearest=hits[i].distance;found=true;}
            if(!found){p+=Velocity*remaining;remaining=0;break;}
            Vector3 contactPosition=p+direction*Mathf.Max(0,nearest-.0001f);
            if(TryReceiveAt(colliderRod[closest.collider],contactPosition))
            {p=contactPosition;remaining-=Mathf.Max(nearest/speed,.00001f);PlayerContacts++;continue;}
            var normal=ball.parent.InverseTransformDirection(closest.normal);normal.y=0;
            if(normal.sqrMagnitude<.001f){p+=Velocity*remaining;remaining=0;break;}
            normal.Normalize();p+=direction*Mathf.Max(0,nearest-.0001f);
            Respond(closest.collider,p,normal);
            p+=normal*.0003f;remaining-=Mathf.Max(nearest/speed,.00001f);PlayerContacts++;
        }
        float side=HalfWidth-Radius;
        if(Mathf.Abs(p.x)>side){p.x=Mathf.Sign(p.x)*(2*side-Mathf.Abs(p.x));Velocity=new Vector3(-Velocity.x*.88f,0,Velocity.z*.98f);WallContacts++;AI.OnWallContact();}
        bool opening=Mathf.Abs(p.x)+Radius<=GoalHalf;
        if(Mathf.Abs(p.z)>GoalZ+Radius&&opening)
        {ball.localPosition=p;Goal(p.z>0);return;}
        float end=GoalZ-Radius;
        if(Mathf.Abs(p.z)>end&&!opening){p.z=Mathf.Sign(p.z)*(2*end-Mathf.Abs(p.z));Velocity=new Vector3(Velocity.x*.98f,0,-Velocity.z*.85f);WallContacts++;}
        // Damped rolling without vertical hops; the sphere remains on the playfield plane.
        Velocity=Vector3.MoveTowards(Velocity,Vector3.zero,.025f*dt);p.y=.775f;ball.localPosition=p;
        if(Mathf.Abs(p.x)>.5f||Mathf.Abs(p.z)>.8f)Serve();
    }
    void Goal(bool blue)
    {
        BallControl.Release();
        if(blue)BlueScore++;else CoralScore++;Velocity=Vector3.zero;serveSign=blue?1:-1;
        Message=blue?(Spectator?"BLUE AI GOAL!":"BLUE GOAL!"):"CORAL AI GOAL!";serveAt=MatchTime+.7f;
        if(goalSparks){goalSparks.transform.position=ball.position;goalSparks.Play(true);}Play(goalClip);
    }
    bool CanControl(int i)=>IsAutomatedRod(i)||!Spectator&&IsBlue(i)&&hands&&hands.IsHoldingRod(i);
    bool TryReceiveAt(int i,Vector3 p)
    {
        if(i==ignoreRod&&MatchTime<ignoreUntil)return false;
        if(!BallControl.TryReceive(i,rods[i],feet[i],p,Velocity,rodVelocity[i],angularVelocity[i],CanControl(i)))return false;
        Velocity=rodVelocity[i]+(Velocity-rodVelocity[i])*.08f;return true;
    }
    void UpdateControl(Vector3 p,float dt)
    {
        int owner=ControlledRod;
        if(owner>=0)
            Velocity=BallControl.Step(dt,rods[owner],feet[owner],p,Velocity,rodVelocity[owner],angularVelocity[owner],CanControl(owner));
        else
        {
            BallControl.Step(dt,null,null,p,Velocity,Vector3.zero,0,false);
            for(int i=0;i<rods.Length;i++)
                if(TryReceiveAt(i,p))break;
        }
    }
    void Respond(Collider collider,Vector3 p,Vector3 normal)
    {
        int i=colliderRod[collider];
        AI.OnPlayerContact(i);
        if(ControlledRod>=0&&ControlledRod!=i)BallControl.Release();
        if(TryReceiveAt(i,p))return;
        bool foot=collider.name=="Foot";
        Vector3 moving=rodVelocity[i];
        if(foot)
        {
            Vector3 spin=Vector3.Cross(Vector3.right*(angularVelocity[i]*Mathf.Deg2Rad),p-rods[i].localPosition);
            spin.y=0;moving+=spin;
        }
        Vector3 relative=Velocity-moving;
        float incoming=Vector3.Dot(relative,normal);
        if(incoming>=0)return;
        float restitution=foot?Mathf.Lerp(.08f,.6f,Mathf.Clamp01(relative.magnitude/2.5f)):.82f;
        Velocity=Vector3.ClampMagnitude(relative-(1+restitution)*incoming*normal+moving,3.5f);
    }
}
