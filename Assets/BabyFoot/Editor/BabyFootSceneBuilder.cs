using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

// One-time, non-destructive scene generator. Existing scenes are never overwritten.
[InitializeOnLoad]
internal static class BabyFootSceneBuilder
{
    const string Root = "Assets/BabyFoot";
    const string ScenePath = Root + "/Scenes/BabyFoot_Prototype.unity";
    static Material navy, white, teal, red, metal, pitch, rubber, line, wood;
    static BabyFootSceneBuilder() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall += AutoCreate; }
    static void AutoCreate()
    {
        if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += AutoCreate; return; }
        for (int i=0;i<SceneManager.sceneCount;i++) if (SceneManager.GetSceneAt(i).isDirty) return;
        Create();
    }
    static GameObject Group(string name, Transform parent=null)
    {
        var go=new GameObject(name); if(parent) go.transform.SetParent(parent,false); return go;
    }
    static Material Mat(string name, Color color, float metallic=0, float smooth=.3f)
    {
        string path=Root+"/Materials/"+name+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing) return existing;
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name=name;
        m.SetColor("_BaseColor",color); m.SetFloat("_Metallic",metallic); m.SetFloat("_Smoothness",smooth);
        AssetDatabase.CreateAsset(m,path); return m;
    }
    static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider=true)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
        go.transform.localPosition=pos; go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static GameObject Box(string name,Transform p,Vector3 pos,Vector3 size,Material mat,bool collider=true)
        => Shape(name,PrimitiveType.Cube,p,pos,size,mat,collider);
    static void Text(string name,string value,Transform parent,Vector3 pos,float size,Color color,Vector3 rotation)
    {
        var go=Group(name,parent); go.transform.localPosition=pos; go.transform.localEulerAngles=rotation;
        var t=go.AddComponent<TextMesh>(); t.text=value; t.fontSize=64; t.characterSize=size*.2f;
        t.anchor=TextAnchor.MiddleCenter; t.alignment=TextAlignment.Center; t.color=color;
    }
    [MenuItem("BabyFoot/Create First Scene")]
    static void Create()
    {
        if(File.Exists(ScenePath)) { Debug.Log("BabyFoot: scene already exists; nothing overwritten."); return; }
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Root+"/Scenes"); Directory.CreateDirectory(Root+"/Materials");
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        navy=Mat("Midnight",new Color(.035f,.065f,.10f));
        white=Mat("Warm White",new Color(.83f,.85f,.83f));
        teal=Mat("Team Cyan",new Color(.02f,.65f,.73f),.15f,.45f);
        red=Mat("Team Coral",new Color(.98f,.22f,.10f),.1f,.4f);
        metal=Mat("Brushed Steel",new Color(.58f,.65f,.68f),.72f,.72f);
        pitch=Mat("Pitch Green",new Color(.07f,.27f,.23f),0,.18f);
        rubber=Mat("Rubber",new Color(.025f,.03f,.04f),0,.15f);
        line=Mat("Field Markings",new Color(.80f,.91f,.82f));
        wood=Mat("Oak Accent",new Color(.55f,.35f,.18f),0,.25f);

        var room=Group("01 | Room").transform;
        Box("Floor",room,new Vector3(0,-.06f,0),new Vector3(6,.12f,6),white);
        Box("Feature Wall",room,new Vector3(0,1.5f,2.5f),new Vector3(6,3,.12f),navy);
        Box("Left Wall",room,new Vector3(-3,1.5f,0),new Vector3(.12f,3,5),white);
        Box("Training Mat",room,new Vector3(0,.006f,0),new Vector3(2.6f,.012f,2.8f),navy,false);
        for(int s=-1;s<=1;s+=2)
            Box("Mat Boundary",room,new Vector3(s*1.28f,.014f,0),new Vector3(.012f,.002f,2.7f),teal,false);
        Text("Wall Title","BABYFOOT",room,new Vector3(0,2.03f,2.42f),.16f,Color.white,Vector3.zero);
        Text("Wall Subtitle","PERCEPTION  /  REACTION  /  ANTICIPATION",room,new Vector3(0,1.68f,2.42f),.027f,new Color(.3f,.8f,.83f),Vector3.zero);
        Text("Wall Edition","M2 MTI3D     -     VR LAB     /     01",room,new Vector3(0,1.49f,2.42f),.022f,new Color(.65f,.7f,.74f),Vector3.zero);
        Box("Wall Accent",room,new Vector3(0,1.32f,2.42f),new Vector3(1.8f,.012f,.01f),teal,false);

        var table=Group("02 | Table - provisional Leonhart Pro reference").transform;
        // metres: cabinet 1.43 x .74, total height .90. Field top .755 is provisional.
        Box("Cabinet",table,new Vector3(0,.645f,0),new Vector3(.74f,.16f,1.43f),navy);
        Box("Playfield 1.113 x 0.680 m",table,new Vector3(0,.745f,0),new Vector3(.68f,.02f,1.113f),pitch);
        for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
        {
            Box("Leg",table,new Vector3(x*.285f,.285f,z*.58f),new Vector3(.085f,.57f,.085f),navy);
            Shape("Adjustable Foot",PrimitiveType.Cylinder,table,new Vector3(x*.285f,.018f,z*.58f),new Vector3(.10f,.018f,.10f),rubber);
        }
        for(int s=-1;s<=1;s+=2)
        {
            Box("Side Wall",table,new Vector3(s*.35575f,.8225f,0),new Vector3(.0315f,.135f,1.43f),white);
            Box("Side Rail",table,new Vector3(s*.35575f,.893f,0),new Vector3(.036f,.014f,1.43f),navy);
            Box("Side Accent",table,new Vector3(s*.373f,.69f,0),new Vector3(.006f,.018f,1.26f),teal,false);
            // Goal opening: .205 wide x .080 high. Provisional assembly of published spare-part dimensions.
            for(int x=-1;x<=1;x+=2)
                Box("End Wall",table,new Vector3(x*.22125f,.8225f,s*.57225f),new Vector3(.2375f,.135f,.0315f),white);
            Box("Goal Lintel",table,new Vector3(0,.868f,s*.57225f),new Vector3(.205f,.064f,.0315f),white);
            Box("Goal Recess Floor",table,new Vector3(0,.745f,s*.624f),new Vector3(.205f,.02f,.135f),rubber);
            Box("Goal Back",table,new Vector3(0,.80f,s*.694f),new Vector3(.205f,.11f,.025f),rubber);
            Box("End Deck",table,new Vector3(0,.89f,s*.646f),new Vector3(.74f,.02f,.138f),navy);
            for(int i=0;i<10;i++)
                Box("Score Bead "+i,table,new Vector3(-.135f+i*.03f,.914f,s*.647f),new Vector3(.024f,.025f,.024f),s<0?teal:red,false);
        }
        var markings=Group("Field Markings",table).transform;
        Box("Centre Line",markings,new Vector3(0,.756f,0),new Vector3(.66f,.001f,.003f),line,false);
        for(int s=-1;s<=1;s+=2)
        {
            Box("Touchline",markings,new Vector3(s*.326f,.756f,0),new Vector3(.003f,.001f,1.08f),line,false);
            Box("Goal Line",markings,new Vector3(0,.756f,s*.54f),new Vector3(.65f,.001f,.003f),line,false);
            Box("Penalty Area Front",markings,new Vector3(0,.756f,s*.38f),new Vector3(.36f,.001f,.003f),line,false);
            for(int x=-1;x<=1;x+=2)
                Box("Penalty Area Side",markings,new Vector3(x*.18f,.756f,s*.46f),new Vector3(.003f,.001f,.16f),line,false);
        }
        for(int i=0;i<64;i++)
        {
            float a=i*Mathf.PI*2/64;
            var segment=Box("Centre Circle",markings,new Vector3(Mathf.Cos(a)*.095f,.756f,Mathf.Sin(a)*.095f),new Vector3(.003f,.001f,.011f),line,false);
            segment.transform.localEulerAngles=new Vector3(0,-a*Mathf.Rad2Deg,0);
        }
        var rods=Group("Rods - independent slide and rotation pivots",table).transform;
        float[] zs={-.5565f,-.375f,-.225f,-.075f,.075f,.225f,.375f,.5565f};
        int[] counts={1,2,3,5,5,3,2,1}; bool[] blues={true,true,false,true,false,true,false,false};
        for(int i=0;i<8;i++)
        {
            var rod=Group((blues[i]?"Cyan":"Coral")+" - "+(counts[i]==1?"Goalkeeper":counts[i]+" Players"),rods).transform;
            rod.localPosition=new Vector3(0,.84f,zs[i]);
            var shaft=Shape("Shaft 16 mm",PrimitiveType.Cylinder,rod,Vector3.zero,new Vector3(.016f,.66f,.016f),metal,false);
            shaft.transform.localEulerAngles=new Vector3(0,0,90);
            float handleSide=blues[i]?1:-1;
            var handle=Shape("Grip",PrimitiveType.Cylinder,rod,new Vector3(handleSide*.59f,0,0),new Vector3(.031f,.065f,.031f),rubber,false);
            handle.transform.localEulerAngles=new Vector3(0,0,90);
            for(int s=-1;s<=1;s+=2)
            {
                var bumper=Shape("Bumper",PrimitiveType.Cylinder,rod,new Vector3(s*.31f,0,0),new Vector3(.035f,.022f,.035f),rubber,false);
                bumper.transform.localEulerAngles=new Vector3(0,0,90);
            }
            for(int j=0;j<counts[i];j++)
            {
                float spacing=counts[i]==2?.235f:counts[i]==3?.185f:.115f;
                var player=Group("Player "+(j+1),rod).transform;
                player.localPosition=new Vector3((j-(counts[i]-1)*.5f)*spacing,0,0);
                Material team=blues[i]?teal:red;
                Shape("Head",PrimitiveType.Sphere,player,new Vector3(0,.038f,0),Vector3.one*.026f,team,false);
                Box("Torso",player,new Vector3(0,.003f,0),new Vector3(.027f,.044f,.025f),team);
                Box("Leg",player,new Vector3(0,-.031f,0),new Vector3(.019f,.025f,.018f),team);
                Box("Foot",player,new Vector3(0,-.055f,0),new Vector3(.023f,.028f,.025f),team);
            }
        }
        var ball=Shape("Ball - provisional diameter 35 mm",PrimitiveType.Sphere,table,new Vector3(.08f,.775f,-.13f),Vector3.one*.035f,white);
        var rb=ball.AddComponent<Rigidbody>(); rb.mass=.023f; rb.isKinematic=true;
        // Static layout preview: dynamics and measurement are deliberately a later milestone.
        var lighting=Group("03 | Lighting").transform;
        var sun=Group("Key Light",lighting); sun.transform.rotation=Quaternion.Euler(48,-30,0);
        var light=sun.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f; light.shadows=LightShadows.Soft;
        var fill=Group("Fill",lighting); fill.transform.rotation=Quaternion.Euler(40,145,0);
        var fl=fill.AddComponent<Light>(); fl.type=LightType.Directional; fl.intensity=.45f; fl.color=new Color(.72f,.85f,1);
        RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.5f,.57f,.65f);
        RenderSettings.ambientEquatorColor=new Color(.28f,.32f,.36f); RenderSettings.ambientGroundColor=new Color(.15f,.17f,.18f);
        var cameras=Group("04 | Viewing").transform;
        var camgo=Group("Desktop Preview - active without headset",cameras); camgo.tag="MainCamera";
        camgo.transform.position=new Vector3(1.65f,1.65f,-1.85f); camgo.transform.LookAt(new Vector3(0,.98f,0));
        var cam=camgo.AddComponent<Camera>(); cam.fieldOfView=52; cam.nearClipPlane=.03f; cam.farClipPlane=30;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.04f,.07f,.1f); camgo.AddComponent<AudioListener>();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab");
        if(prefab)
        {
            var xr=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            xr.name="VR Rig - enable with headset; disable Desktop Preview"; xr.transform.SetParent(cameras,false);
            xr.transform.position=new Vector3(.95f,0,-.25f); xr.transform.rotation=Quaternion.Euler(0,-90,0); xr.SetActive(false);
        }
        Text("Prototype Status","01  /  DEFENSE",room,new Vector3(0,1.13f,2.42f),.035f,Color.white,Vector3.zero);
        EditorSceneManager.SaveScene(scene,ScenePath);
        AssetDatabase.SaveAssets();
        if(SceneView.lastActiveSceneView)
            SceneView.lastActiveSceneView.LookAt(new Vector3(0,.68f,0),Quaternion.Euler(27,-42,0),2.2f);
        Selection.activeGameObject=table.gameObject;
        Capture(cam);
        File.WriteAllText("Library/BabyFootSceneBuild.txt","SUCCESS\nScene: "+ScenePath+"\nRods: 8\nPlayers: 22\nXR prefab: "+(prefab?"present, inactive":"missing")+"\n");
        Debug.Log("BabyFoot: first scene created and saved. 8 rods, 22 players, desktop preview and inactive VR rig. Interior dimensions provisional. No gameplay or eye tracking yet.");
    }
    static void Capture(Camera cam)
    {
        var rt=new RenderTexture(1600,1000,24); var prev=RenderTexture.active;
        var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try { cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,1600,1000),0,0); tex.Apply(); File.WriteAllBytes("Library/BabyFootPreview.png",tex.EncodeToPNG()); }
        finally { cam.targetTexture=null; RenderTexture.active=prev; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
    }
}
