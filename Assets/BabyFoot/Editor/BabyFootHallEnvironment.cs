using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// One-time static venue dressing. No new realtime lights, particles or per-frame scripts.
[InitializeOnLoad]
internal static class BabyFootHallEnvironment
{
    const string Folder="Assets/BabyFoot/Environment";
    const string Marker="Library/BabyFootHallEnvironmentV1.txt";
    static Transform root;
    static Mesh cube;
    sealed class Batch { public Material material;public List<CombineInstance> pieces=new List<CombineInstance>(); }
    static readonly Dictionary<string,Batch> batches=new Dictionary<string,Batch>();
    static BabyFootHallEnvironment(){ if(!BabyFootLegacyAutoSetup.Enabled)return; EditorApplication.delayCall+=Apply;}
    static void Apply()
    {
        if(File.Exists(Marker))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Apply;return;}
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype")return;
        try{Build();}catch(Exception ex){File.WriteAllText(Marker,"FAIL: "+ex);Debug.LogError("BabyFoot hall: "+ex);}
    }
    static Material Mat(string name,Color colour,float smooth=.15f,float metal=0,Color? emission=null)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=name;material.enableInstancing=true;
        material.SetColor("_BaseColor",colour);material.SetFloat("_Smoothness",smooth);material.SetFloat("_Metallic",metal);
        if(emission.HasValue){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",emission.Value);}
        AssetDatabase.CreateAsset(material,Folder+"/Materials/"+name+".mat");return material;
    }
    static Texture2D Surface(string name,int size,Color baseColour,float variation)
    {
        var texture=new Texture2D(size,size,TextureFormat.RGB24,true,false);texture.name=name;texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=4;
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float grain=Mathf.PerlinNoise(x*.43f,y*.43f)*.45f+Mathf.PerlinNoise(x*.035f,y*.035f)*.55f-.5f;
            float pore=Mathf.PerlinNoise(x*1.23f+31,y*1.23f+17)>.77f?-.045f:0;
            pixels[y*size+x]=baseColour*(1+grain*variation+pore);
        }
        texture.SetPixels(pixels);texture.Apply(true,false);AssetDatabase.CreateAsset(texture,Folder+"/Textures/"+name+".asset");return texture;
    }
    static void Box(string name,Material material,Vector3 position,Vector3 scale,Quaternion? rotation=null)
    {
        if(!batches.TryGetValue(material.name,out var batch)){batch=new Batch{material=material};batches.Add(material.name,batch);}
        batch.pieces.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(position,rotation??Quaternion.identity,scale)});
    }
    static void Label(string name,string words,Vector3 position,float height,Vector3 rotation,Color colour)
    {
        var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localEulerAngles=rotation;
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var text=go.AddComponent<TextMesh>();text.font=font;text.fontSize=64;text.characterSize=height*.17f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text=words;text.color=colour;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    static void Build()
    {
        var scene=SceneManager.GetActiveScene();var exercise=UnityEngine.Object.FindFirstObjectByType<BabyFootDefense>();
        if(!exercise)throw new Exception("No gameplay scene loaded.");
        if(GameObject.Find("Competition Hall Surroundings"))throw new Exception("Hall already exists without completion marker; inspect before rebuilding.");
        Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Textures");Directory.CreateDirectory("Assets/BabyFoot/Scenes/Backups");
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/BabyFoot/Scenes/Backups/Before_Hall_Environment.unity"),true);
        int oldLights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
        int oldColliders=exercise.ball.parent.GetComponentsInChildren<Collider>(true).Length;
        var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=temporary.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(temporary);
        root=new GameObject("Competition Hall Surroundings").transform;
        var legacyRoom=GameObject.Find("Room");if(legacyRoom)legacyRoom.SetActive(false);
        var floor=Mat("Hall Concrete Floor",Color.white,.12f);floor.SetTexture("_BaseMap",Surface("Concrete Aggregate",512,new Color(.25f,.265f,.275f),.35f));floor.SetTextureScale("_BaseMap",new Vector2(9,10));
        var wall=Mat("Hall Warm Acoustic Walls",Color.white,.08f);wall.SetTexture("_BaseMap",Surface("Acoustic Plaster",256,new Color(.30f,.31f,.325f),.18f));wall.SetTextureScale("_BaseMap",new Vector2(8,3));
        var dark=Mat("Hall Charcoal Panels",new Color(.085f,.095f,.11f));
        var steel=Mat("Hall Steel Structure",new Color(.11f,.135f,.16f),.35f,.5f);
        var ceiling=Mat("Hall Roof Liner",new Color(.095f,.105f,.12f),.08f);
        var ivory=Mat("Hall Ivory Fixtures",new Color(.70f,.72f,.70f),.25f,.15f);
        var timber=Mat("Hall Bench Timber",new Color(.30f,.20f,.12f),.25f);
        var glass=Mat("Hall Dark Glazing",new Color(.06f,.105f,.13f),.65f,.3f);
        var strip=Mat("Hall Warm Light Diffusers",new Color(.73f,.76f,.77f),.2f,0,new Color(.8f,.9f,1)*.45f);
        var green=Mat("Hall Exit Light",new Color(.035f,.22f,.13f),.1f,0,new Color(.04f,.45f,.18f)*.65f);
        var paint=Mat("Hall Muted Floor Markings",new Color(.46f,.48f,.46f),.05f);
        var accent=Mat("Hall Violet Event Branding",new Color(.16f,.07f,.22f));
        var seam=Mat("Hall Floor Joint",new Color(.16f,.175f,.18f),.05f);
        const float X=8.5f,Z=9.5f,H=6.6f;
        Box("Continuous outer floor",floor,new Vector3(0,-.075f,-.3f),new Vector3(17.4f,.1f,20.2f));
        // Back and side walls; front wall is segmented around a real recessed entry.
        Box("Rear wall",wall,new Vector3(0,H*.5f,Z),new Vector3(17.4f,H,.2f));
        for(int s=-1;s<=1;s+=2)
        {
            Box("Side wall",wall,new Vector3(s*X,H*.5f,0),new Vector3(.2f,H,19));
            Box("Entry wing",wall,new Vector3(s*5.025f,H*.5f,-Z),new Vector3(7.15f,H,.2f));
            Box("Skirting side",dark,new Vector3(s*8.38f,.22f,0),new Vector3(.10f,.45f,19));
            Box("Skirting rear",dark,new Vector3(s*4.25f,.22f,9.36f),new Vector3(8.5f,.45f,.12f));
        }
        Box("Entry header",wall,new Vector3(0,4.68f,-Z),new Vector3(2.9f,3.84f,.2f));
        Box("Entry recessed floor",floor,new Vector3(0,-.075f,-10.5f),new Vector3(2.9f,.1f,2.4f));
        Box("Entry back",dark,new Vector3(0,1.4f,-11.65f),new Vector3(2.9f,2.8f,.18f));
        for(int s=-1;s<=1;s+=2)
        {
            Box("Entry reveal",dark,new Vector3(s*1.45f,1.4f,-10.5f),new Vector3(.18f,2.8f,2.4f));
            Box("Recessed door",glass,new Vector3(s*.65f,1.22f,-11.52f),new Vector3(1.24f,2.44f,.05f));
            Box("Door frame",ivory,new Vector3(s*1.31f,1.25f,-11.45f),new Vector3(.05f,2.5f,.07f));
            Box("Door pull bar",ivory,new Vector3(s*.23f,1.05f,-11.39f),new Vector3(.035f,.52f,.045f));
            Box("Entry edge",steel,new Vector3(s*1.5f,1.4f,-9.36f),new Vector3(.1f,2.8f,.12f));
        }
        Box("Entry ceiling",ceiling,new Vector3(0,2.82f,-10.5f),new Vector3(3,.12f,2.5f));
        Box("Entrance sign",dark,new Vector3(0,3.15f,-9.32f),new Vector3(3.5f,.5f,.12f));
        Label("Entrance lettering","ENTRANCE",new Vector3(0,3.15f,-9.24f),.22f,new Vector3(0,180,0),new Color(.8f,.83f,.82f));
        // Ribbed roof, structural beams and integrated diffusers; zero extra Light components.
        Box("Roof",ceiling,new Vector3(0,H+.08f,0),new Vector3(17.4f,.16f,19.4f));
        for(int j=-3;j<=3;j++)
        {
            float z=j*2.75f;
            Box("Roof cross beam",steel,new Vector3(0,6.35f,z),new Vector3(17,.26f,.16f));
            for(int s=-1;s<=1;s+=2)
            {
                Box("Structural pier",steel,new Vector3(s*8.23f,3.15f,z),new Vector3(.24f,6.3f,.27f));
                Box("Pier base",dark,new Vector3(s*8.23f,.25f,z),new Vector3(.38f,.5f,.4f));
                Box("Roof knee brace",steel,new Vector3(s*7.7f,5.95f,z),new Vector3(.14f,1.25f,.16f),Quaternion.Euler(0,0,s*45));
            }
        }
        for(int i=-8;i<=8;i++)Box("Roof rib",steel,new Vector3(i,6.48f,0),new Vector3(.035f,.06f,19));
        for(int x=-1;x<=1;x+=2)for(int z=-2;z<=2;z++)
        {
            Box("Light housing",steel,new Vector3(x*5.8f,6.22f,z*3.25f),new Vector3(.7f,.10f,1.65f));
            Box("Recessed diffuser",strip,new Vector3(x*5.8f,6.155f,z*3.25f),new Vector3(.54f,.022f,1.48f));
        }
        // Wall acoustics and panel seams add scale and depth behind the event stands.
        for(int s=-1;s<=1;s+=2)for(int j=-2;j<=2;j++)
        {
            Box("Acoustic panel",dark,new Vector3(s*8.365f,3.65f,j*3.25f),new Vector3(.07f,2.8f,2.55f));
            for(int rib=-5;rib<=5;rib++)Box("Acoustic vertical rib",steel,new Vector3(s*8.31f,3.65f,j*3.25f+rib*.20f),new Vector3(.025f,2.65f,.018f));
        }
        Box("Rear event fascia",accent,new Vector3(0,3.7f,9.34f),new Vector3(10.5f,1.65f,.12f));
        Label("Hall identity","BABYFOOT  /  TRAINING HALL",new Vector3(0,3.85f,9.24f),.42f,Vector3.zero,new Color(.8f,.83f,.85f));
        Label("Hall subtitle","FOCUS     /     PRECISION     /     PLAY",new Vector3(0,3.32f,9.23f),.15f,Vector3.zero,new Color(.55f,.62f,.68f));
        for(int s=-1;s<=1;s+=2)
        {
            Box("Side access door",glass,new Vector3(s*8.36f,1.2f,-7.2f),new Vector3(.07f,2.4f,1.3f));
            Box("Side exit sign",green,new Vector3(s*8.27f,2.67f,-7.2f),new Vector3(.08f,.29f,.82f));
            Label("Exit lettering "+s,"EXIT",new Vector3(s*8.20f,2.67f,-7.2f),.12f,new Vector3(0,s*90,0),new Color(.8f,1,.86f));
            // Waiting benches and equipment carts stay well outside the playing area.
            for(int z=-1;z<=1;z+=2)
            {
                Box("Bench seat",timber,new Vector3(s*6.9f,.46f,z*5.3f),new Vector3(.45f,.07f,1.8f));
                Box("Bench back",timber,new Vector3(s*7.08f,.76f,z*5.3f),new Vector3(.07f,.55f,1.8f));
                for(int foot=-1;foot<=1;foot+=2)Box("Bench support",steel,new Vector3(s*6.9f,.22f,z*5.3f+foot*.65f),new Vector3(.35f,.44f,.07f));
            }
            for(int level=0;level<3;level++)Box("Equipment rack shelf",steel,new Vector3(s*6.6f,.25f+level*.35f,7.8f),new Vector3(1.15f,.05f,.55f));
            for(int foot=-1;foot<=1;foot+=2)Box("Rack upright",steel,new Vector3(s*6.6f+foot*.5f,.6f,7.8f),new Vector3(.045f,1.2f,.55f));
            Box("Equipment case",dark,new Vector3(s*6.6f,.48f,7.8f),new Vector3(.65f,.35f,.43f));
            Box("Case latch",ivory,new Vector3(s*6.6f,.48f,7.57f),new Vector3(.12f,.07f,.02f));
            Box("Circulation stripe",paint,new Vector3(s*5.65f,-.018f,0),new Vector3(.035f,.002f,14));
        }
        for(int i=-4;i<=4;i++)
        {
            Box("Concrete joint X",seam,new Vector3(i*2,-.017f,0),new Vector3(.008f,.001f,19));
            Box("Concrete joint Z",seam,new Vector3(0,-.017f,i*2),new Vector3(17,.001f,.008f));
        }
        int triangles=0;
        foreach(var item in batches.Values)
        {
            var mesh=new Mesh{name=item.material.name+" Static Mesh"};mesh.CombineMeshes(item.pieces.ToArray(),true,true);mesh.RecalculateBounds();
            triangles+=mesh.triangles.Length/3;AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/"+mesh.name+".asset");
            var go=new GameObject(item.material.name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=item.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        }
        var floorCollider=root.gameObject.AddComponent<BoxCollider>();floorCollider.center=new Vector3(0,-.075f,-.3f);floorCollider.size=new Vector3(17.4f,.1f,20.2f);
        var sky=new Material(Shader.Find("Skybox/Procedural")){name="Hall Night Neutral Sky"};
        sky.SetFloat("_SunDisk",0);sky.SetFloat("_Exposure",.35f);sky.SetFloat("_AtmosphereThickness",.7f);sky.SetColor("_SkyTint",new Color(.12f,.16f,.23f));sky.SetColor("_GroundColor",new Color(.07f,.08f,.10f));
        AssetDatabase.CreateAsset(sky,Folder+"/Materials/Hall Skybox.mat");RenderSettings.skybox=sky;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.13f,.145f,.17f);RenderSettings.fogStartDistance=12;RenderSettings.fogEndDistance=36;
        foreach(var cam in Resources.FindObjectsOfTypeAll<Camera>())if(cam.gameObject.scene==scene){cam.clearFlags=CameraClearFlags.Skybox;cam.backgroundColor=new Color(.045f,.055f,.075f);}
        if(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length!=oldLights)throw new Exception("Realtime light count changed.");
        if(exercise.ball.parent.GetComponentsInChildren<Collider>(true).Length!=oldColliders)throw new Exception("Gameplay colliders changed.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Capture("Library/BabyFootHall_Table.png",new Vector3(1.55f,1.6f,-1.75f),new Vector3(0,.8f,0),65);
        Capture("Library/BabyFootHall_Surroundings.png",new Vector3(.7f,1.65f,-.3f),new Vector3(0,2.0f,-9),80);
        Capture("Library/BabyFootHall_Roof.png",new Vector3(.7f,1.65f,-.3f),new Vector3(0,6.5f,2),80);
        File.WriteAllText(Marker,"PASS: closed competition hall with recessed entrance, roof structure, acoustic walls, concrete surfaces, circulation lanes and event furniture.\nAdded static triangles: "+triangles+"\nGeometry material batches: "+batches.Count+"\nExtra realtime lights: 0\nExtra floor colliders: 1\nGameplay colliders preserved: "+oldColliders+"\nSkybox and camera clears corrected; gentle distant linear fog. Original gameplay lighting retained. New surroundings do not cast realtime shadows. Scene saved with pre-change backup.\n");
        Debug.Log("BabyFoot: competition hall environment saved ("+triangles+" triangles / "+batches.Count+" static material batches).");
    }
    internal static void Capture(string path,Vector3 position,Vector3 target,float fov)
    {
        var go=new GameObject("Temporary Hall Review Camera");var cam=go.AddComponent<Camera>();go.transform.position=position;go.transform.LookAt(target);cam.fieldOfView=fov;cam.nearClipPlane=.03f;cam.farClipPlane=80;cam.clearFlags=CameraClearFlags.Skybox;
        var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
