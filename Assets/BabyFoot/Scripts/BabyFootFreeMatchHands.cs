using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

[DefaultExecutionOrder(-80)]
public class BabyFootFreeMatchHands : MonoBehaviour
{
    public BabyFootFreeMatch match;
    public Transform leftHand,rightHand;
    public float grabRadius=.12f;
    public string Status {get;private set;}="Grip or trigger: grab a blue handle";
    readonly int[] held={-1,-1};readonly bool[] mustRelease={false,false};
    readonly float[] handX=new float[2],rodX=new float[2],angle=new float[2];
    readonly Quaternion[] handRotation=new Quaternion[2];
    Transform[] handles;Renderer[][] visuals;MaterialPropertyBlock tint;
    int[] colours;

    readonly int[] preview={-1,-1};

    float[] halfLength;
    public int PreviewRod(int hand)=>preview[hand];
    void Start()
    {
        handles=new Transform[match.rods.Length];visuals=new Renderer[match.rods.Length][];colours=new int[match.rods.Length];tint=new MaterialPropertyBlock();
        halfLength=new float[handles.Length];
        for(int i=0;i<handles.Length;i++)
        {
            var rod=match.rods[i];
            var proxy=rod.Find("Handle Visual");handles[i]=proxy;
            var visible=new System.Collections.Generic.List<Renderer>();
            foreach(var r in rod.GetComponentsInChildren<Renderer>(false))
            {
                if(!r.enabled || !(r.transform.IsChildOf(proxy)||r.name=="Handle"))continue;
                visible.Add(r);
            }
            visuals[i]=visible.ToArray();
            var mesh=proxy.GetComponent<MeshFilter>();
            
            if(mesh)
            {
                var e=mesh.sharedMesh.bounds.extents;
                var axis=rod.parent.right;
                halfLength[i]=Mathf.Abs(Vector3.Dot(axis,proxy.TransformVector(Vector3.right*e.x)))+
                    Mathf.Abs(Vector3.Dot(axis,proxy.TransformVector(Vector3.up*e.y)))+
                    Mathf.Abs(Vector3.Dot(axis,proxy.TransformVector(Vector3.forward*e.z)));
            }
        }
    }
    public bool IsHoldingRod(int index)=>held[0]==index||held[1]==index;
    public void ReleaseAll(){for(int hand=0;hand<2;hand++){held[hand]=preview[hand]=-1;mustRelease[hand]=true;}}
    public bool Grab(int hand,int rod)
    {
        if(!match.IsHumanRod(rod)||held[1-hand]==rod)return false;
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
    float HandleDistance(int i,Vector3 position)
    {
        Vector3 axis=match.rods[i].parent.right;
        Vector3 delta=position-handles[i].position;
        return (delta-axis*Mathf.Clamp(Vector3.Dot(delta,axis),-halfLength[i],halfLength[i])).magnitude;
    }

    public void UpdateGrip(int hand,bool tracked,bool pressed,float now)
    {
        var pose=hand==0?leftHand:rightHand;


        if(!tracked||match.Paused||match.Spectator)
        {
            held[hand]=preview[hand]=-1;mustRelease[hand]=true;return;
        }
        if(!pressed){held[hand]=-1;mustRelease[hand]=false;}
        if(held[hand]>=0){preview[hand]=-1;ApplyPose(hand);return;}

        // Keep the highlighted candidate stable near the boundary between handles.
        int best=-1;float nearest=grabRadius;
        for(int i=0;i<handles.Length;i++)
        {
            if(!match.IsHumanRod(i)||held[1-hand]==i)continue;
            float d=HandleDistance(i,pose.position);
            if(d<nearest){nearest=d;best=i;}
        }
        int old=preview[hand];
        if(old>=0 && old!=held[1-hand] && best>=0 && HandleDistance(old,pose.position)<=grabRadius && HandleDistance(old,pose.position)<nearest+.008f)
            best=old;
        preview[hand]=best;
        if(!pressed||mustRelease[hand])return;
        if(best>=0 && Grab(hand,best))preview[hand]=-1;
    }
    void Update()
    {
        if(handles==null)return;
        for(int hand=0;hand<2;hand++)
        {
            var device=InputDevices.GetDeviceAtXRNode(hand==0?XRNode.LeftHand:XRNode.RightHand);
            UpdateGrip(hand,Tracked(device),BabyFootRodInput.IsPressed(device,held[hand]>=0),Time.unscaledTime);
        }
        for(int i=0;i<handles.Length;i++)if(BabyFootFreeMatch.IsBlue(i))
        {
            int colour=held[0]==i||held[1]==i?2:preview[0]==i||preview[1]==i?1:0;
            if(colour!=colours[i]){foreach(var r in visuals[i]){if(colour==0)r.SetPropertyBlock(null);else{tint.Clear();tint.SetColor("_BaseColor",colour==2?new Color(.1f,.9f,.65f):new Color(1,.75f,.15f));r.SetPropertyBlock(tint);}}colours[i]=colour;}
        }
        Status="Left: "+(held[0]>=0?match.rods[held[0]].name:"free")+" | Right: "+(held[1]>=0?match.rods[held[1]].name:"free");
    }
    void OnDisable(){ReleaseAll();if(visuals!=null)foreach(var list in visuals)if(list!=null)foreach(var r in list)if(r)r.SetPropertyBlock(null);}
}
