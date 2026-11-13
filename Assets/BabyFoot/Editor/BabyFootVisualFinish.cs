using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Explicit, local editor requests only. A fresh clone never runs visual migrations.
[InitializeOnLoad]
public static class BabyFootVisualFinish
{
    const string Request="Library/BabyFootVisualFinish.request";
    const string Output="Library/BabyFootChecks/VisualFinish/";
    static BabyFootVisualFinish(){EditorApplication.update+=Poll;}
    [MenuItem("Tools/BabyFoot/Visual Finish/Inspect and capture")]
    public static void InspectMenu(){File.WriteAllText(Request,"inspect");}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string action=File.ReadAllText(Request).Trim();File.Delete(Request);Directory.CreateDirectory(Output);
        try
        {
            if(action=="inspect")Inspect();
            else if(action=="apply")Apply();
            else if(action=="capture")CaptureSet("After");
            else if(action=="gameplay")BabyFootFreeMatchCheck.Run(false);
            else if(action=="review")BabyFootSpectatorReview.Run();
            else if(action=="bake")BakeInterior();
            else if(action=="cleanup")Cleanup();
            File.WriteAllText(Output+"Status.txt","OK "+action+" "+DateTime.Now.ToString("s"));
        }
        catch(Exception e){File.WriteAllText(Output+"Status.txt","FAIL "+action+"\n"+e);Debug.LogException(e);}
    }
    static void Inspect()
    {
        var s=SceneManager.GetActiveScene();var b=new StringBuilder();
        b.AppendLine(s.path+" dirty="+s.isDirty+" pipeline="+GraphicsSettings.currentRenderPipeline);
        b.AppendLine("Ambient "+RenderSettings.ambientMode+" "+RenderSettings.ambientLight+" intensity="+RenderSettings.ambientIntensity);
        foreach(var go in s.GetRootGameObjects())b.AppendLine("ROOT "+go.name+" active="+go.activeSelf);
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(!r.enabled)continue;
            b.AppendLine("RENDER "+PathOf(r.transform)+" bounds="+r.bounds+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name+" ["+AssetDatabase.GetAssetPath(m)+"]":"null")));
        }
        foreach(var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))b.AppendLine("LIGHT "+l.name+" "+l.type+" intensity="+l.intensity+" color="+l.color+" shadows="+l.shadows+" pos="+l.transform.position+" rot="+l.transform.eulerAngles);
        foreach(var v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))b.AppendLine("VOLUME "+v.name+" "+v.sharedProfile);
        File.WriteAllText(Output+"Inspect.txt",b.ToString());CaptureSet("Before");
    }
    static string PathOf(Transform t){return t.parent?PathOf(t.parent)+"/"+t.name:t.name;}
    static void CaptureSet(string prefix)
    {
        Capture(prefix+"_Table",new Vector3(1.40f,1.48f,-1.55f),new Vector3(0,.65f,0),48);
        Capture(prefix+"_Player",new Vector3(.72f,1.40f,-.50f),new Vector3(0,.79f,0),65);
        Capture(prefix+"_Hall",new Vector3(2.8f,2.1f,-3.8f),new Vector3(0,1.15f,.8f),65);
    }
    static void Capture(string name,Vector3 position,Vector3 target,float fov)
    {
        var controllers=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&PathOf(r.transform).Contains("Controller Visual")).ToArray();
        foreach(var r in controllers)r.enabled=false;
        var go=new GameObject("Temporary Finish Review Camera");var cam=go.AddComponent<Camera>();
        go.transform.position=position;go.transform.LookAt(target);cam.fieldOfView=fov;cam.nearClipPlane=.02f;cam.farClipPlane=70;cam.allowHDR=true;cam.allowMSAA=true;
        var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var rt=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.antiAliasing=4;var old=RenderTexture.active;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes(Output+name+".png",tex.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);foreach(var r in controllers)if(r)r.enabled=true;}
    }
    const string Assets="Assets/BabyFoot/VisualFinish/";
    const string ScenePath="Assets/BabyFoot/Scenes/BabyFoot_FreeMatch_Polished.unity";
    static Material Lit(string name,Color color,float metal,float smooth)
    {
        string path=Assets+"Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
        m.SetFloat("_EnvironmentReflections",1);m.SetFloat("_SpecularHighlights",1);m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static Texture2D ImportMap(string file,bool normal=false,bool linear=false,bool readable=false)
    {
        string path=Assets+"Textures/"+file;
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!importer)throw new Exception("Missing texture: "+path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&!linear;
        importer.mipmapEnabled=true;importer.anisoLevel=8;importer.maxTextureSize=2048;importer.isReadable=readable;importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Pbr(Material m,string id,float bump,float finish)
    {
        m.SetTexture("_BaseMap",ImportMap(id+"_Diffuse.jpg"));m.SetTexture("_BumpMap",ImportMap(id+"_nor_gl.jpg",true));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",bump);
        if(id=="concrete_floor_02")
        {
            var diffuse=ImportMap(id+"_Diffuse.jpg",false,false,true);var values=diffuse.GetPixels();
            for(int i=0;i<values.Length;i++){float value=.58f+(values[i].grayscale-.5f)*.20f;values[i]=new Color(value*.97f,value*.99f,value,1);}
            var neutral=new Texture2D(diffuse.width,diffuse.height,TextureFormat.RGB24,true);neutral.SetPixels(values);neutral.Apply();
            File.WriteAllBytes(Assets+"Textures/Quiet Concrete.png",neutral.EncodeToPNG());UnityEngine.Object.DestroyImmediate(neutral);AssetDatabase.ImportAsset(Assets+"Textures/Quiet Concrete.png");m.SetTexture("_BaseMap",ImportMap("Quiet Concrete.png"));
        }
        var rough=ImportMap(id+"_Rough.jpg",false,true,true);var px=rough.GetPixels();
        for(int i=0;i<px.Length;i++)px[i]=new Color(0,0,0,Mathf.Clamp01((1-px[i].r)*.5f+finish));
        var tex=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,true,true);tex.SetPixels(px);tex.Apply();
        string file=id+"_UnityMetalSmooth.png";File.WriteAllBytes(Assets+"Textures/"+file,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(Assets+"Textures/"+file);
        m.SetTexture("_MetallicGlossMap",ImportMap(file,false,true));m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);EditorUtility.SetDirty(m);
    }
    static Texture2D Micro(string name,bool normal,float intensity)
    {
        string path=Assets+"Textures/"+name+".png";
        var tex=new Texture2D(256,256,TextureFormat.RGBA32,true,true);var pixels=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            uint h=unchecked((uint)(x*73856093)^(uint)(y*19349663));h^=h>>13;h*=1274126177;h^=h>>16;
            float n=(h%10000)/10000f-.5f;
            pixels[y*256+x]=normal?new Color(.5f+n*intensity,.5f+Mathf.Sin((x+y)*2.3f)*intensity*.3f,1,1):new Color(1+n*intensity,1+n*intensity,1+n*intensity,1);
        }
        tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);return ImportMap(name+".png",normal,!normal);
    }
    static void Planar(MeshRenderer r,string name,float scale,bool wood)
    {
        var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)return;
        var original=mf.sharedMesh;var mesh=new Mesh{name=name};var verts=original.vertices;var norms=original.normals;var tris=original.triangles;
        var v=new Vector3[tris.Length];var n=new Vector3[tris.Length];var uv=new Vector2[tris.Length];var indices=new int[tris.Length];
        for(int i=0;i<tris.Length;i++)
        {
            int old=tris[i];v[i]=verts[old];n[i]=norms[old];indices[i]=i;
            var p=r.transform.TransformPoint(v[i]);var dir=r.transform.TransformDirection(n[i]);
            if(wood)uv[i]=Mathf.Abs(dir.x)>.55f?new Vector2(p.y/scale,p.z/scale):Mathf.Abs(dir.z)>.55f?new Vector2(p.y/scale,p.x/scale):new Vector2(p.x/scale,p.z/scale);
            else uv[i]=Mathf.Abs(dir.y)>.55f?new Vector2(p.x/scale,p.z/scale):Mathf.Abs(dir.x)>.55f?new Vector2(p.z/scale,p.y/scale):new Vector2(p.x/scale,p.y/scale);
        }
        mesh.indexFormat=tris.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateTangents();mesh.RecalculateBounds();
        string path=Assets+"Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
        mf.sharedMesh=mesh;
    }
    static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.isDirty)throw new Exception("Current scene has unsaved changes; preserve them before applying finish.");
        if(scene.path==ScenePath)scene=EditorSceneManager.OpenScene("Assets/BabyFoot/Scenes/BabyFoot_FreeMatch.unity");
        if(scene.path!=ScenePath)
        {
            if(scene.name!="BabyFoot_FreeMatch")throw new Exception("Expected the free-match scene.");
            if(!EditorSceneManager.SaveScene(scene,ScenePath,true))throw new Exception("Scene copy failed.");
            scene=EditorSceneManager.OpenScene(ScenePath);
        }
        foreach(string dir in new[]{"Materials","Meshes","Lighting"})Directory.CreateDirectory(Assets+dir);
        AssetDatabase.Refresh();
        var table=scene.GetRootGameObjects().First(g=>g.name.StartsWith("Table Leonhart"));
        int colliders=table.GetComponentsInChildren<Collider>(true).Length;
        var match=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatch>();
        string signature=GeometrySignature(table);
        var walnut=Lit("Satin Walnut",Color.white,0,.4f);Pbr(walnut,"fine_grained_wood",.16f,.30f);
        var concrete=Lit("Honed Concrete",new Color(.66f,.68f,.70f),0,.2f);Pbr(concrete,"concrete_floor_02",.12f,.12f);
        var graphite=Lit("Graphite Powder Coat",new Color(.095f,.105f,.112f),.32f,.35f);
        var chrome=Lit("Polished Chrome",new Color(.82f,.85f,.87f),.94f,.82f);
        var steel=Lit("Brushed Aluminium",new Color(.52f,.56f,.60f),.82f,.52f);
        var ivory=Lit("Warm Ivory Laminate",new Color(.79f,.76f,.67f),0,.44f);
        var red=Lit("Coral Enamel",new Color(.62f,.055f,.045f),.08f,.48f);
        var blue=Lit("Cobalt Enamel",new Color(.025f,.12f,.48f),.08f,.48f);
        var rubber=Lit("Moulded Rubber",new Color(.037f,.039f,.041f),0,.21f);
        var skin=Lit("Painted Skin",new Color(.64f,.39f,.23f),0,.34f);
        var black=Lit("Black Player Paint",new Color(.029f,.025f,.023f),0,.4f);
        var pitch=Lit("Satin Green Playing Bed",new Color(.16f,.29f,.235f),0,.31f);
        var lines=Lit("Ivory Pitch Inlay",new Color(.80f,.79f,.69f),0,.30f);
        var ball=Lit("Warm Competition Ball",new Color(.91f,.85f,.65f),0,.27f);
        var gold=Lit("Champagne Detail",new Color(.50f,.39f,.22f),.72f,.57f);
        var micro=Micro("PolymerMicroNormal",true,.12f);var grain=Micro("PolymerMicroAlbedo",false,.045f);
        foreach(var m in new[]{graphite,rubber,pitch,ball}){m.SetTexture("_BumpMap",micro);m.SetFloat("_BumpScale",.25f);m.EnableKeyword("_NORMALMAP");m.SetTexture("_BaseMap",grain);m.SetTextureScale("_BaseMap",new Vector2(6,6));}
        foreach(var r in table.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                string name=mats[i]?mats[i].name:"";Material replace=null;
                if(name.Contains("Player_Team_Blue"))replace=blue;else if(name.Contains("Player_Team_Red"))replace=red;
                else if(name.Contains("Player_Skin"))replace=skin;else if(name.Contains("Player_Black")||name.Contains("Face_Details"))replace=black;
                else if(name.Contains("Steel")||name.Contains("Collar"))replace=chrome;
                else if(name.Contains("Rubber")||name.Contains("Handle_Graphite"))replace=rubber;
                else if(name.Contains("Warm White"))replace=ball;else if(name.Contains("Field Lines"))replace=lines;
                else if(name.Contains("Pitch"))replace=pitch;else if(name.Contains("Ivory"))replace=ivory;
                else if(name.Contains("Cyan"))replace=blue;else if(name.Contains("Coral"))replace=red;else if(name.Contains("Graphite"))replace=graphite;
                if(replace)mats[i]=replace;
            }
            if(r.name=="Table_Cabinet"||r.name=="Table_End_Decks"){mats=new[]{walnut};Planar(r,r.name+"_WalnutUV",1.1f,true);}
            if(r.name=="Table_Cabinet_Accent")mats=new[]{gold};
            if(r.name=="Table_Leg_Inlay")mats=new[]{graphite};
            r.sharedMaterials=mats;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            if(r.name=="Table_Playing_Surface")Planar(r,"Pitch_MetricUV",.45f,false);
        }
        // Scene-local replacements for embedded FBX materials: never edit import subassets.
        var arena=GameObject.Find("BabyFoot_Arena_LowPoly");
        var seats=Lit("Arena Upholstered Seats",new Color(.28f,.34f,.35f),0,.22f);seats.SetTexture("_BumpMap",micro);seats.EnableKeyword("_NORMALMAP");seats.SetFloat("_BumpScale",.18f);
        var dark=Lit("Arena Charcoal Architecture",new Color(.105f,.12f,.125f),.12f,.32f);
        var plum=Lit("Muted Plum Event Panels",new Color(.20f,.085f,.135f),0,.25f);
        var white=Lit("Architectural Ivory",new Color(.72f,.70f,.64f),0,.31f);
        var accent=Lit("Muted Brass Wayfinding",new Color(.46f,.34f,.18f),.35f,.45f);
        var lightMat=Lit("Warm Opal Diffuser",new Color(.9f,.83f,.68f),0,.35f);lightMat.EnableKeyword("_EMISSION");lightMat.SetColor("_EmissionColor",new Color(1,.88f,.68f)*4);lightMat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        foreach(var r in arena.GetComponentsInChildren<MeshRenderer>())
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                string name=mats[i]?mats[i].name:"";
                mats[i]=name.Contains("Silver")?steel:name.Contains("Seats")?seats:name.Contains("Purple")?plum:name.Contains("Cyan")?accent:name.Contains("White")?white:dark;
                if(r.name.Contains("Light_Lenses"))mats[i]=lightMat;
                if(r.name=="Floor_Carpet"||r.name=="Play_Zone_Midnight")mats[i]=concrete;
            }
            r.sharedMaterials=mats;
            if(r.name=="Floor_Carpet"||r.name=="Play_Zone_Midnight")Planar(r,r.name+"_MetricUV",2.4f,false);
        }
        var hall=GameObject.Find("Competition Hall Surroundings");
        foreach(var r in hall.GetComponentsInChildren<MeshRenderer>())
        {
            if(r.GetComponent<TextMesh>())continue;
            string name=r.name;var mat=r.sharedMaterial;
            if(name.Contains("Floor")&&!name.Contains("Markings")&&!name.Contains("Joint")){r.sharedMaterial=concrete;Planar(r,name+"_MetricUV",2.4f,false);}
            else if(name.Contains("Warm Light"))r.sharedMaterial=lightMat;
            else if(name.Contains("Steel"))r.sharedMaterial=steel;
            else if(name.Contains("Timber")){r.sharedMaterial=walnut;Planar(r,name+"_WalnutUV",1.1f,true);}
            else if(name.Contains("Violet"))r.sharedMaterial=plum;
            else if(mat)
            {
                string path=Assets+"Materials/"+name+".mat";var clone=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!clone){clone=new Material(mat);AssetDatabase.CreateAsset(clone,path);}
                clone.SetFloat("_EnvironmentReflections",1);clone.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                if(name.Contains("Acoustic"))clone.SetColor("_BaseColor",new Color(.84f,.82f,.77f));
                r.sharedMaterial=clone;
            }
        }
        SetupLighting();
        foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
            camera.allowHDR=true;camera.allowMSAA=true;
        }
        if(colliders!=table.GetComponentsInChildren<Collider>(true).Length||signature!=GeometrySignature(table)||match.rods.Length!=8)throw new Exception("Gameplay geometry changed.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        CaptureSet("After");
        File.WriteAllText(Output+"Verification.txt","PASS: scene copied; original scene and materials preserved.\nTable collider count: "+colliders+"; eight rods; transform/collider signature unchanged.\nPhotographic wood and concrete PBR maps; metric UV copies; original meshes unchanged.\nScene-scoped render pipeline and volume; saved visual scene.\n");
    }
    static string GeometrySignature(GameObject table)
    {
        return string.Join("\n",table.GetComponentsInChildren<Collider>(true).Select(c=>PathOf(c.transform)+" "+c.GetType()+" "+c.transform.position.ToString("F6")+" "+c.transform.rotation.ToString("F6")+" "+EditorJsonUtility.ToJson(c)));
    }
    static void BakeInterior()
    {
        if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Open the polished scene first.");
        var root=GameObject.Find("Professional Visual Finish");
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string path=PathOf(r.transform);
            if(!r.enabled||r.GetComponent<TextMesh>()||path.Contains("Rods -")||path.Contains("Ball -")||path.Contains("XR Origin")||path.Contains("Interface"))continue;
            if(!path.StartsWith("Table Leonhart")&&!path.StartsWith("BabyFoot_Arena")&&!path.StartsWith("Competition Hall"))continue;
            var filter=r.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
            if(filter.sharedMesh.uv2.Length==filter.sharedMesh.vertexCount&&filter.sharedMesh.name.StartsWith("Lightmapped ")){GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)|StaticEditorFlags.ContributeGI);r.receiveGI=ReceiveGI.Lightmaps;continue;}
            string meshPath=Assets+"Meshes/Lightmapped_"+filter.sharedMesh.name.Replace("/","_")+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!mesh){mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Lightmapped "+filter.sharedMesh.name;Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,meshPath);}
            filter.sharedMesh=mesh;GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)|StaticEditorFlags.ContributeGI);
            r.receiveGI=ReceiveGI.Lightmaps;
        }
        var group=root.GetComponent<LightProbeGroup>();if(!group)group=root.AddComponent<LightProbeGroup>();
        group.probePositions=(from x in new[]{-.65f,0f,.65f} from y in new[]{.60f,.88f,1.30f} from z in new[]{-.8f,0f,.8f} select new Vector3(x,y,z)-root.transform.position).ToArray();
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
        {
            string name="Baked Softbox "+x+" "+z;var g=GameObject.Find(name);if(!g){g=new GameObject(name);g.transform.SetParent(root.transform,true);}
            g.transform.position=new Vector3(x*1.05f,2.95f,z*1.2f);g.transform.rotation=Quaternion.Euler(90,0,0);
            var light=g.GetComponent<Light>();if(!light)light=g.AddComponent<Light>();light.type=LightType.Rectangle;light.lightmapBakeType=LightmapBakeType.Baked;light.areaSize=new Vector2(1.2f,.75f);light.intensity=5;light.color=new Color(1,.91f,.79f);light.range=8;
        }
        var key=GameObject.Find("Key Light").GetComponent<Light>();key.lightmapBakeType=LightmapBakeType.Mixed;
        Lightmapping.TryGetLightingSettings(out var settings);
        if(!settings||!AssetDatabase.GetAssetPath(settings).StartsWith(Assets))
        {
            settings=new LightingSettings();settings.name="Interior Baked Bounce";AssetDatabase.CreateAsset(settings,Assets+"Lighting/Interior Baked Bounce.lighting");Lightmapping.lightingSettings=settings;
        }
        settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;settings.lightmapResolution=22;settings.lightmapMaxSize=2048;settings.indirectSampleCount=64;settings.directSampleCount=32;settings.environmentSampleCount=32;settings.maxBounces=3;settings.mixedBakeMode=MixedLightingMode.IndirectOnly;
        settings.ao=true;settings.aoMaxDistance=.12f;settings.aoExponentIndirect=.65f;
        EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Lightmapping.bakeCompleted-=BakeComplete;Lightmapping.bakeCompleted+=BakeComplete;
        if(!Lightmapping.BakeAsync())throw new Exception("Lightmap bake did not start.");
        File.WriteAllText(Output+"Bake.txt","RUNNING "+DateTime.Now.ToString("s"));
    }
    static void BakeComplete()
    {
        Lightmapping.bakeCompleted-=BakeComplete;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(Output+"Bake.txt","COMPLETE maps="+LightmapSettings.lightmaps.Length+" probes="+(LightmapSettings.lightProbes?LightmapSettings.lightProbes.count:0));
        File.WriteAllText(Request,"capture");
    }
    static void Cleanup()
    {
        var dependencies=new System.Collections.Generic.HashSet<string>(AssetDatabase.GetDependencies(ScenePath,true));int removed=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{Assets+"Meshes"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!dependencies.Contains(path)&&path.StartsWith(Assets+"Meshes/")){if(AssetDatabase.DeleteAsset(path))removed++;}
        }
        File.WriteAllText(Output+"Cleanup.txt","Removed "+removed+" unused generated mesh copies; retained scene and lighting dependencies.");
        AssetDatabase.SaveAssets();CaptureSet("After");
    }
    static void SetupLighting()
    {
        var go=GameObject.Find("Professional Visual Finish");if(!go)go=new GameObject("Professional Visual Finish");
        var binding=go.GetComponent<BabyFootVisualProfile>();if(!binding)binding=go.AddComponent<BabyFootVisualProfile>();
        string pipelinePath=Assets+"Lighting/Balanced PCVR.asset";
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if(!pipeline){pipeline=UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);pipeline.name="Balanced PCVR";AssetDatabase.CreateAsset(pipeline,pipelinePath);}
        pipeline.supportsHDR=true;pipeline.maxAdditionalLightsCount=2;pipeline.shadowDistance=5;pipeline.shadowCascadeCount=2;
        var serialized=new SerializedObject(pipeline);serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;serialized.FindProperty("m_ReflectionProbeBlending").boolValue=true;serialized.FindProperty("m_ReflectionProbeBoxProjection").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
        string rendererPath=Assets+"Lighting/Interior Forward Renderer.asset";
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if(!renderer)
        {
            var originalRenderer=(UniversalRendererData)serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
            renderer=UnityEngine.Object.Instantiate(originalRenderer);renderer.name="Interior Forward Renderer";renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,rendererPath);
            var ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Subtle Contact Occlusion";AssetDatabase.AddObjectToAsset(ao,renderer);renderer.rendererFeatures.Add(ao);
            var settings=new SerializedObject(ao);var p=settings.FindProperty("m_Settings");
            p.FindPropertyRelative("Downsample").boolValue=true;p.FindPropertyRelative("Intensity").floatValue=1.2f;p.FindPropertyRelative("Radius").floatValue=.075f;p.FindPropertyRelative("DirectLightingStrength").floatValue=.22f;
            p.FindPropertyRelative("Samples").enumValueIndex=2;p.FindPropertyRelative("AOMethod").enumValueIndex=1;p.FindPropertyRelative("BlurQuality").enumValueIndex=1;
            settings.ApplyModifiedPropertiesWithoutUndo();ao.Create();EditorUtility.SetDirty(ao);renderer.SetDirty();EditorUtility.SetDirty(renderer);
        }
        serialized.Update();serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;serialized.ApplyModifiedPropertiesWithoutUndo();
        binding.enabled=false;binding.pipeline=pipeline;binding.enabled=true;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.58f,.63f,.68f);RenderSettings.ambientEquatorColor=new Color(.38f,.39f,.39f);RenderSettings.ambientGroundColor=new Color(.20f,.18f,.15f);
        RenderSettings.reflectionIntensity=.85f;RenderSettings.fog=true;RenderSettings.fogColor=new Color(.18f,.19f,.20f);RenderSettings.fogStartDistance=16;RenderSettings.fogEndDistance=42;
        var key=GameObject.Find("Key Light").GetComponent<Light>();key.transform.rotation=Quaternion.Euler(66,-35,0);key.intensity=1.65f;key.color=new Color(1,.93f,.82f);key.shadows=LightShadows.Soft;key.shadowStrength=.75f;key.shadowBias=.025f;key.shadowNormalBias=.015f;
        var fill=GameObject.Find("Fill").GetComponent<Light>();fill.intensity=.55f;fill.color=new Color(.80f,.88f,1);fill.transform.rotation=Quaternion.Euler(40,130,0);fill.shadows=LightShadows.None;
        RenderSettings.sun=key;
        var volume=go.GetComponent<Volume>();if(!volume)volume=go.AddComponent<Volume>();volume.isGlobal=true;
        string profilePath=Assets+"Lighting/Natural Interior Grade.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
        if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.ACES);
        if(!profile.TryGet<ColorAdjustments>(out var color)){color=profile.Add<ColorAdjustments>(true);AssetDatabase.AddObjectToAsset(color,profile);}color.postExposure.Override(.25f);color.contrast.Override(8);color.saturation.Override(-7);
        volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
        var probe=go.GetComponent<ReflectionProbe>();if(!probe)probe=go.AddComponent<ReflectionProbe>();probe.transform.position=new Vector3(0,1.25f,0);probe.size=new Vector3(9,6,10);probe.center=Vector3.zero;probe.boxProjection=true;probe.resolution=256;probe.hdr=true;probe.nearClipPlane=.08f;probe.farClipPlane=35;probe.mode=ReflectionProbeMode.Baked;probe.intensity=.85f;
        AssetDatabase.SaveAssets();
        string probePath=Assets+"Lighting/Training Hall Reflection.exr";
        if(!Lightmapping.BakeReflectionProbe(probe,probePath))throw new Exception("Reflection probe bake failed.");
        AssetDatabase.ImportAsset(probePath);probe.bakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(probePath);
        DynamicGI.UpdateEnvironment();
    }
}
