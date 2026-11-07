using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootDefenseInstaller
{
    static BabyFootDefenseInstaller() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Install; }
    static void Install()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall+=Install; return; }
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        if(Object.FindFirstObjectByType<BabyFootDefense>()) return;
        var table=GameObject.Find("02 | Table - provisional Leonhart Pro reference"); if(!table) return;
        var rods=table.transform.Find("Rods - independent slide and rotation pivots");
        var controller=new GameObject("05 | Defense Exercise").AddComponent<BabyFootDefense>();
        controller.goalkeeper=rods.GetChild(0);
        controller.ball=table.transform.Find("Ball - provisional diameter 35 mm");
        for(int i=0;i<rods.childCount;i++) rods.GetChild(i).gameObject.SetActive(i==0 || i==2);
        var cam=Camera.main; cam.transform.position=new Vector3(.88f,1.40f,-.72f); cam.transform.LookAt(new Vector3(0,.77f,-.30f)); cam.fieldOfView=53;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/BabyFootDefenseInstalled.txt","Defense exercise installed and saved\n");
        Debug.Log("BabyFoot: defense exercise installed. Space starts, A/D move goalkeeper, R resets. CSV in Application.persistentDataPath. Deterministic simplified trajectories, no calibrated reaction measurement.");
    }
}
