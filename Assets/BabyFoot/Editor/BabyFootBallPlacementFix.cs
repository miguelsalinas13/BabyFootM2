using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootBallPlacementFix
{
    static BabyFootBallPlacementFix() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Apply; }
    static void Apply()
    {
        if(File.Exists("Library/BabyFootBallPlacementFixedV1.txt")) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Apply; return; }
        var c=Object.FindFirstObjectByType<BabyFootDefense>(); var presentation=Object.FindFirstObjectByType<BabyFootPresentation>();
        if(!c || !presentation || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        Directory.CreateDirectory("Assets/BabyFoot/Scenes/Backups");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),AssetDatabase.GenerateUniqueAssetPath("Assets/BabyFoot/Scenes/Backups/Before_Ball_Placement.unity"),true);
        var rod=presentation.attackingRod; Transform centre=null;
        foreach(Transform child in rod)
            if(child.name.StartsWith("Player ") && (centre==null || Mathf.Abs(child.localPosition.x)<Mathf.Abs(centre.localPosition.x))) centre=child;
        var visual=centre.Find("Player Visual");
        var renderers=visual.GetComponentsInChildren<Renderer>(true); var bounds=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds);
        float radius=c.ball.GetComponent<SphereCollider>().radius*Mathf.Abs(c.ball.lossyScale.x);
        bool towardNegativeZ=c.goalkeeper.position.z<rod.position.z;
        var p=c.ball.position;
        p.x=centre.position.x;
        p.z=towardNegativeZ?bounds.min.z-radius-.001f:bounds.max.z+radius+.001f;
        var spawn=new GameObject("Shot Spawn - in front of attacker").transform; spawn.SetParent(c.ball.parent,true); spawn.position=p;
        Undo.RecordObject(c,"Correct ball starting position"); Undo.RecordObject(c.ball,"Place ball in front of attacker");
        c.shotSpawn=spawn; c.ball.position=p;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/BabyFootBallPlacementFixedV1.txt","Spawn table-local position: "+spawn.localPosition.ToString("F6")+"\nBall radius: "+radius+"\nBall lies beyond the attacking player bounds toward the goalkeeper, with 1 mm clearance.\n");
        Debug.Log("BabyFoot: ball placed in front of attacker, toward defended goal. Ready, next trial and reset share the same spawn.");
    }
}
