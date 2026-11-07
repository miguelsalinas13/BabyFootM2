using System.IO;
using UnityEngine;

internal static class BabyFootEditorPaths
{
    internal static string PreviewDirectory
    {
        get
        {
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Library","BabyFootChecks","Previews"));
            Directory.CreateDirectory(path);
            return path+Path.DirectorySeparatorChar;
        }
    }
}
