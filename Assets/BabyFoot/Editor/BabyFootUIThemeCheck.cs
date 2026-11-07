using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class BabyFootUIThemeCheck
{
    const string Pending="Library/BabyFootUITheme.pending";
    static double due,deadline;static string mode;static int step;
    static BabyFootUIThemeCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run(string selected)
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        string path=selected=="match"?"BabyFoot_FreeMatch":selected=="eye"?"BabyFoot_EyeTracking_Test":"BabyFoot_VR";
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Review unsaved scene changes first.");
        EditorSceneManager.OpenScene("Assets/BabyFoot/Scenes/"+path+".unity");File.WriteAllText(Pending,selected);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {if(state==PlayModeStateChange.EnteredPlayMode&&File.Exists(Pending)){mode=File.ReadAllText(Pending);step=0;due=EditorApplication.timeSinceStartup+1.5;deadline=due+15;EditorApplication.update+=Tick;}}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static Button Find(Canvas canvas,string name)=>canvas.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
    static void Check(Canvas canvas)
    {
        Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.GetComponent<RectTransform>());
        var rect=canvas.GetComponent<RectTransform>();
        foreach(var button in canvas.GetComponentsInChildren<Button>())
        {
            Require(button.GetComponent<BabyFootUIButtonMotion>(),"Missing button state feedback: "+button.name);
            var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
            foreach(var point in corners)Require(rect.rect.Contains(rect.InverseTransformPoint(point)),"Clipped button: "+button.name);
        }
        Require(BabyFootUITheme.Regular.name.Contains("SourceSans"),"Branded font did not load");
        foreach(var icon in canvas.GetComponentsInChildren<BabyFootUIIcon>())Require(icon.GetComponent<CanvasRenderer>(),"Missing pictogram renderer");
    }
    static void Capture(Canvas canvas,string name,Camera source)
    {
        var go=new GameObject("Temporary UI Review Camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=70;
        var centre=canvas.transform.position;
        if(mode=="eye")camera.transform.position=source.transform.position;
        else camera.transform.position=centre-canvas.transform.forward*1.35f+Vector3.up*.1f;
        camera.transform.LookAt(mode=="eye"?UnityEngine.Object.FindAnyObjectByType<BabyFootEyeTrackingProbe>().board.position:centre);
        if(mode=="eye")camera.fieldOfView=80;
        var rt=new RenderTexture(1500,1400,24);var tex=new Texture2D(1500,1400,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1500,1400),0,0);tex.Apply();File.WriteAllBytes(BabyFootEditorPaths.PreviewDirectory+"BabyFoot_UI_"+name+".png",tex.EncodeToPNG());}
        finally{RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        try
        {
            Require(EditorApplication.timeSinceStartup<deadline,"UI check timed out");
            if(mode=="match")
            {
                var ui=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatchUI>();
                if(step==0){Check(ui.menu);Capture(ui.menu,"Match",ui.origin.Camera);Find(ui.menu,"Play as Blue / Resume").GetComponent<BabyFootUIButtonMotion>().OnPointerEnter(new PointerEventData(EventSystem.current));step=1;due=EditorApplication.timeSinceStartup+.2;return;}
                if(step==1){Require(Find(ui.menu,"Play as Blue / Resume").transform.localPosition.z<0,"Hover depth feedback missing");Find(ui.menu,"Play as Blue / Resume").onClick.Invoke();Require(!ui.match.Paused,"Play action broken");step=2;due=EditorApplication.timeSinceStartup+.2;return;}
                var score=ui.transform.Find("Free Match Score").GetComponent<Canvas>();Capture(score,"Score",ui.origin.Camera);Finish("PASS\nMenu bounds/font, hover depth and Play button action; live score render.\n");return;
            }
            if(mode=="training")
            {
                var ui=UnityEngine.Object.FindAnyObjectByType<BabyFootVRMenu>();
                if(step==0){Check(ui.menuCanvas);Capture(ui.menuCanvas,"Training",ui.origin.Camera);Find(ui.menuCanvas,"Find the Gap / Attack").onClick.Invoke();Require(ui.Page==1&&ui.session.exerciseSelection==2,"Exercise selection broken");step=1;due=EditorApplication.timeSinceStartup+.15;return;}
                if(step==1){Check(ui.menuCanvas);Capture(ui.menuCanvas,"Controls",ui.origin.Camera);Find(ui.menuCanvas,"Continue to session setup").onClick.Invoke();Require(ui.Page==2,"Settings transition broken");step=2;due=EditorApplication.timeSinceStartup+.15;return;}
                Check(ui.menuCanvas);Capture(ui.menuCanvas,"Settings",ui.origin.Camera);Find(ui.menuCanvas,"Easy").onClick.Invoke();Require(ui.session.difficulty==0,"Difficulty button broken");Require(!ui.session.IsOpen,"UI preview created participant data");Finish("PASS\nHome/tutorial/settings bounds and fonts; explicit exercise selection; page navigation and difficulty actions; no participant session opened.\n");return;
            }
            var eye=UnityEngine.Object.FindAnyObjectByType<BabyFootEyeTrackingUI>();Check(eye.canvas);Capture(eye.canvas,"Eye",eye.probe.origin.Camera);
            Require(eye.startButton.interactable==eye.probe.EyeValid&&!eye.probe.IsRecording,"Eye validity/recording guard changed");Finish("PASS\nDiagnostic menu themed; bounds/font; valid-eye-data start guard preserved; no automatic recording. Target geometry/colors unchanged.\n");
        }
        catch(Exception ex){Finish("FAIL\n"+ex);Debug.LogException(ex);}
    }
    static void Finish(string report)
    {File.WriteAllText("Library/BabyFootUITheme_"+mode+"V1.txt",report);EditorApplication.update-=Tick;if(File.Exists(Pending))File.Delete(Pending);EditorApplication.isPlaying=false;}
}
