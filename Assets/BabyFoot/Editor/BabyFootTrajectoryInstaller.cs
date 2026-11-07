using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootTrajectoryInstaller
{
    static BabyFootTrajectoryInstaller() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Apply; }
    static void Apply()
    {
        if(File.Exists("Library/BabyFootTrajectoryInstalledV1.txt")) return;
        if(EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Apply; return; }
        var c=Object.FindFirstObjectByType<BabyFootDefense>(); var p=Object.FindFirstObjectByType<BabyFootPresentation>();
        if(!c || !p || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        Directory.CreateDirectory("Assets/BabyFoot/Scenes/Backups");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),AssetDatabase.GenerateUniqueAssetPath("Assets/BabyFoot/Scenes/Backups/Before_Trajectory_Exercise.unity"),true);
        var t=c.gameObject.GetComponent<BabyFootTrajectory>(); if(!t) t=c.gameObject.AddComponent<BabyFootTrajectory>();
        t.exercise=c; t.presentation=p; c.trajectory=t;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/BabyFootTrajectoryInstalledV1.txt","Second exercise installed. Full table, distant shot source, direct or one specular side-wall reflection, fixed raised intermediate rods and swept player-clearance validation. Original reaction layout retained.\n");
    }
}
