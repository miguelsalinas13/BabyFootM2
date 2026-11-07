using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Find the Gap: static ball, manually aligned three-player bar, moving defensive line/keeper.
// No gap markers or live path evaluation are presented to the participant.
public class BabyFootAttack : MonoBehaviour
{
    public BabyFootDefense exercise;
    public int difficulty=1;
    public Transform AttackingRod { get; private set; }
    public bool IsActive => aiming || striking || flying;
    public bool IsStriking => striking;
    public const float StrikeContactDelay=.085f;
    public int RequestedStriker=-1;
    public int PlannedStriker { get; private set; }
    public double ContactSeconds { get; private set; }=-1;
    public BabyFootAttackMotion Motion { get; private set; }
    public int WallBounces { get; private set; }
    public float ShotAngleDegrees { get; private set; }
    public float LaunchSpeed { get; private set; }
    public float WristSpeedDegrees { get; private set; }
    public float ShotRodX { get; private set; }
    BabyFootFootContact footContact;
    [NonSerialized] public bool unlimitedPractice;
    public Vector3 ShotVelocity => velocity;
    public const float WallRestitution=.90f;
    public bool IsAiming => aiming;
    public bool ShotFired { get; private set; }
    public int LastShotFrame { get; private set; }=-1;
    public bool ContactMade { get; private set; }
    public bool GapOpenAtShot { get; private set; }
    public bool PredictedGapOpen { get; private set; }
    public double DecisionSeconds { get; private set; }=-1;
    public double TrialDurationSeconds { get; private set; }
    public double ElapsedSeconds => IsActive?Time.realtimeSinceStartupAsDouble-readyTime:TrialDurationSeconds;
    public float AlignmentError { get; private set; }
    public float ShotTargetX { get; private set; }
    public float BallStartX => ballStart.x;
    public int TrialSeed { get; private set; }
    public string ReadyUtc { get; private set; }="";
    public string ShotUtc { get; private set; }="";
    public string Reason { get; private set; }="";
    public string Blocker { get; private set; }="";
    public string StrikerName { get; private set; }="";
    public float DefenderFrequency => 0; // aperiodic motion, retained for old metadata readers
    public float TimeLimit => unlimitedPractice?float.PositiveInfinity:new[]{10f,8f,6f}[Mathf.Clamp(difficulty,0,2)];
    public float RemainingSeconds => Mathf.Max(0,TimeLimit-(float)(Time.realtimeSinceStartupAsDouble-readyTime));
    Transform[] rods;
    Vector3[] originalPositions;
    Quaternion[] originalRotations;
    bool[] originalActive;
    Camera camera;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float cameraFov;
    readonly HashSet<Collider> defenders=new HashSet<Collider>();
    readonly RaycastHit[] hits=new RaycastHit[64];
    bool configured,aiming,striking,flying,goalEntryClear;
    int beginFrame;
    double readyTime,shotTime,shotElapsed,simulated;
    double commandTime;
    Vector3 ballStart,velocity;
    const float Radius=.0175f,GoalZ=-.5565f,GoalHalfWidth=.1025f;
    void Cache()
    {
        if(rods!=null)return;
        var list=new List<Transform>();foreach(Transform rod in exercise.goalkeeper.parent)list.Add(rod);
        list.Sort((a,b)=>a.localPosition.z.CompareTo(b.localPosition.z));rods=list.ToArray();
        originalPositions=new Vector3[rods.Length];originalRotations=new Quaternion[rods.Length];originalActive=new bool[rods.Length];
        for(int i=0;i<rods.Length;i++){originalPositions[i]=rods[i].localPosition;originalRotations[i]=rods[i].localRotation;originalActive[i]=rods[i].gameObject.activeSelf;}
        AttackingRod=rods[2];
        footContact=new BabyFootFootContact(AttackingRod);
        for(int i=0;i<2;i++)foreach(var c in rods[i].GetComponentsInChildren<Collider>(true))if(c.enabled && !c.isTrigger)defenders.Add(c);
        foreach(var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if(!cam.isActiveAndEnabled || UnityEngine.XR.XRSettings.isDeviceActive)continue;
            bool tracked=false;foreach(var c in cam.GetComponents<MonoBehaviour>())if(c && c.GetType().Name.Contains("TrackedPoseDriver"))tracked=true;
            if(!tracked){camera=cam;break;}
        }
    }
    public void Configure(bool on)
    {
        if(!on && rods==null)return;
        Cache(); Cancel();
        if(on && !configured && camera)
        {cameraPosition=camera.transform.position;cameraRotation=camera.transform.rotation;cameraFov=camera.fieldOfView;}
        exercise.trajectory.presentation.SetAttackingRod(null);
        for(int i=0;i<rods.Length;i++)
        {
            rods[i].localPosition=originalPositions[i];rods[i].localRotation=originalRotations[i];
            rods[i].gameObject.SetActive(on?i<=2:originalActive[i]);
        }
        if(on)
        {
            exercise.trajectory.presentation.SetAttackingRod(AttackingRod);
            exercise.ball.localPosition=new Vector3(0,.775f,-.257f);
            if(camera && !UnityEngine.XR.XRSettings.isDeviceActive)
            {
                camera.transform.position=exercise.ball.parent.TransformPoint(new Vector3(0,1.42f,.33f));
                camera.transform.LookAt(exercise.ball.parent.TransformPoint(new Vector3(0,.775f,-.40f)));camera.fieldOfView=50;
            }
        }
        else if(configured && camera && !UnityEngine.XR.XRSettings.isDeviceActive)
        {camera.transform.position=cameraPosition;camera.transform.rotation=cameraRotation;camera.fieldOfView=cameraFov;}
        configured=on;Physics.SyncTransforms();
    }
    public bool BeginTrial(int seed,float side)
    {
        if(IsActive)return false;
        if(!configured)Configure(true);
        TrialSeed=seed;var random=new System.Random(seed);
        PlannedStriker=RequestedStriker>=0?Mathf.Clamp(RequestedStriker,0,2):random.Next(3);
        Transform planned=null;int index=0;
        foreach(Transform figure in AttackingRod)if(figure.name.StartsWith("Player ")){if(index==PlannedStriker)planned=figure;index++;}
        float figureX=planned.localPosition.x;
        ballStart=new Vector3(figureX+(side<0?-1:1)*(float)(.035+random.NextDouble()*.030),.775f,-.257f);
        Motion=new BabyFootAttackMotion(seed^31991,difficulty);
        exercise.ball.localPosition=ballStart;AttackingRod.localPosition=originalPositions[2];
        ShotFired=ContactMade=GapOpenAtShot=PredictedGapOpen=false; goalEntryClear=false;ContactSeconds=-1; WallBounces=0;ShotAngleDegrees=0;
        DecisionSeconds=-1;TrialDurationSeconds=0;AlignmentError=0;ShotTargetX=ballStart.x;ShotUtc="";Reason=Blocker=StrikerName="";LaunchSpeed=0;WristSpeedDegrees=0;
        ReadyUtc=DateTime.UtcNow.ToString("O");readyTime=Time.realtimeSinceStartupAsDouble;beginFrame=Time.frameCount;
        aiming=true;striking=flying=false;SetDefenders(0);return true;
    }
    public void Cancel(){aiming=striking=flying=false;}
    void SetDefenders(double elapsed)
    {
        var pose=Motion!=null?Motion.AdvanceTo(elapsed,exercise.ball.localPosition,flying?velocity:Vector3.zero,flying):new BabyFootAttackMotion.Pose();
        var line=originalPositions[1];line.x=pose.line;rods[1].localPosition=line;
        var keeper=originalPositions[0];keeper.x=pose.keeper;rods[0].localPosition=keeper;
        rods[0].localRotation=originalRotations[0]; // goalkeeper never tilts
        Physics.SyncTransforms();
    }
    public bool Fire()
    {
        if(exercise.externalControls)return false;
        return CommitShot(exercise.shotSpeed,false);
    }
    public static float WristLaunchSpeed(float angularSpeedDegrees) => Mathf.Clamp(Mathf.Abs(angularSpeedDegrees)*Mathf.Deg2Rad*.065f*1.5f,.45f,3f);
    public bool StrikeFromController(float angularSpeedDegrees)
    {
        float angle=Mathf.DeltaAngle(0,AttackingRod.localEulerAngles.x);
        return TryControllerStrike(angle-4,angle,angularSpeedDegrees);
    }
    public bool TryControllerStrike(float from,float to,float speedDegrees)
    {
        if(!exercise.externalControls||!aiming||speedDegrees<35||to-from<.3f)return false;
        if(!footContact.Sweep(exercise.ball.position,Radius,from,to,out _,out _))return false;
        WristSpeedDegrees=speedDegrees;
        return CommitShot(WristLaunchSpeed(speedDegrees),true);
    }
    bool CommitShot(float speed,bool immediate)
    {
        if(!aiming)return false;
        double now=Time.realtimeSinceStartupAsDouble;
        if(now-readyTime>=TimeLimit){Complete("timeout","decision_timeout");return false;}
        SetDefenders(now-readyTime);
        ShotFired=true;LastShotFrame=Time.frameCount;ShotUtc=DateTime.UtcNow.ToString("O");DecisionSeconds=now-readyTime;
        ShotRodX=AttackingRod.localPosition.x;
        AlignmentError=float.MaxValue;
        foreach(Transform figure in AttackingRod)
        {
            if(!figure.name.StartsWith("Player "))continue;
            float error=ballStart.x-exercise.ball.parent.InverseTransformPoint(figure.position).x;
            if(Mathf.Abs(error)<Mathf.Abs(AlignmentError)){AlignmentError=error;StrikerName=figure.name;}
        }
        ContactMade=immediate||Mathf.Abs(AlignmentError)<=.022f;
        commandTime=now;shotElapsed=now-readyTime;simulated=0;
        LaunchSpeed=speed;
        velocity=ContactDirection(AlignmentError)*speed;
        ShotAngleDegrees=Mathf.Atan2(velocity.x,-velocity.z)*Mathf.Rad2Deg;
        ShotTargetX=BabyFootAttackMotion.FoldX(ballStart.x+velocity.x*((GoalZ-ballStart.z)/velocity.z));
        if(ContactMade)GapOpenAtShot=EvaluateClear();
        PredictedGapOpen=false; // no offline query may advance or control the live AI
        aiming=false;striking=true;flying=false;
        exercise.NotifyAttackShot(now);
        if(immediate)
        {
            // Controller crossed the physical strike plane: no automatic backswing or delay.
            striking=false;ContactSeconds=now-readyTime;exercise.NotifyAttackContact(ContactMade);
            if(!ContactMade){Complete("miss","no_ball_contact");return true;}
            shotTime=now;shotElapsed=now-readyTime;simulated=0;flying=true;
        }
        return true;
    }
    public static Vector3 ContactDirection(float offset)
    {
        return BabyFootFootContact.Direction(offset);
    }
    bool EvaluateClear()
    {
        // Frozen-pose diagnostic only. Trace the actual edge-driven direction and
        // side banks; never move defenders ahead in time to inspect future behavior.
        var position=ballStart;var direction=velocity.normalized;
        for(int bounce=0;bounce<5;bounce++)
        {
            float end=(GoalZ-position.z)/direction.z;
            float wall=Mathf.Abs(direction.x)>.00001f?((direction.x>0?.3225f:-.3225f)-position.x)/direction.x:float.PositiveInfinity;
            float length=Mathf.Min(end,wall);
            int count=Physics.SphereCastNonAlloc(exercise.ball.parent.TransformPoint(position),Radius,exercise.ball.parent.TransformDirection(direction),hits,length,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(defenders.Contains(hits[i].collider))return false;
            position+=direction*length;
            if(end<=wall)return Mathf.Abs(position.x)+Radius*Mathf.Sqrt(1+direction.x*direction.x/(direction.z*direction.z))<=GoalHalfWidth;
            direction.x=-direction.x;position+=direction*.00001f;
        }
        return false;
    }
    bool AdvanceBall(float dt)
    {
        var position=exercise.ball.localPosition;float remaining=dt;
        for(int contact=0;contact<4 && remaining>0;contact++)
        {
            float speed=velocity.magnitude;var direction=velocity/speed;
            float wallTime=Mathf.Abs(velocity.x)>.00001f?((velocity.x>0?.3225f:-.3225f)-position.x)/velocity.x:float.PositiveInfinity;
            float step=Mathf.Min(remaining,Mathf.Max(0,wallTime));
            int count=Physics.SphereCastNonAlloc(exercise.ball.parent.TransformPoint(position),Radius,exercise.ball.parent.TransformDirection(direction),hits,speed*step,~0,QueryTriggerInteraction.Ignore);
            float nearest=float.MaxValue;Collider blocker=null;
            for(int i=0;i<count;i++)if(defenders.Contains(hits[i].collider) && hits[i].distance<nearest){nearest=hits[i].distance;blocker=hits[i].collider;}
            if(count==hits.Length){Complete("blocked","collision_query_capacity");return false;}
            if(blocker)
            {
                exercise.ball.localPosition=position+direction*nearest;
                Blocker=blocker.transform.parent.parent.name+" / "+blocker.transform.parent.name;
                Complete("blocked","defender_contact");return false;
            }
            Vector3 to=position+velocity*step;
            // Stop a wide shot when its leading edge meets the solid end wall.
            float goalFront=GoalZ+Radius;
            if(position.z>=goalFront && to.z<goalFront)
            {
                var crossing=Vector3.Lerp(position,to,(goalFront-position.z)/(to.z-position.z));
                float impactX=position.x+velocity.x*((GoalZ-position.z)/velocity.z);
                float projectedRadius=Radius*Mathf.Sqrt(1+velocity.x*velocity.x/(velocity.z*velocity.z));
                goalEntryClear=Mathf.Abs(impactX)+projectedRadius<=GoalHalfWidth;
                if(!goalEntryClear){exercise.ball.localPosition=crossing;Complete("miss","end_wall");return false;}
            }
            if(to.z+Radius<GoalZ)
            {
                exercise.ball.localPosition=to;
                bool goal=goalEntryClear;
                Complete(goal?"goal":"miss",goal?"goal":"wide");return false;
            }
            position=to;remaining-=step;
            if(wallTime<=step+.0000001f)
            {
                position.x=Mathf.Clamp(position.x,-.3225f,.3225f);
                velocity=new Vector3(-velocity.x,velocity.y,velocity.z)*WallRestitution;
                WallBounces++;exercise.NotifyAttackWall();
                position.x+=Mathf.Sign(velocity.x)*.00001f;
            }
        }
        exercise.ball.localPosition=position;return true;
    }
    void Update()
    {
        if(!configured || !exercise.IsAttack || !IsActive)return;
        double now=Time.realtimeSinceStartupAsDouble;
        if(aiming)
        {
            SetDefenders(now-readyTime);
            var keyboard=exercise.externalControls?null:Keyboard.current;
            float input=keyboard==null?0:(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed?1:0);
            var p=AttackingRod.localPosition;p.x=Mathf.Clamp(p.x+input*exercise.barSpeed*((keyboard!=null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))?.18f:1)*Time.unscaledDeltaTime,-.09f,.09f);AttackingRod.localPosition=p;
            if(now-readyTime>=TimeLimit){Complete("timeout","decision_timeout");return;}
            if(Time.frameCount>beginFrame && keyboard!=null && keyboard.spaceKey.wasPressedThisFrame)Fire();
            return;
        }
        if(striking)
        {
            SetDefenders(now-readyTime);
            if(now-commandTime<StrikeContactDelay)return;
            striking=false;ContactSeconds=now-readyTime;
            exercise.NotifyAttackContact(ContactMade);
            if(!ContactMade){Complete("miss","no_ball_contact");return;}
            shotTime=now;shotElapsed=now-readyTime;simulated=0;flying=true;
        }
        double until=now-shotTime;
        // Real trajectory integration: wall contacts change the velocity; the AI
        // observes only current visible motion, with delayed bounded reactions.
        until=Math.Min(until,3);
        while(flying && simulated<until)
        {
            double nextTime=Math.Min(until,simulated+1.0/240);
            SetDefenders(shotElapsed+nextTime);
            if(!AdvanceBall((float)(nextTime-simulated)))return;
            simulated=nextTime;
        }
        if(flying && until>=3)Complete("miss","flight_timeout");
    }
    void Complete(string result,string reason)
    {
        aiming=striking=flying=false;Reason=reason;TrialDurationSeconds=Time.realtimeSinceStartupAsDouble-readyTime;
        exercise.NotifyAttackCompleted(result);
    }
}
