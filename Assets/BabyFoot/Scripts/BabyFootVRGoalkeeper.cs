using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// Shared controller adapter: constrained slide, vertical keeper, wrist-driven attack.
[DefaultExecutionOrder(-100)]
public class BabyFootVRGoalkeeper : MonoBehaviour
{
    public BabyFootDefense exercise;
    public Transform leftHand,rightHand,handle;
    public Camera headCamera;
    public float grabRadius=.12f;
    public bool FreeControl {get;set;}
    public bool IsHeld=>owner;
    public bool IsNear {get;private set;}
    public string TrackingStatus {get;private set;}="Bring a controller to the handle";
    public string Hint=>IsHeld?(exercise.IsAttack?"GRABBED | Slide, then swing through the ball":"GRABBED | Push or pull along the rod"):IsNear?"HOLD SIDE GRIP TO GRAB":"Bring a controller to this handle";
    public Transform ControlledRod=>rod;
    Transform owner,rod;
    XRNode ownerNode;
    bool mustRelease,previousActive,armed;
    float startHandX,startRodX,startRodAngle,previousAngle;
    Quaternion startHandRotation,restKeeperRotation;
    Renderer[] handleRenderers;
    MaterialPropertyBlock tint;
    TextMesh hint;
    int lastTint=-1;
    static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
    void Start()
    {
        restKeeperRotation=exercise.goalkeeper.localRotation;
        if(!headCamera)headCamera=Camera.main;
        var go=new GameObject("Handle Grip Instruction");go.transform.SetParent(transform,false);
        hint=go.AddComponent<TextMesh>();hint.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.GetComponent<MeshRenderer>().sharedMaterial=hint.font.material;hint.fontSize=44;hint.characterSize=.0018f;hint.anchor=TextAnchor.MiddleCenter;hint.color=Color.white;
        tint=new MaterialPropertyBlock();RefreshTarget();
    }
    static bool Tracked(InputDevice d)=>d.isValid&&d.TryGetFeatureValue(CommonUsages.isTracked,out bool value)&&value;
    static bool Grip(InputDevice d)=>d.TryGetFeatureValue(CommonUsages.gripButton,out bool button)&&button || d.TryGetFeatureValue(CommonUsages.grip,out float amount)&&amount>.65f;
    public static float Slide(float handX,float anchorX,float rodX,float limit)=>Mathf.Clamp(rodX+handX-anchorX,-limit,limit);
    public static float Twist(Quaternion from,Quaternion to,Vector3 axis)
    {
        var delta=to*Quaternion.Inverse(from);
        float projection=Vector3.Dot(new Vector3(delta.x,delta.y,delta.z),axis.normalized);
        return Mathf.DeltaAngle(0,2*Mathf.Atan2(projection,delta.w)*Mathf.Rad2Deg);
    }
    public static bool StrikeCrossing(float before,float after,float speed,ref bool ready)
    {
        if(after<=-12)ready=true;
        if(ready&&before<18&&after>=18&&speed>=100){ready=false;return true;}
        return false;
    }
    public void Release()
    {
        owner=null;armed=false;mustRelease=true;
    }
    public void RefreshTarget()
    {
        var next=exercise.IsAttack&&exercise.attack?exercise.attack.AttackingRod:exercise.goalkeeper;
        if(!next || next==rod)return;
        ClearTint();Release();rod=next;handle=rod.Find("Handle Visual");
        handleRenderers=handle?handle.GetComponentsInChildren<Renderer>(true):null;lastTint=-1;
    }
    void ClearTint(){if(handleRenderers!=null)foreach(var r in handleRenderers)if(r)r.SetPropertyBlock(null);}
    void Anchor()
    {
        startHandX=rod.parent.InverseTransformPoint(owner.position).x;startRodX=rod.localPosition.x;
        startHandRotation=owner.rotation;startRodAngle=exercise.IsAttack?Mathf.DeltaAngle(0,rod.localEulerAngles.x):0;
        previousAngle=startRodAngle;armed=false;
    }
    public void BeginHold(Transform hand,XRNode node)
    {
        RefreshTarget();if(!rod||!hand)return;
        owner=hand;ownerNode=node;Anchor();Pulse(.2f);
    }
    void Pulse(float strength){if(owner)owner.GetComponent<HapticImpulsePlayer>()?.SendHapticImpulse(strength,.035f);}
    // Pose entry point also allows deterministic synthetic integration checks.
    public void ApplyHeldPose(float deltaTime)
    {
        if(!owner||!rod)return;
        bool active=exercise.IsTrialActive;
        if(active&&!previousActive)Anchor();
        bool movable=FreeControl || active;
        if(movable)
        {
            var p=rod.localPosition;
            p.x=Slide(rod.parent.InverseTransformPoint(owner.position).x,startHandX,startRodX,exercise.lateralLimit);
            rod.localPosition=p;
            if(exercise.IsAttack)
            {
                float angle=Mathf.Clamp(startRodAngle+Twist(startHandRotation,owner.rotation,rod.parent.TransformDirection(Vector3.right)),-85,85);
                float speed=(angle-previousAngle)/Mathf.Max(.001f,deltaTime);
                rod.localRotation=Quaternion.AngleAxis(angle,Vector3.right);
                if(exercise.attack.TryControllerStrike(previousAngle,angle,speed))Pulse(.5f);
                previousAngle=angle;
            }
        }
        if(!exercise.IsAttack)exercise.externalGoalkeeperX=rod.localPosition.x;
        previousActive=active;
    }
    void Update()
    {
        RefreshTarget();if(!rod||!handle)return;
        var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool lt=Tracked(left),rt=Tracked(right),lg=Grip(left),rg=Grip(right);
        if(!lg&&!rg)mustRelease=false;
        bool nearLeft=lt&&Vector3.Distance(leftHand.position,handle.position)<=grabRadius;
        bool nearRight=rt&&Vector3.Distance(rightHand.position,handle.position)<=grabRadius;
        IsNear=nearLeft||nearRight;
        if(owner)
        {
            var device=ownerNode==XRNode.LeftHand?left:right;
            if(!Tracked(device)||!Grip(device)||!handle.gameObject.activeInHierarchy)Release();
        }
        if(!owner&&!mustRelease)
        {
            if(nearLeft&&lg)BeginHold(leftHand,XRNode.LeftHand);
            else if(nearRight&&rg)BeginHold(rightHand,XRNode.RightHand);
        }
        if(owner)ApplyHeldPose(Time.unscaledDeltaTime);
        else {previousActive=exercise.IsTrialActive;exercise.externalGoalkeeperX=exercise.goalkeeper.localPosition.x;}
        if(!exercise.IsAttack)exercise.goalkeeper.localRotation=restKeeperRotation;
        TrackingStatus=!lt&&!rt?"Controllers not tracked":Hint;
        bool show=FreeControl||!exercise.IsTrialActive;
        if(hint)
        {
            hint.gameObject.SetActive(show);hint.text=Hint;
            hint.transform.position=handle.position+Vector3.up*.16f;
            if(headCamera)hint.transform.rotation=Quaternion.LookRotation(hint.transform.position-headCamera.transform.position,Vector3.up);
        }
        int state=IsHeld?2:IsNear?1:0;
        if(state!=lastTint&&handleRenderers!=null)
        {
            foreach(var r in handleRenderers)if(r){if(state==0)r.SetPropertyBlock(null);else{tint.Clear();tint.SetColor(BaseColor,state==2?new Color(.1f,.9f,.65f):new Color(1,.75f,.15f));r.SetPropertyBlock(tint);}}
            lastTint=state;
        }
    }
    void OnDisable(){Release();ClearTint();if(hint)hint.gameObject.SetActive(false);}
}
