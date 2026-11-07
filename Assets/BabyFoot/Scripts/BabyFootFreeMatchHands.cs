using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

[DefaultExecutionOrder(-80)]
public class BabyFootFreeMatchHands : MonoBehaviour
{
    public BabyFootFreeMatch match;
    public Transform leftHand,rightHand;
    public float grabRadius=.12f;
    public string Status {get;private set;}="Side grip: grab any blue handle";
    readonly int[] held={-1,-1};readonly bool[] mustRelease={false,false};
    readonly float[] handX=new float[2],rodX=new float[2],angle=new float[2];
    readonly Quaternion[] handRotation=new Quaternion[2];
    Transform[] handles;Renderer[][] visuals;MaterialPropertyBlock tint;
    int[] colours;
    void Start()
    {
        handles=new Transform[match.rods.Length];visuals=new Renderer[match.rods.Length][];colours=new int[match.rods.Length];tint=new MaterialPropertyBlock();
        for(int i=0;i<handles.Length;i++){handles[i]=match.rods[i].Find("Handle Visual");visuals[i]=handles[i].GetComponentsInChildren<Renderer>(true);}
    }
    public bool IsHoldingRod(int index)=>held[0]==index||held[1]==index;
    public void ReleaseAll(){for(int hand=0;hand<2;hand++){held[hand]=-1;mustRelease[hand]=true;}}
    public bool Grab(int hand,int rod)
    {
        if(match.Spectator||!BabyFootFreeMatch.IsBlue(rod)||held[1-hand]==rod)return false;
        var pose=hand==0?leftHand:rightHand;var target=match.rods[rod];held[hand]=rod;
        handX[hand]=target.parent.InverseTransformPoint(pose.position).x;rodX[hand]=target.localPosition.x;
        handRotation[hand]=pose.rotation;angle[hand]=match.RodAngle(rod);
        pose.GetComponent<HapticImpulsePlayer>()?.SendHapticImpulse(.2f,.035f);return true;
    }
    public void ApplyPose(int hand)
    {
        if(held[hand]<0)return;
        var pose=hand==0?leftHand:rightHand;int i=held[hand];var rod=match.rods[i];
        var range=match.TravelRange(i);
        float x=Mathf.Clamp(rodX[hand]+rod.parent.InverseTransformPoint(pose.position).x-handX[hand],range.x,range.y);
        angle[hand]+=BabyFootVRGoalkeeper.Twist(handRotation[hand],pose.rotation,rod.parent.TransformDirection(Vector3.right));
        handRotation[hand]=pose.rotation;match.SetRodPose(i,x,angle[hand]);
    }
    static bool Tracked(InputDevice d)=>d.isValid&&d.TryGetFeatureValue(CommonUsages.isTracked,out bool value)&&value;
    static bool Grip(InputDevice d)=>d.TryGetFeatureValue(CommonUsages.gripButton,out bool b)&&b||d.TryGetFeatureValue(CommonUsages.grip,out float v)&&v>.65f;
    void Update()
    {
        if(handles==null)return;
        int hoverLeft=-1,hoverRight=-1;
        for(int hand=0;hand<2;hand++)
        {
            var device=InputDevices.GetDeviceAtXRNode(hand==0?XRNode.LeftHand:XRNode.RightHand);var pose=hand==0?leftHand:rightHand;bool tracked=Tracked(device),grip=Grip(device);
            if(!grip)mustRelease[hand]=false;
            if(match.Paused||match.Spectator||!tracked||!grip){if(held[hand]>=0){held[hand]=-1;mustRelease[hand]=true;}}
            float nearest=grabRadius;int hover=-1;
            if(tracked&&!match.Paused&&!match.Spectator)for(int i=0;i<handles.Length;i++)if(BabyFootFreeMatch.IsBlue(i))
            {float d=Vector3.Distance(pose.position,handles[i].position);if(d<nearest){nearest=d;hover=i;}}
            if(hand==0)hoverLeft=hover;else hoverRight=hover;
            if(held[hand]<0&&!mustRelease[hand]&&grip&&hover>=0)Grab(hand,hover);
            if(held[hand]>=0)ApplyPose(hand);
        }
        for(int i=0;i<handles.Length;i++)if(BabyFootFreeMatch.IsBlue(i))
        {
            int colour=held[0]==i||held[1]==i?2:hoverLeft==i||hoverRight==i?1:0;
            if(colour!=colours[i]){foreach(var r in visuals[i]){if(colour==0)r.SetPropertyBlock(null);else{tint.Clear();tint.SetColor("_BaseColor",colour==2?new Color(.1f,.9f,.65f):new Color(1,.75f,.15f));r.SetPropertyBlock(tint);}}colours[i]=colour;}
        }
        Status="Left: "+(held[0]>=0?match.rods[held[0]].name:"free")+" | Right: "+(held[1]>=0?match.rods[held[1]].name:"free");
    }
    void OnDisable(){ReleaseAll();if(visuals!=null)foreach(var list in visuals)if(list!=null)foreach(var r in list)if(r)r.SetPropertyBlock(null);}
}
