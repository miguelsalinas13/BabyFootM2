using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class BabyFootEyeTrackingUI : MonoBehaviour
{
    public BabyFootEyeTrackingProbe probe;
    public Canvas canvas;
    public Text status,details;
    public Button startButton,stopButton,alignButton;
    Text debugLabel;
    float nextRefresh;
    Font font;
    Text Label(Transform parent,string value,int size,float height)
    {
        var go=new GameObject("Label",typeof(RectTransform));go.transform.SetParent(parent,false);
        var text=go.AddComponent<Text>();BabyFootUITheme.StyleText(text,size,size>=28);text.text=value;
        go.AddComponent<LayoutElement>().preferredHeight=height;return text;
    }
    Button MakeButton(Transform parent,string value,UnityEngine.Events.UnityAction action,out Text label)
    {
        var button=BabyFootUITheme.Button(parent,value,action,58,value=="Start 9-point test");
        label=button.GetComponentInChildren<Text>();return button;
    }
    void Start(){Build();}
    public void Build()
    {
        if(canvas)return;
        font=BabyFootUITheme.Regular;
        var root=new GameObject("Eye Test Controls",typeof(RectTransform));root.transform.SetParent(probe.board,false);root.transform.localPosition=new Vector3(0,-1f,-.07f);root.transform.localScale=Vector3.one*.0015f;
        canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=probe.origin.Camera;
        root.AddComponent<TrackedDeviceGraphicRaycaster>();BabyFootUITheme.Panel(root);root.GetComponent<RectTransform>().sizeDelta=new Vector2(1220,530);
        var layout=root.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(24,24,16,16);layout.spacing=8;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
        BabyFootUITheme.Header(root.transform,"Gaze validation",108);
        Label(root.transform,"Point with your controller. Select with the trigger.",22,32);
        details=Label(root.transform,"",24,100);
        var row=new GameObject("Controls",typeof(RectTransform));row.transform.SetParent(root.transform,false);var horizontal=row.AddComponent<HorizontalLayoutGroup>();horizontal.spacing=10;horizontal.childControlWidth=true;horizontal.childControlHeight=true;row.AddComponent<LayoutElement>().preferredHeight=58;
        startButton=MakeButton(row.transform,"Start 9-point test",()=>probe.StartValidation(),out _);
        stopButton=MakeButton(row.transform,"Stop recording",()=>probe.StopValidation(),out _);
        var second=new GameObject("Display controls",typeof(RectTransform));second.transform.SetParent(root.transform,false);var line=second.AddComponent<HorizontalLayoutGroup>();line.spacing=10;line.childControlWidth=true;line.childControlHeight=true;second.AddComponent<LayoutElement>().preferredHeight=58;
        MakeButton(second.transform,"Debug marker: OFF",()=>probe.showDebugGaze=!probe.showDebugGaze,out debugLabel);
        alignButton=MakeButton(second.transform,"Align board / recenter",probe.AlignBoard,out _);
        Label(root.transform,"Complete Quest eye calibration first. This test checks accuracy; it does not calibrate.\nLook at each amber dot for 2.5 seconds. Missing data stay invalid; no head-gaze fallback.",23,70);
        var header=new GameObject("Eye Data Status",typeof(RectTransform));header.transform.SetParent(probe.board,false);header.transform.localPosition=new Vector3(0,.89f,-.03f);header.transform.localScale=Vector3.one*.0015f;
        var hud=header.AddComponent<Canvas>();hud.renderMode=RenderMode.WorldSpace;hud.worldCamera=probe.origin.Camera;header.GetComponent<RectTransform>().sizeDelta=new Vector2(1650,230);
        status=Label(header.transform,"EYE TRACKING / DIAGNOSTIC",32,230);status.rectTransform.anchorMin=Vector2.zero;status.rectTransform.anchorMax=Vector2.one;status.rectTransform.offsetMin=Vector2.zero;status.rectTransform.offsetMax=Vector2.zero;
        BabyFootUITheme.Hud(header,status);
        Refresh();
    }
    void Update(){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.1f;Refresh();}}
    public void Refresh()
    {
        if(!canvas)return;
        status.text="BABYFOOT / EYE TRACKING TEST\n"+probe.Status+"\n"+(probe.IsRecording?$"LOOK AT TARGET {probe.ActiveTarget+1} / {probe.targets.Length}":"Waiting for validation - no recording");
        status.color=probe.EyeValid?BabyFootUITheme.Hex("97D0D6"):BabyFootUITheme.Orange;
        details.text=probe.IsRecording?$"Recording: {probe.RecordedRows} frame observations | {probe.ValidRows} valid\nFollow the amber target. Debug gaze is hidden during recording.":probe.LastResult+"\n"+probe.ControllerStatus;
        startButton.interactable=probe.EyeValid&&!probe.IsRecording;stopButton.interactable=probe.IsRecording;alignButton.interactable=probe.HeadTracked&&!probe.IsRecording;
        debugLabel.text="Debug marker: "+(probe.showDebugGaze?"ON":"OFF")+(probe.IsRecording?" (hidden)":"");
    }
}
