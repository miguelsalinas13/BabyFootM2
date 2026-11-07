using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootEnvironmentInspect
{
    static BabyFootEnvironmentInspect(){ if(!BabyFootLegacyAutoSetup.Enabled)return; EditorApplication.delayCall+=Read;}
    static void Read()
    {
        if(File.Exists("Library/BabyFootEnvironmentBefore.txt"))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Read;return;}
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        var text=new StringBuilder();
        text.AppendLine("Sky: "+(RenderSettings.skybox?RenderSettings.skybox.name:"none")+" | ambient "+RenderSettings.ambientMode+" | fog "+RenderSettings.fog);
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Bounds bounds=new Bounds(root.transform.position,Vector3.zero);bool first=true;int count=0;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.enabled && r.gameObject.activeInHierarchy){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);count++;}
            text.AppendLine(root.name+" active="+root.activeSelf+" pos="+root.transform.position+" renderers="+count+" bounds="+bounds);
        }
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))text.AppendLine("Camera "+c.name+" clear="+c.clearFlags+" pos="+c.transform.position);
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))text.AppendLine("Light "+l.name+" "+l.type+" intensity="+l.intensity+" shadow="+l.shadows);
        File.WriteAllText("Library/BabyFootEnvironmentBefore.txt",text.ToString());
    }
}
