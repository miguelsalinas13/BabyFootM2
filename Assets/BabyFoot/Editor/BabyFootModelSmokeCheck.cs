using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootModelSmokeCheck
{
    const string Report="Library/BabyFootModelSmokeCheck.txt";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int step;
    static double deadline;
    static BabyFootDefense controller;
    static BabyFootModelSmokeCheck() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Prepare; }
    static object Read(string field) => typeof(BabyFootDefense).GetField(field,Flags).GetValue(controller);
    static void Prepare()
    {
        if(File.Exists(Report)) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall+=Prepare; return; }
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        if(!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Capture();
            EditorApplication.isPlaying=true;
        }
        deadline=EditorApplication.timeSinceStartup+45;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline) { End("FAIL: timed out"); return; }
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        controller=UnityEngine.Object.FindFirstObjectByType<BabyFootDefense>(); if(!controller) return;
        string phase=Read("phase").ToString();
        if(step==0)
        {
            // Keep automated trials separate from participant data.
            string csv=Path.GetFullPath("Library/BabyFootModelSmokeTrials.csv");
            File.WriteAllText(csv,"trial,target_x_m,speed_m_s,result,response_s,keeper_x_m,utc\n");
            typeof(BabyFootDefense).GetField("csv",Flags).SetValue(controller,csv);
            controller.SendMessage("Begin"); step=1;
        }
        else if(step==1 && phase=="Result")
        {
            if(!Read("message").ToString().StartsWith("GOAL")) { End("FAIL: unattended shot was not a goal"); return; }
            controller.SendMessage("Begin");
            controller.goalkeeper.localPosition+=Vector3.right*.01f; step=2;
        }
        else if(step==2 && phase=="Result")
        {
            if(!Read("message").ToString().Contains("false start")) { End("FAIL: premature movement not detected"); return; }
            controller.SendMessage("Begin"); step=3;
        }
        else if(step==3 && phase=="Flying")
        {
            var p=controller.goalkeeper.localPosition; p.x=(float)Read("target"); controller.goalkeeper.localPosition=p; step=4;
        }
        else if(step==4 && phase=="Result")
        {
            if(!Read("message").ToString().StartsWith("SAVE")) { End("FAIL: aligned goalkeeper did not save"); return; }
            var lines=File.ReadAllLines((string)Read("csv"));
            bool passed=lines.Length==4 && lines[1].Contains(",goal,") && lines[2].Contains(",false_start,") && lines[3].Contains(",save,");
            End(passed?"PASS: goal, false_start and save recorded in separate smoke-test CSV. Imported visuals preserve gameplay references.":"FAIL: CSV results mismatch");
        }
    }
    static void End(string result)
    {
        EditorApplication.update-=Tick; File.WriteAllText(Report,result+"\n");
        Debug.Log("BabyFoot model smoke check: "+result); EditorApplication.isPlaying=false;
    }
    static void Capture()
    {
        var cameraGo=new GameObject("Temporary Model Review Camera");
        var camera=cameraGo.AddComponent<Camera>();
        var c=UnityEngine.Object.FindFirstObjectByType<BabyFootDefense>();
        if(!c) { UnityEngine.Object.DestroyImmediate(cameraGo); return; }
        Vector3 focus=c.goalkeeper.position+new Vector3(0,.005f,.12f);
        cameraGo.transform.position=focus+new Vector3(.36f,.26f,-.32f);
        cameraGo.transform.LookAt(focus); camera.fieldOfView=51; camera.nearClipPlane=.005f; camera.farClipPlane=20;
        var rt=new RenderTexture(1400,1000,24); var prev=RenderTexture.active;
        var texture=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1400,1000),0,0); texture.Apply(); File.WriteAllBytes("Library/BabyFootImportedModels.png",texture.EncodeToPNG()); }
        finally { RenderTexture.active=prev; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(cameraGo); }
    }
}
