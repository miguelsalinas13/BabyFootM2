using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

[InitializeOnLoad]
public static class BabyFootFreeMatchCheck
{
    const string Pending="Library/BabyFootFreeMatchCheck.pending";
    static int step;static double due,deadline;static bool training;
    static BabyFootFreeMatch match;static BabyFootVRMenu menu;static BabyFootSession session;static BabyFootDefense defense;
    static int shots;
    static BabyFootFreeMatchCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run(bool trainingChecks)
    {if(EditorApplication.isPlaying)throw new Exception("Stop Play first");File.WriteAllText(Pending,trainingChecks?"training":"free");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&File.Exists(Pending)){training=File.ReadAllText(Pending)=="training";step=0;due=EditorApplication.timeSinceStartup+1.5;deadline=due+20;EditorApplication.update+=Tick;}
    }
    static void Require(bool value,string why){if(!value)throw new Exception(why);}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        try{Require(EditorApplication.timeSinceStartup<deadline,"Check timeout");if(training)Training();else Free();}
        catch(Exception e){Finish("FAIL\nstep="+step+"\n"+e);Debug.LogException(e);}
    }
    static void Free()
    {
        if(step==0)
        {
            match=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatch>();Require(match&&match.rods.Length==8,"Match/rods missing");
            CaptureMenu();
            Require(!UnityEngine.Object.FindAnyObjectByType<BabyFootSession>()&&!UnityEngine.Object.FindAnyObjectByType<BabyFootDefense>(),"Training component in casual match");
            var hands=match.GetComponent<BabyFootFreeMatchHands>();hands.enabled=false;match.enabled=false;match.aiEnabled=false;match.StartMatch();
            for(int i=0;i<8;i++)match.SetRodPose(i,0,0);
            match.PlaceBall(new Vector3(.30f,.775f,-.28f),new Vector3(2,0,0));for(int i=0;i<8;i++)match.Simulate(.02f);
            Require(match.WallContacts>0&&Mathf.Abs(match.ball.localPosition.x)<=.323f,"Side wall rebound failed");
            int contacts=match.PlayerContacts;match.PlaceBall(new Vector3(0,.775f,-.13f),new Vector3(0,0,.8f));for(int i=0;i<8;i++)match.Simulate(.02f);
            Require(match.PlayerContacts>contacts&&!match.Paused,"Player rebound stopped play or missed");
            match.PlaceBall(new Vector3(.24f,.775f,-.52f),new Vector3(0,0,-1));match.Simulate(.06f);Require(match.Velocity.z>0,"Solid end wall did not rebound");
            for(int i=0;i<8;i++)match.SetRodPose(i,-.09f,75);
            match.PlaceBall(new Vector3(.075f,.775f,.54f),new Vector3(0,0,1));match.Simulate(.06f);Require(match.BlueScore==1,"Blue goal not counted");
            match.Serve();match.PlaceBall(new Vector3(.075f,.775f,-.54f),new Vector3(0,0,-1));match.Simulate(.06f);Require(match.CoralScore==1,"AI goal not counted");
            match.Serve();match.SetRodPose(5,0,0);match.SetRodPose(3,0,0);
            hands.leftHand.position=match.rods[5].Find("Handle Visual").position;hands.leftHand.rotation=Quaternion.identity;
            hands.rightHand.position=match.rods[3].Find("Handle Visual").position;hands.rightHand.rotation=Quaternion.identity;
            Require(hands.Grab(0,5)&&hands.Grab(1,3)&&!hands.Grab(1,5)&&!hands.Grab(1,2),"Two-hand/team ownership failed");
            hands.leftHand.position+=Vector3.right*.04f;hands.rightHand.position-=Vector3.right*.04f;hands.ApplyPose(0);hands.ApplyPose(1);
            Require(Mathf.Abs(match.rods[5].localPosition.x-.04f)<.001f&&Mathf.Abs(match.rods[3].localPosition.x+.04f)<.001f,"Independent slide failed");
            match.SetRodPose(0,.04f,70);Require(Quaternion.Angle(match.rods[0].localRotation,Quaternion.AngleAxis(70,Vector3.right))<.1f,"Keeper rotation blocked");
            for(int i=0;i<8;i++)match.SetRodPose(i,0,0);match.PlaceBall(new Vector3(.3f,.775f,0),Vector3.zero);match.Simulate(.02f);
            match.PlaceBall(new Vector3(0,.775f,-.257f),Vector3.zero);match.aiEnabled=true;shots=match.AIShots;step=1;return;
        }
        match.Simulate(.02f);if(match.AIShots<=shots)return;
        Require(match.ball.localPosition.y>.77f&&match.Velocity.magnitude<=3.501f&&match.Velocity.z<0,"Ball bounds/speed/AI forward strike failed");
        Finish("PASS\nIndependent free scene; eight rods and no training/session scripts; side-wall and player rebounds continue play; solid end-wall rebound; both scoring directions; two independent blue hands/team ownership; keeper can rotate; live AI strikes a reachable ball; planar bounded ball speed. No participant session exports.\nSynthetic poses and software simulation; hardware comfort/performance unvalidated.\n");
    }
    static void Training()
    {
        if(step==0)
        {
            menu=UnityEngine.Object.FindAnyObjectByType<BabyFootVRMenu>();session=menu.session;defense=session.exercise;menu.control.enabled=false;
            Require(!defense.penalizeEarlyMovement,"Early-movement penalty still enabled by default");
            session.dataDirectoryOverride="Library/FreeMatchChecks/Training";session.participantCode="EARLY_MOVE";session.practiceTrials=1;session.measurementTrials=2;
            menu.SelectExercise(0);menu.StartPractice();menu.NextTrial();defense.externalGoalkeeperX=.04f;step=1;due=EditorApplication.timeSinceStartup+.2;return;
        }
        if(step==1){Require(defense.IsTrialActive,"Early movement cancelled the attempt");Require(defense.PreShotMovement&&!defense.ReactionEligible,"Timing eligibility missing");step=2;return;}
        if(step==2)
        {
            if(defense.IsTrialActive)return;Require(session.CurrentStage=="Break","Permissive practice did not complete");
            var folder=Directory.GetDirectories("Library/FreeMatchChecks/Training","EARLY_MOVE_*").OrderByDescending(Directory.GetLastWriteTimeUtc).First();var rows=File.ReadAllLines(System.IO.Path.Combine(folder,"practice.csv"));
            Require(rows.Length==2&&rows[0].Split(',').Length==41&&rows[1].Split(',').Length==41,"Defensive schema count");Require(!rows[1].Contains("false_start")&&rows[1].EndsWith(",false"),"Early movement was penalized or timed");Require(defense.LastResponseSeconds<0,"Anticipatory movement counted as reaction");
            session.EndSession();session.ReturnToSetup();menu.SelectExercise(2);defense.attack.unlimitedPractice=true;
            for(int figure=0;figure<3;figure++)
            {
                defense.attack.RequestedStriker=figure;defense.BeginTrial(.05f);Align();
                Require(defense.attack.TryControllerStrike(0,8,160),"Short actual contact failed for figure "+figure);Require(defense.attack.ContactMade&&!defense.attack.IsStriking,"Wrong contact/delay");defense.CancelTrial();
            }
            defense.attack.RequestedStriker=1;defense.BeginTrial(.05f);
            Require(!defense.attack.TryControllerStrike(0,8,160)&&defense.attack.IsAiming,"Off-ball swing fired or ended trial");Align();Require(defense.attack.TryControllerStrike(-60,60,3000),"Swept fast contact missed");defense.CancelTrial();
            Require(Vector3.Angle(BabyFootAttack.ContactDirection(.015f),Vector3.back)<8,"Small offset still gives exaggerated diagonal");
            defense.attack.unlimitedPractice=false;
            Finish("PASS\nEarly movement no longer cancels attempts; pre-movement flags and exclusion from reaction timing; defensive41-column exports; actual short contact on all three attacking figures without forced backswing; off-ball swing permits retry; fast swept contact; small alignment errors stay near straight.\n");
        }
    }
    static void Align()
    {
        var a=defense.attack;var rod=a.AttackingRod;float offset=float.MaxValue;
        foreach(Transform figure in rod)if(figure.name.StartsWith("Player ")){float d=a.BallStartX-defense.ball.parent.InverseTransformPoint(figure.position).x;if(Mathf.Abs(d)<Mathf.Abs(offset))offset=d;}
        var p=rod.localPosition;p.x+=offset;rod.localPosition=p;
    }
    static void CaptureMenu()
    {
        var ui=match.GetComponent<BabyFootFreeMatchUI>();Require(ui.menu,"Free menu missing");Canvas.ForceUpdateCanvases();
        var root=ui.menu.GetComponent<RectTransform>();
        foreach(var button in ui.menu.GetComponentsInChildren<UnityEngine.UI.Button>())
        {var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);foreach(var point in corners)Require(root.rect.Contains(root.InverseTransformPoint(point)),"Clipped match button: "+button.name);}
        var go=new GameObject("Temporary Match Review Camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(ui.origin.Camera);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=75;
        camera.transform.position=new Vector3(1.2f,1.65f,0);camera.transform.LookAt(new Vector3(.05f,1.5f,0));
        var rt=new RenderTexture(1400,1200,24);var tex=new Texture2D(1400,1200,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,1200),0,0);tex.Apply();File.WriteAllBytes(BabyFootEditorPaths.PreviewDirectory+"BabyFoot_FreeMatch_Menu.png",tex.EncodeToPNG());}
        finally{RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
    static void Finish(string report)
    {
        File.WriteAllText(training?"Library/BabyFootTrainingContactV3.txt":"Library/BabyFootFreeMatchCheckV1.txt",report);EditorApplication.update-=Tick;
        if(File.Exists(Pending))File.Delete(Pending);EditorApplication.isPlaying=false;
    }
}
