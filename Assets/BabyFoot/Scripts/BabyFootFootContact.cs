using System.Collections.Generic;
using UnityEngine;

// Cached oriented foot boxes, evaluated across the complete angular sweep.
// No per-frame allocation or repeated Physics.SyncTransforms is needed for striking.
public sealed class BabyFootFootContact
{
    struct Foot { public Vector3 centre,half;public Quaternion rotation;public string name; }
    readonly Transform rod;
    readonly Foot[] feet;
    public BabyFootFootContact(Transform target)
    {
        rod=target;var list=new List<Foot>();
        foreach(var box in target.GetComponentsInChildren<BoxCollider>(true))if(box.name=="Foot")
            list.Add(new Foot{centre=target.InverseTransformPoint(box.transform.TransformPoint(box.center)),half=Vector3.Scale(box.size*.5f,box.transform.lossyScale),rotation=Quaternion.Inverse(target.rotation)*box.transform.rotation,name=box.transform.parent.name});
        feet=list.ToArray();
    }
    public bool Sweep(Vector3 ballWorld,float radius,float from,float to,out float offset,out string name)
    {
        offset=0;name="";int steps=Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(to-from)/2f),1,90);
        for(int step=0;step<=steps;step++)
        {
            var turn=Quaternion.AngleAxis(Mathf.Lerp(from,to,step/(float)steps),Vector3.right);
            foreach(var foot in feet)
            {
                Vector3 centre=rod.parent.TransformPoint(rod.localPosition+turn*foot.centre);
                var rotation=rod.parent.rotation*turn*foot.rotation;
                var local=Quaternion.Inverse(rotation)*(ballWorld-centre);
                var closest=new Vector3(Mathf.Clamp(local.x,-foot.half.x,foot.half.x),Mathf.Clamp(local.y,-foot.half.y,foot.half.y),Mathf.Clamp(local.z,-foot.half.z,foot.half.z));
                if((local-closest).sqrMagnitude<=radius*radius)
                {offset=rod.parent.InverseTransformDirection(ballWorld-centre).x;name=foot.name;return true;}
            }
        }
        return false;
    }
    // A pin requires an inclined foot physically touching the sphere from above.
    // No targeting radius or attraction toward a nearby figure is used.
    public bool Supports(Vector3 ballWorld,float radius,out string name)
    {
        name="";
        float angle=Mathf.Abs(Angle(rod));
        if(angle<14||angle>65)return false;
        foreach(var foot in feet)
        {
            Vector3 centre=rod.TransformPoint(foot.centre);
            if(centre.y-ballWorld.y<.004f)continue;
            var local=Quaternion.Inverse(rod.rotation*foot.rotation)*(ballWorld-centre);
            var closest=new Vector3(Mathf.Clamp(local.x,-foot.half.x,foot.half.x),Mathf.Clamp(local.y,-foot.half.y,foot.half.y),Mathf.Clamp(local.z,-foot.half.z,foot.half.z));
            if(Mathf.Abs(local.x)<=foot.half.x+.010f&&(local-closest).sqrMagnitude<=radius*radius)
            {name=foot.name;return true;}
        }
        return false;
    }
    // Euler decomposition flips Y/Z at a half turn; extract the actual shaft twist.
    public static float Angle(Transform rod)
    {
        var rotation=rod.localRotation;
        return Mathf.DeltaAngle(0,2*Mathf.Atan2(rotation.x,rotation.w)*Mathf.Rad2Deg);
    }
    public static Vector3 Direction(float offset,bool negativeZ=true)
    {
        float edge=Mathf.Clamp01((Mathf.Abs(offset)-.008f)/.021f);
        float angle=Mathf.Sign(offset)*edge*edge*55*Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angle),0,(negativeZ?-1:1)*Mathf.Cos(angle));
    }
}
