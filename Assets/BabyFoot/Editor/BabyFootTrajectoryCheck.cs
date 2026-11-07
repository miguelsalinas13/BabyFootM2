using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootTrajectoryCheck
{
    const string Report="Library/BabyFootTrajectoryCheckV2.txt";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static bool begun;
    static int modeIndex;
    static double deadline;
    static BabyFootSession session;
    static BabyFootDefense exercise;
    static BabyFootTrajectoryCheck() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Prepare; }
    static object Read(object obj,string field) => obj.GetType().GetField(field,Flags).GetValue(obj);
    static void Prepare()
    {
        if(File.Exists(Report)) return;
        if(EditorApplication.isUpdating || EditorApplication.isCompiling) { EditorApplication.delayCall+=Prepare; return; }
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        if(!File.Exists("Library/BabyFootTrajectoryInstalledV1.txt")) { EditorApplication.delayCall+=Prepare; return; }
        if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true;
        deadline=EditorApplication.timeSinceStartup+100; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) { End("FAIL: session check timed out"); return; }
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            session=UnityEngine.Object.FindFirstObjectByType<BabyFootSession>(); if(!session) return;
            exercise=session.exercise; Application.runInBackground=true;
            if(!begun)
            {
                if(modeIndex==1)
                {
                    exercise.ConfigureExercise(BabyFootDefense.ExerciseMode.TrajectoryAnticipation);
                    var route=exercise.trajectory;
                    Assert(route.VisibleRodCount==8,"long-shot table is incomplete");
                    Assert(Quaternion.Angle(exercise.goalkeeper.localRotation,Quaternion.identity)<.01f,"goalkeeper is not vertical");
                    foreach(float target in new[]{-.075f,.075f}) foreach(int wall in new[]{-1,0,1})
                    {
                        Assert(route.Prepare(target,wall!=0,wall),"blocked route: "+route.Error);
                        var points=route.CopyPoints();
                        Assert(points[0].z>.3f && Mathf.Abs(points.Last().x-target)<.00001f,"invalid source or goal target");
                        Assert(Vector3.Distance(route.Evaluate(route.Length),points.Last())<.00001f,"arc-length endpoint mismatch");
                        if(wall!=0)
                        {
                            var incoming=(points[1]-points[0]).normalized; var outgoing=(points[2]-points[1]).normalized;
                            Assert(Mathf.Abs(incoming.x+outgoing.x)<.0001f && Mathf.Abs(incoming.z-outgoing.z)<.0001f,"reflection is not specular");
                            Assert(Mathf.Abs(points[1].x-wall*(.34f-.0175f))<.00001f,"wall radius offset wrong");
                            route.ResetObservation(); route.Observe(route.BounceDistance-.001f); Assert(!route.BounceObserved,"bounce observed too early");
                            route.Observe(route.BounceDistance+.001f); Assert(route.BounceObserved,"bounce not observed");
                        }
                        for(int i=0;i<=200;i++)
                        {
                            var point=route.Evaluate(route.Length*i/200f);
                            Assert(Mathf.Abs(point.x)<=.32251f,"ball crosses side-wall boundary");
                        }
                    }
                    exercise.ConfigureExercise(BabyFootDefense.ExerciseMode.ReactionDefense);
                    Assert(route.VisibleRodCount==2,"reaction layout was not restored");
                    Assert(Vector3.Distance(exercise.ball.position,exercise.shotSpawn.position)<.00001f,"reaction spawn was not restored");
                }
                session.exerciseSelection=modeIndex;
                foreach(int length in new[]{10,20,30}) for(int seed=0;seed<50;seed++)
                {
                    var targets=BabyFootSession.BuildTargets(length,seed);
                    Assert(targets.Count(x=>x<0)==length/2 && targets.Count(x=>x>0)==length/2,"target imbalance");
                    Assert(targets.SequenceEqual(BabyFootSession.BuildTargets(length,seed)),"seed not reproducible");
                }
                session.dataDirectoryOverride=Path.GetFullPath("Library/TrajectoryChecks");
                session.participantCode="invalid,code"; Assert(!session.StartSession(),"unsafe participant code accepted");
                session.participantCode="TEST_COMPLETE"; session.practiceTrials=3; session.measurementTrials=10; session.difficulty=2;
                Assert(session.StartSession(),"could not start session"); Assert(exercise.sessionManaged,"legacy controls still enabled"); begun=true;
            }
            string stage=Read(session,"stage").ToString();
            if(stage=="Practice" || stage=="Measurement")
            {
                if(!exercise.IsTrialActive)
                {
                    session.StartNextTrial();
                    int started=(int)Read(session,"started"); session.StartNextTrial(); Assert((int)Read(session,"started")==started,"double start admitted");
                    typeof(BabyFootDefense).GetField("scheduled",Flags).SetValue(exercise,Time.realtimeSinceStartupAsDouble+.04);
                }
                int index=(int)Read(session,"started"); string phase=Read(exercise,"phase").ToString();
                if(index%3==0 && phase=="Waiting") exercise.goalkeeper.localPosition+=Vector3.right*.01f;
                if(index%3==2 && phase=="Flying")
                {
                    var p=exercise.goalkeeper.localPosition; p.x=exercise.LastTargetX; exercise.goalkeeper.localPosition=p;
                }
            }
            else if(stage=="Break") session.BeginMeasurement();
            else if(stage=="Summary")
            {
                string folder=(string)Read(session,"folder");
                var practice=File.ReadAllLines(Path.Combine(folder,"practice.csv"));
                var measurement=File.ReadAllLines(Path.Combine(folder,"measurement.csv"));
                Assert(practice.Length==4 && measurement.Length==11,"practice/measurement row counts incorrect");
                Assert(practice.Skip(1).All(x=>x.Split(',')[2]=="practice"),"practice block labels incorrect");
                Assert(measurement.Skip(1).All(x=>x.Split(',')[2]=="measurement"),"practice mixed into measurement");
                Assert(measurement.Skip(1).All(x=>x.Split(',').Length==23),"trajectory data columns missing");
                Assert(measurement.Skip(1).All(x=>x.Split(',')[14]==(modeIndex==1?"Trajectory Anticipation":"Reaction Defense")),"exercise labels incorrect");
                if(modeIndex==1) Assert(measurement.Skip(1).Count(x=>x.Split(',')[15]=="one_wall")==5,"route-family schedule not balanced");
                Assert(measurement.Skip(1).Count(x=>float.Parse(x.Split(',')[4],System.Globalization.CultureInfo.InvariantCulture)<0)==5,"measurement left/right not balanced");
                foreach(string result in new[]{"save","goal","false_start"}) Assert(measurement.Any(x=>x.Contains(","+result+",")),"missing outcome "+result);
                var meta=JsonUtility.FromJson<BabyFootSession.SessionMetadata>(File.ReadAllText(Path.Combine(folder,"session.json")));
                Assert(meta.status=="completed" && meta.completedPractice==3 && meta.completedMeasurement==10,"completion metadata incorrect");
                Assert(meta.saves+meta.goals+meta.falseStarts==10,"summary contains practice outcomes");
                Assert(File.ReadAllLines(Path.Combine(folder,"summary.csv")).Length==2,"summary not written");
                // A second session must not reuse counts and must preserve an interrupted trial.
                session.participantCode="TEST_ABORT"; Assert(session.StartSession(),"second session failed");
                Assert((int)Read(session,"finished")==0,"session counters not reset");
                session.StartNextTrial(); session.EndSession();
                string aborted=(string)Read(session,"folder");
                var abortedMeta=JsonUtility.FromJson<BabyFootSession.SessionMetadata>(File.ReadAllText(Path.Combine(aborted,"session.json")));
                Assert(abortedMeta.status=="aborted","interruption not marked");
                Assert(File.ReadAllLines(Path.Combine(aborted,"practice.csv")).Last().Contains(",aborted,"),"interrupted trial lost");
                if(modeIndex==0) { modeIndex=1; begun=false; return; }
                End("PASS: both exercise session flows; eight-rod long-shot layout and reaction restoration; six collision-clear direct/bank routes; specular reflection, ball-radius wall boundary and arc-length endpoint; 150 balanced/reproducible schedules; invalid code rejected; duplicate start blocked; complete practice + measurement with save/goal/false_start; separated CSVs; measurement-only summary; new-session reset and interrupted-trial persistence. Test files: Library/TrajectoryChecks.");
            }
        }
        catch(Exception e) { End("FAIL: "+e); }
    }
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
    static void End(string result)
    {
        EditorApplication.update-=Tick; File.WriteAllText(Report,result); Debug.Log("BabyFoot sessions: "+result); EditorApplication.isPlaying=false;
    }
}
