using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class BabyFootPresentationInstaller
{
    static BabyFootPresentationInstaller() { if(!BabyFootLegacyAutoSetup.Enabled)return;  EditorApplication.delayCall+=Install; }
    static void Install()
    {
        if(File.Exists("Library/BabyFootPresentationInstalledV1.txt")) return;
        if(EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Install; return; }
        if(SceneManager.GetActiveScene().name!="BabyFoot_Prototype") return;
        var c=Object.FindFirstObjectByType<BabyFootDefense>(); if(!c) return;
        if(Object.FindFirstObjectByType<BabyFootPresentation>()) return;
        Directory.CreateDirectory("Assets/BabyFoot/Scenes/Backups");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),AssetDatabase.GenerateUniqueAssetPath("Assets/BabyFoot/Scenes/Backups/Before_Presentation.unity"),true);
        Transform attacker=null;
        foreach(Transform rod in c.goalkeeper.parent) if(rod.gameObject.activeSelf && rod!=c.goalkeeper) { attacker=rod; break; }
        if(!attacker) { Debug.LogError("BabyFoot: no active attacking rod found."); return; }
        var effectRoot=new GameObject("06 | Presentation");
        var sparksGo=new GameObject("Goal Sparks - post-result only"); sparksGo.transform.SetParent(effectRoot.transform);
        sparksGo.transform.position=c.goalkeeper.position+new Vector3(0,-.025f,0);
        sparksGo.transform.rotation=Quaternion.LookRotation(new Vector3(0,1,.6f));
        var ps=sparksGo.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.loop=false; main.playOnAwake=false; main.duration=.7f; main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.6f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(.35f,.75f); main.startSize=new ParticleSystem.MinMaxCurve(.006f,.012f);
        main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.45f,.08f),new Color(1,.9f,.42f)); main.gravityModifier=.12f;
        main.maxParticles=32; main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission; emission.rateOverTime=0; emission.SetBursts(new[]{new ParticleSystem.Burst(0,24)});
        var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=32; shape.radius=.045f;
        var lifetime=ps.sizeOverLifetime; lifetime.enabled=true; lifetime.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
        var collision=ps.collision; collision.enabled=false; var lights=ps.lights; lights.enabled=false;
        var trails=ps.trails; trails.enabled=false;
        string folder="Assets/BabyFoot/Effects"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.name="Goal Spark Unlit";
        mat.SetColor("_BaseColor",new Color(1,.68f,.16f)); mat.enableInstancing=true;
        AssetDatabase.CreateAsset(mat,folder+"/GoalSpark.mat");
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); var mesh=Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh); mesh.name="Spark Mesh";
        Object.DestroyImmediate(cube); AssetDatabase.CreateAsset(mesh,folder+"/SparkMesh.asset");
        var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode=ParticleSystemRenderMode.Mesh; renderer.mesh=mesh; renderer.sharedMaterial=mat;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; renderer.enableGPUInstancing=true;
        var presentation=effectRoot.AddComponent<BabyFootPresentation>(); presentation.enabled=false;
        presentation.exercise=c; presentation.attackingRod=attacker; presentation.goalSparks=ps; presentation.enabled=true;
        // One shadow-casting light for table readability. All particle lights remain disabled.
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if(light.name=="Key Light") { light.intensity=1.15f; light.color=new Color(1,.96f,.9f); light.shadows=LightShadows.Soft; light.shadowStrength=.7f; light.shadowBias=.02f; light.shadowNormalBias=.1f; }
            if(light.name=="Fill") { light.intensity=.35f; light.color=new Color(.79f,.87f,1); light.shadows=LightShadows.None; }
        }
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.6f,.64f,.7f); RenderSettings.ambientEquatorColor=new Color(.38f,.42f,.48f); RenderSettings.ambientGroundColor=new Color(.2f,.22f,.26f);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Library/BabyFootPresentationInstalledV1.txt","Strike begins at shot onset; same stroke for both directions.\nGoal sparks: 24 burst / 32 maximum, unlit mesh, no collisions, trails or lights.\nKey and fill lighting adjusted.\n");
        Debug.Log("BabyFoot: synchronized strike, post-goal sparks and lighting installed.");
    }
}
