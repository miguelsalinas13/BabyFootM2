using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BabyFootVRCheck
{
    const string Pending="Library/BabyFootVRCheck.pending";
    const string Report="Library/BabyFootVRCheckV1.txt";
    static int step;
    static double next;
    static BabyFootSession session;
    static BabyFootVRGoalkeeper input;
    static BabyFootDefense defense;
    static BabyFootVRCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        if(SceneManager.GetActiveScene().path!=BabyFootVRSceneSetup.VRPath)throw new Exception("Open VR scene first.");
        File.WriteAllText(Pending,"run");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && File.Exists(Pending))
        {step=0;next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.ExitingPlayMode)EditorApplication.update-=Tick;
    }
    static void Require(bool condition,string why){if(!condition)throw new Exception(why);}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(step==0)
            {
                session=UnityEngine.Object.FindAnyObjectByType<BabyFootSession>();defense=session.exercise;input=UnityEngine.Object.FindAnyObjectByType<BabyFootVRGoalkeeper>();
                var menu=UnityEngine.Object.FindAnyObjectByType<BabyFootVRMenu>();
                Require(menu.menuCanvas && menu.statusText,"World menu and HUD missing");
                Require(menu.menuCanvas.renderMode==RenderMode.WorldSpace,"Menu is not world space");
                Require(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Length>=8,"Menu controls missing");
                Require(defense.externalControls&&session.vrInterface,"VR input adapter not enabled");
                Require(Mathf.Approximately(BabyFootVRGoalkeeper.Slide(.12f,.02f,0,.09f),.09f),"Right stop failed");
                Require(Mathf.Approximately(BabyFootVRGoalkeeper.Slide(-.12f,-.02f,0,.09f),-.09f),"Left stop failed");
                Require(Mathf.Abs(BabyFootVRGoalkeeper.Slide(.035f,.02f,.01f,.09f)-.025f)<.0001f,"1:1 axial slide failed");
                input.enabled=false;session.dataDirectoryOverride="Library/VRChecks";session.participantCode="VR_AUTOCHECK";session.practiceTrials=1;session.measurementTrials=2;
                Require(session.StartSession(),"VR practice failed to start");session.StartNextTrial();defense.externalGoalkeeperX=.01f;
                step=1;next=EditorApplication.timeSinceStartup+.25;return;
            }
            if(step==1)
            {
                Require(session.CurrentStage=="Break" && !defense.IsTrialActive,"Pre-shot physical displacement did not produce practice false start");
                session.BeginMeasurement();session.StartNextTrial();defense.externalGoalkeeperX=.01f;
                step=2;next=EditorApplication.timeSinceStartup+.25;return;
            }
            if(step==2)
            {
                Require(session.CompletedTrials==1,"First measurement trial missing");session.StartNextTrial();defense.externalGoalkeeperX=.01f;
                step=3;next=EditorApplication.timeSinceStartup+.25;return;
            }
            Require(session.CurrentStage=="Summary" && !session.IsOpen,"Measurement did not finalize");
            var dirs=Directory.GetDirectories("Library/VRChecks").OrderByDescending(Directory.GetLastWriteTimeUtc).ToArray();string folder=dirs[0];
            Require(File.ReadAllLines(Path.Combine(folder,"practice.csv")).Length==2,"Practice export count");
            Require(File.ReadAllLines(Path.Combine(folder,"measurement.csv")).Length==3,"Measurement export count");
            Require(File.ReadAllText(Path.Combine(folder,"session.json")).Contains("vr_controller_grip_1_to_1_slide"),"VR input metadata missing");
            Require(Quaternion.Angle(defense.goalkeeper.localRotation,Quaternion.identity)<.1f,"Keeper tilted");
            File.WriteAllText(Report,"PASS\nWorld-space menu and HUD initialize; 1:1 slide and both end stops; external input produces pre-shot false starts; practice + measurement + summary and separate CSV exports; VR input metadata; vertical goalkeeper.\nSynthetic input test only. Headset visuals, controller picking and Air Link latency require hands-on validation.\n");
            Finish();
        }
        catch(Exception e){File.WriteAllText(Report,"FAIL\n"+e);Debug.LogException(e);Finish();}
    }
    static void Finish(){EditorApplication.update-=Tick;if(File.Exists(Pending))File.Delete(Pending);EditorApplication.isPlaying=false;}
}
