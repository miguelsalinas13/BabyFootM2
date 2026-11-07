using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BabyFootEyeCheck
{
    const string Pending="Library/BabyFootEyeCheck.pending",Report="Library/BabyFootEyeCheckV1.txt";
    static double due;
    static BabyFootEyeCheck(){EditorApplication.playModeStateChanged+=Changed;}
    static void Require(bool b,string why){if(!b)throw new Exception(why);}
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        if(SceneManager.GetActiveScene().path!=BabyFootEyeSceneSetup.ScenePath)throw new Exception("Open diagnostic scene first.");
        CheckData();File.WriteAllText(Pending,"run");EditorApplication.isPlaying=true;
    }
    static void CheckData()
    {
        Require(BabyFootGazeMath.ValidPose(true,3,Vector3.zero,Quaternion.identity,.01,.25),"Valid gaze pose rejected");
        Require(!BabyFootGazeMath.ValidPose(false,3,Vector3.zero,Quaternion.identity,0,.25),"Untracked gaze accepted");
        Require(!BabyFootGazeMath.ValidPose(true,2,Vector3.zero,Quaternion.identity,0,.25),"Partial pose accepted");
        Require(!BabyFootGazeMath.ValidPose(true,3,Vector3.zero,Quaternion.identity,.3,.25),"Stale gaze accepted");
        Require(!BabyFootGazeMath.ValidPose(true,3,new Vector3(float.NaN,0,0),Quaternion.identity,0,.25),"NaN pose accepted");
        Require(!BabyFootGazeMath.ValidPose(true,3,Vector3.zero,new Quaternion(0,0,0,0),0,.25),"Zero quaternion accepted");
        var ray=new Ray(Vector3.zero,Vector3.forward);
        Require(BabyFootGazeMath.PlaneIntersection(ray,new Vector3(0,0,2),Vector3.forward,out var hit)&&Vector3.Distance(hit,new Vector3(0,0,2))<.0001f,"Forward plane intersection");
        Require(!BabyFootGazeMath.PlaneIntersection(new Ray(Vector3.zero,Vector3.right),new Vector3(0,0,2),Vector3.forward,out _),"Parallel ray accepted");
        Require(!BabyFootGazeMath.PlaneIntersection(new Ray(Vector3.zero,Vector3.back),new Vector3(0,0,2),Vector3.forward,out _),"Backward intersection accepted");
        Require(BabyFootGazeMath.ErrorDegrees(ray,new Vector3(0,0,2))<.001f,"Centre angular error");
        var angled=new Ray(Vector3.zero,Quaternion.Euler(0,5,0)*Vector3.forward);
        Require(Mathf.Abs(BabyFootGazeMath.ErrorDegrees(angled,new Vector3(0,0,2))-5)<.001f,"Five-degree angular error");
        var targets=Enumerable.Repeat(new Vector3(0,0,2),9).ToArray();
        using(var log=new BabyFootGazeLog("Library/EyeTrackingChecks",new BabyFootGazeLog.Metadata{targets=targets,secondsPerTarget=2.5f,settlingSeconds=.7f,maximumInputAgeSeconds=.25f}))
        {
            var sample=new BabyFootGazeSample{time=1,frame=100,inputTime=.99,age=.01,valid=true,newInput=true,settled=true,headTracked=true,status="valid",target=0,targetPosition=targets[0],ray=angled,angularError=5};
            log.Append(sample);sample.time=1.01;sample.newInput=false;log.Append(sample);
            sample.time=1.02;sample.valid=false;sample.status="eye_not_tracked";log.Append(sample);
            log.End("aborted");
            var lines=File.ReadAllLines(Path.Combine(log.Folder,"gaze.csv"));Require(lines.Length==4,"CSV row count");
            Require(lines.All(l=>l.Split(',').Length==31),"CSV column consistency");
            var invalid=lines[3].Split(',');Require(invalid[5]=="false"&&invalid.Skip(10).Take(6).All(string.IsNullOrEmpty)&&invalid[29]=="","Invalid row substituted gaze coordinates");
            var summary=File.ReadAllLines(Path.Combine(log.Folder,"summary.csv"));Require(summary.Length==10,"Nine-target summary");
            var first=summary[1].Split(',');Require(first[1]=="3"&&first[2]=="2"&&first[4]=="1"&&first[5]=="5.000000","Duplicate input weighting or error summary");
            Require(summary[2].Split(',')[5]=="","No-data target mean should be blank");
            Require(File.ReadAllText(Path.Combine(log.Folder,"test.json")).Contains("aborted"),"Abort metadata missing");
        }
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&File.Exists(Pending)){due=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.ExitingPlayMode)EditorApplication.update-=Tick;
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        try
        {
            var probe=UnityEngine.Object.FindAnyObjectByType<BabyFootEyeTrackingProbe>();var ui=UnityEngine.Object.FindAnyObjectByType<BabyFootEyeTrackingUI>();
            Require(probe&&ui&&ui.canvas&&ui.status,"Runtime diagnostic UI failed to initialize");
            Require(probe.targets.Length==9&&probe.targetRenderers.Length==9,"Nine targets missing");
            Require(!probe.IsRecording&&probe.RecordedRows==0,"Unexpected automatic recording");
            Require(!probe.gazeMarker.gameObject.activeSelf&&!probe.gazeLine.enabled,"Debug feedback should default off");
            if(!probe.EyeValid){Require(!probe.StartValidation(),"Invalid eye data allowed recording");Require(!ui.startButton.interactable,"Start button enabled without eye data");}
            Require(UnityEngine.Object.FindObjectsByType<BabyFootSession>(FindObjectsSortMode.None).Length==0,"Gameplay session leaked into diagnostic scene");
            Require(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1,"Multiple active cameras");
            File.WriteAllText(Report,"PASS\nPose validity rejects untracked, partial, stale, NaN and zero quaternion. Plane geometry/known angular error checked. 31-column exports retain invalid rows with blank gaze; repeated input excluded from settled error mean; nine-target summary and abort metadata checked. Runtime menu, nine targets, debug hidden, no auto recording, invalid-data start guard, isolated from gameplay sessions, one active camera.\nSynthetic software checks only; no successful headset/eye data, controller UI picking, angular accuracy or latency validation claimed.\n");
        }
        catch(Exception e){File.WriteAllText(Report,"FAIL\n"+e);Debug.LogException(e);}
        finally{EditorApplication.update-=Tick;if(File.Exists(Pending))File.Delete(Pending);EditorApplication.isPlaying=false;}
    }
}
