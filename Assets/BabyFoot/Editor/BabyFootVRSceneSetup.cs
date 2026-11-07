using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR;

// One-time scene migration. The PC scene is never overwritten.
[InitializeOnLoad]
public static class BabyFootVRSceneSetup
{
    const string Marker="Library/BabyFootVRSceneV1.txt";
    public const string VRPath="Assets/BabyFoot/Scenes/BabyFoot_VR.unity";
    static BabyFootVRSceneSetup() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=TryBuild; }
    static void TryBuild()
    {
        if(File.Exists(Marker)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{Build();}catch(Exception e){File.WriteAllText(Marker,"FAIL\n"+e);Debug.LogException(e);}
    }
    [MenuItem("BabyFoot/VR/Build first VR scene")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        if(File.Exists(VRPath))throw new InvalidOperationException("VR scene already exists. Review it instead of overwriting.");
        var source=SceneManager.GetActiveScene();
        if(source.path!="Assets/BabyFoot/Scenes/BabyFoot_Prototype.unity")throw new InvalidOperationException("Open BabyFoot_Prototype first.");
        if(source.isDirty)throw new InvalidOperationException("Source has unsaved changes. Save or review them first.");
        string originalHash=Hash(source.path);
        if(!AssetDatabase.CopyAsset(source.path,VRPath))throw new Exception("Could not duplicate prototype.");
        var scene=EditorSceneManager.OpenScene(VRPath,OpenSceneMode.Single);
        foreach(var old in UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include,FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)) c.gameObject.SetActive(false);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
        if(!prefab)throw new Exception("Starter Assets XR rig not found.");
        var rig=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);rig.name="BabyFoot XR Origin";
        rig.transform.SetPositionAndRotation(new Vector3(.95f,0,-.42f),Quaternion.Euler(0,-90,0));
        var origin=rig.GetComponent<XROrigin>();origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;
        origin.Camera.nearClipPlane=.03f;origin.Camera.farClipPlane=45;origin.Camera.clearFlags=CameraClearFlags.Skybox;
        origin.Camera.tag="MainCamera";
        rig.GetComponent<CharacterController>().enabled=false;
        var locomotion=rig.transform.Find("Locomotion");if(locomotion)locomotion.gameObject.SetActive(false);
        foreach(var t in rig.GetComponentsInChildren<Transform>(true))if(t.name=="Gaze Interactor"||t.name=="Teleport Interactor")t.gameObject.SetActive(false);
        if(!UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>())new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        foreach(var es in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(es.gameObject);
        var events=new GameObject("VR Event System");events.AddComponent<EventSystem>();events.AddComponent<XRUIInputModule>();
        var exercise=UnityEngine.Object.FindAnyObjectByType<BabyFootDefense>();exercise.externalControls=true;exercise.externalGoalkeeperX=0;
        var session=UnityEngine.Object.FindAnyObjectByType<BabyFootSession>();session.vrInterface=true;session.exerciseSelection=0;session.practiceTrials=3;session.measurementTrials=10;
        var root=new GameObject("VR Training Interface");
        var control=root.AddComponent<BabyFootVRGoalkeeper>();control.exercise=exercise;
        control.leftHand=rig.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Left Controller");
        control.rightHand=rig.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Right Controller");
        control.handle=exercise.goalkeeper.Find("Handle Visual");if(!control.handle)throw new Exception("Goalkeeper handle missing.");
        var menu=root.AddComponent<BabyFootVRMenu>();menu.session=session;menu.control=control;menu.origin=origin;
        var openxr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        if(openxr){openxr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;EditorUtility.SetDirty(openxr);}
        // Keep the desktop prototype available in builds, start with VR.
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(VRPath,true),new EditorBuildSettingsScene("Assets/BabyFoot/Scenes/BabyFoot_Prototype.unity",true)};
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        int cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled);
        int listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c=>c.enabled);
        if(cameras!=1||listeners!=1)throw new Exception($"Expected one active camera/listener, got {cameras}/{listeners}.");
        if(originalHash!=Hash("Assets/BabyFoot/Scenes/BabyFoot_Prototype.unity"))throw new Exception("PC scene changed unexpectedly.");
        File.WriteAllText(Marker,$"PASS\nScene: {VRPath}\nPC scene SHA256 unchanged: {originalHash}\nActive cameras/listeners: {cameras}/{listeners}\nTracking: Floor, scale 1, no locomotion, vertical goalkeeper\nNear clip .03m. PC OpenXR SinglePassInstanced. Controller UI + grip slide.\nHardware validation pending. Find the Gap remains desktop-only until physical strike integration.\n");
        Selection.activeGameObject=rig;Debug.Log("BabyFoot VR scene created and saved. Connect Air Link before Play.");
    }
    static string Hash(string path){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));}
}
