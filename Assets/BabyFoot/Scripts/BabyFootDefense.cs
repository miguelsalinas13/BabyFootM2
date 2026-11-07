using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class BabyFootDefense : MonoBehaviour
{
    public event Action TrialReset;
    public event Action<double> ShotLaunched;
    public event Action<string> TrialCompleted;
    public event Action<bool> AttackContact;
    public event Action AttackWall;
    public void NotifyAttackWall(){AttackWall?.Invoke();}
    public void NotifyAttackContact(bool made){AttackContact?.Invoke(made);}
    public Transform goalkeeper, ball;
    public Transform shotSpawn;
    public enum ExerciseMode { ReactionDefense, TrajectoryAnticipation, FindTheGap }
    public ExerciseMode exerciseMode;
    public BabyFootTrajectory trajectory;
    public BabyFootAttack attack;
    public bool IsAttack => exerciseMode==ExerciseMode.FindTheGap;
    [HideInInspector] public bool nextWallBounce;
    [HideInInspector] public bool nextPlayerBounce;
    [HideInInspector] public int nextTrajectorySeed;
    public double OcclusionOnsetSeconds { get; private set; }=-1;
    public bool occludeFinalSegment;
    public float occlusionFraction=.55f;
    public bool BallOccluded { get; private set; }
    public bool OcclusionObserved { get; private set; }
    Renderer[] ballRenderers;
    bool[] ballRendererEnabled;
    void SetBallOccluded(bool hidden)
    {
        if(ballRenderers==null)
        {
            ballRenderers=ball.GetComponentsInChildren<Renderer>(true);
            ballRendererEnabled=Array.ConvertAll(ballRenderers,r=>r.enabled);
        }
        for(int i=0;i<ballRenderers.Length;i++) if(ballRenderers[i]) ballRenderers[i].enabled=ballRendererEnabled[i] && !hidden;
        BallOccluded=hidden;
    }
    [HideInInspector] public int nextWallSide=1;
    public bool IsLongShot => exerciseMode==ExerciseMode.TrajectoryAnticipation && trajectory;
    public string PreparationError { get; private set; }="";
    public void ConfigureExercise(ExerciseMode mode)
    {
        CancelTrial();
        if(attack) attack.Configure(false);
        exerciseMode=mode;
        if(trajectory) trajectory.Configure(mode==ExerciseMode.TrajectoryAnticipation);
        ResetPositions();
        if(IsAttack) { EnsureAttack(); attack.Configure(true); }
    }
    public float barSpeed=.45f, lateralLimit=.09f, shotSpeed=.85f;
    public bool externalControls;
    public bool penalizeEarlyMovement;
    public bool PreShotMovement {get;private set;}
    public float KeeperXAtOnset {get;private set;}
    public bool ReactionEligible=>!PreShotMovement;
    [NonSerialized] public float externalGoalkeeperX;
    [HideInInspector] public bool sessionManaged;
    public bool IsTrialActive => IsAttack?(attack && attack.IsActive):(phase==Phase.Waiting || phase==Phase.Flying);
    public bool StimulusPresented { get; private set; }
    public double LastResponseSeconds => IsAttack?-1:response;
    public float LastTargetX => IsAttack && attack?attack.ShotTargetX:target;
    public float LastWaitSeconds { get; private set; }
    enum Phase { Ready, Waiting, Flying, Result }
    Phase phase;
    Vector3 start, velocity;
    double scheduled, onset, response=-1;
    int trial, saves, goals;
    string message="Press SPACE to prepare a shot", csv;
    float target;
    static string N(double v) => v.ToString("F6",CultureInfo.InvariantCulture);
    void EnsureAttack()
    {
        if(!attack) attack=GetComponent<BabyFootAttack>();
        if(!attack) attack=gameObject.AddComponent<BabyFootAttack>();
        attack.exercise=this;
    }
    public void NotifyAttackShot(double onset)
    { StimulusPresented=true; ShotLaunched?.Invoke(onset); }
    public void NotifyAttackCompleted(string result)
    { phase=Phase.Result; TrialCompleted?.Invoke(result); }
    void Awake()
    {
        EnsureAttack();
        var rb=ball.GetComponent<Rigidbody>(); if(rb) rb.isKinematic=true;
        ResetPositions();
    }
    void ResetPositions()
    {
        SetBallOccluded(false); OcclusionObserved=false; OcclusionOnsetSeconds=-1;
        TrialReset?.Invoke();
        goalkeeper.localPosition=new Vector3(0,.84f,-.5565f);
        if(IsLongShot) { ball.localPosition=trajectory.StartLocal; trajectory.ResetObservation(); }
        else if(shotSpawn) ball.position=shotSpawn.position;
        else ball.localPosition=new Vector3(0,.7725f,-.256f);
    }
    void Begin()
    {
        BeginTrial(UnityEngine.Random.value<.5f?-.075f:.075f);
    }
    public bool BeginTrial(float targetX)
    {
        if(IsTrialActive) return false;
        PreparationError="";
        if(IsAttack)
        {
            ResetPositions(); StimulusPresented=false; response=-1; LastWaitSeconds=0;
            return attack.BeginTrial(nextTrajectorySeed,targetX);
        }
        if(IsLongShot && !trajectory.Prepare(targetX,nextWallBounce,nextWallSide,nextPlayerBounce,nextTrajectorySeed)) { PreparationError=trajectory.Error; return false; }
        if(!sessionManaged && csv==null)
        {
            csv=Path.Combine(Application.persistentDataPath,"BabyFoot_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+".csv");
            File.WriteAllText(csv,"trial,target_x_m,speed_m_s,result,response_s,keeper_x_m,utc\n");
        }
        ResetPositions(); trial++; response=-1;PreShotMovement=false;KeeperXAtOnset=0;
        StimulusPresented=false;
        target=IsLongShot?trajectory.ActualTargetX:targetX;
        LastWaitSeconds=UnityEngine.Random.Range(.8f,2f);
        scheduled=Time.realtimeSinceStartupAsDouble+LastWaitSeconds;
        phase=Phase.Waiting; message="Ready: wait for the shot";
        return true;
    }
    public void CancelTrial()
    {
        if(attack) attack.Cancel();
        phase=Phase.Ready; ResetPositions();
    }
    void Update()
    {
        if(IsAttack) return;
        var keyboard=externalControls?null:Keyboard.current;
        if(!sessionManaged && keyboard!=null && keyboard.rKey.wasPressedThisFrame) { CancelTrial(); message="Press SPACE to prepare a shot"; return; }
        if(!sessionManaged && keyboard!=null && (phase==Phase.Ready || phase==Phase.Result) && keyboard.spaceKey.wasPressedThisFrame) Begin();
        float input=keyboard==null?0:(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed?1:0);
        if(phase==Phase.Waiting && Time.realtimeSinceStartupAsDouble>=scheduled)
        {
            onset=Time.realtimeSinceStartupAsDouble; start=ball.localPosition;
            KeeperXAtOnset=goalkeeper.localPosition.x;
            velocity=(new Vector3(target,start.y,-.5565f)-start).normalized*shotSpeed;
            phase=Phase.Flying; message="Defend the goal";
            StimulusPresented=true;
            // The visual strike starts on this exact shot event, with no wind-up or direction cue.
            ShotLaunched?.Invoke(onset);
        }
        if(phase==Phase.Waiting || phase==Phase.Flying)
        {
            var p=goalkeeper.localPosition; p.x=Mathf.Clamp(externalControls?externalGoalkeeperX:p.x+input*barSpeed*Time.unscaledDeltaTime,-lateralLimit,lateralLimit); goalkeeper.localPosition=p;
            if(phase==Phase.Waiting&&Mathf.Abs(p.x)>=.005f)
            {
                PreShotMovement=true;
                if(penalizeEarlyMovement){Finish("false_start");return;}
            }
            if(phase==Phase.Flying&&ReactionEligible&&Mathf.Abs(p.x-KeeperXAtOnset)>=.005f&&response<0)response=Time.realtimeSinceStartupAsDouble-onset;
        }
        if(phase!=Phase.Flying) return;
        // Deterministic trajectory and swept interval collision: avoids tunnelling at low FPS.
        Vector3 old=ball.localPosition;
        float travelled=shotSpeed*(float)(Time.realtimeSinceStartupAsDouble-onset);
        Vector3 next=IsLongShot?trajectory.Evaluate(travelled):start+velocity*(float)(Time.realtimeSinceStartupAsDouble-onset);
        if(IsLongShot)
        {
            trajectory.Observe(travelled);
            if(occludeFinalSegment && travelled>=trajectory.Length*Mathf.Clamp(occlusionFraction,.3f,.9f))
            { if(!OcclusionObserved) OcclusionOnsetSeconds=Time.realtimeSinceStartupAsDouble-onset; SetBallOccluded(true); OcclusionObserved=true; }
        }
        const float radius=.0175f, halfDepth=.0125f, halfWidth=.0115f;
        float front=goalkeeper.localPosition.z+halfDepth+radius;
        float back=goalkeeper.localPosition.z-halfDepth-radius;
        if(old.z>=back && next.z<=front)
        {
            float a=Mathf.Clamp01((old.z-front)/(old.z-next.z));
            float b=Mathf.Clamp01((old.z-back)/(old.z-next.z));
            float xa=(IsLongShot?trajectory.AtZ(Mathf.Lerp(old.z,next.z,a)).x:Mathf.Lerp(old.x,next.x,a))-goalkeeper.localPosition.x;
            float xb=(IsLongShot?trajectory.AtZ(Mathf.Lerp(old.z,next.z,b)).x:Mathf.Lerp(old.x,next.x,b))-goalkeeper.localPosition.x;
            if(Mathf.Min(xa,xb)<=halfWidth+radius && Mathf.Max(xa,xb)>=-halfWidth-radius)
            { ball.localPosition=IsLongShot?trajectory.AtZ(Mathf.Lerp(old.z,next.z,a)):Vector3.Lerp(old,next,a); saves++; Finish("save"); return; }
        }
        ball.localPosition=next;
        // Whole sphere passes the goal plane.
        if(next.z+radius<-.5565f) { goals++; Finish("goal"); }
    }
    void Finish(string result)
    {
        SetBallOccluded(false);
        phase=Phase.Result;
        message=result=="false_start"?"Movement before the shot: false start":result=="save"?"SAVE":"GOAL";
        if(response>=0) message+="  |  movement onset: "+(response*1000).ToString("F0")+" ms";
        message+="\nSPACE: next trial  /  R: reset position";
        try { if(!sessionManaged) File.AppendAllText(csv,trial+","+N(target)+","+N(shotSpeed)+","+result+","+(response>=0?N(response):"")+","+N(goalkeeper.localPosition.x)+","+DateTime.UtcNow.ToString("O")+"\n"); }
        catch(Exception e) { Debug.LogError("BabyFoot: could not save the trial: "+e.Message); }
        TrialCompleted?.Invoke(result);
    }
    void OnGUI()
    {
        if(sessionManaged || externalControls) return;
        var style=new GUIStyle(GUI.skin.box) { fontSize=18, alignment=TextAnchor.MiddleLeft, padding=new RectOffset(18,18,12,12) };
        GUI.Box(new Rect(16,16,Mathf.Min(720,Screen.width-32),135),"BABYFOOT  /  DEFENSE\nA-D or arrows: move goalkeeper  |  SPACE: start trial\n"+message+"\nSaves: "+saves+"    Goals: "+goals,style);
        GUI.enabled=phase==Phase.Ready || phase==Phase.Result;
        if(GUI.Button(new Rect(16,158,180,36),"Start trial")) Begin();
        GUI.enabled=true;
    }
}

