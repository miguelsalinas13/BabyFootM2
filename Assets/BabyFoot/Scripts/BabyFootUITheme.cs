using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Tournament interface colours.
public static class BabyFootUITheme
{
    public static readonly Color Plum=Hex("123659"),Orange=Hex("9BD9EF"),Ink=Hex("122C41"),Cream=Hex("F1F4F4"),Muted=Hex("4F6778"),Line=Hex("B9CDD8"),Dark=Hex("102C46");
    static Font regular,semibold;static Sprite rounded;
    public static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}
    public static Font Regular=>regular?regular:regular=Resources.Load<Font>("BabyFootUI/SourceSans3-Regular")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    public static Font Semibold=>semibold?semibold:semibold=Resources.Load<Font>("BabyFootUI/SourceSans3-Semibold")??Regular;
    public static Sprite Rounded
    {
        get
        {
            if(rounded)return rounded;
            const int size=96;const float radius=14;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.name="BabyFoot Rounded Surface";texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(radius-x-.5f,x+.5f-(size-radius)),dy=Mathf.Max(radius-y-.5f,y+.5f-(size-radius));
                float distance=new Vector2(Mathf.Max(dx,0),Mathf.Max(dy,0)).magnitude-radius;
                float alpha=Mathf.Clamp01(.5f-distance);byte grey=(byte)Mathf.Lerp(246,255,y/(float)(size-1));
                pixels[y*size+x]=new Color32(grey,grey,grey,(byte)(alpha*255));
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            rounded=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));rounded.name="BabyFoot Rounded Surface";return rounded;
        }
    }
    public static void StyleText(Text text,int size,bool heading=false)
    {
        text.font=heading?Semibold:Regular;text.fontSize=size;text.color=heading?Plum:Ink;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
    }
    public static Image Surface(GameObject go,Color colour)
    {
        var image=go.GetComponent<Image>();if(!image)image=go.AddComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=colour;return image;
    }
    static RectTransform Decoration(Transform parent,string name,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;
        go.AddComponent<LayoutElement>().ignoreLayout=true;return rect;
    }
    public static void Panel(GameObject root,bool hud=false)
    {
        Surface(root,hud?Dark:Cream);
        var shadow=root.AddComponent<Shadow>();shadow.effectColor=new Color(.08f,.025f,.10f,.38f);shadow.effectDistance=new Vector2(10,-12);shadow.useGraphicAlpha=true;
        var outline=root.AddComponent<Outline>();outline.effectColor=Plum;outline.effectDistance=new Vector2(2,-2);
        var strip=Decoration(root.transform,"Orange edge",new Vector2(0,1),Vector2.one,new Vector2(18,-4),new Vector2(-18,0));strip.gameObject.AddComponent<Image>().color=Orange;strip.GetComponent<Image>().raycastTarget=false;
    }
    static Text FreeText(Transform parent,string name,string content,int size,Color colour,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
    {
        var rect=Decoration(parent,name,min,max,low,high);var text=rect.gameObject.AddComponent<Text>();StyleText(text,size);text.color=colour;text.text=content;return text;
    }
    public static Text Header(Transform parent,string section,float height=126)
    {
        var go=new GameObject("BabyFoot identity",typeof(RectTransform));go.transform.SetParent(parent,false);go.AddComponent<LayoutElement>().preferredHeight=height;
        var rect=go.GetComponent<RectTransform>();Surface(go,Plum).raycastTarget=false;
        FreeText(go.transform,"Campus","TABLE FOOTBALL",17,Hex("C6DFE9"),Vector2.zero,Vector2.one,new Vector2(22,height-32),new Vector2(-140,-7));
        var wordmark=FreeText(go.transform,"BabyFoot wordmark","BABYFOOT",height>=120?58:46,Color.white,Vector2.zero,Vector2.one,new Vector2(20,22),new Vector2(-140,-24));wordmark.font=Semibold;
        FreeText(go.transform,"Section",section.ToUpperInvariant(),18,Orange,Vector2.zero,Vector2.one,new Vector2(23,4),new Vector2(-140,-(height-30)));
        var iconRect=Decoration(go.transform,"Angular court mark",Vector2.zero,Vector2.one,new Vector2(-124,25),new Vector2(-20,-25));iconRect.anchorMin=new Vector2(1,0);
        var mark=iconRect.gameObject.AddComponent<BabyFootUIIcon>();mark.kind=4;mark.color=Orange;
        return wordmark;
    }
    public static GameObject Progress(Transform parent,int active)
    {
        var go=new GameObject("Training progression",typeof(RectTransform));go.transform.SetParent(parent,false);var size=go.AddComponent<LayoutElement>();size.minHeight=32;size.preferredHeight=32;size.flexibleHeight=0;
        var row=go.AddComponent<HorizontalLayoutGroup>();row.spacing=10;row.childControlWidth=true;row.childControlHeight=true;row.childForceExpandWidth=true;row.childForceExpandHeight=false;
        string[] words={"01  SELECT","02  CONTROLS","03  PLAY"};
        for(int i=0;i<3;i++)
        {
            var part=new GameObject(words[i],typeof(RectTransform));part.transform.SetParent(go.transform,false);part.AddComponent<LayoutElement>().flexibleWidth=1;
            var t=part.AddComponent<Text>();StyleText(t,19,true);t.text=words[i];t.color=i==active?Plum:Muted;
            var bar=Decoration(part.transform,"Step line",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,3));bar.gameObject.AddComponent<Image>().color=i==active?Orange:Line;bar.GetComponent<Image>().raycastTarget=false;
        }
        return go;
    }
    public static void ProgressSelection(GameObject progression,int active)
    {for(int i=0;i<progression.transform.childCount;i++){var part=progression.transform.GetChild(i);part.GetComponent<Text>().color=i==active?Plum:Muted;part.GetComponentInChildren<Image>().color=i==active?Orange:Line;}}
    public static Button Button(Transform parent,string value,UnityAction action,float height=58,bool primary=false)
    {
        var go=new GameObject(value,typeof(RectTransform));go.transform.SetParent(parent,false);go.AddComponent<LayoutElement>().preferredHeight=height;
        var image=Surface(go,primary?Orange:Color.white);var shadow=go.AddComponent<Shadow>();shadow.effectColor=new Color(.27f,.12f,.26f,.2f);shadow.effectDistance=new Vector2(0,-4);
        var outline=go.AddComponent<Outline>();outline.effectColor=primary?Orange:Line;outline.effectDistance=new Vector2(1,-1);
        var button=go.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.onClick.AddListener(action);
        var label=FreeText(go.transform,"Button label",value,25,Plum,Vector2.zero,Vector2.one,new Vector2(18,4),new Vector2(-46,-4));label.font=Semibold;
        var arrow=Decoration(go.transform,"Forward mark",new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-35,-10),new Vector2(-15,10));var icon=arrow.gameObject.AddComponent<BabyFootUIIcon>();icon.kind=5;icon.color=Plum;
        var effect=go.AddComponent<BabyFootUIButtonMotion>();effect.Configure(button,image,label,shadow,outline,primary);
        return button;
    }
    public static Button Exercise(Transform parent,string title,string description,string number,int iconKind,UnityAction action,bool primary=false)
    {
        var button=Button(parent,title,action,108,primary);var rect=button.GetComponent<RectTransform>();
        var label=button.GetComponentInChildren<Text>();label.rectTransform.offsetMin=new Vector2(94,36);label.rectTransform.offsetMax=new Vector2(-58,-14);label.fontSize=29;
        var tag=FreeText(rect,"Exercise number",number,31,primary?Plum:Plum,Vector2.zero,Vector2.one,new Vector2(20,48),new Vector2(-700,-12));tag.font=Semibold;
        var badge=Decoration(rect,"Exercise symbol",Vector2.zero,new Vector2(0,0),new Vector2(23,14),new Vector2(65,45));var graphic=badge.gameObject.AddComponent<BabyFootUIIcon>();graphic.kind=iconKind;graphic.color=Plum;
        var line=Decoration(rect,"Lane divider",Vector2.zero,new Vector2(0,1),new Vector2(78,16),new Vector2(81,-16));line.gameObject.AddComponent<Image>().color=primary?Plum:Orange;line.GetComponent<Image>().raycastTarget=false;
        FreeText(rect,"Exercise description",description,22,primary?Ink:Muted,Vector2.zero,Vector2.one,new Vector2(94,9),new Vector2(-55,-58));
        return button;
    }
    public static void Choice(Button button)
    {
        var label=button.GetComponentInChildren<Text>();label.alignment=TextAnchor.MiddleCenter;label.fontSize=22;label.rectTransform.offsetMin=new Vector2(5,3);label.rectTransform.offsetMax=new Vector2(-5,-3);
        var arrow=button.transform.Find("Forward mark");if(arrow)arrow.gameObject.SetActive(false);
    }
    public static void Select(Button[] buttons,int selected)
    {for(int i=0;i<buttons.Length;i++)buttons[i].GetComponent<BabyFootUIButtonMotion>().SetSelected(i==selected);}
    public static void Hud(GameObject root,Text text)
    {Panel(root,true);text.font=Semibold;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;var outline=text.GetComponent<Outline>();if(outline)Object.Destroy(outline);}
}
