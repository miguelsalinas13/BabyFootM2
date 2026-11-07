using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootSessionInstaller
{
    static BabyFootSessionInstaller() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Apply; }
    static void Apply()
    {
        if(File.Exists("Library/BabyFootSessionsInstalledV1.txt")) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Apply; return; }
        var c=Object.FindFirstObjectByType<BabyFootDefense>(); if(!c || SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        if(Object.FindFirstObjectByType<BabyFootSession>()) return;
        Directory.CreateDirectory("Assets/BabyFoot/Scenes/Backups");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),AssetDatabase.GenerateUniqueAssetPath("Assets/BabyFoot/Scenes/Backups/Before_Sessions.unity"),true);
        var go=new GameObject("07 | Session Flow"); go.SetActive(false);
        var session=go.AddComponent<BabyFootSession>(); session.exercise=c; go.SetActive(true);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/BabyFootSessionsInstalledV1.txt","Session flow installed. Default: 5 practice, 20 balanced planned measurement targets. Separate CSVs and session metadata.\n");
        Debug.Log("BabyFoot: session flow installed with separate practice and measurement data.");
    }
}
