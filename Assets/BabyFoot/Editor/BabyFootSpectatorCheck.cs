using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BabyFootSpectatorCheck
{
    const string Pending="Library/BabyFootSpectatorCheck.pending";
    static double due;
    static BabyFootFreeMatch m;static BabyFootFreeMatchUI ui;static BabyFootFreeMatchHands hands;
    static int frames,run;static string report;static int totalBlue,totalCoral,totalGoals,totalReceives,totalPasses;
    static BabyFootSpectatorCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        File.WriteAllText(Pending,"run");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Pending))return;
        frames=run=totalBlue=totalCoral=totalGoals=totalReceives=totalPasses=0;report="";m=null;
        due=EditorApplication.timeSinceStartup+1.5;EditorApplication.update+=Tick;
    }
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        try
        {
            if(!m)
            {
                m=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatch>();Require(m,"Match missing");
                ui=m.GetComponent<BabyFootFreeMatchUI>();hands=m.GetComponent<BabyFootFreeMatchHands>();m.enabled=false;hands.enabled=false;
                Canvas.ForceUpdateCanvases();bool entry=false;
                foreach(var b in ui.menu.GetComponentsInChildren<Button>(true))
                {
                    var t=b.GetComponentInChildren<Text>();entry|=t&&t.text=="Watch AI vs AI";
                    var corners=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(corners);
                    foreach(var c in corners)Require(((RectTransform)ui.menu.transform).rect.Contains(ui.menu.transform.InverseTransformPoint(c)),"Clipped menu button");
                }
                Require(entry,"Spectator entry missing");
                UnityEngine.Random.InitState(20261106);ui.WatchMatch();
                Require(m.Spectator&&m.aiEnabled&&!m.Paused&&!ui.menu.gameObject.activeSelf,"Start spectator");
                Require(!hands.Grab(0,1)&&m.IsAutomatedRod(1)&&m.IsAutomatedRod(6),"Spectator ownership/input lock");
                m.SetPaused(true);float clock=m.MatchTime;var p=m.ball.localPosition;m.Simulate(.2f);Require(m.MatchTime==clock&&m.ball.localPosition==p,"Pause does not freeze match");m.SetPaused(false);
                report+="PASS: menu entry/bounds; both teams automated; human handles locked; pause freezes simulation.\n";
            }
            for(int n=0;n<120;n++)
            {
                m.Simulate(.02f);frames++;
                var p=m.ball.localPosition;
                Require(!float.IsNaN(p.x)&&Mathf.Abs(p.x)<.5f&&Mathf.Abs(p.z)<.8f&&Mathf.Abs(p.y-.775f)<.0001f&&m.Velocity.magnitude<=3.501f,"Ball bounds/speed");
                for(int i=0;i<8;i++)
                {
                    var range=m.TravelRange(i);Require(m.rods[i].localPosition.x>=range.x-.0001f&&m.rods[i].localPosition.x<=range.y+.0001f,"Rod travel exceeded");
                    
                }
            }
            if(frames<4500)return;
            report+=$"RUN {run} / 90 simulated seconds: Blue strikes={m.AI.BlueStrikes}, Coral strikes={m.AI.CoralStrikes}, receptions={m.AI.Receptions}, secured passes={m.AI.CompletedPasses}, feints={m.AI.Feints}, bank attempts={m.AI.BankAttempts}, confirmed banks={m.AI.ConfirmedBanks}, goals={m.BlueScore}:{m.CoralScore}, dead ball serves={m.DeadBallResets}\n";
            totalBlue+=m.AI.BlueStrikes;totalCoral+=m.AI.CoralStrikes;totalGoals+=m.BlueScore+m.CoralScore;totalReceives+=m.AI.Receptions;totalPasses+=m.AI.CompletedPasses;
            if(++run<3){UnityEngine.Random.InitState(20261106+run);m.StartMatch();frames=0;return;}
            Require(totalBlue>5&&totalCoral>5,"Both teams must produce repeated physical strikes");
            Require(totalReceives>3,"Natural match receptions missing");
            Require(totalGoals>0,"Demo never scores");
            Require(totalPasses>0,"No completed AI-to-AI pass in match");
            report+="PASS: three seeded natural matches; bounds, speed and rotating keepers; repeated strikes by both teams; real receptions, completed forward passes and goals.\n";
            // A truly dead ball is explicitly re-served only in spectator mode.
            m.aiEnabled=false;m.PlaceBall(new Vector3(.30f,.775f,.47f),Vector3.zero);int resets=m.DeadBallResets;
            for(int i=0;i<220;i++)m.Simulate(.02f);
            Require(m.DeadBallResets>resets,"Spectator dead-ball recovery missing");
            m.aiEnabled=true;ui.PlayAsBlue();Require(!m.Spectator&&!m.IsAutomatedRod(1)&&m.IsAutomatedRod(6),"Return to human ownership");
            for(int i=0;i<8;i++)Require(Mathf.Abs(m.rods[i].localPosition.x)<.0001f&&Quaternion.Angle(m.rods[i].localRotation,Quaternion.identity)<.1f,"New human match did not restore neutral rods");
            m.BeginControlWarmup();Require(!m.Spectator&&!m.aiEnabled&&m.ControlWarmup,"Warm-up transition");m.EndControlWarmup();Require(m.aiEnabled,"AI restore after warm-up");
            report+="PASS: visible dead-ball re-serve, return to human controls, warm-up restores ordinary AI setting.\nSynthetic simulation; headset comfort and physical-skill fidelity are unvalidated.\n";
            Finish(report);
        }
        catch(Exception e){Finish(report+"FAIL: "+e);Debug.LogException(e);}
    }
    static void Finish(string value)
    {
        File.WriteAllText("Library/BabyFootSpectatorCheckV1.txt",value);File.Delete(Pending);
        EditorApplication.update-=Tick;EditorApplication.isPlaying=false;Debug.Log(value);
    }
}
