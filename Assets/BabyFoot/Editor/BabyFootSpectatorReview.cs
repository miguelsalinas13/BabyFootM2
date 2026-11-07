using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

[InitializeOnLoad]
public static class BabyFootSpectatorReview
{
    const string Pending="Library/BabyFootSpectatorReview.pending";
    static string Output=>BabyFootEditorPaths.PreviewDirectory;
    static double due;static int step;static string report;
    static BabyFootSpectatorReview(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        File.WriteAllText(Pending,"run");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Pending))return;
        step=0;report="";due=EditorApplication.timeSinceStartup+1.5;EditorApplication.update+=Tick;
    }
    static void Require(bool value,string why){if(!value)throw new Exception(why);}
    static void Capture(Camera camera,string name)
    {
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var rt=new RenderTexture(1440,1000,24);var tex=new Texture2D(1440,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,1000),0,0);tex.Apply();File.WriteAllBytes(Output+name+".png",tex.EncodeToPNG());}
        finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);}
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        try
        {
            var ui=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatchUI>();
            if(step==0)
            {
                Canvas.ForceUpdateCanvases();
                var go=new GameObject("Temporary Spectator Menu Review");var camera=go.AddComponent<Camera>();camera.CopyFrom(ui.origin.Camera);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=70;
                camera.transform.position=ui.menu.transform.position-ui.menu.transform.forward*1.35f+Vector3.up*.1f;camera.transform.LookAt(ui.menu.transform.position);
                try{Capture(camera,"BabyFoot_Spectator_Menu");}finally{UnityEngine.Object.DestroyImmediate(go);}
                UnityEngine.Random.InitState(20261106);ui.WatchMatch();ui.match.enabled=false;
                for(int i=0;i<300;i++)ui.match.Simulate(.02f);
                due=EditorApplication.timeSinceStartup+.5;step=1;return;
            }
            if(step==1)
            {
                var view=ui.GetComponent<BabyFootSpectatorView>();
                if(!XRSettings.isDeviceActive)
                {
                    Require(view.MonitorCamera&&view.MonitorCamera.enabled&&!ui.origin.Camera.enabled,"Monitor camera gate");
                    var head=ui.origin.Camera.transform;Vector3 position=head.position;Quaternion rotation=head.rotation;int firstView=view.ViewIndex;
                    for(int i=0;i<3;i++){Capture(view.MonitorCamera,"BabyFoot_Spectator_View_"+view.ViewIndex);view.NextView();}
                    Require(view.ViewIndex==firstView&&Vector3.Distance(head.position,position)<.0001f&&Quaternion.Angle(head.rotation,rotation)<.01f,"Monitor view changed VR head");
                    report+="PASS: three fixed monitor views; only monitor camera active without XR; VR head unchanged.\n";
                    ScreenCapture.CaptureScreenshot(Output+"BabyFoot_Spectator_Desktop.png");
                }
                else
                {
                    Require(!view.MonitorCamera||!view.MonitorCamera.enabled,"Monitor camera active with headset");
                    report+="PASS: active headset preserved; desktop camera disabled. Monitor image review deferred.\n";
                }
                due=EditorApplication.timeSinceStartup+.5;step=2;return;
            }
            if(step==2){ui.PlayAsBlue();due=EditorApplication.timeSinceStartup+.5;step=3;return;}
            var component=ui.GetComponent<BabyFootSpectatorView>();
            Require(!component.MonitorCamera||!component.MonitorCamera.enabled,"Monitor still enabled in player mode");
            Require(ui.origin.Camera.enabled,"Player camera not restored");
            Finish(report+"PASS: returning to Blue restores the player camera. Menu screenshot captured.\n");
        }
        catch(Exception e){Finish(report+"FAIL: "+e);Debug.LogException(e);}
    }
    static void Finish(string value)
    {
        File.WriteAllText("Library/BabyFootSpectatorReviewV1.txt",value);File.Delete(Pending);EditorApplication.update-=Tick;EditorApplication.isPlaying=false;
    }
}
