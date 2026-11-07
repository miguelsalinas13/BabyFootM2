using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public static class BabyFootEyeSceneSetup
{
    public const string ScenePath="Assets/BabyFoot/Scenes/BabyFoot_EyeTracking_Test.unity";
    const string AssetsPath="Assets/BabyFoot/EyeTrackingTest";
    static Material Material(string name,Color color)
    {
        var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.name=name;mat.SetColor("_BaseColor",color);
        AssetDatabase.CreateAsset(mat,AssetsPath+"/"+name+".mat");return mat;
    }
    static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,Transform parent=null)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=material;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;go.GetComponent<Renderer>().receiveShadows=false;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static string Hash(string path){if(!File.Exists(path))return "absent";using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));}
    [MenuItem("BabyFoot/Eye Tracking/Create diagnostic scene")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before creating eye test.");
        if(File.Exists(ScenePath))throw new Exception("Eye test scene exists; open it rather than overwrite it.");
        if(SceneManager.GetActiveScene().isDirty)throw new Exception("Review unsaved scene changes first.");
        string pc="Assets/BabyFoot/Scenes/BabyFoot_Prototype.unity",vr="Assets/BabyFoot/Scenes/BabyFoot_VR.unity";
        string pcHash=Hash(pc),vrHash=Hash(vr);
        var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        var eye=settings?settings.GetFeature<EyeGazeInteraction>():null;if(!eye)throw new Exception("Standalone OpenXR eye profile asset not found.");
        if(!AssetDatabase.IsValidFolder(AssetsPath))AssetDatabase.CreateFolder("Assets/BabyFoot","EyeTrackingTest");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var dark=Material("EyeTest_Background",new Color(.018f,.035f,.055f));var floor=Material("EyeTest_Floor",new Color(.045f,.065f,.08f));
        var idle=Material("EyeTest_IdleTarget",new Color(.14f,.37f,.45f));var active=Material("EyeTest_ActiveTarget",new Color(1,.62f,.12f));var debug=Material("EyeTest_DebugGaze",new Color(.25f,1,.8f));
        Cube("Diagnostic Floor",new Vector3(0,-.025f,1),new Vector3(8,.04f,8),floor);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
        var rig=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);rig.name="Eye Test XR Origin";
        rig.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);var origin=rig.GetComponent<XROrigin>();origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;
        origin.Camera.tag="MainCamera";origin.Camera.nearClipPlane=.03f;origin.Camera.farClipPlane=15;origin.Camera.clearFlags=CameraClearFlags.SolidColor;origin.Camera.backgroundColor=new Color(.008f,.015f,.025f);
        rig.GetComponent<CharacterController>().enabled=false;
        foreach(var t in rig.GetComponentsInChildren<Transform>(true))if(t.name=="Locomotion"||t.name=="Gaze Interactor"||t.name=="Teleport Interactor")t.gameObject.SetActive(false);
        new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();var events=new GameObject("XR Event System");events.AddComponent<EventSystem>();events.AddComponent<XRUIInputModule>();
        var test=new GameObject("Eye Tracking Diagnostic");var probe=test.AddComponent<BabyFootEyeTrackingProbe>();probe.origin=origin;
        var board=new GameObject("Nine Point Validation Board");board.transform.position=new Vector3(0,1.6f,2.2f);probe.board=board.transform;
        Cube("Target Backboard",new Vector3(0,0,.04f),new Vector3(1.7f,1.15f,.04f),dark,board.transform);
        // Disk mesh faces the user (-Z); 24 triangles, no colliders or per-target materials.
        var mesh=new Mesh{name="EyeTest_TargetDisk_24"};var vertices=new Vector3[25];var triangles=new int[72];vertices[0]=Vector3.zero;
        for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;vertices[i+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.026f;triangles[i*3]=0;triangles[i*3+1]=(i+1)%24+1;triangles[i*3+2]=i+1;}
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetsPath+"/TargetDisk.asset");
        Vector2[] points={Vector2.zero,new Vector2(-.55f,.35f),new Vector2(0,.35f),new Vector2(.55f,.35f),new Vector2(-.55f,0),new Vector2(.55f,0),new Vector2(-.55f,-.35f),new Vector2(0,-.35f),new Vector2(.55f,-.35f)};
        probe.targets=new Transform[points.Length];probe.targetRenderers=new Renderer[points.Length];
        for(int i=0;i<points.Length;i++)
        {
            var target=new GameObject("Target "+(i+1));target.transform.SetParent(board.transform,false);target.transform.localPosition=new Vector3(points[i].x,points[i].y,0);
            target.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=target.AddComponent<MeshRenderer>();renderer.sharedMaterial=idle;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            probe.targets[i]=target.transform;probe.targetRenderers[i]=renderer;
            var label=new GameObject("Target Number");label.transform.SetParent(target.transform,false);label.transform.localPosition=new Vector3(0,-.065f,-.001f);
            var text=label.AddComponent<TextMesh>();text.text=(i+1).ToString();text.fontSize=48;text.characterSize=.025f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.5f,.7f,.78f);
        }
        probe.idleMaterial=idle;probe.activeMaterial=active;
        var marker=new GameObject("Debug Eye Gaze Marker");marker.transform.SetParent(test.transform,false);marker.AddComponent<MeshFilter>().sharedMesh=mesh;marker.AddComponent<MeshRenderer>().sharedMaterial=debug;marker.transform.localScale=Vector3.one*.4f;marker.SetActive(false);probe.gazeMarker=marker.transform;
        var rayObject=new GameObject("Debug Eye Ray");rayObject.transform.SetParent(test.transform,false);var ray=rayObject.AddComponent<LineRenderer>();ray.sharedMaterial=debug;ray.positionCount=2;ray.startWidth=ray.endWidth=.0015f;ray.useWorldSpace=true;ray.enabled=false;ray.shadowCastingMode=ShadowCastingMode.Off;probe.gazeLine=ray;
        test.AddComponent<BabyFootEyeTrackingUI>().probe=probe;
        eye.enabled=true;EditorUtility.SetDirty(eye);EditorUtility.SetDirty(settings);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        if(pcHash!=Hash(pc)||vrHash!=Hash(vr))throw new Exception("Gameplay scene hash changed unexpectedly.");
        File.WriteAllText("Library/BabyFootEyeSceneV1.txt","PASS\nSaved "+ScenePath+"\nStandalone Eye Gaze Interaction enabled (project-wide); no head fallback.\nGameplay scene hashes unchanged. Nine 24-triangle target disks. No gameplay scripts changed.\nHardware validation pending.\n");
        Selection.activeGameObject=test;Debug.Log("BabyFoot eye diagnostic scene saved. Needs functioning Quest Pro and eye forwarding over Link.");
    }
}
