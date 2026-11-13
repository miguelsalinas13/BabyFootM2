using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Guided controller onboarding; free controls never write participant session data.
public class BabyFootVRMenu : MonoBehaviour
{
    public BabyFootSession session;
    public BabyFootVRGoalkeeper control;
    public XROrigin origin;
    public Canvas menuCanvas;
    public Text statusText;
    Canvas statusCanvas;
    public bool IsTestingControls=>testing;
    public int Page=>page;
    Font font;
    GameObject progression;
    GameObject exercises,controls,settings,running,between,summary,visibility;
    Text title,subtitle,instruction,controlState,settingsTitle,feedback,summaryLabel,codeLabel;
    UnityEngine.UI.Button nextTrial,recenter,resetDemo;
    UnityEngine.UI.Button[] difficultyButtons,practiceButtons,measurementButtons,visibilityButtons;
    bool testing,lastSecondary,lastPrimary,showDuringTrial;
    int page,demoSeed=217;
    float nextRefresh;
    string lastLayout;
    readonly string[] names={"Reaction Defense","Trajectory Anticipation","Find the Gap"};
    bool Attack=>session.exerciseSelection==2;
    public static Vector3 Stance(bool attack)=>new Vector3(attack?-.95f:.95f,0,attack?-.23f:-.42f);
    Text Label(Transform parent,string value,int size=25,float height=42)
    {
        var go=new GameObject("Label",typeof(RectTransform));go.transform.SetParent(parent,false);
        var t=go.AddComponent<Text>();BabyFootUITheme.StyleText(t,size,size>=28);t.text=value;
        go.AddComponent<LayoutElement>().preferredHeight=height;return t;
    }
    UnityEngine.UI.Button Button(Transform parent,string value,UnityEngine.Events.UnityAction action,float height=56)
    {
        bool primary=value.StartsWith("Start practice")||value.StartsWith("Try controls")||value.StartsWith("Continue to session")||value.StartsWith("Begin measurement")||value.StartsWith("Prepare next");
        return BabyFootUITheme.Button(parent,value,action,height,primary);
    }
    GameObject Group(string name,Transform parent)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var layout=go.AddComponent<VerticalLayoutGroup>();layout.spacing=7;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;return go;
    }
    UnityEngine.UI.Button[] Choices(Transform parent,string[] labels,System.Action<int> change)
    {
        var go=new GameObject("Options",typeof(RectTransform));go.transform.SetParent(parent,false);
        var row=go.AddComponent<HorizontalLayoutGroup>();row.spacing=8;row.childControlWidth=true;row.childControlHeight=true;row.childForceExpandWidth=true;
        var size=go.AddComponent<LayoutElement>();size.minHeight=46;size.preferredHeight=46;size.flexibleHeight=0;
        var buttons=new UnityEngine.UI.Button[labels.Length];for(int i=0;i<labels.Length;i++){int index=i;buttons[i]=Button(go.transform,labels[i],()=>change(index),46);BabyFootUITheme.Choice(buttons[i]);}return buttons;
    }
    static void Mark(UnityEngine.UI.Button[] buttons,int selected) => BabyFootUITheme.Select(buttons,selected);
    void Start()
    {
        session.exerciseSelection=Mathf.Clamp(session.exerciseSelection,0,2);
        if(session.participantCode=="P001")session.participantCode="VR_"+System.DateTime.UtcNow.ToString("MMdd_HHmmss");
        Build();RefreshView();
    }
    public void Build()
    {
        if(menuCanvas)return;
        font=BabyFootUITheme.Regular;
        var root=new GameObject("VR Training Menu",typeof(RectTransform));root.transform.SetParent(transform,false);root.transform.localScale=Vector3.one*.00105f;
        menuCanvas=root.AddComponent<Canvas>();menuCanvas.renderMode=RenderMode.WorldSpace;menuCanvas.worldCamera=origin.Camera;
        root.AddComponent<TrackedDeviceGraphicRaycaster>();BabyFootUITheme.Panel(root);
        root.GetComponent<RectTransform>().sizeDelta=new Vector2(900,1030);
        var layout=root.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(30,30,20,20);layout.spacing=10;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
        title=BabyFootUITheme.Header(root.transform,"Training centre");
        progression=BabyFootUITheme.Progress(root.transform,0);
        subtitle=Label(root.transform,"Point with your controller. Select with the trigger.",22,38);
        exercises=Group("1 - Choose exercise",root.transform);
        Label(exercises.transform,"Choose your game",30,44);
        BabyFootUITheme.Exercise(exercises.transform,"Reaction Defense","Short shots. Quick saves.","01",0,()=>SelectExercise(0));
        BabyFootUITheme.Exercise(exercises.transform,"Trajectory Anticipation","Read the rebounds. Protect the goal.","02",1,()=>SelectExercise(1));
        BabyFootUITheme.Exercise(exercises.transform,"Find the Gap / Attack","Find your opening. Make the shot.","03",2,()=>SelectExercise(2));
        BabyFootUITheme.Exercise(exercises.transform,"Free Match vs AI","Your blue team. A full match against the AI.","04",3,()=>UnityEngine.SceneManagement.SceneManager.LoadScene("Assets/BabyFoot/Scenes/BabyFoot_FreeMatch_Pavillon.unity"),true);
        controls=Group("2 - Learn the controls",root.transform);
        instruction=Label(controls.transform,"",26,175);
        controlState=Label(controls.transform,"",25,80);
        defenseToggle=Button(controls.transform,"",()=>{control.Release();session.exercise.SetDefenseLine(!session.exercise.twoRodDefense);RefreshView();},46);
        Button(controls.transform,"Try controls - no timer or recording",BeginFreeControls,65);
        resetDemo=Button(controls.transform,"Reset ball / centre bar",ResetDemo);
        Button(controls.transform,"Continue to session setup",OpenSettings,65);
        Button(controls.transform,"Back to exercises",BackToExercises);
        settings=Group("3 - Session settings",root.transform);
        settingsTitle=Label(settings.transform,"",29,45);
        difficultyButtons=Choices(settings.transform,new[]{"Easy","Standard","Fast"},i=>session.difficulty=i);
        practiceButtons=Choices(settings.transform,new[]{"3 practice","5 practice","10 practice"},i=>session.practiceTrials=new[]{3,5,10}[i]);
        measurementButtons=Choices(settings.transform,new[]{"10 measured","20 measured","30 measured"},i=>session.measurementTrials=new[]{10,20,30}[i]);
        visibility=Group("Anticipation visibility",settings.transform);
        visibilityButtons=Choices(visibility.transform,new[]{"Fully visible","Hide final 45%"},i=>session.occlusionEnabled=i==1);
        codeLabel=Label(settings.transform,"",23,38);
        Button(settings.transform,"Start practice session",StartPractice,65);
        Button(settings.transform,"Back to control tutorial",()=>{page=1;RefreshView();});
        running=Group("Session",root.transform);
        feedback=Label(running.transform,"",27,155);
        Label(running.transform,"Grab the handle, then press A / X to prepare a trial.",24,60);
        nextTrial=Button(running.transform,"Prepare next trial (A / X)",NextTrial,65);
        Button(running.transform,"End session",()=>{session.EndSession();control.Release();});
        between=Group("Practice complete",root.transform);
        Label(between.transform,"Practice complete. Ready for measurement?",27,100);
        Button(between.transform,"Begin measurement block",()=>session.BeginMeasurement(),65);
        Button(between.transform,"End session",()=>session.EndSession());
        summary=Group("Results",root.transform);
        summaryLabel=Label(summary.transform,"",27,170);
        Label(summary.transform,"Practice excluded. Software timing.\nDevice and Air Link latency not calibrated.",23,75);
        Button(summary.transform,"New session",()=>{session.ReturnToSetup();page=0;control.Release();},65);
        var spacer=new GameObject("Footer spacing",typeof(RectTransform));spacer.transform.SetParent(root.transform,false);spacer.AddComponent<LayoutElement>().flexibleHeight=1;
        recenter=Button(root.transform,"Recenter at table",Recenter);
        Label(root.transform,"A / X: next trial     |     B / Y: show menu",22,35);
        var hud=new GameObject("VR Trial Status",typeof(RectTransform));hud.transform.SetParent(transform,false);hud.transform.localScale=Vector3.one*.001f;
        var canvas=hud.AddComponent<Canvas>();statusCanvas=canvas;canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=origin.Camera;hud.GetComponent<RectTransform>().sizeDelta=new Vector2(850,150);
        statusText=Label(hud.transform,"",29,140);statusText.rectTransform.anchorMin=Vector2.zero;statusText.rectTransform.anchorMax=Vector2.one;statusText.rectTransform.offsetMin=Vector2.zero;statusText.rectTransform.offsetMax=Vector2.zero;
        BabyFootUITheme.Hud(hud,statusText);
        PositionPanels();
    }
    public void SelectExercise(int selection)
    {
        if(session.IsOpen)return;
        StopFreeControls();session.exerciseSelection=Mathf.Clamp(selection,0,2);session.exercise.ConfigureExercise((BabyFootDefense.ExerciseMode)session.exerciseSelection);
        control.RefreshTarget();page=1;Recenter();RefreshView();
    }
    public void BeginFreeControls()
    {
        if(session.IsOpen)return;
        testing=true;control.FreeControl=true;ResetDemo();RefreshView();
    }
    public void ResetDemo()
    {
        if(!testing)return;
        control.Release();session.exercise.CancelTrial();
        if(Attack)
        {
            session.exercise.attack.unlimitedPractice=true;session.exercise.attack.RequestedStriker=1;
            session.exercise.nextTrajectorySeed=demoSeed++;session.exercise.BeginTrial(.05f);
        }
        control.RefreshTarget();PositionPanels();
    }
    UnityEngine.UI.Button defenseToggle;
    void StopFreeControls()
    {
        control.Release();control.FreeControl=false;
        if(testing)session.exercise.CancelTrial();testing=false;
        if(session.exercise.attack)session.exercise.attack.unlimitedPractice=false;
    }
    public void OpenSettings(){StopFreeControls();page=2;RefreshView();}
    public void BackToExercises(){StopFreeControls();page=0;RefreshView();}
    public void StartPractice(){StopFreeControls();showDuringTrial=false;session.StartSession();RefreshView();}
    public void NextTrial()
    {
        if(testing){if(!session.exercise.IsTrialActive)ResetDemo();return;}
        if(!session.exercise.IsTrialActive){showDuringTrial=false;session.StartNextTrial();}
    }
    public void Recenter()
    {
        if(session.exercise.IsTrialActive&&!testing)return;
        control.Release();var head=origin.Camera.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);
        if(forward.sqrMagnitude>.01f)origin.transform.RotateAround(head.position,Vector3.up,Vector3.SignedAngle(forward,Attack?Vector3.right:Vector3.left,Vector3.up));
        var target=Stance(Attack);target.y=head.position.y;origin.transform.position+=target-head.position;PositionPanels();
    }
    void PositionPanels()
    {
        if(!menuCanvas)return;
        menuCanvas.transform.position=new Vector3(Attack?-.05f:.05f,1.52f,testing?.62f:Attack?-.23f:-.36f);
        menuCanvas.transform.rotation=Quaternion.Euler(0,Attack?90:-90,0);
        if(statusCanvas){var hud=statusCanvas.transform;hud.position=new Vector3(Attack?.25f:-.25f,1.75f,.3f);hud.rotation=menuCanvas.transform.rotation;}
    }
    void Update()
    {
        if(!menuCanvas)return;
        var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool secondary=(left.TryGetFeatureValue(CommonUsages.secondaryButton,out bool l)&&l)||(right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool r)&&r);
        bool primary=(left.TryGetFeatureValue(CommonUsages.primaryButton,out bool a)&&a)||(right.TryGetFeatureValue(CommonUsages.primaryButton,out bool x)&&x);
        if(secondary&&!lastSecondary)showDuringTrial=!showDuringTrial;
        if(primary&&!lastPrimary)NextTrial();lastSecondary=secondary;lastPrimary=primary;
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;RefreshView();
    }
    public void RefreshView()
    {
        if(!menuCanvas)return;
        string stage=session.CurrentStage;bool active=session.exercise.IsTrialActive;
        progression.SetActive(stage=="Setup");BabyFootUITheme.ProgressSelection(progression,page);
        exercises.SetActive(stage=="Setup"&&page==0);controls.SetActive(stage=="Setup"&&page==1);settings.SetActive(stage=="Setup"&&page==2);
        running.SetActive(stage=="Practice"||stage=="Measurement");between.SetActive(stage=="Break");summary.SetActive(stage=="Summary");
        menuCanvas.gameObject.SetActive(testing||!active||showDuringTrial);statusCanvas.gameObject.SetActive(!menuCanvas.gameObject.activeSelf);
        subtitle.text=testing?"CONTROL WARM-UP / Move freely before you start.":"Point with your controller. Select with the trigger.";
        instruction.text=Attack?"2 / LEARN ATTACK CONTROLS\n1. Hold GRIP or TRIGGER near the attacking handle.\n2. Push or pull to align a figure with the ball.\n3. Twist your wrist to move the foot through the ball.":"2 / LEARN GOALKEEPER CONTROLS\n1. Bring a controller to the highlighted handle.\n2. Hold GRIP or TRIGGER to grab the handle.\n3. Push or pull along the rod. Release to let go.";
        controlState.text=testing?control.TrackingStatus+(Attack?"\nA / X: next ball after a shot":"\nThe goalkeeper stays vertical."):"Try freely first. The menu moves aside so you can reach the handle.";
        defenseToggle.gameObject.SetActive(session.exerciseSelection==0);
        defenseToggle.GetComponentInChildren<Text>().text=session.exercise.twoRodDefense?"Defense / two handles":"Defense / keeper only";
        if(session.exercise.UseDefenseLine)instruction.text="TWO-HANDLE DEFENSE\nHold grip or trigger on each controller to grab the goalkeeper and the two-player rod. Slide either bar into the path of the shot.";
        resetDemo.interactable=testing;
        settingsTitle.text="3 / "+names[session.exerciseSelection];
        Mark(difficultyButtons,session.difficulty);Mark(practiceButtons,System.Array.IndexOf(new[]{3,5,10},session.practiceTrials));Mark(measurementButtons,System.Array.IndexOf(new[]{10,20,30},session.measurementTrials));Mark(visibilityButtons,session.occlusionEnabled?1:0);
        visibility.SetActive(session.exerciseSelection==1);codeLabel.text="Anonymous participant: "+session.participantCode;
        string message=active?(Attack?(session.exercise.attack.IsAiming?"Align the figure. Swing forward to shoot.":"Shot in flight"):(session.exercise.StimulusPresented?(session.exercise.BallOccluded?"BALL HIDDEN - predict the interception":"Defend the goal"):"Ready - wait for the shot")):session.Feedback.Replace("SPACE","A / X");
        feedback.text=names[session.exerciseSelection]+"\n"+stage.ToUpper()+" | "+session.CompletedTrials+" completed\n"+message;
        nextTrial.interactable=!active;recenter.interactable=!active||testing;
        summaryLabel.text="SESSION RESULTS\n"+session.SummaryText;
        statusText.text=message+(control.IsHeld?"\nHANDLE HELD":"\nHold grip or trigger near the handle")+(string.IsNullOrEmpty(session.Error)?"":"\n"+session.Error);
        if(!string.IsNullOrEmpty(session.Error))subtitle.text=session.Error;
        PositionPanels();
        string layoutKey=stage+":"+page+":"+testing+":"+session.exerciseSelection;
        if(lastLayout!=layoutKey&&menuCanvas.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuCanvas.GetComponent<RectTransform>());
            lastLayout=layoutKey;
        }
    }
    void OnDisable(){if(testing)StopFreeControls();}
}
