using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Tiny depth/color response only while changing state; no materials, lights or shader effects.
public class BabyFootUIButtonMotion : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
{
    Button button;Image face;Text label;Shadow shadow;Outline outline;RectTransform rect;
    bool primary,selected,hover,pressed;int previous=-1;float phase=1;
    Color from,to;float startZ,targetZ;
    public bool Selected=>selected;
    public void Configure(Button b,Image f,Text t,Shadow s,Outline o,bool main)
    {button=b;face=f;label=t;shadow=s;outline=o;primary=main;rect=(RectTransform)transform;Refresh(true);}
    public void SetSelected(bool value){if(selected==value)return;selected=value;Refresh(false);}
    public void OnPointerEnter(PointerEventData data){hover=true;Refresh(false);}
    public void OnPointerExit(PointerEventData data){hover=false;pressed=false;Refresh(false);}
    public void OnPointerDown(PointerEventData data){pressed=true;Refresh(false);}
    public void OnPointerUp(PointerEventData data){pressed=false;Refresh(false);}
    void Refresh(bool immediate)
    {
        if(!button)return;
        int state=!button.interactable?0:pressed?4:hover?3:selected?2:1;
        if(state==previous&&!immediate)return;previous=state;
        bool warm=primary||selected;
        from=face.color;to=state==0?BabyFootUITheme.Hex("E2DCE1"):state==4?BabyFootUITheme.Hex("8EBAD0"):state==3?(warm?BabyFootUITheme.Hex("78CBE8"):BabyFootUITheme.Hex("E2F1F6")):warm?BabyFootUITheme.Orange:Color.white;
        label.color=state==0?BabyFootUITheme.Hex("8C828F"):BabyFootUITheme.Plum;
        outline.effectColor=state==3||selected?BabyFootUITheme.Orange:BabyFootUITheme.Line;
        shadow.effectColor=new Color(.27f,.12f,.26f,state==0?.06f:.2f);
        startZ=rect.localPosition.z;targetZ=state==3?-4:state==4?1:0;phase=immediate?1:0;
        if(immediate){face.color=to;var p=rect.localPosition;p.z=targetZ;rect.localPosition=p;}
    }
    void Update()
    {
        if(!button)return;Refresh(false);if(phase>=1)return;
        phase=Mathf.Min(1,phase+Time.unscaledDeltaTime/ .09f);float ease=phase*phase*(3-2*phase);face.color=Color.Lerp(from,to,ease);var p=rect.localPosition;p.z=Mathf.Lerp(startZ,targetZ,ease);rect.localPosition=p;
    }
    void OnDisable(){hover=pressed=false;previous=-1;}
    void OnEnable(){if(button)Refresh(true);}
}
