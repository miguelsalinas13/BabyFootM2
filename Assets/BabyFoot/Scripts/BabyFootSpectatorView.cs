using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;

// A fixed monitor camera is used only without an active headset. Never moves the VR head.
public class BabyFootSpectatorView:MonoBehaviour
{
    public BabyFootFreeMatchUI ui;
    public BabyFootFreeMatch match;
    public XROrigin origin;
    public Camera MonitorCamera {get;private set;}
    public int ViewIndex {get;private set;}=2;
    bool drivingMonitor,previousCameraEnabled;
    GUIStyle title,label,button,panel;
    static readonly string[] Views={"Side view","Blue end","Overview"};
    public void NextView(){ViewIndex=(ViewIndex+1)%3;PositionCamera();}
    void PositionCamera()
    {
        if(!MonitorCamera)return;
        Vector3 position=ViewIndex==0?new Vector3(1.08f,1.58f,0):ViewIndex==1?new Vector3(.25f,1.5f,-1.1f):new Vector3(.72f,2.0f,-.65f);
        MonitorCamera.transform.position=position;MonitorCamera.transform.LookAt(new Vector3(0,.79f,0));
    }
    void Update()
    {
        bool wanted=match.Spectator&&!XRSettings.isDeviceActive;
        if(wanted&&!MonitorCamera)
        {
            var go=new GameObject("Spectator Monitor Camera");go.transform.SetParent(transform,false);
            MonitorCamera=go.AddComponent<Camera>();MonitorCamera.CopyFrom(origin.Camera);
            MonitorCamera.GetUniversalAdditionalCameraData().renderPostProcessing=origin.Camera.GetUniversalAdditionalCameraData().renderPostProcessing;
            MonitorCamera.stereoTargetEye=StereoTargetEyeMask.None;MonitorCamera.fieldOfView=65;MonitorCamera.depth=origin.Camera.depth+1;PositionCamera();
        }
        if(wanted!=drivingMonitor)
        {
            if(wanted){previousCameraEnabled=origin.Camera.enabled;origin.Camera.enabled=false;}
            else origin.Camera.enabled=previousCameraEnabled;
            drivingMonitor=wanted;
        }
        if(MonitorCamera)MonitorCamera.enabled=wanted;
    }
    void OnDisable(){if(drivingMonitor&&origin&&origin.Camera)origin.Camera.enabled=previousCameraEnabled;if(MonitorCamera)MonitorCamera.enabled=false;drivingMonitor=false;}
    void Styles()
    {
        if(title!=null)return;
        title=new GUIStyle(GUI.skin.label){font=BabyFootUITheme.Semibold,fontSize=24};title.normal.textColor=new Color(1,.68f,.15f);
        label=new GUIStyle(GUI.skin.label){font=BabyFootUITheme.Regular,fontSize=17,wordWrap=true};
        button=new GUIStyle(GUI.skin.button){font=BabyFootUITheme.Semibold,fontSize=17};
        button.normal.background=button.hover.background=button.active.background=BabyFootUITheme.Rounded.texture;
        button.border=new RectOffset(16,16,16,16);button.normal.textColor=BabyFootUITheme.Plum;button.hover.textColor=BabyFootUITheme.Ink;button.active.textColor=BabyFootUITheme.Orange;
        panel=new GUIStyle(GUI.skin.box){border=new RectOffset(16,16,16,16)};panel.normal.background=BabyFootUITheme.Rounded.texture;
    }
    void OnGUI()
    {
        if(XRSettings.isDeviceActive||!ui.menu)return;
        Styles();
        Color oldBackground=GUI.backgroundColor;GUI.backgroundColor=BabyFootUITheme.Cream;
        if(!match.Spectator)
        {
            if(ui.menu.gameObject.activeSelf&&GUI.Button(new Rect(Screen.width-240,18,220,44),"Watch AI vs AI",button))ui.WatchMatch();
            GUI.backgroundColor=oldBackground;return;
        }
        Color old=GUI.color;GUI.color=BabyFootUITheme.Dark;GUI.Box(new Rect(16,16,530,150),GUIContent.none,panel);GUI.color=old;
        GUI.Label(new Rect(30,22,500,32),$"DEMO   /   BLUE AI  {match.BlueScore} : {match.CoralScore}  RED AI",title);
        GUI.Label(new Rect(30,62,500,30),"Blue: "+match.AI.BlueAction,label);
        GUI.Label(new Rect(30,92,500,30),"Red: "+match.AI.CoralAction,label);
        GUI.Label(new Rect(30,126,500,28),match.Paused?"Paused":match.Message,label);
        float y=Screen.height-64;
        if(GUI.Button(new Rect(18,y,125,44),match.Paused?"Resume":"Pause",button))ui.TogglePause();
        if(GUI.Button(new Rect(151,y,150,44),"New match",button)){match.StartMatch();ui.menu.gameObject.SetActive(false);}
        if(GUI.Button(new Rect(309,y,150,44),Views[ViewIndex],button))NextView();
        if(GUI.Button(new Rect(467,y,150,44),"Play as Blue",button))ui.PlayAsBlue();
        GUI.backgroundColor=oldBackground;
    }
}
