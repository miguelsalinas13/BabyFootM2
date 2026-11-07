using UnityEngine;
using UnityEngine.UI;

// Small original court/rod pictograms, drawn directly as UI quads.
[RequireComponent(typeof(CanvasRenderer))]
public class BabyFootUIIcon : MaskableGraphic
{
    public int kind;
    protected override void Awake(){base.Awake();raycastTarget=false;}
    void Line(VertexHelper h,Vector2 a,Vector2 b,float width)
    {
        var r=rectTransform.rect;a=new Vector2(r.xMin+a.x*r.width,r.yMin+a.y*r.height);b=new Vector2(r.xMin+b.x*r.width,r.yMin+b.y*r.height);var n=(b-a).normalized;var side=new Vector2(-n.y,n.x)*width*.5f;
        int i=h.currentVertCount;h.AddVert(a+side,color,Vector2.zero);h.AddVert(b+side,color,Vector2.zero);h.AddVert(b-side,color,Vector2.zero);h.AddVert(a-side,color,Vector2.zero);h.AddTriangle(i,i+1,i+2);h.AddTriangle(i,i+2,i+3);
    }
    protected override void OnPopulateMesh(VertexHelper h)
    {
        h.Clear();
        if(kind==5){Line(h,new Vector2(.2f,.15f),new Vector2(.75f,.5f),2.4f);Line(h,new Vector2(.75f,.5f),new Vector2(.2f,.85f),2.4f);return;}
        if(kind==4){for(int i=0;i<3;i++)Line(h,new Vector2(.06f+i*.28f,.1f),new Vector2(.28f+i*.28f,.9f),12);return;}
        if(kind==0){Line(h,new Vector2(.18f,.85f),new Vector2(.82f,.85f),2);Line(h,new Vector2(.18f,.85f),new Vector2(.18f,.4f),2);Line(h,new Vector2(.82f,.85f),new Vector2(.82f,.4f),2);Line(h,new Vector2(.18f,.4f),new Vector2(.5f,.1f),2);Line(h,new Vector2(.5f,.1f),new Vector2(.82f,.4f),2);}
        else if(kind==1){Line(h,new Vector2(.1f,.15f),new Vector2(.6f,.8f),2.5f);Line(h,new Vector2(.6f,.8f),new Vector2(.9f,.25f),2.5f);Line(h,new Vector2(.1f,.92f),new Vector2(.9f,.92f),2);}
        else
        {
            Line(h,new Vector2(.05f,.65f),new Vector2(.95f,.65f),2);
            for(int i=0;i<3;i++)Line(h,new Vector2(.22f+i*.28f,.2f),new Vector2(.22f+i*.28f,.92f),5);
            if(kind==3)Line(h,new Vector2(.05f,.1f),new Vector2(.95f,.1f),2);
        }
    }
}
