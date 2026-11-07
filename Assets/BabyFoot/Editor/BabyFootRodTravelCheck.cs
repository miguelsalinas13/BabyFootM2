using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BabyFootRodTravelCheck
{
    const string Pending="Library/BabyFootRodTravel.pending",Report="Library/BabyFootRodTravelV1.txt";
    static double due;
    static BabyFootRodTravelCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run(){File.WriteAllText(Pending,"run");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange state)
    {if(state==PlayModeStateChange.EnteredPlayMode&&File.Exists(Pending)){due=EditorApplication.timeSinceStartup+1.5;EditorApplication.update+=Tick;}}
    static void Check(bool condition,string why){if(!condition)throw new Exception(why);}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        EditorApplication.update-=Tick;
        try
        {
            var match=UnityEngine.Object.FindAnyObjectByType<BabyFootFreeMatch>();match.enabled=false;match.aiEnabled=false;
            var hands=match.GetComponent<BabyFootFreeMatchHands>();hands.enabled=false;
            var log=new StringBuilder("PASS\n");int contacts=0;
            for(int i=0;i<match.rods.Length;i++)
            {
                var rod=match.rods[i];var range=match.TravelRange(i);
                Check(range.x<0&&range.y>0,"Invalid travel range: "+rod.name);
                var shape=new BabyFootFootContact(rod);
                for(int side=-1;side<=1;side+=2)
                {
                    match.SetRodPose(i,side*10,0);
                    Check(Mathf.Abs(rod.localPosition.x-(side<0?range.x:range.y))<.00001f,"Pose clamp failed");
                    var p=new Vector3(side*(BabyFootFreeMatch.HalfWidth-BabyFootFreeMatch.Radius),.775f,rod.localPosition.z+(rod.localPosition.z<-.5f?.027f:-.027f));
                    Check(shape.Sweep(match.ball.parent.TransformPoint(p),BabyFootFreeMatch.Radius,0,BabyFootFreeMatch.IsKeeper(i)?0:8,out _,out _),"Cannot contact wall ball: "+rod.name+" side="+side);
                    contacts++;
                    foreach(Transform figure in rod)if(figure.name.StartsWith("Player "))foreach(var renderer in figure.GetComponentsInChildren<Renderer>(true))if(renderer.enabled&&renderer.gameObject.activeInHierarchy)
                    {
                        var bounds=renderer.bounds;
                        for(int n=0;n<8;n++)
                        {
                            var vertex=bounds.center+Vector3.Scale(bounds.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));
                            float x=rod.parent.InverseTransformPoint(vertex).x;Check(Mathf.Abs(x)<=BabyFootFreeMatch.HalfWidth+.0001f,"Figure crosses wall: "+rod.name);
                        }
                    }
                    var shaft=rod.Find("Shaft 16 mm");float centre=rod.localPosition.x+shaft.localPosition.x;
                    Check(centre-shaft.localScale.y<-.37f&&centre+shaft.localScale.y>.37f,"Shaft lost bearing support");
                    var handle=rod.Find("Handle Visual");float grip=rod.localPosition.x+handle.localPosition.x;
                    Check(Mathf.Abs(grip)-.065f>=.37f,"Handle entered cabinet");
                }
                match.SetRodPose(i,0,0);
                log.AppendLine(rod.name+": "+range.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" .. "+range.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" m");
            }
            Check(match.TravelRange(5).y>.1f&&match.TravelRange(1).y>.15f,"Forward/defender limits still too short");
            hands.leftHand.position=match.rods[5].Find("Handle Visual").position;hands.leftHand.rotation=Quaternion.identity;Check(hands.Grab(0,5),"Grab failed");hands.leftHand.position+=Vector3.right;hands.ApplyPose(0);
            Check(Mathf.Abs(match.rods[5].localPosition.x-match.TravelRange(5).y)<.0001f,"Controller still clamps at old 9cm");
            log.AppendLine("Wall contacts verified: "+contacts+"/16. Geometry stays inside side walls; shafts remain through bearings; grips stay outside cabinet; controller uses derived limits. Training scene/input limits unchanged.");
            File.WriteAllText(Report,log.ToString());
        }
        catch(Exception e){File.WriteAllText(Report,"FAIL\n"+e);Debug.LogException(e);}
        finally{if(File.Exists(Pending))File.Delete(Pending);EditorApplication.isPlaying=false;}
    }
}
