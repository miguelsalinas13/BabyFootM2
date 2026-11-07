using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootModelIntegration
{
    const string Root="Assets/BabyFoot/Models";
    static BabyFootModelIntegration() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Apply; }
    static Material Material(string name,Color color,float smoothness)
    {
        string path=Root+"/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(m) return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name=name;
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",smoothness); m.enableInstancing=true;
        AssetDatabase.CreateAsset(m,path); return m;
    }
    static void Apply()
    {
        if(File.Exists("Library/BabyFootModelsIntegratedV1.txt")) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall+=Apply; return; }
        var scene=SceneManager.GetActiveScene(); if(scene.name!="BabyFoot_Prototype") return;
        var exercise=Object.FindFirstObjectByType<BabyFootDefense>(); if(!exercise || !exercise.goalkeeper) return;
        var red=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Player_Red.fbx");
        var blue=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Player_Blue.fbx");
        var grip=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Handle_Graphite.fbx");
        if(!red || !blue || !grip) { Debug.LogError("BabyFoot: missing model assets."); return; }
        Directory.CreateDirectory("Assets/BabyFoot/Scenes/Backups"); Directory.CreateDirectory(Root+"/Materials"); AssetDatabase.Refresh();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/BabyFoot/Scenes/Backups/Before_Model_Integration.unity"),true);
        var redMat=Material("Player_Team_Red",new Color(.65f,.018f,.035f),.55f);
        var blueMat=Material("Player_Team_Blue",new Color(.015f,.13f,.56f),.55f);
        var skin=Material("Player_Skin",new Color(.68f,.39f,.19f),.3f);
        var black=Material("Player_Black",new Color(.012f,.016f,.025f),.4f);
        var face=Material("Player_Face_Details",new Color(.035f,.015f,.008f),.2f);
        var rubber=Material("Handle_Graphite",new Color(.022f,.028f,.038f),.25f);
        var collar=Material("Handle_Collar",new Color(.25f,.31f,.36f),.55f);
        int players=0,handles=0;
        foreach(Transform rod in exercise.goalkeeper.parent)
        {
            bool cyan=rod.name.StartsWith("Cyan");
            foreach(Transform child in rod)
            {
                if(child.name.StartsWith("Player ") && !child.Find("Player Visual"))
                {
                    foreach(var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(cyan?blue:red,child); visual.name="Player Visual";
                    visual.transform.localPosition=Vector3.zero;
                    var bounds=BoundsOf(visual);
                    if(bounds.size.z>bounds.size.y*2) visual.transform.localRotation=Quaternion.Euler(-90,0,0)*visual.transform.localRotation;
                    bounds=BoundsOf(visual);
                    // Match foot bottom to the existing gameplay collision height (69 mm below shaft).
                    float distance=child.position.y-bounds.min.y;
                    visual.transform.localScale*=.069f/distance;
                    foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials=renderer.sharedMaterials;
                        for(int i=0;i<materials.Length;i++)
                        {
                            string n=materials[i]?materials[i].name:"";
                            materials[i]=n.Contains("Skin")?skin:n.Contains("Face")?face:n.Contains("Black")?black:(cyan?blueMat:redMat);
                        }
                        renderer.sharedMaterials=materials;
                    }
                    players++;
                }
                if(child.name=="Grip" && !child.Find("Handle Visual"))
                {
                    child.GetComponent<Renderer>().enabled=false;
                    // Existing primitive grip has rotated/non-unit scale: place visual on the unit-scale rod instead.
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(grip,rod); visual.name="Handle Visual";
                    visual.transform.localPosition=child.localPosition;
                    visual.transform.localRotation=Quaternion.identity;
                    var b=BoundsOf(visual);
                    if(b.size.y>b.size.x*2) visual.transform.localRotation=Quaternion.Euler(0,0,90)*visual.transform.localRotation;
                    else if(b.size.z>b.size.x*2) visual.transform.localRotation=Quaternion.Euler(0,90,0)*visual.transform.localRotation;
                    b=BoundsOf(visual); visual.transform.localScale*=.13f/b.size.x;
                    if(!cyan) visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
                    foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials=renderer.sharedMaterials;
                        for(int i=0;i<materials.Length;i++) materials[i]=materials[i] && materials[i].name.Contains("Collar")?collar:rubber;
                        renderer.sharedMaterials=materials;
                    }
                    handles++;
                }
            }
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        File.WriteAllText("Library/BabyFootModelsIntegratedV1.txt","Players="+players+"\nHandles="+handles+"\nFoot bottom relative to shaft=-0.069 m\nHandle length=0.130 m\nGameplay references and colliders retained\n");
        Debug.Log("BabyFoot: integrated "+players+" player models and "+handles+" handle models. Backup saved. Validate on Quest Pro before final scale approval.");
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>(true); var b=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++) b.Encapsulate(renderers[i].bounds); return b;
    }
}
