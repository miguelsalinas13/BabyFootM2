using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootEnglishMigration
{
    const string Marker="Library/BabyFootEnglishMigrationV1.txt";
    static BabyFootEnglishMigration() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Apply; }
    static void Apply()
    {
        if(File.Exists(Marker)) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall+=Apply; return; }
        var scene=SceneManager.GetActiveScene(); if(scene.name!="BabyFoot_Prototype") return;
        var translations=new Dictionary<string,string> {
            {"DEFENSA","DEFENSE"},{"Defensa","Defense"},{"Portero","Goalkeeper"},
            {"Pelota","Ball"},{"Suelo","Floor"},{"Gradas","Stands"},{"Iluminacion","Lighting"},
            {"Iniciar ensayo","Start trial"},{"Paradas","Saves"},{"Goles","Goals"},
            {"Preparado: espera al tiro","Ready: wait for the shot"},
            {"Defiende la porteria","Defend the goal"}
        };
        int changes=0; var names=new List<string>();
        foreach(var root in scene.GetRootGameObjects())
        foreach(var tr in root.GetComponentsInChildren<Transform>(true))
        {
            string name=tr.name;
            foreach(var kv in translations) name=name.Replace(kv.Key,kv.Value);
            if(name!=tr.name) { Undo.RecordObject(tr.gameObject,"Use English object name"); tr.name=name; changes++; }
            names.Add(tr.name);
            foreach(var component in tr.GetComponents<Component>())
            {
                if(component==null) continue;
                if(!(component is TextMesh) && !component.GetType().FullName.StartsWith("TMPro.") && component.GetType().FullName!="UnityEngine.UI.Text") continue;
                var so=new SerializedObject(component); var text=so.FindProperty("m_text")??so.FindProperty("m_Text");
                if(text==null || text.propertyType!=SerializedPropertyType.String) continue;
                string value=text.stringValue;
                foreach(var kv in translations) value=value.Replace(kv.Key,kv.Value);
                if(value!=text.stringValue) { text.stringValue=value; so.ApplyModifiedProperties(); changes++; }
            }
        }
        if(changes>0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        File.WriteAllText(Marker,"English scene audit completed. Renamed objects/text: "+changes+"\n"+string.Join("\n",names));
        Debug.Log("BabyFoot: English UI and CSV result labels applied. Scene audit complete.");
    }
}
