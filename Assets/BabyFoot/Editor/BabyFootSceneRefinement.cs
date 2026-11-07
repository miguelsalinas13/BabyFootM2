using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootSceneRefinement
{
    static BabyFootSceneRefinement() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Refine; }
    static void Refine()
    {
        if(File.Exists("Library/BabyFootRefinedV2.txt")) return;
        if(EditorApplication.isUpdating || EditorApplication.isCompiling) { EditorApplication.delayCall+=Refine; return; }
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        var cabinet=GameObject.Find("02 | Table - provisional Leonhart Pro reference/Cabinet");
        if(!cabinet) return;
        cabinet.transform.localPosition=new Vector3(0,.645f,0);
        cabinet.transform.localScale=new Vector3(.74f,.16f,1.43f);
        foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            if(text.name=="Wall Title") text.characterSize=.032f;
            else if(text.name=="Wall Subtitle") text.characterSize=.0054f;
            else if(text.name=="Wall Edition") text.characterSize=.0044f;
            else if(text.name=="Prototype Status") text.characterSize=.007f;
        }
        var status=GameObject.Find("01 | Room/Prototype Status");
        if(status) { status.transform.localPosition=new Vector3(0,1.13f,2.42f); status.transform.localRotation=Quaternion.identity; status.GetComponent<TextMesh>().text="01  /  DEFENSE"; }
        var preview=Camera.main;
        preview.transform.LookAt(new Vector3(0,.98f,0)); preview.fieldOfView=52;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        var cam=Camera.main;
        var rt=new RenderTexture(1600,1000,24); var prev=RenderTexture.active;
        var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try { cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,1600,1000),0,0); tex.Apply(); File.WriteAllBytes("Library/BabyFootPreview.png",tex.EncodeToPNG()); }
        finally { cam.targetTexture=null; RenderTexture.active=prev; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
        int players=0,missing=0,activeCameras=0;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach(var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if(tr.name.StartsWith("Player ")) players++;
            foreach(var component in tr.GetComponents<Component>()) if(component==null) missing++;
            var camera=tr.GetComponent<Camera>(); if(camera && camera.isActiveAndEnabled) activeCameras++;
        }
        File.WriteAllText("Library/BabyFootRefinedV2.txt","Scene saved\nPlayers="+players+"\nMissing scripts="+missing+"\nActive cameras="+activeCameras+"\nPlay test pending\n");
        Debug.Log("BabyFoot: visual refinement saved.");
    }
}
