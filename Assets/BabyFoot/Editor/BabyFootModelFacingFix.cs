using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootModelFacingFix
{
    static BabyFootModelFacingFix() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Apply; }
    static void Apply()
    {
        if(File.Exists("Library/BabyFootModelFacingFixed.txt")) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Apply; return; }
        var c=Object.FindFirstObjectByType<BabyFootDefense>(); if(!c || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        int count=0;
        foreach(Transform rod in c.goalkeeper.parent)
        {
            if(!rod.name.StartsWith("Coral")) continue;
            foreach(Transform player in rod)
            {
                var visual=player.Find("Player Visual"); if(!visual) continue;
                visual.localRotation=Quaternion.Euler(0,180,0)*visual.localRotation; count++;
            }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/BabyFootModelFacingFixed.txt","Opposing team visuals rotated: "+count);
        var obj=new GameObject("Temporary Model Preview"); var cam=obj.AddComponent<Camera>();
        var focus=c.goalkeeper.position+new Vector3(0,.005f,.15f);
        obj.transform.position=focus+new Vector3(.4f,.25f,-.32f); obj.transform.LookAt(focus); cam.fieldOfView=51; cam.nearClipPlane=.005f;
        var rt=new RenderTexture(1400,1000,24); var prev=RenderTexture.active; var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        try { cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,1400,1000),0,0); tex.Apply(); File.WriteAllBytes("Library/BabyFootImportedModels.png",tex.EncodeToPNG()); }
        finally { RenderTexture.active=prev; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(obj); }
    }
}
