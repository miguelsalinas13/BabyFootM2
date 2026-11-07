using UnityEngine;

// Derive lateral stops from the actual figure envelope, independent of figure count.
public static class BabyFootRodTravel
{
    public static Vector2 Configure(Transform rod,float halfWidth)
    {
        float min=float.PositiveInfinity,max=float.NegativeInfinity;
        var corners=new Vector3[8];
        foreach(Transform figure in rod)if(figure.name.StartsWith("Player "))
        {
            foreach(var renderer in figure.GetComponentsInChildren<Renderer>(true))if(renderer.enabled&&renderer.gameObject.activeInHierarchy)
            {
                var bounds=renderer.bounds;
                for(int n=0;n<8;n++)
                {
                    corners[n]=bounds.center+Vector3.Scale(bounds.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));
                    float x=rod.InverseTransformPoint(corners[n]).x;min=Mathf.Min(min,x);max=Mathf.Max(max,x);
                }
            }
            // Collider proxies also have to stay inside the playing area.
            foreach(var box in figure.GetComponentsInChildren<BoxCollider>(true))
                for(int n=0;n<8;n++)
                {
                    var vertex=box.center+Vector3.Scale(box.size*.5f,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));
                    float x=rod.InverseTransformPoint(box.transform.TransformPoint(vertex)).x;min=Mathf.Min(min,x);max=Mathf.Max(max,x);
                }
        }
        if(float.IsInfinity(min))return new Vector2(-.09f,.09f);
        const float wallClearance=.001f,bumperPadding=.006f;
        var range=new Vector2(-halfWidth+wallClearance-min+bumperPadding,halfWidth-wallClearance-max-bumperPadding);
        // Short rubber stops now sit beside the outer figures instead of fixed +/-31cm.
        foreach(Transform part in rod)if(part.name=="Bumper")
        {
            var p=part.localPosition;p.x=p.x<0?min-.0035f:max+.0035f;part.localPosition=p;
            var s=part.localScale;s.y=.0025f;part.localScale=s;
        }
        float side=rod.Find("Handle Visual").localPosition.x>=0?1:-1;
        float handleDistance=Mathf.Max(.59f,.37f+.075f+.003f+(side>0?-range.x:range.y));
        foreach(Transform part in rod)if(part.name=="Handle Visual"||part.name=="Grip")
        {var p=part.localPosition;p.x=side*handleDistance;part.localPosition=p;}
        // Preserve support through both cabinet bearings and cover the handle-side shaft.
        float shaftMin=Mathf.Min(-.66f,-.39f-range.y),shaftMax=Mathf.Max(.66f,.39f-range.x);
        if(side>0)shaftMax=Mathf.Max(shaftMax,handleDistance+.065f);else shaftMin=Mathf.Min(shaftMin,-handleDistance-.065f);
        var shaft=rod.Find("Shaft 16 mm");
        if(shaft){var p=shaft.localPosition;p.x=(shaftMin+shaftMax)*.5f;shaft.localPosition=p;var s=shaft.localScale;s.y=(shaftMax-shaftMin)*.5f;shaft.localScale=s;}
        return range;
    }
}
