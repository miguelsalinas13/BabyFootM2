using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootTrajectoryPreview
{
    const string Marker="Library/BabyFootTrajectoryPreviewDoneV2.txt";
    static BabyFootTrajectoryPreview() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Prepare; }
    static void Prepare()
    {
        if(File.Exists(Marker) || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall+=Prepare; return; }
        if(!File.Exists("Library/BabyFootTrajectoryCheckV2.txt")) return;
        if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true;
        EditorApplication.update-=Capture; EditorApplication.update+=Capture;
    }
    static void Capture()
    {
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var c=Object.FindFirstObjectByType<BabyFootDefense>(); if(!c || !c.trajectory) return;
        c.ConfigureExercise(BabyFootDefense.ExerciseMode.TrajectoryAnticipation);
        Camera camera=null; foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if(cam.isActiveAndEnabled) { camera=cam; break; }
        if(!camera) return;
        var rt=new RenderTexture(1600,1100,24); var previous=RenderTexture.active; var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1600,1100),0,0); image.Apply(); File.WriteAllBytes("Library/BabyFootTrajectoryPreview.png",image.EncodeToPNG()); }
        finally { camera.targetTexture=null; RenderTexture.active=previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); }
        c.ConfigureExercise(BabyFootDefense.ExerciseMode.ReactionDefense);
        File.WriteAllText(Marker,"Full-table desktop view captured; original reaction view and layout restored.");
        EditorApplication.update-=Capture; EditorApplication.isPlaying=false;
    }
}
