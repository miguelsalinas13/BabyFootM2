using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootTableSessionCheck
{
    const string Report="Library/BabyFootTableSessionCheckV1.txt";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static bool begun;
    static double deadline;
    static BabyFootSession session;
    static BabyFootDefense exercise;
    static BabyFootTableSessionCheck() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Prepare; }
    static object Read(object obj,string field) => obj.GetType().GetField(field,Flags).GetValue(obj);
    static void Prepare()
    {
        if(File.Exists(Report)) return;
        if(EditorApplication.isUpdating || EditorApplication.isCompiling) { EditorApplication.delayCall+=Prepare; return; }
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        if(!File.Exists("Library/BabyFootTableImportedV1.txt")) { EditorApplication.delayCall+=Prepare; return; }
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
                foreach(int length in new[]{10,20,30}) for(int seed=0;seed<50;seed++)
                {
                    var targets=BabyFootSession.BuildTargets(length,seed);
                    Assert(targets.Count(x=>x<0)==length/2 && targets.Count(x=>x>0)==length/2,"target imbalance");
                    Assert(targets.SequenceEqual(BabyFootSession.BuildTargets(length,seed)),"seed not reproducible");
                }
                session.dataDirectoryOverride=Path.GetFullPath("Library/TableSessionChecks");
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
                End("PASS: 150 balanced/reproducible schedules; invalid code rejected; duplicate start blocked; complete practice + measurement with save/goal/false_start; separated CSVs; measurement-only summary; new-session reset and interrupted-trial persistence. Test files: Library/TableSessionChecks.");
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
