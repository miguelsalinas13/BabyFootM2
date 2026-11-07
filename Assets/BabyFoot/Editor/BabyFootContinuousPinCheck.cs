using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BabyFootContinuousPinCheck
{
    const string Pending="Library/BabyFootContinuousPinCheck.pending";
    static double due;
    static BabyFootContinuousPinCheck(){EditorApplication.playModeStateChanged+=Changed;}
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
        EditorApplication.update-=Tick;string report="";
        try
        {
            var m=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatch>();m.enabled=false;
            var h=m.GetComponent<BabyFootFreeMatchHands>();h.enabled=false;m.StartMatch();m.aiEnabled=false;
            foreach(int rod in new[]{0,1})
            {
                h.ReleaseAll();m.SetRodPose(rod,0,0);m.PlaceBall(new Vector3(.31f,.775f,0),Vector3.zero);h.leftHand.rotation=Quaternion.identity;h.Grab(0,rod);
                for(int n=1;n<=36;n++){h.leftHand.rotation=Quaternion.AngleAxis(n*30,Vector3.right);h.ApplyPose(0);m.Simulate(.02f);Require(Mathf.Abs(m.RodAngle(rod)-n*30)<.02f,"Forward full-turn discontinuity rod="+rod+" step="+n+" angle="+m.RodAngle(rod));}
                for(int n=35;n>=-24;n--){h.leftHand.rotation=Quaternion.AngleAxis(n*30,Vector3.right);h.ApplyPose(0);m.Simulate(.02f);Require(Mathf.Abs(m.RodAngle(rod)-n*30)<.02f,"Reverse full-turn discontinuity rod="+rod);}
                h.ReleaseAll();h.Grab(0,rod);h.ApplyPose(0);Require(Mathf.Abs(m.RodAngle(rod)+720)<.02f,"Regrip lost winding");
            }
            report+="PASS: continuous controller winding +1080 to -720 degrees, crossing all angle wraps; regrip preserves angle; goalkeeper rotates too.\n";
            h.ReleaseAll();for(int i=0;i<8;i++)m.SetRodPose(i,0,90);
            m.SetRodPose(1,0,178);m.PlaceBall(new Vector3(.31f,.775f,0),Vector3.zero);m.Simulate(.02f);
            m.PlaceBall(new Vector3(.1175f,.775f,m.rods[1].localPosition.z),Vector3.zero);m.SetRodPose(1,0,182);m.Simulate(.02f);
            Require(m.Velocity.magnitude<.001f,"Crossing 180 generated a ghost sweep through the ball");
            report+="PASS: a 178->182 degree wrap does not create a false 356-degree sweep or ghost shot.\n";
            int cases=0;
            foreach(int rod in new[]{0,1,3,5})foreach(int side in new[]{-1,1})foreach(float tilt in new[]{28f,35f,42f})foreach(float speed in new[]{1.15f,1.6f})
            {
                if(rod==0&&side<0)continue;
                h.ReleaseAll();for(int i=0;i<8;i++)m.SetRodPose(i,0,90);
                m.SetRodPose(rod,0,-side*tilt);m.PlaceBall(new Vector3(.31f,.775f,0),Vector3.zero);m.Simulate(.02f);
                float x=rod==1?.1175f:0;float z=m.rods[rod].localPosition.z;
                h.Grab(0,rod);m.PlaceBall(new Vector3(x,.775f,z+side*.13f),new Vector3(0,0,-side*speed));
                for(int n=0;n<55;n++)m.Simulate(.02f);
                Require(m.ControlledRod==rod&&m.PossessionSecured,"Pin failed rod="+rod+" side="+side+" angle="+tilt+" speed="+speed+" owner="+m.ControlledRod+" position="+m.ball.localPosition+" velocity="+m.Velocity);
                Require(m.Velocity.magnitude<.05f&&Mathf.Abs(m.ball.localPosition.y-.775f)<.0001f&&m.ball.parent!=m.rods[rod],"Pin motion/plane/parenting");
                cases++;
            }
            report+="PASS: "+cases+" prepared physical receptions/pins at 28,35,42 degrees and 1.15/1.6m/s; both approach sides on outfield rods, field-side goalkeeper reception; stable grounded ball rather than backwards bounce.\n";
            h.ReleaseAll();m.Simulate(.02f);Require(m.ControlledRod<0,"Grip loss did not release pin");
            report+="PASS: grip release leaves ball free. Synthetic tests; headset wrist direction/feel remains for user testing.\n";
        }
        catch(Exception e){report+="FAIL: "+e;Debug.LogException(e);}
        File.WriteAllText("Library/BabyFootContinuousPinCheckV1.txt",report);File.Delete(Pending);EditorApplication.isPlaying=false;
    }
}
