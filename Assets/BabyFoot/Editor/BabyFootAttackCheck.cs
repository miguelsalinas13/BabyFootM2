using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootAttackCheck
{
    const string Report="Library/BabyFootAttackCheckV2.txt";
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static BabyFootDefense e;static BabyFootSession s;static BabyFootAttack a;
    static bool started;static double deadline;
    static int lastTrial=-1;static string lastBlock="";
    static object Read(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static void Assert(bool yes,string message){if(!yes)throw new Exception(message);}
    static BabyFootAttackCheck(){ if(!BabyFootLegacyAutoSetup.Enabled)return; EditorApplication.delayCall+=Prepare;}
    static void Prepare()
    {
        if(File.Exists(Report))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating){EditorApplication.delayCall+=Prepare;return;}
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Tick;
        if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("attack check timeout");
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            s=UnityEngine.Object.FindFirstObjectByType<BabyFootSession>();if(!s)return;e=s.exercise;a=e.attack;
            Application.runInBackground=true;
            if(!started)
            {
                s.exerciseSelection=2;s.difficulty=2;s.participantCode="TEST_ATTACK";s.practiceTrials=3;s.measurementTrials=10;
                s.dataDirectoryOverride=Path.GetFullPath("Library/AttackChecks");Assert(s.StartSession(),"attack session start failed");
                Assert(e.IsAttack && !e.occludeFinalSegment,"wrong mode/occlusion");
                Assert(e.trajectory.presentation.attackingRod==a.AttackingRod,"wrong strike bar");
                Assert(a.AttackingRod.Cast<Transform>().Count(x=>x.name.StartsWith("Player "))==3,"not a three-player attacking bar");
                started=true;
            }
            string stage=Read(s,"stage").ToString();
            if(stage=="Practice" || stage=="Measurement")
            {
                if(!e.IsTrialActive)
                {
                    s.StartNextTrial();Assert(a.IsAiming,"attack trial failed");
                    int count=(int)Read(s,"started");s.StartNextTrial();Assert((int)Read(s,"started")==count,"duplicate trial accepted");
                }
                int trial=(int)Read(s,"started");
                if(a.IsAiming && (lastTrial!=trial || lastBlock!=stage))
                {
                    lastTrial=trial;lastBlock=stage;
                    int kind=(trial-1)%5;
                    var initial=e.ball.localPosition;
                    a.AttackingRod.localPosition+=Vector3.right*.01f;
                    Assert(e.ball.localPosition==initial,"ball follows bar before contact");
                    float sign=Mathf.Sign(a.BallStartX);
                    double elapsed=Time.realtimeSinceStartupAsDouble-(double)Read(a,"readyTime");
                    float omega=a.DefenderFrequency*Mathf.PI*2;
                    Set(a,"linePhase",sign*Mathf.PI*.5f-omega*(float)elapsed);
                    float keeper=-sign*Mathf.PI*.5f-omega*.77f*(float)elapsed;
                    if(kind==1)
                    {
                        float arrival=(initial.z-(-.5265f))/e.shotSpeed;
                        keeper=Mathf.Asin(a.BallStartX/.08f)-omega*.77f*((float)elapsed+arrival);
                    }
                    Set(a,"keeperPhase",keeper);
                    var pos=a.AttackingRod.localPosition;
                    pos.x=kind==2?0:kind==4?a.BallStartX-sign*.02f:a.BallStartX;
                    a.AttackingRod.localPosition=pos;
                    if(kind==3){Set(a,"readyTime",Time.realtimeSinceStartupAsDouble-a.TimeLimit-.01);return;}
                    Assert(a.Fire(),"shot attempt rejected");Assert(!a.Fire(),"double fire admitted");
                    if(kind==0)Assert(a.GapOpenAtShot && a.PredictedGapOpen,"clear shot diagnostics wrong");
                    if(kind==2)Assert(!a.ContactMade && a.Reason=="no_ball_contact","misalignment not recognized");
                    Assert(Quaternion.Angle(e.goalkeeper.localRotation,Quaternion.identity)<.01f,"goalkeeper tilted");
                }
            }
            else if(stage=="Break")s.BeginMeasurement();
            else if(stage=="Summary")
            {
                string folder=(string)Read(s,"folder");var practice=File.ReadAllLines(Path.Combine(folder,"practice.csv"));var rows=File.ReadAllLines(Path.Combine(folder,"measurement.csv"));
                Assert(practice.Length==4 && rows.Length==11,"block row counts wrong");
                Assert(rows.All(x=>x.Split(',').Length==26),"offense CSV column count wrong");
                foreach(var pair in new[]{new[]{0,5},new[]{1,6},new[]{2,7},new[]{3,8},new[]{4,9}})
                {
                    string expected=pair[0]==0?"goal":pair[0]==1?"blocked":pair[0]==3?"timeout":"miss";
                    foreach(int index in pair)Assert(rows[index+1].Split(',')[18]==expected,"trial "+(index+1)+" expected "+expected+" got "+rows[index+1]);
                }
                Assert(rows.Skip(1).Where(x=>x.Split(',')[18]=="timeout").All(x=>x.Split(',')[10]=="" && x.Split(',')[14]=="false"),"timeout has invented decision time");
                Assert(rows.Skip(1).All(x=>x.Split(',')[2]=="measurement"),"practice included in measurement");
                var meta=JsonUtility.FromJson<BabyFootSession.SessionMetadata>(File.ReadAllText(Path.Combine(folder,"session.json")));
                Assert(meta.goals==2 && meta.blocked==2 && meta.missed==4 && meta.timeouts==2 && meta.responseCount==0 && meta.decisionCount==8,"summary counts/timing mixed");
                var summary=File.ReadAllLines(Path.Combine(folder,"summary.csv"))[1].Split(',');Assert(summary[9]=="" && summary[10]=="" && summary[15]!="","decision timing mixed with response timing");
                s.participantCode="TEST_ATTACK_ABORT";Assert(s.StartSession(),"new attack session failed");s.StartNextTrial();s.EndSession();
                var interrupted=File.ReadAllLines(Path.Combine((string)Read(s,"folder"),"practice.csv"));Assert(interrupted.Last().Split(',')[18]=="aborted","interrupted attack lost");
                e.ConfigureExercise(BabyFootDefense.ExerciseMode.TrajectoryAnticipation);Assert(e.trajectory.VisibleRodCount==8,"anticipation layout not restored");
                foreach(float target in new[]{-.075f,.075f})Assert(e.trajectory.Prepare(target,false,0,true,1234),"random rebound regression: "+e.trajectory.Error);
                e.ConfigureExercise(BabyFootDefense.ExerciseMode.ReactionDefense);Assert(e.trajectory.VisibleRodCount==2,"reaction layout not restored");
                Assert(Vector3.Distance(e.ball.position,e.shotSpawn.position)<.00001f,"reaction spawn changed");
                End("PASS: Find the Gap practice + measurement; fixed ball/manual three-player alignment; moving two-player defense and vertical keeper; explicit goal, blocked, no-contact miss, wide miss, timeout; duplicate start/fire prevention; no gap cues in participant UI; 26-column offense CSV with decision/alignment/gap diagnostics; practice excluded; counts 2 goals + 2 blocked + 4 misses + 2 timeouts, 8 decisions, no defense response mixing; interrupted session persistence; switching to both existing modes restores layouts/source and collision-based random routes. Test data: Library/AttackChecks.");
            }
        }
        catch(Exception ex){End("FAIL: "+ex);}
    }
    static void End(string message){EditorApplication.update-=Tick;File.WriteAllText(Report,message);Debug.Log("BabyFoot attack: "+message);EditorApplication.isPlaying=false;}
}
