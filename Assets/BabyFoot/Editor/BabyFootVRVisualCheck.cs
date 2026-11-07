using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BabyFootVRVisualCheck
{
    const string Pending="Library/BabyFootVRVisual.pending",Report="Library/BabyFootVRVisualV2.txt";
    static double due;
    static BabyFootVRVisualCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        File.WriteAllText(Pending,"run");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&File.Exists(Pending)){due=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;}
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        EditorApplication.update-=Tick;
        GameObject cameraObject=null;RenderTexture rt=null;Texture2D tex=null;
        try
        {
            var menu=UnityEngine.Object.FindAnyObjectByType<BabyFootVRMenu>();
            cameraObject=new GameObject("Temporary VR Layout Camera");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(menu.origin.Camera);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=75;
            rt=new RenderTexture(1400,1200,24);tex=new Texture2D(1400,1200,TextureFormat.RGB24,false);
            int totalClipped=0;
            for(int page=0;page<3;page++)
            {
                if(page==1)menu.SelectExercise(2);if(page==2)menu.OpenSettings();menu.RefreshView();Canvas.ForceUpdateCanvases();
                var rect=menu.menuCanvas.GetComponent<RectTransform>();
                foreach(var button in menu.menuCanvas.GetComponentsInChildren<Button>())
                {
                    var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach(var corner in corners)if(!rect.rect.Contains(rect.InverseTransformPoint(corner))){totalClipped++;Debug.LogError("Clipped button page "+page+": "+button.name);break;}
                }
                camera.transform.position=new Vector3(page==0?1.15f:-1.15f,1.65f,page==0?-.42f:-.23f);camera.transform.LookAt(menu.menuCanvas.transform.position);
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,1200),0,0);tex.Apply();
                File.WriteAllBytes(BabyFootEditorPaths.PreviewDirectory+"BabyFoot_VR_Menu_v2_"+page+".png",tex.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;
            }
            if(totalClipped>0)throw new Exception("Clipped buttons="+totalClipped);
            menu.SelectExercise(1);var e=menu.session.exercise;int valid=0;
            for(int seed=0;seed<32&&valid<6;seed++){e.CancelTrial();e.nextPlayerBounce=true;e.nextWallBounce=false;e.nextTrajectorySeed=seed;if(e.BeginTrial(valid%2==0?.075f:-.075f))valid++;}
            if(valid!=6||e.trajectory.VisibleRodCount!=8)throw new Exception("Anticipation preparation regression: routes="+valid);
            e.CancelTrial();
            File.WriteAllText(Report,"PASS\nThree menu pages rendered; zero clipped active buttons; six valid anticipation random-player routes across both target signs, all eight rods visible.\nImage previews in workspace output/BabyFoot_VR_Menu_v2_0..2.png. Real headset UI picking and comfort remain user checks.\n");
        }
        catch(Exception ex){File.WriteAllText(Report,"FAIL\n"+ex);Debug.LogException(ex);}
        finally
        {
            RenderTexture.active=null;if(cameraObject){cameraObject.GetComponent<Camera>().targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraObject);}
            if(rt)UnityEngine.Object.DestroyImmediate(rt);if(tex)UnityEngine.Object.DestroyImmediate(tex);
            if(File.Exists(Pending))File.Delete(Pending);EditorApplication.isPlaying=false;
        }
    }
}
