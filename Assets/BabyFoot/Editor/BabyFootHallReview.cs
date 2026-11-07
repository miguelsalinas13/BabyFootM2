using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootHallReview
{
    const string Report="Library/BabyFootHallReviewV1.txt";
    static double ready;
    static BabyFootHallReview(){ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Review;}
    static void Review()
    {
        if(File.Exists(Report)){EditorApplication.update-=Review;return;}
        if(EditorApplication.timeSinceStartup<ready || EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(!File.Exists("Library/BabyFootHallEnvironmentV2.txt") || SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        EditorApplication.update-=Review;
        BabyFootHallEnvironment.Capture("Library/BabyFootHall_Table.png",new Vector3(1.55f,1.6f,-1.75f),new Vector3(0,.8f,0),65);
        BabyFootHallEnvironment.Capture("Library/BabyFootHall_Surroundings.png",new Vector3(.7f,1.65f,-.3f),new Vector3(0,2.0f,-9),80);
        BabyFootHallEnvironment.Capture("Library/BabyFootHall_Roof.png",new Vector3(.7f,1.65f,-.3f),new Vector3(0,6.5f,2),80);
        var root=GameObject.Find("Competition Hall Surroundings");int missing=0;
        foreach(var go in root.GetComponentsInChildren<Transform>(true))foreach(var c in go.GetComponents<Component>())if(!c)missing++;
        foreach(var r in root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)if(!m || !m.shader || !m.shader.isSupported)missing++;
        File.WriteAllText(Report,(missing==0?"PASS":"FAIL")+": delayed render review, missing scripts/materials/shaders="+missing+"; skybox="+RenderSettings.skybox.name+".\n");
    }
}
