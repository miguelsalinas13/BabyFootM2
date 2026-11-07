using UnityEditor;

// Old one-off migrations must not replay when a clone has a new Library folder.
internal static class BabyFootLegacyAutoSetup
{
    const string Key="BabyFoot.LegacyAutoSetupForThisSession";
    internal static bool Enabled=>SessionState.GetBool(Key,false);

    [MenuItem("Tools/BabyFoot/Development/Enable legacy auto setup for this session")]
    static void Enable()
    {
        SessionState.SetBool(Key,true);
        UnityEngine.Debug.Log("BabyFoot: legacy automatic setup explicitly enabled for this editor session.");
        EditorUtility.RequestScriptReload();
    }

    [MenuItem("Tools/BabyFoot/Development/Disable legacy auto setup")]
    static void Disable(){SessionState.SetBool(Key,false);}
}
