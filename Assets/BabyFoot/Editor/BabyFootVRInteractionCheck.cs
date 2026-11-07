using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

[InitializeOnLoad]
public static class BabyFootVRInteractionCheck
{
    const string Pending="Library/BabyFootVRInteraction.pending",Report="Library/BabyFootVRInteractionV2.txt";
    static int step;static double next,deadline;
    static BabyFootVRMenu menu;static BabyFootSession session;static BabyFootDefense defense;static BabyFootVRGoalkeeper input;static Transform hand;
    static BabyFootVRInteractionCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        File.WriteAllText(Pending,"run");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&File.Exists(Pending)){step=0;next=EditorApplication.timeSinceStartup+2;deadline=next+30;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.ExitingPlayMode)EditorApplication.update-=Tick;
    }
    static void Require(bool value,string why){if(!value)throw new Exception(why);}
    static void Swing(bool align)
    {
        hand.rotation=Quaternion.identity;hand.position=input.handle.position;input.BeginHold(hand,XRNode.RightHand);input.ApplyHeldPose(.02f);
        if(align)
        {
            var rod=defense.attack.AttackingRod;float offset=float.MaxValue;
            foreach(Transform figure in rod)if(figure.name.StartsWith("Player "))
            {float delta=defense.attack.BallStartX-defense.ball.parent.InverseTransformPoint(figure.position).x;if(Mathf.Abs(delta)<Mathf.Abs(offset))offset=delta;}
            hand.position+=rod.parent.TransformDirection(Vector3.right)*offset;input.ApplyHeldPose(.02f);
        }
        var axis=defense.goalkeeper.parent.TransformDirection(Vector3.right);
        hand.rotation=Quaternion.AngleAxis(-25,axis);input.ApplyHeldPose(.08f);
        Require(!defense.attack.ShotFired,"Backswing launched the ball");
        hand.rotation=Quaternion.AngleAxis(25,axis);input.ApplyHeldPose(.1f);
        Require(defense.attack.ShotFired,"Forward wrist swing did not launch");
        Require(defense.attack.ContactMade==align,"Alignment contact result incorrect");
        Require(!defense.attack.IsStriking,"VR introduced an automatic strike delay");
        Require(defense.attack.ContactSeconds>=0,"VR contact phase missing");
        Require(!defense.attack.StrikeFromController(500),"A swing fired twice");
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            Require(EditorApplication.timeSinceStartup<deadline,"Check timed out");
            if(step==0)
            {
                menu=UnityEngine.Object.FindAnyObjectByType<BabyFootVRMenu>();session=menu.session;defense=session.exercise;input=menu.control;
                Require(menu.menuCanvas&&menu.statusText,"Menu missing");
                foreach(string label in new[]{"Reaction Defense","Trajectory Anticipation","Find the Gap / Attack"})Require(GameObject.Find("VR Training Interface").GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b=>b.name==label),"Exercise button missing: "+label);
                input.enabled=false;hand=new GameObject("Synthetic VR hand").transform;
                session.dataDirectoryOverride="Library/VRInteractionChecks";
                menu.SelectExercise(0);menu.BeginFreeControls();Require(menu.IsTestingControls&&!session.IsOpen,"Free controls started a recorded session");
                hand.position=input.handle.position;hand.rotation=Quaternion.identity;input.BeginHold(hand,XRNode.RightHand);input.ApplyHeldPose(.02f);
                hand.position+=Vector3.right*.06f;input.ApplyHeldPose(.02f);Require(Mathf.Abs(defense.goalkeeper.localPosition.x-.06f)<.001f,"Free goalkeeper slide failed");
                hand.position+=Vector3.right;input.ApplyHeldPose(.02f);Require(Mathf.Abs(defense.goalkeeper.localPosition.x-.09f)<.001f,"Stop limit failed");
                Require(Quaternion.Angle(defense.goalkeeper.localRotation,Quaternion.identity)<.1f,"Keeper tilted");
                menu.OpenSettings();menu.SelectExercise(2);Require(input.handle.position.x<0,"Attack handle is on the wrong side");
                menu.BeginFreeControls();Require(float.IsPositiveInfinity(defense.attack.TimeLimit),"Free attack has timer");
                Require(!defense.attack.StrikeFromController(50),"Slow wrist noise fired");Swing(true);
                Require(defense.attack.LaunchSpeed>.5f&&defense.attack.LaunchSpeed<=3,"Physical launch speed out of range");
                Require(!defense.trajectory.presentation.strikeActive,"Automatic animation overrode wrist");
                step=1;next=EditorApplication.timeSinceStartup+.2;return;
            }
            if(step==1)
            {
                menu.OpenSettings();Require(!input.FreeControl&&!defense.attack.unlimitedPractice,"Free state leaked into session");
                session.participantCode="VR_WRIST_CHECK";session.practiceTrials=1;session.measurementTrials=2;menu.StartPractice();menu.NextTrial();
                Require(!defense.attack.Fire(),"Desktop Fire remains active in VR");Swing(true);step=2;return;
            }
            if(step==2)
            {
                if(defense.IsTrialActive)return;Require(session.CurrentStage=="Break","VR practice did not finish");session.BeginMeasurement();menu.NextTrial();Swing(true);step=3;return;
            }
            if(step==3)
            {
                if(defense.IsTrialActive)return;Require(session.CompletedTrials==1,"VR measurement count");menu.NextTrial();Swing(false);step=4;return;
            }
            if(step==4)
            {
                Require(session.CurrentStage=="Summary"&&!session.IsOpen,"VR summary missing");
                string folder=Directory.GetDirectories("Library/VRInteractionChecks","VR_WRIST_CHECK_*").OrderByDescending(Directory.GetLastWriteTimeUtc).First();
                var rows=File.ReadAllLines(Path.Combine(folder,"measurement.csv"));Require(rows.Length==3,"VR measurement export count");Require(File.ReadAllLines(Path.Combine(folder,"practice.csv")).Length==2,"VR practice export count");
                Require(rows[0].Split(',').Length==39&&rows[1].Split(',').Length==39&&rows[2].Split(',').Length==39,"VR attack schema mismatch");
                Require(rows[1].Contains("vr_grip_slide_wrist_strike"),"Input mode missing");
                Require(rows[2].Contains("no_ball_contact"),"Air swing result missing");
                Require(File.ReadAllText(Path.Combine(folder,"session.json")).Contains("vr_grip_slide_wrist_strike"),"VR metadata missing");
                session.ReturnToSetup();session.vrInterface=false;defense.externalControls=false;session.participantCode="PC_REGRESSION";session.StartSession();session.StartNextTrial();
                Require(defense.attack.Fire()&&defense.attack.IsStriking&&defense.attack.ContactSeconds<0,"Desktop timing changed");session.EndSession();
                folder=Directory.GetDirectories("Library/VRInteractionChecks","PC_REGRESSION_*").OrderByDescending(Directory.GetLastWriteTimeUtc).First();Require(File.ReadAllLines(Path.Combine(folder,"practice.csv"))[0].Split(',').Length==37,"Desktop schema changed");
                session.ReturnToSetup();session.vrInterface=true;defense.externalControls=true;menu.SelectExercise(0);session.participantCode="VR_DEFENSE_CHECK";session.StartSession();session.StartNextTrial();defense.externalGoalkeeperX=.01f;
                step=5;next=EditorApplication.timeSinceStartup+.25;return;
            }
            Require(session.CurrentStage=="Break","Reaction false-start regression");session.EndSession();
            File.WriteAllText(Report,"PASS\nThree explicit exercise buttons; unrecorded free-control movement and stops; vertical keeper; attack-side handle; untimed free attack; slow/backward gestures rejected; real forward wrist/offset contact and immediate launch; no duplicate shot or automatic animation; VR practice/measurement/summary with contact and air swing; 39-column VR exports and input metadata; desktop delayed Fire and 37-column schema retained; defense false-start regression.\nSynthetic poses only. Real headset reach, grip picking, wrist comfort and latency still require user testing.\n");Finish();
        }
        catch(Exception e){File.WriteAllText(Report,"FAIL\nstep="+step+"\n"+e);Debug.LogException(e);Finish();}
    }
    static void Finish(){EditorApplication.update-=Tick;if(File.Exists(Pending))File.Delete(Pending);EditorApplication.isPlaying=false;}
}
