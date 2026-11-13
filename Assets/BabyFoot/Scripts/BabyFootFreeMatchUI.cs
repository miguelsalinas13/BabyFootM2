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
    enum LaneMode { Auto, Always, Off }
    LaneMode laneMode;
    string LaneLabel=>"Lane cue / "+laneMode.ToString().ToLowerInvariant();
    LineRenderer lane;
    Material laneMaterial;
    void UpdateLane()
    {
        bool visible=laneMode!=LaneMode.Off && (laneMode==LaneMode.Always || match.PracticeSuccesses<3) && match.ReceivingFeed && !match.Paused;
        if(!visible){if(lane)lane.enabled=false;return;}
        if(!lane)
        {
            var go=new GameObject("Receiving lane cue");
            go.transform.SetParent(match.ball.parent,false);
            lane=go.AddComponent<LineRenderer>();lane.useWorldSpace=false;lane.loop=false;
            lane.positionCount=32;lane.widthMultiplier=.003f;
            laneMaterial=new Material(Shader.Find("Sprites/Default"));lane.sharedMaterial=laneMaterial;
            lane.startColor=lane.endColor=new Color(1f,.89f,.46f,.85f);
            for(int i=0;i<32;i++)
            {
                float a=Mathf.Lerp(Mathf.PI*.12f,Mathf.PI*.88f,i/31f);
                lane.SetPosition(i,new Vector3(Mathf.Cos(a)*.027f,0,Mathf.Sin(a)*.027f));
            }
        }
        lane.transform.localPosition=match.RecoveryTarget-Vector3.up*(BabyFootFreeMatch.Radius-.002f);
        lane.enabled=true;
    }
    void OnDestroy(){if(lane)Destroy(lane.gameObject);if(laneMaterial)Destroy(laneMaterial);}
    Font font;
    BabyFootMatchGaze gaze;
    string LevelLabel => "AI / "+new[]{"Beginner","Club","Expert","Master"}[Mathf.Clamp(match.difficulty,0,3)];
    string RoleLabel => match.Role == BabyFootFreeMatch.BlueRole.Defense
        ? "2 vs 2 / You defend / AI plays midfield and attack"
        : match.Role == BabyFootFreeMatch.BlueRole.Attack
            ? "2 vs 2 / You play midfield and attack / AI defends"
            : "Solo / You control all four rods";

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
    Transform Row(Transform parent,float height)
    {
        var row=new GameObject("Actions",typeof(RectTransform));
        row.transform.SetParent(parent,false);
        row.AddComponent<LayoutElement>().preferredHeight=height;
        var layout=row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing=14;layout.childForceExpandWidth=true;
        layout.childControlWidth=true;layout.childControlHeight=true;
        return row.transform;
    }
    void Mode(Transform row,string title,string photo,UnityEngine.Events.UnityAction action)
    {
        var button=BabyFootUITheme.Button(row,title,action,205,photo=="Play");
        button.GetComponent<LayoutElement>().flexibleWidth=1;
        var picture=new GameObject("Preview",typeof(RectTransform)).AddComponent<RawImage>();
        picture.transform.SetParent(button.transform,false);
        picture.texture=Resources.Load<Texture2D>("BabyFootUI/"+photo);picture.raycastTarget=false;
        var rect=picture.rectTransform;rect.anchorMin=new Vector2(0,.32f);rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(5,0);rect.offsetMax=new Vector2(-5,-5);
        var label=button.GetComponentInChildren<Text>();label.rectTransform.anchorMax=new Vector2(1,.32f);
        var arrow=(RectTransform)button.transform.Find("Forward mark");arrow.anchorMin=arrow.anchorMax=new Vector2(1,.16f);
    }
    void Start()
    {
        font=BabyFootUITheme.Regular;
        var root=new GameObject("Free Match Menu",typeof(RectTransform));root.transform.SetParent(transform,false);root.transform.SetPositionAndRotation(new Vector3(.05f,1.55f,0),Quaternion.Euler(0,-90,0));root.transform.localScale=Vector3.one*.0011f;
        menu=root.AddComponent<Canvas>();menu.renderMode=RenderMode.WorldSpace;menu.worldCamera=origin.Camera;root.AddComponent<TrackedDeviceGraphicRaycaster>();BabyFootUITheme.Panel(root);root.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,925);
        var layout=root.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(28,28,20,20);layout.spacing=9;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
        BabyFootUITheme.Header(root.transform,"Open match");
        var modes=Row(root.transform,205);
        Mode(modes,"Play as Blue","Play",PlayAsBlue);
        Mode(modes,"Watch match","Watch",WatchMatch);
        Text roleLabel = null;
        var roleButton = BabyFootUITheme.Button(root.transform, RoleLabel, () => {
            var next = (BabyFootFreeMatch.BlueRole)(((int)match.Role + 1) % 3);
            match.SetRole(next);
            roleLabel.text = RoleLabel;
        }, 60);
        roleLabel = roleButton.GetComponentInChildren<Text>();
        Text(root.transform,"Practice",28,42);
        var practice=Row(root.transform,66);
        Button(practice,"Retention practice",()=>{hands.ReleaseAll();match.BeginControlWarmup();started=true;menu.gameObject.SetActive(false);});
        Button(practice,"Training exercises",()=>UnityEngine.SceneManagement.SceneManager.LoadScene("Assets/BabyFoot/Scenes/BabyFoot_VR.unity"));
        Button(practice,"Rebound recovery",()=>{hands.ReleaseAll();match.BeginRecoveryPractice();started=true;menu.gameObject.SetActive(false);});
        gaze=gameObject.AddComponent<BabyFootMatchGaze>();
        var settings=Row(root.transform,60);
        Text difficultyLabel=null,gazeLabel=null;
        var levelButton=BabyFootUITheme.Button(settings,LevelLabel,()=>{
            match.difficulty=(match.difficulty+1)%4;
            difficultyLabel.text=LevelLabel;
        },60);
        difficultyLabel=levelButton.GetComponentInChildren<Text>();
        var recordButton=BabyFootUITheme.Button(settings,gaze.Status,()=>{gaze.Toggle();gazeLabel.text=gaze.Status;},60);
        gazeLabel=recordButton.GetComponentInChildren<Text>();
        Text cueLabel=null;
        var cueButton=BabyFootUITheme.Button(settings,LaneLabel,()=>{
            laneMode=(LaneMode)(((int)laneMode+1)%3);cueLabel.text=LaneLabel;
        },60);
        cueLabel=cueButton.GetComponentInChildren<Text>();
        Text(root.transform,"Match controls",28,42);
        var tools=Row(root.transform,65);
        Button(tools,"Serve ball",()=>{if(!started)PlayAsBlue();else{match.SetPaused(false);menu.gameObject.SetActive(false);}match.Serve();});
        Button(tools,"Restart match",()=>{match.StartMatch();started=true;menu.gameObject.SetActive(false);});
        Button(tools,"Recenter",Recenter);
        guides=PlayerPrefs.GetInt("BabyFoot.MatchHints",1)!=0;
        Text guideLabel=null;
        var guideButton=BabyFootUITheme.Button(root.transform,guides?"Play hints / on":"Play hints / off",()=>{
            guides=!guides;hintUntil=0;PlayerPrefs.SetInt("BabyFoot.MatchHints",guides?1:0);
            guideLabel.text=guides?"Play hints / on":"Play hints / off";
        },48);
        guideLabel=guideButton.GetComponentInChildren<Text>();
        Text(root.transform,"A / X   Serve ball       B / Y   Pause",21,40);
        var hud=new GameObject("Free Match Score",typeof(RectTransform));hud.transform.SetParent(transform,false);hud.transform.SetPositionAndRotation(new Vector3(-.3f,1.85f,0),Quaternion.Euler(0,-90,0));hud.transform.localScale=Vector3.one*.0011f;
        var canvas=hud.AddComponent<Canvas>();scoreCanvas=canvas;canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=origin.Camera;hud.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,190);
        var rows=hud.AddComponent<VerticalLayoutGroup>();rows.childControlHeight=true;rows.childControlWidth=true;rows.childForceExpandHeight=false;score=Text(hud.transform,"",43,72);info=Text(hud.transform,"",22,100);BabyFootUITheme.Hud(hud,score);info.color=BabyFootUITheme.Hex("CBDDE8");info.alignment=TextAnchor.MiddleCenter;
        var view=gameObject.AddComponent<BabyFootSpectatorView>();view.ui=this;view.match=match;view.origin=origin;
    }
    bool guides=true;
    string activeHint="";
    float hintUntil;
    string MatchHint()
    {
        int owner=match.ControlledRod;
        if(owner>=0 && BabyFootFreeMatch.IsBlue(owner))
            return match.PossessionSecured?"Slide to open a lane, then twist to pass or shoot.":"Keep the foot tilted until the ball settles.";
        if(owner>=0)return "Cover the gap between the ball and your goal.";
        if(match.ControlWarmup)return "Use grip or trigger on the blue two-player rod and tilt the foot toward the incoming ball.";
        if(!hands.IsHoldingRod(0)&&!hands.IsHoldingRod(1)&&!hands.IsHoldingRod(3)&&!hands.IsHoldingRod(5))
            return "Reach for a blue handle. Squeeze when it lights up.";
        return "Slide to receive. To switch rods, release, reach and hold grip or trigger.";
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
        UpdateLane();
        gaze.Observe(match,origin,laneMode!=LaneMode.Off,lane && lane.enabled, guides,(int)laneMode);
        if(Time.unscaledTime<refresh)return;refresh=Time.unscaledTime+.1f;
        score.text=match.ControlWarmup?match.RetentionTask:$"<color=#97D0D6>BLUE</color>  {match.BlueScore}   :   {match.CoralScore}  <color=#FF938A>RED</color>";
        string possession=match.ControlledRod<0?match.Message:match.PossessionSecured?"Ball secured / "+match.rods[match.ControlledRod].name:"Receiving / "+match.rods[match.ControlledRod].name;
        if(match.Spectator)
        {
            score.text=$"<color=#97D0D6>BLUE AI</color>  {match.BlueScore}   :   {match.CoralScore}  <color=#FF938A>RED AI</color>";
            info.text="SPECTATOR / "+possession+"\nB / Y  Pause & menu";
        }
        else if(match.ControlWarmup && !match.RecoveryPractice)
        {
            string tip=!hands.IsHoldingRod(1)?"Grab the blue two-player handle."
                :match.ControlledRod!=1?"Tilt the foot toward the incoming ball. A / X retries."
                :match.RetentionTask.StartsWith("3/")?"Keep the same foot on top and move the handle sideways."
                :$"Keep the foot still: {match.BallControl.StableSeconds:F1} s";
            info.text=match.RetentionTask.StartsWith("Practice complete")?"All three steps complete. A / X starts again.":tip;
        }
        else if(match.RecoveryPractice)info.text=match.RecoveryCue+"\n"+(gaze.Status=="Stop recording"?gaze.Coverage:gaze.Status);
        else
        {
            if(guides && Time.unscaledTime>=hintUntil)
            {
                activeHint=MatchHint();
                hintUntil=Time.unscaledTime+1.25f;
            }
            info.text=possession+"\n"+(guides?activeHint:hands.Status);
        }
        scoreCanvas.gameObject.SetActive(!menu.gameObject.activeSelf);
    }
}
