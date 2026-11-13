using System;
using System.Collections.Generic;
using UnityEngine;

// Endless casual match: continuous XZ ball, real player collider rebounds and swept foot strikes.
// Independent of training/session scripts; no participant file output.
public class BabyFootFreeMatch : MonoBehaviour
{
    BabyFootScoreRail scoreRail;
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
    public string Message {get;private set;}="Blue vs Red AI";
    public bool aiEnabled=true;
    public int difficulty=1;
    public readonly BabyFootBallControl BallControl=new BabyFootBallControl();
    public bool ControlWarmup {get;private set;}
    public bool RecoveryPractice {get;private set;}
    public int Attempt {get;private set;}
    public int Recoveries {get;private set;}
    public float BounceTime {get;private set;}=-1;
    public float SecuredTime {get;private set;}=-1;
    public bool AttemptClosed {get;private set;}
    public Vector3 RecoveryTarget {get;private set;}
    float launchTime;
    int feedSide;
    public string RecoveryCue => AttemptClosed
        ? (SecuredTime>=0 ? $"Secured in {SecuredTime-BounceTime:F2} s" : "Time expired") + "   /   A or X for next ball"
        : "Track the rebound, then find the receiving lane";
    public int PracticeSuccesses {get;private set;}
    public bool ReceivingFeed=>ControlWarmup && ControlledRod<0 && (RecoveryPractice?
        BounceTime>=0 && !AttemptClosed:retentionStage<3 && ball.localPosition.z>=rods[1].localPosition.z-.025f);
    int retentionStage;
    bool trackingCarry;
    float carryStartX;
    public string RetentionTask
    {
        get
        {
            if(RecoveryPractice)return $"Rebound recovery   {Recoveries}/{Attempt}";
            switch (retentionStage)
            {
                case 0: return "1/3   Stop the ball under a blue defender (1 s)";
                case 1: return "2/3   Faster feed: keep the ball pinned (2 s)";
                case 2: return "3/3   Keep the pin and slide sideways (4 cm)";
                default: return "Practice complete   /   A or X to repeat";
            }
        }
    }
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
    int ownGoalCandidate=-1;
    public int OwnGoals {get;private set;}
    public int DeadBallResets {get;private set;}
    public enum BlueRole { AllRods, Defense, Attack }
    public BlueRole Role { get; private set; }

    public bool IsHumanRod(int index)
    {
        if (Spectator || !IsBlue(index)) return false;
        if (ControlWarmup || Role == BlueRole.AllRods) return true;
        bool defensive = index == 0 || index == 1;
        return Role == BlueRole.Defense ? defensive : !defensive;
    }

    public void SetRole(BlueRole role)
    {
        if (Role == role) return;
        Role = role;
        if (Spectator || ControlWarmup) return;
        hands?.ReleaseAll();
        BallControl.Release();
        AI.Reset();
    }

    public bool IsAutomatedRod(int index) => aiEnabled && !IsHumanRod(index);
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
    float ignoreUntil,sideRollUntil;
    AudioSource audioSource;AudioClip contactClip,goalClip,bankClip,dropClip;
    float serveProgress=-1, nextImpact;
    Vector3 deliveryVelocity;
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
        scoreRail=new BabyFootScoreRail(ball.parent);
        scoreRail.SetScore(0,0,false);
        var rb=ball.GetComponent<Rigidbody>();if(rb)rb.isKinematic=true;
        ballCollider=ball.GetComponent<SphereCollider>();
        var audioObject=new GameObject("Match Audio");audioObject.transform.SetParent(transform,false);audioSource=audioObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;audioSource.minDistance=.5f;audioSource.maxDistance=5;
        contactClip=Resources.Load<AudioClip>("BabyFootAudio/strike");goalClip=Resources.Load<AudioClip>("BabyFootAudio/net");
        bankClip=Resources.Load<AudioClip>("BabyFootAudio/bank");dropClip=Resources.Load<AudioClip>("BabyFootAudio/drop");
    }
        // Match sound assets are shared by all rods.
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
        if(!ControlWarmup)return;ControlWarmup=false;RecoveryPractice=false;aiEnabled=savedAI;
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
        EndControlWarmup();hands?.ReleaseAll();BlueScore=CoralScore=0;DeadBallResets=OwnGoals=0;scoreRail.SetScore(0,0,false);
        for(int i=0;i<rods.Length;i++)
        {SetRodPose(i,0,0);previousX[i]=previousAngle[i]=angularVelocity[i]=0;rodVelocity[i]=Vector3.zero;cooldown[i]=MatchTime;}
        AI.Reset(true);serveSign=UnityEngine.Random.value<.5f?-1:1;SetPaused(false);Serve();
    }
    public void BeginControlWarmup()=>BeginPractice(false);
    public void BeginRecoveryPractice()=>BeginPractice(true);
    void BeginPractice(bool recovery)
    {
        SetSpectatorMode(false);
        if(!ControlWarmup){savedAI=aiEnabled;ControlWarmup=true;}
        RecoveryPractice=recovery;Attempt=Recoveries=PracticeSuccesses=0;retentionStage=0;aiEnabled=false;SetPaused(false);
        for(int i=0;i<rods.Length;i++){SetRodPose(i,0,i==1?0:75);previousAngle[i]=Mathf.DeltaAngle(0,rods[i].localEulerAngles.x);previousX[i]=0;}
        Serve();
    }
    public void Serve()
    {
        ownGoalCandidate=-1;sideRollUntil=0;BallControl.Release(0);feedDelay=0;AI.Reset();deadTime=0;
        if(ControlWarmup)
        {
            if(RecoveryPractice)
            {
                Attempt++;feedSide=Attempt%2==1?1:-1;
                BounceTime=SecuredTime=-1;AttemptClosed=false;
                serveAt=-1;ignoreRod=-1;feedDelay=1.2f;launchTime=MatchTime+feedDelay;
                float rodZ=rods[1].localPosition.z;
                PlaceBall(new Vector3(feedSide*.265f,.775f,rodZ+.19f),Vector3.zero);
                RecoveryTarget=new Vector3(feedSide*.21f,.775f,rodZ);
                Message=RecoveryCue;return;
            }
            if(retentionStage==3)retentionStage=0;
            trackingCarry=false;
            serveAt=-1;ignoreRod=-1;feedDelay=1.2f;
            float x=rods[1].localPosition.x; x+=x>0?-.1175f:.1175f;
            PlaceBall(new Vector3(x,.775f,-.13f),Vector3.zero);
            RecoveryTarget=new Vector3(x,.775f,rods[1].localPosition.z);
            Message="Receive on the blue two-player rod / A or X repeats";return;
        }
        serveAt=-1;
        deliveryVelocity=new Vector3(UnityEngine.Random.Range(-.1f,.1f),0,.65f*serveSign);
        serveProgress=0;
        ball.localPosition=ServePosition(0);
        Velocity=Vector3.zero;
        ignoreRod=-1;Message="Serving";
    }
    static Vector3 ServePosition(float t)
    {
        float fall=Mathf.Min(1,t/.46f);
        float bounce=Mathf.Clamp01((t-.46f)/.36f);
        float height=.15f*(1-fall*fall)+.08f*bounce*(1-bounce);
        return new Vector3(.32f*(1-t),.775f+height,0);
    }
    public void PlaceBall(Vector3 localPosition,Vector3 velocity)
    {
        BallControl.Release(0);
        serveProgress=-1;localPosition.y=.775f;velocity.y=0;ball.localPosition=localPosition;Velocity=Vector3.ClampMagnitude(velocity,3.5f);
    }
    void Play(AudioClip clip,float volume=.65f){if(clip){audioSource.transform.position=ball.position;audioSource.pitch=1+.055f*Mathf.Sin((PlayerContacts+WallContacts)*2.4f);audioSource.PlayOneShot(clip,volume);}}
    public bool FootInStroke(int index,float from,float to)
    {
        return feet[index].Sweep(ball.position,Radius,from,to,out _,out _);
    }
    public bool TryStrike(int index,float from,float to,float dt)
    {
        float speed=(to-from)/Mathf.Max(dt,.001f);
        if(index==ignoreRod && MatchTime<ignoreUntil)return false;
        if(IsAutomatedRod(index)&&(!AI.IsStriking(index)||speed*(IsBlue(index)?-1:1)<0))return false; // Powered AI strokes only face the opponent goal.
        if(Mathf.Abs(speed)<180||MatchTime<cooldown[index]||serveAt>=0||serveProgress>=0)return false;
        // Keep the retained friction contact within the solver's millimetre contact skin.
        float contactRadius=Radius+(ControlledRod==index?.0015f:0);
        if(!feet[index].Sweep(ball.position,contactRadius,from,to,out float offset,out _))return false;
        var direction=BabyFootFootContact.Direction(offset,speed>0);
        BallControl.Release();
        Velocity=Vector3.ClampMagnitude(direction*Mathf.Clamp(Mathf.Abs(speed)*Mathf.Deg2Rad*.065f*1.5f,.35f,3.5f)+Vector3.right*(rodVelocity[index].x*.8f),3.5f);
        ownGoalCandidate=-1;
        cooldown[index]=MatchTime+.14f;ignoreRod=index;ignoreUntil=MatchTime+.09f;PlayerContacts++;
        if(IsAutomatedRod(index)){AIShots++;AI.OnStrike(index,speed,to);}else AI.OnPlayerContact(index);Play(contactClip);return true;
    }
    public void Simulate(float delta)
    {
        if(Paused||delta<=0)return;
        scoreRail.Step(delta);
        MatchTime+=delta;
        if(serveProgress>=0)
        {
            float previous=serveProgress;
            serveProgress=Mathf.Min(1,previous+delta/.52f);
            ball.localPosition=ServePosition(serveProgress);
            if(previous<.46f && serveProgress>=.46f) Play(dropClip,.44f);
            if(previous<.82f && serveProgress>=.82f) Play(dropClip,.16f);
            if(serveProgress>=1){PlaceBall(ServePosition(1),deliveryVelocity);Message="Ball in play";}
            return;
        }
        if(feedDelay>0){feedDelay-=delta;if(feedDelay<=0)Velocity=RecoveryPractice?new Vector3(feedSide*.54f,0,-.54f):new Vector3(0,0,retentionStage==1?-.8f:-.55f);}
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
        if(ControlWarmup)UpdateRetentionPractice();
        if(Spectator&&serveAt<0)
        {
            if(ControlledRod<0&&Vector3.Distance(ball.localPosition,lastMotionPosition)<.004f)deadTime+=delta;
            else{deadTime=0;lastMotionPosition=ball.localPosition;}
            if(deadTime>2.5f){DeadBallResets++;Serve();Message="Dead ball / new serve";}
        }
    }
    void UpdateRetentionPractice()
    {
        if(RecoveryPractice)
        {
            if(AttemptClosed)return;
            if(BounceTime>=0 && ControlledRod==1 && PossessionSecured)
            {
                SecuredTime=MatchTime;Recoveries++;PracticeSuccesses++;AttemptClosed=true;
            }
            else if(MatchTime-launchTime>=4)AttemptClosed=true;
            return;
        }
        if(retentionStage>=3)return;
        if(ControlledRod!=1 || !PossessionSecured)
        {
            trackingCarry=false;
            return;
        }
        if(retentionStage==2)
        {
            if(!trackingCarry)
            {
                carryStartX=ball.localPosition.x;
                trackingCarry=true;
            }
            if(Mathf.Abs(ball.localPosition.x-carryStartX)<.04f)return;
        }
        else if(BallControl.StableSeconds < retentionStage+1)return;
        PracticeSuccesses++;retentionStage++;
        if(retentionStage<3)Serve();
    }
    void FixedUpdate(){Simulate(Time.fixedDeltaTime);}
    // Approximate a shallow crowned strip in the unreachable gap between adjacent rows.
    // Its shoulders taper to a flat receiving area so a lateral pass stays on its row.
    float GapAcceleration(float z)
    {
        for(int i=1;i<rods.Length;i++)
        {
            float left=rods[i-1].localPosition.z,right=rods[i].localPosition.z;
            if(z<left || z>right)continue;
            float distance=Mathf.Min(z-left,right-z);
            float slope=Mathf.SmoothStep(0f,.38f,Mathf.InverseLerp(.038f,.056f,distance));
            return z<(left+right)*.5f?-slope:slope;
        }
        return 0;
    }
    void AdvanceBall(float dt)
    {
        var p=ball.localPosition;
        UpdateControl(p,dt);
        if(ControlledRod<0 && !ControlWarmup && MatchTime>=sideRollUntil)Velocity+=Vector3.forward*(GapAcceleration(p.z)*dt);
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
        if(Mathf.Abs(p.x)>side){p.x=Mathf.Sign(p.x)*(2*side-Mathf.Abs(p.x));float normalSpeed=Mathf.Abs(Velocity.x);
            float tangent=Mathf.MoveTowards(Velocity.z,0,Mathf.Min(Mathf.Abs(Velocity.z)*.04f,normalSpeed*.06f));
            Velocity=new Vector3(-Velocity.x*.88f,0,tangent);
            if(RecoveryPractice&&!AttemptClosed&&BounceTime<0)
            {
                BounceTime=MatchTime;
                if(Mathf.Abs(tangent)>.01f)
                    RecoveryTarget=new Vector3(p.x+Velocity.x*(rods[1].localPosition.z-p.z)/tangent,p.y,rods[1].localPosition.z);
            }
            WallContacts++;Impact(bankClip,Velocity.magnitude);AI.OnWallContact();}
        bool opening=Mathf.Abs(p.x)+Radius<=GoalHalf;
        if(Mathf.Abs(p.z)>GoalZ+Radius&&opening)
        {ball.localPosition=p;Goal(p.z>0);return;}
        float end=GoalZ-Radius;
        if(Mathf.Abs(p.z)>end&&!opening){ownGoalCandidate=-1;p.z=Mathf.Sign(p.z)*(2*end-Mathf.Abs(p.z));Velocity=new Vector3(Velocity.x*.98f,0,-Velocity.z*.85f);WallContacts++;Impact(bankClip,Velocity.magnitude);}
        // Damped rolling without vertical hops; the sphere remains on the playfield plane.
        Velocity=Vector3.MoveTowards(Velocity,Vector3.zero,.025f*dt);p.y=.775f;ball.localPosition=p;
        if(Mathf.Abs(p.x)>.5f||Mathf.Abs(p.z)>.8f)Serve();
    }
    void Goal(bool blue)
    {
        if(RecoveryPractice){AttemptClosed=true;Velocity=Vector3.zero;ball.localPosition=new Vector3(ball.localPosition.x,.775f,Mathf.Sign(ball.localPosition.z)*GoalZ);return;}
        BallControl.Release();
        if(ownGoalCandidate>=0 && IsBlue(ownGoalCandidate)!=blue)OwnGoals++;
        if(blue)BlueScore++;else CoralScore++;Velocity=Vector3.zero;serveSign=blue?1:-1;
        scoreRail.SetScore(BlueScore,CoralScore,true);
        Message=blue?(Spectator?"BLUE AI GOAL!":"BLUE GOAL!"):"RED AI GOAL!";serveAt=MatchTime+.7f;
        if(goalSparks){goalSparks.transform.position=ball.position;goalSparks.Play(true);}Play(goalClip);
    }
    bool CanControl(int i)=>IsAutomatedRod(i)||IsHumanRod(i)&&hands&&hands.IsHoldingRod(i);
    bool TryReceiveAt(int i,Vector3 p)
    {
        if(i==ignoreRod&&MatchTime<ignoreUntil)return false;
        if(!BallControl.TryReceive(i,rods[i],feet[i],p,Velocity,rodVelocity[i],angularVelocity[i],CanControl(i)))return false;
        ownGoalCandidate=-1;
        Velocity=rodVelocity[i]+(Velocity-rodVelocity[i])*.08f;return true;
    }
    void UpdateControl(Vector3 p,float dt)
    {
        int owner=ControlledRod;
        if(owner>=0)
        {
            Velocity=BallControl.Step(dt,rods[owner],feet[owner],p,Velocity,rodVelocity[owner],angularVelocity[owner],CanControl(owner));
            if(BallControl.SidePush)
            {
                // Allow the releasing toe to lift clear before another strike is eligible.
                ignoreRod=owner;ignoreUntil=MatchTime+.065f;sideRollUntil=MatchTime+.85f;
            }
        }
        else
        {
            BallControl.Step(dt,null,null,p,Velocity,Vector3.zero,0,false);
            for(int i=0;i<rods.Length;i++)
                if(TryReceiveAt(i,p))break;
        }
    }
    void Impact(AudioClip clip,float speed)
    {
        if(MatchTime<nextImpact||speed<.12f)return;
        nextImpact=MatchTime+.035f;
        Play(clip,Mathf.Clamp(speed*.38f,.10f,.85f));
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
        float incoming=Vector3.Dot(relative,normal);if(incoming<0)Impact(contactClip,-incoming);
        if(incoming>=0)return;
        float restitution=foot?Mathf.Lerp(.08f,.6f,Mathf.Clamp01(relative.magnitude/2.5f)):.82f;
        float priorZ=Velocity.z;
        Velocity=Vector3.ClampMagnitude(relative-(1+restitution)*incoming*normal+moving,3.5f);
        float ownDirection=IsBlue(i)?-1:1;
        if(Velocity.z*ownDirection<=0)ownGoalCandidate=-1;
        else if(priorZ*ownDirection<=0)ownGoalCandidate=i;
    }
}
