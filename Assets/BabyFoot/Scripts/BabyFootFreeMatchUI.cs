using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class BabyFootFreeMatchUI : MonoBehaviour
{
    public BabyFootFreeMatch match;
    public BabyFootFreeMatchHands hands;
    public XROrigin origin;
    public Canvas menu;
    Text score,info;
    Canvas scoreCanvas;
    bool primary,secondary,started;
    float refresh;
    Font font;
    public void PlayAsBlue()
    {
        bool wasSpectator=match.Spectator;match.SetSpectatorMode(false);match.EndControlWarmup();
        if(!started||wasSpectator){match.StartMatch();started=true;}else match.SetPaused(false);
        menu.gameObject.SetActive(false);
    }
    public void WatchMatch()
    {
        hands.ReleaseAll();if(!match.Spectator)match.StartSpectator();else match.SetPaused(false);
        started=true;menu.gameObject.SetActive(false);
    }
    public void TogglePause()
    {
        bool open=!menu.gameObject.activeSelf;menu.gameObject.SetActive(open);match.SetPaused(open);hands.ReleaseAll();
    }
    Text Text(Transform parent,string content,int size,float height)
    {
        var go=new GameObject("Label",typeof(RectTransform));go.transform.SetParent(parent,false);var t=go.AddComponent<Text>();BabyFootUITheme.StyleText(t,size,size>=28);t.text=content;go.AddComponent<LayoutElement>().preferredHeight=height;return t;
    }
    void Button(Transform parent,string title,UnityEngine.Events.UnityAction action)
    {BabyFootUITheme.Button(parent,title,action,60,title.StartsWith("Play as Blue"));}
    void Start()
    {
        font=BabyFootUITheme.Regular;
        var root=new GameObject("Free Match Menu",typeof(RectTransform));root.transform.SetParent(transform,false);root.transform.SetPositionAndRotation(new Vector3(.05f,1.55f,0),Quaternion.Euler(0,-90,0));root.transform.localScale=Vector3.one*.0011f;
        menu=root.AddComponent<Canvas>();menu.renderMode=RenderMode.WorldSpace;menu.worldCamera=origin.Camera;root.AddComponent<TrackedDeviceGraphicRaycaster>();BabyFootUITheme.Panel(root);root.GetComponent<RectTransform>().sizeDelta=new Vector2(900,1010);
        var layout=root.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(28,28,20,20);layout.spacing=9;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
        BabyFootUITheme.Header(root.transform,"Open match");
        Text(root.transform,"YOUR TEAM  /  BLUE                         OPPONENT  /  CORAL",23,38);
        Text(root.transform,"Hold side grip to grab a blue handle. Slide and twist to play.\nTilt the foot over the ball to pin it against the pitch.\nSlide gently to carry it; lift to release or flick to pass.\nAll rods rotate continuously, including the goalkeeper.",24,140);
        Button(root.transform,"Play as Blue / Resume",PlayAsBlue);
        Button(root.transform,"Watch AI vs AI",WatchMatch);
        Button(root.transform,"Receive a pass / warm-up",()=>{hands.ReleaseAll();match.BeginControlWarmup();started=true;menu.gameObject.SetActive(false);});
        Button(root.transform,"New ball (A / X)",()=>match.Serve());
        Button(root.transform,"New match",()=>{match.StartMatch();started=true;menu.gameObject.SetActive(false);});
        Button(root.transform,"Recenter at table",Recenter);
        Button(root.transform,"Training exercises",()=>UnityEngine.SceneManagement.SceneManager.LoadScene("Assets/BabyFoot/Scenes/BabyFoot_VR.unity"));
        Text(root.transform,"A / X  New ball                         B / Y  Pause & menu",21,45);
        var hud=new GameObject("Free Match Score",typeof(RectTransform));hud.transform.SetParent(transform,false);hud.transform.SetPositionAndRotation(new Vector3(-.3f,1.85f,0),Quaternion.Euler(0,-90,0));hud.transform.localScale=Vector3.one*.0011f;
        var canvas=hud.AddComponent<Canvas>();scoreCanvas=canvas;canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=origin.Camera;hud.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,190);
        var rows=hud.AddComponent<VerticalLayoutGroup>();rows.childControlHeight=true;rows.childControlWidth=true;rows.childForceExpandHeight=false;score=Text(hud.transform,"",43,72);info=Text(hud.transform,"",22,100);BabyFootUITheme.Hud(hud,score);info.color=BabyFootUITheme.Hex("E1CDDF");info.alignment=TextAnchor.MiddleCenter;
        var view=gameObject.AddComponent<BabyFootSpectatorView>();view.ui=this;view.match=match;view.origin=origin;
    }
    public void Recenter()
    {
        hands.ReleaseAll();var head=origin.Camera.transform;var facing=Vector3.ProjectOnPlane(head.forward,Vector3.up);
        if(facing.sqrMagnitude>.01f)origin.transform.RotateAround(head.position,Vector3.up,Vector3.SignedAngle(facing,Vector3.left,Vector3.up));
        var target=new Vector3(.95f,head.position.y,0);origin.transform.position+=target-head.position;
    }
    void Update()
    {
        if(!menu)return;
        var l=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var r=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool a=l.TryGetFeatureValue(CommonUsages.primaryButton,out bool lp)&&lp||r.TryGetFeatureValue(CommonUsages.primaryButton,out bool rp)&&rp;
        bool b=l.TryGetFeatureValue(CommonUsages.secondaryButton,out bool ls)&&ls||r.TryGetFeatureValue(CommonUsages.secondaryButton,out bool rs)&&rs;
        if(a&&!primary&&!match.Paused)match.Serve();
        if(b&&!secondary&&started)TogglePause();
        primary=a;secondary=b;
        if(Time.unscaledTime<refresh)return;refresh=Time.unscaledTime+.1f;
        score.text=match.ControlWarmup?"BALL CONTROL / WARM-UP":$"<color=#97D0D6>BLUE</color>  {match.BlueScore}   :   {match.CoralScore}  <color=#F4A8B3>CORAL</color>";
        string possession=match.ControlledRod<0?match.Message:match.PossessionSecured?"Ball secured / "+match.rods[match.ControlledRod].name:"Receiving / "+match.rods[match.ControlledRod].name;
        if(match.Spectator)
        {
            score.text=$"<color=#97D0D6>BLUE AI</color>  {match.BlueScore}   :   {match.CoralScore}  <color=#F4A8B3>CORAL AI</color>";
            info.text="SPECTATOR / "+possession+"\nB / Y  Pause & menu";
        }
        else info.text=possession+"\n"+hands.Status;
        scoreCanvas.gameObject.SetActive(!menu.gameObject.activeSelf);
    }
}
