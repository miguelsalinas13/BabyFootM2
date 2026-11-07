using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BabyFootBallControlCheck
{
    const string Pending="Library/BabyFootBallControlCheck.pending";
    static double due;
    static BabyFootBallControlCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        File.WriteAllText(Pending,"run");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Pending))return;
        due=EditorApplication.timeSinceStartup+1.5;EditorApplication.update+=Tick;
    }
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        EditorApplication.update-=Tick;
        string report="";
        try
        {
            var m=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatch>();Require(m,"Free match missing");
            var h=m.GetComponent<BabyFootFreeMatchHands>();h.enabled=false;m.enabled=false;
            m.BeginControlWarmup();Require(!m.aiEnabled&&m.ControlWarmup&&!m.Paused,"Warm-up state");
            m.SetRodPose(1,0,-32);h.leftHand.position=m.rods[1].Find("Handle Visual").position;
            Require(h.Grab(0,1),"Receiver grip");
            for(int i=0;i<120;i++)m.Simulate(.02f);
            Require(m.ControlledRod==1&&m.PossessionSecured,"Gentle pass not secured: owner="+m.ControlledRod+" p="+m.ball.localPosition+" v="+m.Velocity);
            report+="PASS: physical incoming warm-up pass received and secured by held blue two-player rod.\n";
            float originalX=m.ball.localPosition.x;
            for(int i=0;i<20;i++){m.SetRodPose(1,(i+1)*.002f,-32);m.Simulate(.02f);}
            Require(m.ControlledRod==1&&m.ball.localPosition.x>originalX+.025f,"Controlled lateral carry");
            Require(m.ball.parent!=m.rods[1]&&Mathf.Abs(m.ball.localPosition.y-.775f)<.0001f,"Ball reparented or lifted");
            h.ReleaseAll();m.Simulate(.02f);Require(m.ControlledRod<0,"Grip loss did not release");
            report+="PASS: gentle lateral carry, planar ball, no parenting, grip-loss release.\n";
            m.SetRodPose(1,0,-32);m.Serve();h.Grab(0,1);
            for(int i=0;i<120;i++)m.Simulate(.02f);
            Require(m.ControlledRod==1,"Second reception");
            for(int i=0;i<32;i++){m.SetRodPose(1,0,-31+i);m.Simulate(.02f);}
            Require(m.ControlledRod<0&&m.Velocity.magnitude<.35f,"Slow lift fired a powered shot or kept control");
            m.SetRodPose(1,0,-32);m.Serve();for(int i=0;i<120;i++)m.Simulate(.02f);
            Require(m.ControlledRod==1,"Reception after lifting");
            bool struck=m.TryStrike(1,-32,-40,.02f);
            Require(struck&&m.ControlledRod<0&&m.Velocity.z>0&&m.Velocity.magnitude>=.35f&&m.Velocity.magnitude<1,"Controlled short pass release: hit="+struck+" owner="+m.ControlledRod+" p="+m.ball.localPosition+" v="+m.Velocity);
            report+="PASS: slow lift releases without a powered shot; quicker contact releases a short pass with bounded power.\n";
            m.Serve();h.ReleaseAll();for(int i=0;i<120;i++)m.Simulate(.02f);
            Require(m.ControlledRod<0,"Unheld rod captured ball");
            m.Serve();h.Grab(0,1);m.SetRodPose(1,0,-32);m.PlaceBall(new Vector3(.1175f,.775f,-.29f),new Vector3(0,0,-2.5f));
            for(int i=0;i<8;i++)m.Simulate(.02f);
            Require(m.ControlledRod<0,"Fast shot automatically trapped");
            m.SetPaused(true);Require(m.ControlledRod<0,"Pause possession reset");
            report+="PASS: unheld rods and fast shots do not auto-capture; pause clears control.\n";
            m.EndControlWarmup();m.StartMatch();Require(m.aiEnabled&&!m.ControlWarmup,"AI restore");
            m.aiEnabled=false;
            foreach(int receiver in new[]{3,5})
            {
                h.ReleaseAll();for(int i=0;i<8;i++)m.SetRodPose(i,0,75);
                m.SetRodPose(receiver,0,-32);
                m.PlaceBall(new Vector3(.30f,.775f,0),Vector3.zero);m.Simulate(.02f);
                m.PlaceBall(new Vector3(0,.775f,m.rods[receiver].localPosition.z+.12f),new Vector3(0,0,-.55f));
                h.Grab(0,receiver);for(int i=0;i<80;i++)m.Simulate(.02f);
                Require(m.ControlledRod==receiver&&m.PossessionSecured,"Reception on blue rod "+receiver+" owner="+m.ControlledRod+" p="+m.ball.localPosition.ToString("F5")+" v="+m.Velocity.ToString("F5"));
                m.PlaceBall(new Vector3(.06f,.775f,m.rods[receiver].localPosition.z+.065f),Vector3.zero);
                for(int i=0;i<12;i++)m.Simulate(.02f);
                Require(m.ControlledRod<0,"Nearby non-contact ball attracted/captured");
            }
            report+="PASS: midfield and attack rods receive too; nearby balls outside contact remain free.\n";
            m.aiEnabled=false;for(int i=0;i<8;i++)m.SetRodPose(i,0,0);
            m.PlaceBall(new Vector3(.3f,.775f,-.28f),new Vector3(2,0,0));for(int i=0;i<8;i++)m.Simulate(.02f);
            Require(m.WallContacts>0,"Wall rebound regression");
            m.SetRodPose(0,0,60);Require(Quaternion.Angle(m.rods[0].localRotation,Quaternion.AngleAxis(60,Vector3.right))<.1f,"Keeper rotation blocked");
            m.SetRodPose(2,0,32);m.PlaceBall(new Vector3(.3f,.775f,0),Vector3.zero);m.Simulate(.02f);
            m.aiEnabled=true;m.PlaceBall(new Vector3(0,.775f,-.31f),new Vector3(0,0,.55f));
            bool aiReceived=false,aiPassed=false;int shots=m.AIShots;
            for(int i=0;i<160;i++){m.Simulate(.02f);aiReceived|=m.ControlledRod==2;aiPassed|=aiReceived&&m.AIShots>shots;}
            Require(aiReceived&&aiPassed,"AI did not receive then release via same physical contact rules");
            report+="PASS: full match restores AI; walls and rotating keeper retained; AI receives and releases under same contact gates.\n";
            var ui=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatchUI>();Canvas.ForceUpdateCanvases();
            bool warmupButton=false;
            foreach(var b in ui.menu.GetComponentsInChildren<Button>(true))
            {
                var t=b.GetComponentInChildren<Text>();warmupButton|=t&&t.text.Contains("Receive a pass");
                var corners=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(corners);
                var panel=(RectTransform)ui.menu.transform;
                foreach(var corner in corners)Require(panel.rect.Contains(panel.InverseTransformPoint(corner)),"Menu button clipped");
            }
            Require(warmupButton,"Warm-up menu missing");
            report+="PASS: themed menu warm-up entry and button bounds.\nSynthetic software tests; Quest hand comfort and real-skill transfer require user validation.\n";
        }
        catch(Exception e){report+="FAIL: "+e;Debug.LogException(e);}
        File.WriteAllText("Library/BabyFootBallControlCheckV1.txt",report);
        File.Delete(Pending);EditorApplication.isPlaying=false;Debug.Log(report);
    }
}
