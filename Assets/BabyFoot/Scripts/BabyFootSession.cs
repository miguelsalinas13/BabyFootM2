using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

// Owns session progression and participant data; presentation remains event-driven.
public class BabyFootSession : MonoBehaviour
{
    public BabyFootDefense exercise;
    public int practiceTrials=5, measurementTrials=20;
    public string participantCode="P001";
    public int difficulty=1;
    public int exerciseSelection;
    public bool vrInterface;
    public string CurrentStage => stage.ToString();
    public string Feedback => feedback;
    public string Error => error;
    public int CompletedTrials => finished;
    public bool IsOpen => open;
    public string SummaryText => exercise.IsAttack ? $"Goals: {goals} | Blocked: {blocked} | Missed: {missed}" : $"Saves: {saves} | Goals: {goals}\nEarly movement: {preShotMovementTrials} | Responses: {responses.Count}";
    public void ReturnToSetup() { if(!open) { stage=Stage.Setup; error=""; } }
    readonly string[] exerciseNames={"Reaction Defense","Trajectory Anticipation","Find the Gap"};
    int[] practiceRoutes,measurementRoutes,practiceStrikers,measurementStrikers;
    [NonSerialized] public bool occlusionEnabled=true;
    [NonSerialized] public string dataDirectoryOverride;
    enum Stage { Setup, Practice, Break, Measurement, Summary }
    Stage stage;
    readonly float[] speeds={.65f,.85f,1.35f};
    readonly float[] attackSpeeds={.85f,1.15f,1.5f};
    readonly float[] anticipationSpeeds={1.05f,1.45f,1.85f};
    public float SelectedSpeed => (exerciseSelection==2?attackSpeeds:exerciseSelection==1?anticipationSpeeds:speeds)[Mathf.Clamp(difficulty,0,2)];
    readonly string[] difficulties={"Easy","Standard","Fast"};
    readonly List<double> responses=new List<double>();
    readonly List<double> decisions=new List<double>();
    int blocked,missed,timeouts,preShotMovementTrials;
    float[] practiceTargets, measurementTargets;
    int started, finished, saves, goals, falseStarts, noResponses, preparationRetries;
    bool pending, open;
    string folder, csv, error="", feedback="", sessionId;
    SessionMetadata metadata;
    Vector2 panelScroll;
    [Serializable] public class SessionMetadata
    {
        public string protocol="BabyFoot prototype v9", sessionId,participantCode,startUtc,endUtc,status,difficulty,exerciseMode;
        public string inputMode="desktop_keyboard";
        public string earlyMovementPolicy="allowed; timing excluded when movement >=5mm is observed before launch";
        public int preShotMovementTrials;
        public string vrAttackDefinition="VR attack v2: actual sphere-versus-oriented-foot-box swept contact over the wrist motion, no forced backswing or fixed-angle trigger. Minimum forward angular speed 35deg/s. Missed contact leaves aiming active for retry. Physical wrist speed sets launch power. Contact angle has 8mm straight dead zone, then quadratic edge deviation up to55deg; no automatic aiming. Planar ball model; hardware latency uncalibrated.";
        public string trajectoryConditions="Anticipation: at most 10% direct; remaining wall or seeded multi-contact player paths. Random static bar slides/angles; XZ swept-sphere collisions on actual player colliders, side-wall reflections; no spin or speed loss. Candidate routes are rejection-sampled to reachable outer goal targets with balanced planned signs; not unconstrained match physics. Layout can disclose visual cues.";
        public bool occlusionEnabled;
        public float visiblePathFraction=.55f;
        public string occlusionDefinition="When enabled in anticipation, hide ball renderer after 55% of path length; keep trajectory and collisions, reveal on result. Software sampling by frame.";
        public int seed,practiceTrials,measurementTrials,completedPractice,completedMeasurement,saves,goals,falseStarts,noResponses,responseCount;
        public float shotSpeed,barSpeed,lateralLimit,responseThresholdM=.005f,minimumWaitS=.8f,maximumWaitS=2f;
        public Vector3 shotSpawnLocal,goalkeeperStartLocal;
        public string responseDefinition="Software shot onset to goalkeeper displacement >= 5 mm; uncalibrated device latency.";
        public int blocked,missed,timeouts,decisionCount;
        public float defenderFrequencyHz,decisionTimeLimitS,guaranteedGapWindowS,defenderMaxSpeedMps;
        public string motionProfile="active_cover_sweep_and_delayed_interception";
        public string attackDefinition="Find the Gap v3: no automatic aiming. Centred impact goes straight; signed edge offset uses a softened power curve up to 55 degrees. Side-wall sphere-centre boundary ±0.3225 m, restitution 0.90. Solid end-wall miss ends attempt. Defenders sweep adjacent lanes while awaiting a shot, then track visible ball motion with delayed observations and bounded speed/acceleration; no scheduled openings or future alignment input. Legacy guaranteed-window/frequency/predicted-gap columns not applicable. Fine control: Shift+A/D.";
        public float defenderReactionDelayS,defenderMaxAccelerationMps2,wallRestitution=.90f;
        public string balanceDefinition="Planned measurement targets are balanced. Early movement is allowed by default and excluded from reaction timing. Optional strict policy consumes false-start trials.";
    }
    void OnEnable()
    {
        if(!exercise) return;
        exercise.sessionManaged=true;
        exercise.TrialCompleted+=OnTrialCompleted;
    }
    void OnDisable()
    {
        if(open) EndSession("interrupted");
        if(exercise) { exercise.TrialCompleted-=OnTrialCompleted; exercise.sessionManaged=false; }
    }
    void OnApplicationQuit() { if(open) EndSession("interrupted"); }
    void Update()
    {
        if(!vrInterface && Keyboard.current!=null && Keyboard.current.spaceKey.wasPressedThisFrame && !(exercise.IsAttack && (exercise.IsTrialActive || exercise.attack.LastShotFrame==Time.frameCount))) StartNextTrial();
    }
    static string N(double value) => value.ToString("F6",CultureInfo.InvariantCulture);
    public static float[] BuildTargets(int count,int seed)
    {
        var values=new float[count]; for(int i=0;i<count;i++) values[i]=i<count/2?-.075f:.075f;
        var random=new System.Random(seed);
        for(int i=count-1;i>0;i--) { int j=random.Next(i+1); float t=values[i]; values[i]=values[j]; values[j]=t; }
        return values;
    }
    public static int[] BuildRoutes(int count,int seed)
    {
        var values=new int[count]; int direct=count/10, wall=Mathf.Max(1,count/5);
        for(int i=0;i<count;i++) values[i]=i<direct?0:i<direct+wall?1:2;
        var random=new System.Random(seed);
        for(int i=count-1;i>0;i--) { int j=random.Next(i+1); int t=values[i]; values[i]=values[j]; values[j]=t; }
        return values;
    }
    public static int[] BuildStrikers(int count,int seed)
    {
        var values=new int[count];for(int i=0;i<count;i++)values[i]=i%3;
        var random=new System.Random(seed);
        for(int i=count-1;i>0;i--){int j=random.Next(i+1);int t=values[i];values[i]=values[j];values[j]=t;}
        return values;
    }
    public bool StartSession()
    {
        if(open || exercise.IsTrialActive) return false;
        participantCode=participantCode.Trim();
        if(!Regex.IsMatch(participantCode,"^[A-Za-z0-9_-]{1,24}$")) { error="Use an anonymous code: 1-24 letters, digits, '-' or '_'."; return false; }
        practiceTrials=Mathf.Clamp(practiceTrials,1,20);
        measurementTrials=Mathf.Clamp(measurementTrials,2,100); if(measurementTrials%2!=0) measurementTrials++;
        difficulty=Mathf.Clamp(difficulty,0,2); exercise.shotSpeed=SelectedSpeed;
        exerciseSelection=Mathf.Clamp(exerciseSelection,0,2);
        exercise.ConfigureExercise((BabyFootDefense.ExerciseMode)exerciseSelection);
        if(exercise.IsAttack) exercise.attack.difficulty=difficulty;
        exercise.occludeFinalSegment=occlusionEnabled && exercise.IsLongShot; exercise.occlusionFraction=.55f;
        sessionId=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
        int seed=Guid.NewGuid().GetHashCode();
        practiceTargets=BuildTargets(practiceTrials,seed^113);
        measurementTargets=BuildTargets(measurementTrials,seed);
        practiceRoutes=BuildRoutes(practiceTrials,seed^719);
        measurementRoutes=BuildRoutes(measurementTrials,seed^631);
        practiceStrikers=BuildStrikers(practiceTrials,seed^157);measurementStrikers=BuildStrikers(measurementTrials,seed^293);
        string dataRoot=string.IsNullOrEmpty(dataDirectoryOverride)?Path.Combine(Application.persistentDataPath,"Sessions"):dataDirectoryOverride;
        folder=Path.Combine(dataRoot,participantCode+"_"+sessionId);
        metadata=new SessionMetadata {sessionId=sessionId,participantCode=participantCode,startUtc=DateTime.UtcNow.ToString("O"),status="in_progress",seed=seed,difficulty=difficulties[difficulty],practiceTrials=practiceTrials,measurementTrials=measurementTrials,shotSpeed=exercise.shotSpeed,barSpeed=exercise.barSpeed,lateralLimit=exercise.lateralLimit,shotSpawnLocal=exercise.shotSpawn?exercise.shotSpawn.localPosition:exercise.ball.localPosition,goalkeeperStartLocal=new Vector3(0,.84f,-.5565f)};
        if(exercise.IsAttack){ metadata.defenderFrequencyHz=exercise.attack.DefenderFrequency;metadata.decisionTimeLimitS=exercise.attack.TimeLimit;metadata.guaranteedGapWindowS=0;metadata.defenderMaxSpeedMps=new[]{.23f,.34f,.46f}[difficulty];metadata.defenderReactionDelayS=new[]{.34f,.24f,.16f}[difficulty];metadata.defenderMaxAccelerationMps2=new[]{.8f,1.3f,1.9f}[difficulty]; }
        metadata.inputMode=vrInterface?(exercise.IsAttack?"vr_grip_slide_wrist_strike":"vr_controller_grip_1_to_1_slide"):"desktop_keyboard";
        if(vrInterface) metadata.responseDefinition=exercise.IsAttack?"Decision time: trial ready to forward wrist crossing; actual speed in trial shot_speed_m_s. Software sampling; uncalibrated controller/headset/Air Link latency.":"Software shot onset to goalkeeper displacement >= 5 mm, controller grip with 1:1 axial hand displacement; headset/controller/Air Link latency uncalibrated. barSpeed is a desktop parameter, not a VR speed cap.";
        metadata.earlyMovementPolicy=exercise.penalizeEarlyMovement?"strict false-start penalty":"allowed; continue trial, exclude pre-movement trials from response-time statistics";
        if(!exercise.IsAttack)metadata.responseDefinition="Shot onset to >=5mm displacement from keeper position at launch; exclude trials with pre-launch movement >=5mm. Software timing; input/hardware latency uncalibrated.";
        metadata.occlusionEnabled=exercise.occludeFinalSegment;
        metadata.exerciseMode=exerciseNames[exerciseSelection];
        if(exercise.IsLongShot) metadata.shotSpawnLocal=exercise.trajectory.StartLocal;
        try
        {
            Directory.CreateDirectory(folder);
            string header="session_id,participant_code,block,trial,target_x_m,speed_m_s,wait_s,result,stimulus_presented,response_s,keeper_x_m,bar_speed_m_s,threshold_m,utc,exercise_mode,route_kind,wall_side,start_x_m,start_z_m,path_length_m,planned_bounce_s,bounce_observed,keeper_error_m,deflector,deflector_x_m,deflector_z_m,deflector_rod_offset_m,contact_normal_x,occlusion_enabled,visible_path_fraction,occlusion_observed,trial_seed,player_bounces,wall_bounces,observed_contacts,occlusion_onset_s,generation_ms,contact_trace\n";
            if(exercise.IsAttack) header="session_id,participant_code,block,trial,exercise_mode,difficulty,trial_seed,ready_utc,shot_utc,trial_duration_s,decision_s,attacker_x_m,ball_start_x_m,alignment_error_m,shot_attempted,ball_contact,gap_open_at_shot,predicted_gap_open_at_arrival,result,reason,blocker,shot_speed_m_s,defender_frequency_hz,time_limit_s,shot_target_x_m,striker,planned_striker,motion_profile,guaranteed_gap_window_s,defender_max_speed_m_s,contact_s,launch_angle_deg,wall_bounces,defender_reaction_delay_s,defender_max_acceleration_m_s2,defender_line_target_m,keeper_target_m\n";
            if(exercise.IsAttack && vrInterface) header=header.TrimEnd('\n')+",wrist_speed_deg_s,input_mode\n";
            if(!exercise.IsAttack)header=header.TrimEnd('\n')+",pre_shot_movement,keeper_x_at_onset_m,reaction_eligible\n";
            File.WriteAllText(Path.Combine(folder,"practice.csv"),header);
            File.WriteAllText(Path.Combine(folder,"measurement.csv"),header);
            WriteMetadata();
        }
        catch(Exception e) { error="Could not create session files: "+e.Message; return false; }
        open=true; error=""; decisions.Clear();blocked=missed=timeouts=preShotMovementTrials=0; responses.Clear(); saves=goals=falseStarts=noResponses=0;
        started=finished=preparationRetries=0; pending=false; stage=Stage.Practice; feedback="Practice only. These results do not enter the final summary.";
        csv=Path.Combine(folder,"practice.csv"); exercise.CancelTrial(); return true;
    }
    public void StartNextTrial()
    {
        if(!open || pending || (stage!=Stage.Practice && stage!=Stage.Measurement)) return;
        var targets=stage==Stage.Practice?practiceTargets:measurementTargets;
        if(started>=targets.Length) return;
        var routes=stage==Stage.Practice?practiceRoutes:measurementRoutes;
        if(exercise.IsAttack)exercise.attack.RequestedStriker=(stage==Stage.Practice?practiceStrikers:measurementStrikers)[started];
        exercise.nextTrajectorySeed=unchecked(metadata.seed+started*7919+preparationRetries*104723+(stage==Stage.Practice?104729:0));
        exercise.nextWallBounce=routes[started]==1;
        exercise.nextPlayerBounce=routes[started]==2;
        exercise.nextWallSide=(started+metadata.seed)%2==0?1:-1;
        if(exercise.BeginTrial(targets[started])) { preparationRetries=0; started++; pending=true; feedback=exercise.IsAttack?"Align a player with the ball. Choose your moment and press SPACE.":"Wait for the shot. You may reposition before the shot."; }
        else { preparationRetries++; feedback="Could not prepare trial: "+exercise.PreparationError; }
    }
    public void BeginMeasurement()
    {
        if(!open || stage!=Stage.Break) return;
        started=finished=preparationRetries=0; pending=false; feedback="Ready for measurement. Press SPACE when ready.";
        csv=Path.Combine(folder,"measurement.csv"); stage=Stage.Measurement; exercise.CancelTrial();
    }
    void OnTrialCompleted(string outcome)
    {
        if(!open || !pending) return;
        pending=false;
        if(!Record(outcome)) return;
        finished++;
        feedback=outcome=="save"?"SAVE":outcome=="goal"?"GOAL":outcome=="blocked"?"BLOCKED":outcome=="miss"?"MISS - "+exercise.attack.Reason:outcome=="timeout"?"TIME OUT":"FALSE START - movement before the shot";
        if(stage==Stage.Practice)
        {
            metadata.completedPractice=finished;
            if(finished>=practiceTrials) { stage=Stage.Break; feedback="Practice complete."; }
        }
        else
        {
            metadata.completedMeasurement=finished;
            if(outcome=="save") saves++; else if(outcome=="goal") goals++; else if(outcome=="false_start") falseStarts++;
            if(exercise.IsAttack)
            {
                if(outcome=="blocked")blocked++;else if(outcome=="miss")missed++;else if(outcome=="timeout")timeouts++;
                if(exercise.attack.ShotFired)decisions.Add(exercise.attack.DecisionSeconds);
            }
            if(exercise.StimulusPresented && exercise.LastResponseSeconds>=0) responses.Add(exercise.LastResponseSeconds);
            if(!exercise.IsAttack && exercise.PreShotMovement)preShotMovementTrials++;
            if(!exercise.IsAttack && exercise.ReactionEligible && exercise.StimulusPresented && exercise.LastResponseSeconds<0) noResponses++;
            if(finished>=measurementTrials) { EndSession("completed"); return; }
        }
        try { WriteMetadata(); } catch(Exception e) { StorageError(e); }
    }
    bool Record(string outcome)
    {
        if(exercise.IsAttack) return RecordAttack(outcome);
        string block=stage==Stage.Practice?"practice":"measurement";
        string response=exercise.StimulusPresented && exercise.LastResponseSeconds>=0?N(exercise.LastResponseSeconds):"";
        string row=string.Join(",",sessionId,participantCode,block,started.ToString(CultureInfo.InvariantCulture),N(exercise.LastTargetX),N(exercise.shotSpeed),N(exercise.LastWaitSeconds),outcome,exercise.StimulusPresented?"true":"false",response,N(exercise.goalkeeper.localPosition.x),N(exercise.barSpeed),"0.005000",DateTime.UtcNow.ToString("O"))+"\n";
        bool longShot=exercise.IsLongShot;
        var origin=longShot?exercise.trajectory.StartLocal:metadata.shotSpawnLocal;
        float length=longShot?exercise.trajectory.Length:Vector3.Distance(origin,new Vector3(exercise.LastTargetX,origin.y,-.5565f));
        string bounce=longShot && exercise.trajectory.BounceDistance>=0?N(exercise.trajectory.BounceDistance/exercise.shotSpeed):"";
        string positionalError=exercise.StimulusPresented?N(Mathf.Abs(exercise.goalkeeper.localPosition.x-exercise.LastTargetX)):"";
        row=row.TrimEnd('\n')+","+string.Join(",",metadata.exerciseMode,longShot?exercise.trajectory.RouteKind:"direct",longShot?exercise.trajectory.WallSide:0,N(origin.x),N(origin.z),N(length),bounce,longShot && exercise.trajectory.BounceObserved?"true":"false",positionalError)+"\n";
        var route=exercise.trajectory;
        bool player=longShot && route.PlayerBounceCount>0;
        row=row.TrimEnd('\n')+","+string.Join(",",player?route.DeflectorName:"",player?N(route.DeflectorLocal.x):"",player?N(route.DeflectorLocal.z):"",player?N(route.DeflectorRodOffset):"",player?N(route.ContactNormal.x):"",metadata.occlusionEnabled?"true":"false",metadata.occlusionEnabled?N(exercise.occlusionFraction):"",exercise.OcclusionObserved?"true":"false")+"\n";
        row=row.TrimEnd('\n')+","+string.Join(",",exercise.nextTrajectorySeed,longShot?route.PlayerBounceCount:0,longShot?route.WallBounceCount:0,longShot?route.ObservedContacts:0,exercise.OcclusionOnsetSeconds>=0?N(exercise.OcclusionOnsetSeconds):"",longShot?N(route.GenerationMilliseconds):"",longShot?route.ContactTrace:"")+"\n";
        row=row.TrimEnd('\n')+","+(exercise.PreShotMovement?"true":"false")+","+N(exercise.KeeperXAtOnset)+","+(exercise.ReactionEligible?"true":"false")+"\n";
        try { File.AppendAllText(csv,row); return true; }
        catch(Exception e) { StorageError(e); return false; }
    }
    bool RecordAttack(string outcome)
    {
        var a=exercise.attack;
        string row=string.Join(",",sessionId,participantCode,stage==Stage.Practice?"practice":"measurement",started,metadata.exerciseMode,metadata.difficulty,a.TrialSeed,a.ReadyUtc,a.ShotUtc,N(a.ElapsedSeconds),a.ShotFired?N(a.DecisionSeconds):"",N(a.ShotFired?a.ShotRodX:a.AttackingRod.localPosition.x),N(a.BallStartX),a.ShotFired?N(a.AlignmentError):"",a.ShotFired?"true":"false",a.ContactMade?"true":"false",a.ContactMade?(a.GapOpenAtShot?"true":"false"):"","",outcome,outcome=="aborted"?"session_interrupted":a.Reason,a.Blocker,N(a.ShotFired?a.LaunchSpeed:exercise.shotSpeed),"",N(a.TimeLimit),a.ContactMade?N(a.ShotTargetX):"",a.ShotFired?a.StrikerName:"",a.PlannedStriker+1,a.Motion.Profile,"",N(a.Motion.MaxSpeed),a.ContactSeconds>=0?N(a.ContactSeconds):"",a.ShotFired?N(a.ShotAngleDegrees):"",a.WallBounces,N(a.Motion.ReactionDelay),N(a.Motion.MaxAcceleration),N(a.Motion.TargetLine),N(a.Motion.TargetKeeper))+"\n";
        if(vrInterface) row=row.TrimEnd('\n')+","+(a.ShotFired?N(a.WristSpeedDegrees):"")+",vr_grip_slide_wrist_strike\n";
        try{File.AppendAllText(csv,row);return true;}catch(Exception e){StorageError(e);return false;}
    }
    static double Average(List<double> values){double sum=0;foreach(var v in values)sum+=v;return values.Count>0?sum/values.Count:0;}
    void StorageError(Exception e)
    {
        error="Data storage failed. Session stopped: "+e.Message; open=false; pending=false;
        if(metadata!=null) { metadata.status="storage_error"; metadata.endUtc=DateTime.UtcNow.ToString("O"); }
        if(exercise) exercise.CancelTrial(); stage=Stage.Summary; Debug.LogError(error);
    }
    public void EndSession(string status="aborted")
    {
        if(!open) return;
        if(pending)
        {
            pending=false; if(!Record("aborted")) return;
        }
        open=false;
        if(exercise && status!="completed") exercise.CancelTrial();
        metadata.status=status; metadata.endUtc=DateTime.UtcNow.ToString("O");
        metadata.saves=saves; metadata.goals=goals; metadata.falseStarts=falseStarts; metadata.noResponses=noResponses; metadata.responseCount=responses.Count;metadata.preShotMovementTrials=preShotMovementTrials;metadata.blocked=blocked;metadata.missed=missed;metadata.timeouts=timeouts;metadata.decisionCount=decisions.Count;
        stage=Stage.Summary;
        try
        {
            WriteMetadata();
            string mean=responses.Count>0?N(MeanResponse()):"", median=responses.Count>0?N(MedianResponse()):"";
            File.WriteAllText(Path.Combine(folder,"summary.csv"),"session_id,status,completed_measurement,planned_measurement,saves,goals,false_starts,no_responses,response_count,mean_response_s,median_response_s,blocked,missed,timeouts,decision_count,mean_decision_s\n"+string.Join(",",sessionId,status,metadata.completedMeasurement,measurementTrials,saves,goals,falseStarts,noResponses,responses.Count,mean,median,blocked,missed,timeouts,decisions.Count,decisions.Count>0?N(Average(decisions)):"")+"\n");
        }
        catch(Exception e) { error="Could not finalize session: "+e.Message; Debug.LogError(error); }
    }
    void WriteMetadata() { File.WriteAllText(Path.Combine(folder,"session.json"),JsonUtility.ToJson(metadata,true)); }
    double MeanResponse() { double sum=0; foreach(double r in responses) sum+=r; return sum/responses.Count; }
    double MedianResponse() { var list=new List<double>(responses); list.Sort(); int m=list.Count/2; return list.Count%2==0?(list[m-1]+list[m])*.5:list[m]; }
    void OnGUI()
    {
        if(vrInterface) return;
        var old=GUI.color;
        var panel=new GUIStyle(GUI.skin.box) {padding=new RectOffset(22,22,18,18)};
        var title=new GUIStyle(GUI.skin.label) {fontSize=24,fontStyle=FontStyle.Bold,wordWrap=true};
        var text=new GUIStyle(GUI.skin.label) {fontSize=17,wordWrap=true};
        var button=new GUIStyle(GUI.skin.button) {fontSize=17,padding=new RectOffset(12,12,10,10)};
        float width=Mathf.Min(exercise.IsAttack && stage!=Stage.Setup && stage!=Stage.Break && stage!=Stage.Summary?360:560,Screen.width-32);
        bool full=stage==Stage.Setup || stage==Stage.Break || stage==Stage.Summary;
        GUILayout.BeginArea(new Rect(full?(Screen.width-width)*.5f:16,full?Mathf.Max(16,(Screen.height-500)*.5f):16,width,Mathf.Min(full?550:290,Screen.height-32)),panel);
        panelScroll=GUILayout.BeginScrollView(panelScroll);
        GUILayout.Label("BABYFOOT / TRAINING",title);
        if(stage==Stage.Setup)
        {
            GUILayout.Label("Exercise",text);
            int next=GUILayout.SelectionGrid(exerciseSelection,exerciseNames,1,button);
            if(next!=exerciseSelection) { exerciseSelection=next; exercise.ConfigureExercise((BabyFootDefense.ExerciseMode)next); }
            GUILayout.Label(exerciseSelection==2?"Centred contact shoots straight; edge contact angles the ball. Use side-wall banks to beat reactive defenders.":exerciseSelection==1?"Mostly random player rebounds, including multiple contacts. Occasional wall shots; direct shots limited to 10%.":"Short direct shots. Only goalkeeper and attacking rod are active.",text);
            if(exerciseSelection==1)
            {
                GUILayout.Label("Ball visibility",text);
                occlusionEnabled=GUILayout.SelectionGrid(occlusionEnabled?1:0,new[]{"Fully visible","Hide final 45%"},2,button)==1;
                GUILayout.Label(occlusionEnabled?"Occlusion ON: predict the final section after the ball disappears.":"Occlusion OFF: ball remains visible for the whole shot.",text);
            }
            GUILayout.Label("Anonymous participant code (do not enter a name)",text);
            participantCode=GUILayout.TextField(participantCode,24,new GUIStyle(GUI.skin.textField){fontSize=20});
            GUILayout.Space(12); GUILayout.Label("Difficulty",text);
            difficulty=GUILayout.SelectionGrid(difficulty,difficulties,3,button);
            GUILayout.Label("Shot speed: "+SelectedSpeed.ToString("F2",CultureInfo.InvariantCulture)+" m/s",text);
            GUILayout.Space(8); GUILayout.Label("Practice trials",text);
            int[] practices={3,5,10}; int pi=Array.IndexOf(practices,practiceTrials); if(pi<0) pi=1;
            practiceTrials=practices[GUILayout.SelectionGrid(pi,new[]{"3","5","10"},3,button)];
            GUILayout.Label("Measurement trials",text);
            int[] blocks={10,20,30}; int bi=Array.IndexOf(blocks,measurementTrials); if(bi<0) bi=1;
            measurementTrials=blocks[GUILayout.SelectionGrid(bi,new[]{"10","20","30"},3,button)];
            GUILayout.Space(10); GUILayout.Label(exerciseSelection==2?"A/D: move bar. Shift+A/D: fine control. SPACE: shoot or prepare next trial.\nPractice and measurement are saved separately.":"A/D or arrows: move goalkeeper. Wait for the ball to move.\nPractice and measurement are saved separately.",text);
            if(GUILayout.Button("Start practice session",button)) StartSession();
        }
        else if(stage==Stage.Break)
        {
            GUILayout.Label(exerciseNames[exerciseSelection],text);
            GUILayout.Label("Practice complete",title);
            GUILayout.Label(exercise.IsAttack?"The next block is measurement. A/D: align attacking bar. SPACE: shoot during a trial; prepare next trial after a result.":"The next block is measurement. Early movement is allowed; those trials are excluded from reaction-time statistics.\n\nA/D or arrows: move goalkeeper. SPACE: start the next trial.",text);
            if(GUILayout.Button("Begin measurement block",button)) BeginMeasurement();
            if(GUILayout.Button("End session",button)) EndSession();
        }
        else if(stage==Stage.Summary)
        {
            GUILayout.Label(exerciseNames[exerciseSelection],text);
            GUILayout.Label("Session "+(metadata!=null?metadata.status:"stopped"),title);
            if(exercise.IsAttack) GUILayout.Label("Measurement: "+(metadata!=null?metadata.completedMeasurement:0)+" / "+measurementTrials+" trials\nGoals scored: "+goals+"    Blocked: "+blocked+"\nMissed: "+missed+"    Time outs: "+timeouts,text);
            else GUILayout.Label("Measurement: "+(metadata!=null?metadata.completedMeasurement:0)+" / "+measurementTrials+" trials\nSaves: "+saves+"    Goals: "+goals+"\nFalse starts: "+falseStarts+"    No movement response: "+noResponses,text);
            if(exercise.IsAttack){if(decisions.Count>0)GUILayout.Label("Mean decision time: "+Average(decisions).ToString("F2")+" s (n="+decisions.Count+")",text);}
            else if(responses.Count>0) GUILayout.Label("Mean response: "+(MeanResponse()*1000).ToString("F0")+" ms\nMedian response: "+(MedianResponse()*1000).ToString("F0")+" ms  (n="+responses.Count+")",text);
            else GUILayout.Label("No valid post-shot movement responses recorded.",text);
            GUILayout.Label("Practice excluded. Software timing; hardware latency not calibrated.",text);
            GUILayout.Label("Files: "+folder,new GUIStyle(text){fontSize=13});
            if(GUILayout.Button("New session",button)) { stage=Stage.Setup; error=""; }
        }
        else
        {
            GUILayout.Label(exerciseNames[exerciseSelection],text);
            GUILayout.Label((stage==Stage.Practice?"PRACTICE":"MEASUREMENT")+"  "+finished+" / "+(stage==Stage.Practice?practiceTrials:measurementTrials)+" completed",text);
            if(exercise.IsAttack) GUILayout.Label(exercise.IsTrialActive?(exercise.attack.IsAiming?"Align and shoot | "+exercise.attack.RemainingSeconds.ToString("F1")+" s remaining":(exercise.attack.IsStriking?"Striking":"Shot in flight")):feedback,text);
            else GUILayout.Label(exercise.IsTrialActive?(exercise.StimulusPresented?(exercise.BallOccluded?"BALL HIDDEN - predict the interception":"Defend the goal"):"Ready - wait for the shot"):feedback,text);
            GUILayout.Label(exercise.IsAttack?"A/D: bar | Shift: fine | SPACE: shoot / next":"A/D or arrows: move  |  SPACE: next trial",text);
            if(exercise.IsAttack && exercise.attack.IsAiming && GUILayout.Button("Shoot",button))exercise.attack.Fire();
            GUI.enabled=!pending;
            if(GUILayout.Button("Start next trial",button)) StartNextTrial();
            GUI.enabled=true;
            if(GUILayout.Button("End session",button)) EndSession();
        }
        if(!string.IsNullOrEmpty(error)) { GUI.color=new Color(1,.6f,.5f); GUILayout.Label(error,text); }
        GUI.color=old; GUILayout.EndScrollView(); GUILayout.EndArea();
    }
}
