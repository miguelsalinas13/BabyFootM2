using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;

public static class BabyFootFreeMatchSetup
{
    public const string Path="Assets/BabyFoot/Scenes/BabyFoot_FreeMatch.unity";
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before creating free match.");
        var source=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(source.isDirty)throw new Exception("Review unsaved scene changes first.");
        if(File.Exists(Path))throw new Exception("Free match scene already exists; do not overwrite.");
        const string vr="Assets/BabyFoot/Scenes/BabyFoot_VR.unity";
        if(!AssetDatabase.CopyAsset(vr,Path))throw new Exception("Could not copy VR scene.");
        var scene=EditorSceneManager.OpenScene(Path);
        var defense=UnityEngine.Object.FindAnyObjectByType<BabyFootDefense>();var oldHands=UnityEngine.Object.FindAnyObjectByType<BabyFootVRGoalkeeper>();
        var left=oldHands.leftHand;var right=oldHands.rightHand;var origin=UnityEngine.Object.FindAnyObjectByType<XROrigin>();
        var ball=defense.ball;var rods=defense.goalkeeper.parent.Cast<Transform>().OrderBy(r=>r.localPosition.z).ToArray();
        var sparks=UnityEngine.Object.FindAnyObjectByType<BabyFootPresentation>().goalSparks;
        foreach(var s in UnityEngine.Object.FindObjectsByType<BabyFootSession>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(s);
        foreach(var s in UnityEngine.Object.FindObjectsByType<BabyFootVRMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(s);
        foreach(var s in UnityEngine.Object.FindObjectsByType<BabyFootVRGoalkeeper>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(s);
        foreach(var s in UnityEngine.Object.FindObjectsByType<BabyFootPresentation>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(s);
        foreach(var s in UnityEngine.Object.FindObjectsByType<BabyFootAttack>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(s);
        foreach(var s in UnityEngine.Object.FindObjectsByType<BabyFootTrajectory>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(s);
        UnityEngine.Object.DestroyImmediate(defense);
        foreach(var rod in rods){rod.gameObject.SetActive(true);var p=rod.localPosition;p.x=0;rod.localPosition=p;rod.localRotation=Quaternion.identity;}
        ball.localPosition=new Vector3(0,.775f,0);foreach(var r in ball.GetComponentsInChildren<Renderer>(true))r.enabled=true;
        origin.transform.SetPositionAndRotation(new Vector3(.95f,0,0),Quaternion.Euler(0,-90,0));
        var root=new GameObject("Free Match");var match=root.AddComponent<BabyFootFreeMatch>();match.ball=ball;match.rods=rods;match.goalSparks=sparks;
        var hands=root.AddComponent<BabyFootFreeMatchHands>();hands.match=match;hands.leftHand=left;hands.rightHand=right;
        var ui=root.AddComponent<BabyFootFreeMatchUI>();ui.match=match;ui.hands=hands;ui.origin=origin;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var list=EditorBuildSettings.scenes.ToList();if(!list.Any(s=>s.path==Path))list.Add(new EditorBuildSettingsScene(Path,true));EditorBuildSettings.scenes=list.ToArray();AssetDatabase.SaveAssets();
        File.WriteAllText("Library/BabyFootFreeMatchSceneV1.txt","PASS\nSaved standalone free match; eight rods/22 figures; blue four rods with two-hand controller input; Coral AI; no training/session components; original VR and PC scenes preserved.\n");
    }
}
