using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootReactiveAttackCheck
{
    const string Report="Library/BabyFootReactiveAttackCheckV2.txt";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static BabyFootDefense e;static BabyFootAttack a;static BabyFootSession s;
    static int phase,caseIndex;static bool launched,sessionStarted;static string outcome="";
    static double deadline;
    static object Read(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static void Property(object o,string name,object value)=>Set(o,"<"+name+">k__BackingField",value);
    static void Assert(bool yes,string text){if(!yes)throw new Exception(text);}
    static BabyFootReactiveAttackCheck(){ if(!BabyFootLegacyAutoSetup.Enabled)return; EditorApplication.delayCall+=Prepare;}
    static void Prepare()
    {
        if(File.Exists(Report))return;
        if(EditorApplication.isUpdating || EditorApplication.isCompiling){EditorApplication.delayCall+=Prepare;return;}
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Tick;
        if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;
    }
    static void TestTracking()
    {
        foreach(int d in new[]{0,1,2})
        {
            var m=new BabyFootAttackMotion(445,d);var other=new BabyFootAttackMotion(445,d);
            var ball=new Vector3(.02f,.775f,-.257f);var initial=m.Current;
            for(int i=1;i<=480;i++)
            {
                var before=m.Current;var pose=m.AdvanceTo(i/240.0,ball,Vector3.zero,false);var repeat=other.AdvanceTo(i/240.0,ball,Vector3.zero,false);
                Assert(Mathf.Abs(pose.line)<=.0901f && Mathf.Abs(pose.keeper)<=.0901f,"AI outside bar limits");
                Assert(Mathf.Max(Mathf.Abs(pose.line-before.line),Mathf.Abs(pose.keeper-before.keeper))*240<=m.MaxSpeed+.001,"AI teleports/exceeds speed");
                Assert(pose.line==repeat.line && pose.keeper==repeat.keeper,"AI not reproducible");
            }
            Assert(Mathf.Abs(m.Current.keeper)<.006f,"keeper does not cover goal cone");
            Assert(Mathf.Abs(m.Current.line)>.07f,"defensive line does not close central lane");
            var visible=new Vector3(.12f,.775f,-.30f);var velocity=new Vector3((-.075f-visible.x)/(.5565f-.30f),0,-1);
            float old=m.TargetKeeper;
            m.AdvanceTo(2+m.ReactionDelay-.03,visible,velocity,true);
            Assert(Mathf.Abs(m.TargetKeeper-old)<.0001f,"AI reacts before observation latency");
            m.AdvanceTo(2+m.ReactionDelay+.12,visible,velocity,true);
            Assert(m.TargetKeeper<-.06f,"AI ignores visible angled shot");
        }
        Assert(Mathf.Abs(BabyFootAttackMotion.FoldX(.7f)+.055f)<.0001f,"bank intercept folding wrong");
        Assert(BabyFootAttack.ContactDirection(0)==Vector3.back,"centred contact is not straight");
        Assert(BabyFootAttack.ContactDirection(.01f).x>0 && BabyFootAttack.ContactDirection(-.01f).x<0,"edge sign wrong");
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("reactive check timed out");
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            s=UnityEngine.Object.FindFirstObjectByType<BabyFootSession>();if(!s)return;e=s.exercise;a=e.attack;Application.runInBackground=true;
            if(phase==0)
            {
                TestTracking();e.ConfigureExercise(BabyFootDefense.ExerciseMode.FindTheGap);
                e.TrialCompleted+=Capture;phase=1;
            }
            if(phase==1)
            {
                if(!launched)
                {
                    a.difficulty=1;a.RequestedStriker=caseIndex==0?1:caseIndex==4?0:2;e.shotSpeed=1.15f;
                    Assert(e.BeginTrial(.075f),"functional trial did not begin");
                    float x=caseIndex==0?.05f:caseIndex==4?-.23f:.23f;
                    var ball=new Vector3(x,.775f,-.257f);Set(a,"ballStart",ball);e.ball.localPosition=ball;
                    float target=caseIndex==0?.05f:caseIndex==4?.06f:-.06f;
                    float slope=0;
                    if(caseIndex==2)slope=(target-x)/(.5565f-.257f);
                    if(caseIndex==3)slope=(2*.3225f-target-x)/(.5565f-.257f);
                    if(caseIndex==4)slope=(-2*.3225f-target-x)/(.5565f-.257f);
                    float error=Mathf.Atan(slope)*Mathf.Rad2Deg/70*.022f;
                    var figure=a.AttackingRod.Cast<Transform>().Where(t=>t.name.StartsWith("Player ")).ElementAt(a.RequestedStriker);
                    var root=a.AttackingRod.localPosition;root.x=x-figure.localPosition.x-error;Assert(Mathf.Abs(root.x)<=.09f,"unreachable test alignment");a.AttackingRod.localPosition=root;
                    float rowX=BabyFootAttackMotion.FoldX(x+slope*(.375f-.257f));
                    float line=0,best=-1;
                    foreach(float slide in new[]{-.09f,0,.09f})
                    {float gap=Mathf.Min(Mathf.Abs(rowX-slide+.1175f),Mathf.Abs(rowX-slide-.1175f));if(gap>best){best=gap;line=slide;}}
                    Property(a.Motion,"Current",new BabyFootAttackMotion.Pose{line=line,keeper=target>=0?-.085f:.085f});
                    Property(a.Motion,"MaxSpeed",0f); // isolate contact/bank physics; live AI is tested above and below
                    outcome="";Assert(a.Fire(),"shot failed");
                    if(caseIndex<=1)Assert(Mathf.Abs(a.ShotVelocity.x)<.00001f,"corner shot auto-aims at goal");
                    launched=true;
                }
                if(!a.IsActive && outcome!="")
                {
                    if(caseIndex==1)Assert(outcome=="miss" && a.Reason=="end_wall","centred corner shot was assisted: "+outcome+" "+a.Reason);
                    else Assert(outcome=="goal","edge/bank goal failed case "+caseIndex+": "+outcome+" "+a.Reason);
                    if(caseIndex>=3)Assert(a.WallBounces==1 && e.trajectory.presentation.wallSoundCount>0,"side-wall bank did not occur");
                    caseIndex++;launched=false;
                    if(caseIndex>=5){e.TrialCompleted-=Capture;phase=2;}
                }
                return;
            }
            if(phase==2)
            {
                if(!sessionStarted)
                {
                    s.exerciseSelection=2;s.difficulty=1;s.practiceTrials=3;s.measurementTrials=10;s.participantCode="TEST_REACTIVE";s.dataDirectoryOverride=Path.GetFullPath("Library/ReactiveAttackChecks");
                    Assert(s.StartSession(),"reactive session did not begin");sessionStarted=true;
                }
                string stage=Read(s,"stage").ToString();
                if(stage=="Practice" || stage=="Measurement")
                {
                    if(!e.IsTrialActive){s.StartNextTrial();Assert(a.IsAiming,"session trial failed");}
                    if(a.IsAiming)
                    {
                        int n=(int)Read(s,"started");
                        if(n%4==0){Set(a,"readyTime",Time.realtimeSinceStartupAsDouble-a.TimeLimit-.01);return;}
                        var f=a.AttackingRod.Cast<Transform>().Where(t=>t.name.StartsWith("Player ")).ElementAt(a.PlannedStriker);
                        var root=a.AttackingRod.localPosition;root.x=n%3==0?0:a.BallStartX-f.localPosition.x;a.AttackingRod.localPosition=root;
                        a.Fire();
                    }
                }
                else if(stage=="Break")s.BeginMeasurement();
                else if(stage=="Summary")
                {
                    var rows=File.ReadAllLines(Path.Combine((string)Read(s,"folder"),"measurement.csv"));
                    Assert(rows.Length==11 && rows.All(x=>x.Split(',').Length==37),"v7 CSV count/schema wrong");
                    Assert(rows.Skip(1).All(x=>x.Split(',')[17]=="" && x.Split(',')[28]==""),"obsolete predicted/scheduled gaps still exported");
                    Assert(rows.Skip(1).All(x=>x.Split(',')[27]=="delayed_ball_tracking_defense"),"wrong AI metadata");
                    var meta=JsonUtility.FromJson<BabyFootSession.SessionMetadata>(File.ReadAllText(Path.Combine((string)Read(s,"folder"),"session.json")));
                    Assert(meta.goals+meta.blocked+meta.missed+meta.timeouts==10 && meta.responseCount==0,"summary mixes outcomes/practice/timing");
                    e.ConfigureExercise(BabyFootDefense.ExerciseMode.TrajectoryAnticipation);Assert(e.trajectory.Prepare(.075f,false,0,true,1234),"anticipation regression");e.ConfigureExercise(BabyFootDefense.ExerciseMode.ReactionDefense);Assert(e.trajectory.VisibleRodCount==2,"reaction restoration regression");
                    End("PASS: no automatic corner aim; centred and signed-edge direction; actual centred corner shot misses solid end wall, inward edge shot scores, left/right side-wall bank goals with real velocity reflection and sound; AI closes ball/goal lanes instead of scheduled gaps, reacts to visible angled motion only after latency, bounded/reproducible slides; complete reactive practice + measurement, 37-column data with angle/banks/AI metrics and obsolete gap fields blank; measurement-only outcomes; existing defense/anticipation restored. Physics bank cases use passive defenders to isolate collision behavior; session uses live reactive AI. Test data: Library/ReactiveAttackChecks.");
                }
            }
        }
        catch(Exception ex){End("FAIL: "+ex);}
    }
    static void Capture(string value){outcome=value;}
    static void End(string message){EditorApplication.update-=Tick;if(e)e.TrialCompleted-=Capture;File.WriteAllText(Report,message);Debug.Log("BabyFoot reactive attack: "+message);EditorApplication.isPlaying=false;}
}
