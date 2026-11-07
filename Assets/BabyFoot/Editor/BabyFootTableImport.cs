using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootTableImport
{
    const string Folder="Assets/BabyFoot/Models/Table";
    static BabyFootTableImport() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Apply; }
    static void Apply()
    {
        if(File.Exists("Library/BabyFootTableImportedV1.txt")) return;
        if(EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Apply; return; }
        var exercise=Object.FindFirstObjectByType<BabyFootDefense>();
        if(!exercise || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        var body=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/BabyFoot_Table_Body.fbx");
        var field=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/BabyFoot_Table_Field.fbx");
        if(!body || !field) { Debug.LogError("BabyFoot: missing table FBX assets."); return; }
        var rods=exercise.goalkeeper.parent; var table=rods.parent;
        if(table.Find("Table Visuals - Blender")) return;
        Directory.CreateDirectory("Assets/BabyFoot/Scenes/Backups");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),AssetDatabase.GenerateUniqueAssetPath("Assets/BabyFoot/Scenes/Backups/Before_Table_Import.unity"),true);
        int collidersBefore=table.GetComponentsInChildren<Collider>(true).Length;
        int hidden=0;
        foreach(var r in table.GetComponentsInChildren<Renderer>(true))
        {
            if(r.transform.IsChildOf(rods) || r.transform==exercise.ball || r.transform.IsChildOf(exercise.ball)) continue;
            if(r.enabled) hidden++;
            Undo.RecordObject(r,"Replace provisional table visual"); r.enabled=false;
        }
        var root=new GameObject("Table Visuals - Blender"); root.transform.SetParent(table,false);
        var bodyInstance=(GameObject)PrefabUtility.InstantiatePrefab(body,root.transform); bodyInstance.name="Table Body"; bodyInstance.transform.localPosition=Vector3.zero;
        var fieldInstance=(GameObject)PrefabUtility.InstantiatePrefab(field,root.transform); fieldInstance.name="Table Field"; fieldInstance.transform.localPosition=Vector3.zero;
        foreach(var go in new[]{bodyInstance,fieldInstance})
        {
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++) materials[i]=ResolveMaterial(materials[i]?materials[i].name:"");
                r.sharedMaterials=materials;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
                if(go==fieldInstance) r.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        var b=BoundsOf(bodyInstance); var f=BoundsOf(fieldInstance);
        bool valid=f.size.x>.67f && f.size.x<.69f && f.size.z>1.10f && f.size.z<1.13f && f.size.y<.03f;
        bool preserved=table.GetComponentsInChildren<Collider>(true).Length==collidersBefore && rods.childCount==8;
        if(!valid || !preserved) { Debug.LogError("BabyFoot table import validation failed. Check imported scale/orientation or colliders."); return; }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/BabyFootTableImportedV1.txt","PASS: body and field imported.\nBody bounds: "+b.size.ToString("F5")+"\nField bounds: "+f.size.ToString("F5")+"\nPreserved colliders: "+collidersBefore+"\nPreserved rods: "+rods.childCount+"\nReplaced provisional renderers: "+hidden+"\nNo duplicated moving parts.\n");
        Capture(table.position);
        Debug.Log("BabyFoot: Blender table imported, field dimensions verified, original colliders and moving roots preserved. Scene saved.");
    }
    static Material ResolveMaterial(string importedName)
    {
        string name; Color color; float metallic=0,smooth=.35f;
        if(importedName.Contains("Graphite")) { name="Graphite"; color=new Color(.025f,.033f,.042f); smooth=.45f; }
        else if(importedName.Contains("Ivory")) { name="Ivory"; color=new Color(.77f,.8f,.78f); }
        else if(importedName.Contains("Steel")) { name="Steel"; color=new Color(.42f,.49f,.54f); metallic=.7f; smooth=.65f; }
        else if(importedName.Contains("Rubber")) { name="Rubber"; color=new Color(.012f,.015f,.02f); smooth=.15f; }
        else if(importedName.Contains("Cyan")) { name="Cyan"; color=new Color(.015f,.48f,.57f); }
        else if(importedName.Contains("Coral")) { name="Coral"; color=new Color(.68f,.025f,.035f); }
        else if(importedName.Contains("Pitch")) { name="Pitch"; color=new Color(.026f,.19f,.12f); smooth=.15f; }
        else { name="Field Lines"; color=new Color(.82f,.87f,.78f); smooth=.15f; }
        string path=Folder+"/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat) return mat;
        Directory.CreateDirectory(Folder+"/Materials");
        mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name="Table "+name;
        mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",smooth); mat.enableInstancing=true;
        AssetDatabase.CreateAsset(mat,path); return mat;
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>(true); var bounds=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds); return bounds;
    }
    static void Capture(Vector3 tablePosition)
    {
        var go=new GameObject("Temporary Final Table Camera"); var camera=go.AddComponent<Camera>();
        go.transform.position=tablePosition+new Vector3(1.5f,1.65f,-1.65f); go.transform.LookAt(tablePosition+new Vector3(0,.62f,0));
        camera.fieldOfView=49; camera.nearClipPlane=.01f; camera.farClipPlane=30;
        var rt=new RenderTexture(1600,1200,24); var old=RenderTexture.active; var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,1600,1200),0,0); image.Apply(); File.WriteAllBytes("Library/BabyFootFinalTable.png",image.EncodeToPNG()); }
        finally { camera.targetTexture=null; RenderTexture.active=old; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(go); }
    }
}
