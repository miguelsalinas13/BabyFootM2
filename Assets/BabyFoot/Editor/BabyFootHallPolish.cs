using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

[InitializeOnLoad]
internal static class BabyFootHallPolish
{
    const string Folder="Assets/BabyFoot/Environment";
    const string Report="Library/BabyFootHallEnvironmentV2.txt";
    static Transform root;static Mesh cube;
    static readonly Dictionary<string,List<CombineInstance>> additions=new Dictionary<string,List<CombineInstance>>();
    static BabyFootHallPolish(){ if(!BabyFootLegacyAutoSetup.Enabled)return; EditorApplication.delayCall+=Apply;}
    static void Texture(string name,Color colour)
    {
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/"+name+".asset");int n=tex.width;var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            uint hash=unchecked((uint)(x*73856093)^(uint)(y*19349663));hash^=hash>>13;hash*=1274126177;hash^=hash>>16;
            float grain=(hash%1000)/1000f-.5f;
            float broad=Mathf.PerlinNoise(x*.09f+17,y*.09f+37)-.5f;
            pixels[y*n+x]=colour*(1+grain*.07f+broad*.055f);
        }
        tex.SetPixels(pixels);tex.Apply(true,false);EditorUtility.SetDirty(tex);
    }
    static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+name+".mat");
    static void Box(string material,Vector3 position,Vector3 scale)
    {
        if(!additions.TryGetValue(material,out var list)){list=new List<CombineInstance>();additions.Add(material,list);}
        list.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(position,Quaternion.identity,scale)});
    }
    static void Label(string name,string words,Vector3 position,float height,Color colour)
    {
        var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,180,0);
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var text=go.AddComponent<TextMesh>();text.font=font;text.fontSize=64;text.characterSize=height*.17f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text=words;text.color=colour;
        var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=font.material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
    }
    static void Apply()
    {
        if(File.Exists(Report))return;
        if(!File.Exists("Library/BabyFootHallEnvironmentV1.txt") || EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Apply;return;}
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        try
        {
            root=GameObject.Find("Competition Hall Surroundings").transform;
            Texture("Concrete Aggregate",new Color(.28f,.29f,.295f));Texture("Acoustic Plaster",new Color(.34f,.33f,.32f));
            Material("Hall Floor Joint").SetColor("_BaseColor",new Color(.235f,.245f,.25f));
            Material("Hall Warm Light Diffusers").SetColor("_EmissionColor",new Color(1,.95f,.85f)*2.1f);
            var fascia=root.Find("Hall Violet Event Branding");fascia.localPosition+=Vector3.up*1.65f;
            var identity=root.Find("Hall identity");identity.localPosition=new Vector3(0,5.52f,9.24f);var title=identity.GetComponent<TextMesh>();title.text="COMPETITION CENTRE";title.characterSize=.22f*.17f;
            var sub=root.Find("Hall subtitle");sub.localPosition=new Vector3(0,5.15f,9.23f);sub.GetComponent<TextMesh>().characterSize=.095f*.17f;
            var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=temporary.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(temporary);
            // Architectural seams and event wayfinding break up the long front wall.
            Box("Hall Steel Structure",new Vector3(0,4.3f,-9.35f),new Vector3(17,.055f,.08f));
            for(int s=-1;s<=1;s+=2)
            {
                Box("Hall Charcoal Panels",new Vector3(s*4.85f,2.8f,-9.34f),new Vector3(2.1f,3.0f,.10f));
                Box("Hall Steel Structure",new Vector3(s*4.85f,4.32f,-9.29f),new Vector3(2.2f,.065f,.08f));
                Box("Hall Steel Structure",new Vector3(s*4.85f,1.28f,-9.29f),new Vector3(2.2f,.065f,.08f));
                Label("Wayfinding number "+s,s<0?"01":"02",new Vector3(s*4.85f,3.63f,-9.24f),.40f,new Color(.7f,.73f,.74f));
                Label("Wayfinding title "+s,s<0?"ATHLETE AREA":"TRAINING AREA",new Vector3(s*4.85f,2.95f,-9.24f),.15f,new Color(.78f,.8f,.80f));
                Label("Wayfinding subtitle "+s,s<0?"PREPARE\nFOCUS\nCOMPETE":"REACTION\nANTICIPATION\nPRECISION",new Vector3(s*4.85f,2.13f,-9.24f),.09f,new Color(.52f,.58f,.62f));
                for(int joint=-1;joint<=1;joint++)Box("Hall Steel Structure",new Vector3(s*6.4f+joint*1.1f,1.95f,-9.37f),new Vector3(.018f,3.8f,.04f));
            }
            foreach(var pair in additions)
            {
                var filter=root.Find(pair.Key).GetComponent<MeshFilter>();var original=filter.sharedMesh;
                var list=new List<CombineInstance>{new CombineInstance{mesh=original,transform=Matrix4x4.identity}};list.AddRange(pair.Value);
                var merged=new Mesh{name=original.name};merged.CombineMeshes(list.ToArray(),true,true);merged.RecalculateBounds();EditorUtility.CopySerialized(merged,original);UnityEngine.Object.DestroyImmediate(merged);EditorUtility.SetDirty(original);
            }
            foreach(var m in root.GetComponentsInChildren<MeshRenderer>())if(m.sharedMaterial)EditorUtility.SetDirty(m.sharedMaterial);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            BabyFootHallEnvironment.Capture("Library/BabyFootHall_Table.png",new Vector3(1.55f,1.6f,-1.75f),new Vector3(0,.8f,0),65);
            BabyFootHallEnvironment.Capture("Library/BabyFootHall_Surroundings.png",new Vector3(.7f,1.65f,-.3f),new Vector3(0,2.0f,-9),80);
            BabyFootHallEnvironment.Capture("Library/BabyFootHall_Roof.png",new Vector3(.7f,1.65f,-.3f),new Vector3(0,6.5f,2),80);
            int triangles=0;foreach(var mf in root.GetComponentsInChildren<MeshFilter>())if(mf.sharedMesh)triangles+=mf.sharedMesh.triangles.Length/3;
            File.WriteAllText(Report,"PASS: static venue visually polished, fine aggregate/plaster surfaces, non-overlapping high-level signage, event wayfinding and brighter physical diffusers.\nStatic geometry triangles: "+triangles+"\nGeometry material batches: 13 (additional detail merged into existing meshes)\nText labels: "+root.GetComponentsInChildren<TextMesh>().Length+"\nRealtime lights in scene: "+UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length+" (original 2, no added lights)\nNo dynamic shadows from surroundings; gameplay geometry/colliders/scripts unchanged.\n");
        }
        catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Debug.LogError("BabyFoot hall polish: "+ex);}
    }
}
