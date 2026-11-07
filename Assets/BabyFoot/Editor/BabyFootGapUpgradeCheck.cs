#if BABYFOOT_LEGACY_SCHEDULED_GAP_TESTS
// Historical v6 scheduled-window checks, superseded by reactive-defender tests.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootGapUpgradeCheck
{
    const string Report="Library/BabyFootGapUpgradeCheckV1.txt";
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static BabyFootSession s;static BabyFootDefense e;static BabyFootAttack a;static BabyFootPresentation p;
    static bool started;static int motionChecks,lastTrial=-1,stillFrames;static string lastBlock="";static double deadline;
    static readonly bool[] scoredSlots=new bool[3];
    static object Read(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Set(object o,string f,object value)=>o.GetType().GetField(f,Flags).SetValue(o,value);
    static void Assert(bool yes,string message){if(!yes)throw new Exception(message);}
    static BabyFootGapUpgradeCheck(){ if(!BabyFootLegacyAutoSetup.Enabled)return; EditorApplication.delayCall+=Prepare;}
    static void Prepare()
    {
        if(File.Exists(Report))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating){EditorApplication.delayCall+=Prepare;return;}
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        deadline=EditorApplication.timeSinceStartup+160;EditorApplication.update+=Tick;
        if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;
    }
    static void CheckMotion(int index)
    {
        int d=index%3;int seed=5100+index;
        float ballX=new[]{-.25f,0,.25f}[index/3%3];float speed=new[]{.85f,1.15f,1.5f}[d];float limit=new[]{10f,8f,6f}[d];
        var m=new BabyFootAttackMotion(seed,d,ballX,speed,limit,BabyFootAttack.StrikeContactDelay);
        var repeat=new BabyFootAttackMotion(seed,d,ballX,speed,limit,BabyFootAttack.StrikeContactDelay);
        Assert(m.Openings.Count>=2,"fewer than two scheduled opportunities");
        Assert(Mathf.Abs(m.OpportunitySeconds-new[]{1.2f,.7f,.38f}[d])<.0001f,"wrong window difficulty");
        for(float t=0;t<limit;t+=.01f)
        {
            var pose=m.Evaluate(t);var next=m.Evaluate(t+.001f);var same=repeat.Evaluate(t);
            Assert(Mathf.Abs(pose.line)<=.0851f && Mathf.Abs(pose.keeper)<=.0801f,"bar outside physical limits");
            Assert(Mathf.Max(Mathf.Abs(next.line-pose.line),Mathf.Abs(next.keeper-pose.keeper))/.001f<=m.MaxSpeed+.003f,"motion exceeds speed bound");
            Assert(pose.line==same.line && pose.keeper==same.keeper,"seeded movement not reproducible");
        }
        Assert(m.Knots.Zip(m.Knots.Skip(1),(x,y)=>x.line==y.line && x.keeper==y.keeper).Any(x=>x),"random motion has no pauses");
        foreach(var knot in m.Knots.Skip(1).Take(m.Knots.Count-2))
        {
            var x=m.Evaluate(knot.time-.0001);var y=m.Evaluate(knot.time+.0001);
            Assert(Mathf.Abs(x.line-y.line)<.0001f && Mathf.Abs(x.keeper-y.keeper)<.0001f,"motion jumps at direction change");
        }
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("upgrade check timed out");
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            s=UnityEngine.Object.FindFirstObjectByType<BabyFootSession>();if(!s)return;e=s.exercise;a=e.attack;p=e.trajectory.presentation;Application.runInBackground=true;
            if(motionChecks<90){CheckMotion(motionChecks++);return;}
            if(!started)
            {
                Assert(p.FeedbackReady,"audio feedback clips not loaded");
                for(int n=3;n<=30;n++)
                {
                    var slots=BabyFootSession.BuildStrikers(n,731);var counts=new[]{0,1,2}.Select(k=>slots.Count(v=>v==k)).ToArray();
                    Assert(counts.Max()-counts.Min()<=1 && slots.SequenceEqual(BabyFootSession.BuildStrikers(n,731)),"unbalanced/non-reproducible striker schedule");
                }
                s.exerciseSelection=2;s.difficulty=2;s.practiceTrials=3;s.measurementTrials=10;s.participantCode="TEST_GAP_V2";s.dataDirectoryOverride=Path.GetFullPath("Library/GapUpgradeChecks");
                Assert(s.StartSession(),"session failed");started=true;
            }
            string stage=Read(s,"stage").ToString();
            if(stage=="Practice" || stage=="Measurement")
            {
                if(!e.IsTrialActive){s.StartNextTrial();Assert(a.IsAiming,"trial failed");}
                int trial=(int)Read(s,"started");
                if(a.IsAiming && (trial!=lastTrial || stage!=lastBlock))
                {
                    lastTrial=trial;lastBlock=stage;
                    int kind=stage=="Practice"?0:(trial-1)%5;
                    float elapsed=kind==0?a.Motion.Openings[0].start+.1f:0;
                    Set(a,"readyTime",Time.realtimeSinceStartupAsDouble-elapsed);
                    var figure=a.AttackingRod.Cast<Transform>().Where(x=>x.name.StartsWith("Player ")).ElementAt(a.PlannedStriker);
                    float aligned=a.BallStartX-figure.localPosition.x;
                    var root=a.AttackingRod.localPosition;root.x=kind==2?0:kind==4?aligned-.02f:aligned;a.AttackingRod.localPosition=root;
                    if(kind==4)
                    {
                        // Isolate the wide-shot outcome with stationary clear defenders.
                        // Other trials use the real randomized moving schedule.
                        float row=Mathf.Lerp(a.BallStartX,.02f*16*(.5565f-.257f),(.375f-.257f)/(.5565f-.257f));
                        float best=-1,safe=0;
                        foreach(float x in new[]{-.085f,0,.085f})
                        {float gap=Mathf.Min(Mathf.Abs(row-x+.1175f),Mathf.Abs(row-x-.1175f));if(gap>best){best=gap;safe=x;}}
                        for(int k=0;k<a.Motion.Knots.Count;k++){var knot=a.Motion.Knots[k];knot.line=safe;knot.keeper=-.08f;a.Motion.Knots[k]=knot;}
                    }
                    if(kind==3){Set(a,"readyTime",Time.realtimeSinceStartupAsDouble-a.TimeLimit-.01);return;}
                    var before=e.ball.localPosition;int contactSounds=p.contactSoundCount;
                    Assert(a.Fire(),"attempt rejected");Assert(!a.Fire(),"double fire accepted");
                    Assert(a.IsStriking && e.ball.localPosition==before && a.ContactSeconds<0 && p.contactSoundCount==contactSounds,"ball/sound starts before foot stroke contact");
                    if(kind==0)Assert(a.PredictedGapOpen,"scheduled window is not collision-clear for forward "+a.PlannedStriker);
                    if(kind==2)Assert(!a.ContactMade,"misalignment did not miss ball");
                }
                if(a.IsStriking)
                {
                    double age=Time.realtimeSinceStartupAsDouble-(double)Read(a,"commandTime");
                    if(age<BabyFootAttack.StrikeContactDelay)
                    {Assert(e.ball.localPosition==(Vector3)Read(a,"ballStart"),"ball moved during windup");stillFrames++;}
                }
            }
            else if(stage=="Break")s.BeginMeasurement();
            else if(stage=="Summary")
            {
                string folder=(string)Read(s,"folder");var practice=File.ReadAllLines(Path.Combine(folder,"practice.csv"));var rows=File.ReadAllLines(Path.Combine(folder,"measurement.csv"));
                Assert(practice.Length==4 && rows.Length==11 && rows.All(x=>x.Split(',').Length==31),"upgraded CSV schema/block counts wrong");
                foreach(string row in practice.Skip(1))
                {var cells=row.Split(',');Assert(cells[18]=="goal","an outer forward cannot score: "+row);scoredSlots[int.Parse(cells[26])-1]=true;}
                Assert(scoredSlots.All(x=>x),"not all three forwards scored");
                string[] expected={"goal","blocked","miss","timeout","miss"};
                for(int i=0;i<10;i++)Assert(rows[i+1].Split(',')[18]==expected[i%5],"wrong upgraded outcome at trial "+(i+1)+": "+rows[i+1]);
                Assert(rows.Skip(1).Where(x=>x.Split(',')[14]=="true").All(x=>double.Parse(x.Split(',')[30],System.Globalization.CultureInfo.InvariantCulture)-double.Parse(x.Split(',')[10],System.Globalization.CultureInfo.InvariantCulture)>=BabyFootAttack.StrikeContactDelay-.002),"contact timestamp precedes animation");
                Assert(stillFrames>0 && p.contactSoundCount>=9 && p.airSoundCount>=2 && p.blockSoundCount>=2 && p.goalSoundCount>=5 && p.missSoundCount>=2,"contact/air/block/goal/miss feedback not distinguished");
                var meta=JsonUtility.FromJson<BabyFootSession.SessionMetadata>(File.ReadAllText(Path.Combine(folder,"session.json")));
                Assert(meta.goals==2 && meta.blocked==2 && meta.missed==4 && meta.timeouts==2 && meta.decisionCount==8 && meta.responseCount==0,"summary mixes practice or defensive timing");
                e.ConfigureExercise(BabyFootDefense.ExerciseMode.TrajectoryAnticipation);Assert(e.trajectory.VisibleRodCount==8,"anticipation restoration failed");Assert(e.trajectory.Prepare(.075f,false,0,true,1234),"rebound regression");
                e.ConfigureExercise(BabyFootDefense.ExerciseMode.ReactionDefense);Assert(e.trajectory.VisibleRodCount==2,"reaction restoration failed");
                End("PASS: 90 seeded motion profiles across three difficulty levels and forward positions; bounded slides/speeds, smooth changes and pauses, reproducibility, >=2 opportunities per profile, 1.20/0.70/0.38 s difficulty windows; balanced forward schedules; all three forwards scored in real moving-collider shots; command-to-contact windup keeps ball still and separates contact timestamp; all five feedback clips loaded and distinct contact/air/block/goal/wide playback calls exercised; 31-column CSV and measurement-only decision summary; goals/blocks/no-contact/wide/timeouts; existing mode restoration and random rebounds. Test files: Library/GapUpgradeChecks.");
            }
        }
        catch(Exception ex){End("FAIL: "+ex);}
    }
    static void End(string message){EditorApplication.update-=Tick;File.WriteAllText(Report,message);Debug.Log("BabyFoot gap upgrade: "+message);EditorApplication.isPlaying=false;}
}

#endif
