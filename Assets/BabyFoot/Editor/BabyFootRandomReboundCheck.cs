using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootRandomReboundCheck
{
    const string Report="Library/BabyFootRandomReboundCheckV2.txt";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int geometry,mode;
    static bool started;
    static int hiddenFrames,multiPaths;
    static double deadline,maxGeneration;
    static readonly HashSet<string> players=new HashSet<string>();
    static BabyFootDefense e;
    static BabyFootSession s;
    static BabyFootRandomReboundCheck(){ if(!BabyFootLegacyAutoSetup.Enabled)return; EditorApplication.delayCall+=Prepare;}
    static object Read(object o,string f)=>o.GetType().GetField(f,Flags).GetValue(o);
    static void Assert(bool yes,string message){if(!yes)throw new Exception(message);}
    static void Prepare()
    {
        if(File.Exists(Report)) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating){EditorApplication.delayCall+=Prepare;return;}
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        deadline=EditorApplication.timeSinceStartup+220;
        EditorApplication.update+=Tick;
        if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("check timed out");
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            s=UnityEngine.Object.FindFirstObjectByType<BabyFootSession>(); if(!s)return;
            e=s.exercise; Application.runInBackground=true;
            if(geometry<40)
            {
                if(geometry==0)
                {
                    Assert(s.occlusionEnabled,"anticipation must default to occlusion ON");
                    e.ConfigureExercise(BabyFootDefense.ExerciseMode.TrajectoryAnticipation);
                }
                int seed=1000+geometry/2; float target=geometry%2==0?-.075f:.075f;
                var t=e.trajectory;
                Assert(t.Prepare(target,false,0,true,seed),"random seed "+seed+" target "+target+": "+t.Error);
                Assert(t.PlayerBounceCount>=1,"player route has no physical contact");
                if((seed&3)==0)Assert(t.PlayerBounceCount>=2,"multiple-contact condition lost");
                if(t.PlayerBounceCount>=2)multiPaths++;
                Assert(Mathf.Sign(t.ActualTargetX)==Mathf.Sign(target) && Mathf.Abs(t.ActualTargetX)>=.055f && Mathf.Abs(t.ActualTargetX)<=.082f,"goal target not defendable/outer");
                Assert(t.Length<2.801f && t.CopyPoints().Length>=3,"unbounded or empty route");
                Assert(Vector3.Distance(t.Evaluate(t.Length),t.CopyPoints().Last())<.0001f,"path endpoint drift");
                var pp=t.CopyPoints();
                for(int i=0;i<t.Contacts.Length;i++)
                {
                    var c=t.Contacts[i];
                    var incoming=(pp[i+1]-pp[i]).normalized; var outgoing=(pp[i+2]-pp[i+1]).normalized;
                    Assert(Vector3.Distance(Vector3.Reflect(incoming,c.normal),outgoing)<.002f,"nonphysical change of direction");
                    if(c.collider)players.Add(c.collider.transform.parent.parent.name+"/"+c.collider.transform.parent.name);
                }
                var saved=t.CopyPoints(); var trace=t.ContactTrace;
                Assert(t.Prepare(target,false,0,true,seed),"repeat seed failed");
                Assert(saved.SequenceEqual(t.CopyPoints()) && trace==t.ContactTrace,"seed does not reproduce physics path");
                maxGeneration=Math.Max(maxGeneration,t.GenerationMilliseconds);
                geometry++;return;
            }
            if(!started)
            {
                Assert(multiPaths>0 && players.Count>=4,"insufficient random player/path variety");
                e.ConfigureExercise(BabyFootDefense.ExerciseMode.ReactionDefense);
                Assert(e.trajectory.VisibleRodCount==2,"reaction layout not restored");
                for(int n=10;n<=100;n+=10)
                {
                    var r=BabyFootSession.BuildRoutes(n,123);
                    Assert(r.Count(x=>x==0)<=n/10,"too many easy direct routes");
                    Assert(r.Count(x=>x==2)>=n*6/10,"too few random-player routes");
                    Assert(r.SequenceEqual(BabyFootSession.BuildRoutes(n,123)),"schedule not reproducible");
                }
                s.exerciseSelection=mode==0?0:1;s.occlusionEnabled=mode!=1;
                s.difficulty=2;s.practiceTrials=3;s.measurementTrials=10;
                s.participantCode="TEST_RANDOM";s.dataDirectoryOverride=Path.GetFullPath("Library/RandomReboundChecks");
                Assert(s.StartSession(),"session did not start");
                Assert(Mathf.Abs(e.shotSpeed-(mode==0?1.35f:1.85f))<.0001f,"wrong per-exercise speed");
                Assert(e.occludeFinalSegment==(mode==2),"wrong visibility condition");
                started=true;
            }
            string stage=Read(s,"stage").ToString();
            if(stage=="Practice" || stage=="Measurement")
            {
                if(!e.IsTrialActive)
                {
                    s.StartNextTrial();Assert(e.IsTrialActive,"trial preparation failed: "+e.PreparationError);
                    typeof(BabyFootDefense).GetField("scheduled",Flags).SetValue(e,Time.realtimeSinceStartupAsDouble+.04);
                }
                int trial=(int)Read(s,"started");string phase=Read(e,"phase").ToString();
                if(trial%3==0 && phase=="Waiting") e.goalkeeper.localPosition+=Vector3.right*.01f;
                if(trial%3==2 && phase=="Flying")
                {var p=e.goalkeeper.localPosition;p.x=e.LastTargetX;e.goalkeeper.localPosition=p;}
                if(e.BallOccluded)
                {
                    hiddenFrames++;
                    Assert(mode==2,"ball hidden in visible condition");
                    Assert(e.ball.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"hidden flag set but renderer visible");
                    Assert(e.OcclusionOnsetSeconds>=0,"occlusion onset not recorded");
                }
            }
            else if(stage=="Break")s.BeginMeasurement();
            else if(stage=="Summary")
            {
                string folder=(string)Read(s,"folder");var rows=File.ReadAllLines(Path.Combine(folder,"measurement.csv"));
                Assert(rows.Length==11 && rows.All(x=>x.Split(',').Length==38),"CSV schema/row count changed incorrectly");
                foreach(string outcome in new[]{"save","goal","false_start"})Assert(rows.Skip(1).Any(x=>x.Split(',')[7]==outcome),"missing "+outcome);
                if(mode>0)
                {
                    Assert(rows.Skip(1).Count(x=>x.Split(',')[15]=="direct")==1,"direct route quota");
                    Assert(rows.Skip(1).Count(x=>x.Split(',')[15]=="random_players")==7,"random-player quota");
                    Assert(rows.Skip(1).Where(x=>x.Split(',')[15]=="random_players").All(x=>int.Parse(x.Split(',')[32])>=1 && x.Split(',')[37].Length>0),"contact data missing");
                }
                Assert(!e.BallOccluded && e.ball.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"ball not revealed after result");
                if(mode==2)Assert(hiddenFrames>=3 && rows.Skip(1).Any(x=>x.Split(',')[35].Length>0),"occlusion not visibly exercised/recorded");
                if(mode<2){mode++;started=false;return;}
                End("PASS: 40 random routes and identical seeded repeats; multiple player contacts in "+multiPaths+" paths; "+players.Count+" different players contacted; all changes of direction follow swept-sphere contact normals; outer defendable targets, bounded paths, no more than 10% direct routes; reaction + visible/occluded anticipation session flows; 38-column CSV with contact traces; save/goal/false_start; actual renderers hidden for "+hiddenFrames+" sampled frames and restored at result; anticipation Fast 1.85 m/s, reaction unchanged. Max tested preparation ms: "+maxGeneration.ToString("F2")+". Test data: Library/RandomReboundChecks.");
            }
        }
        catch(Exception ex){End("FAIL: "+ex);}
    }
    static void End(string message)
    {
        EditorApplication.update-=Tick;File.WriteAllText(Report,message);Debug.Log("BabyFoot random rebounds: "+message);EditorApplication.isPlaying=false;
    }
}
