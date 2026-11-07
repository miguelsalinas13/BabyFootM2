using System;
using System.Collections.Generic;
using UnityEngine;

// Controlled constant-speed paths with verified wall or static player-foot reflection.
public class BabyFootTrajectory : MonoBehaviour
{
    public BabyFootDefense exercise;
    public BabyFootPresentation presentation;
    public float ballRadius=.0175f, innerHalfWidth=.34f;
    Transform[] rods;
    Vector3[] positions;
    Quaternion[] rotations;
    bool[] active;
    Transform reactionAttacker, distantAttacker, sourcePlayer;
    Vector3 reactionSpawn;
    Camera desktopCamera;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float cameraFov;
    bool configured;
    Vector3[] points;
    public string RouteKind { get; private set; }="direct";
    public int WallSide { get; private set; }
    public float Length { get; private set; }
    public float BounceDistance { get; private set; }=-1;
    public Vector3 StartLocal { get; private set; }
    public bool BounceObserved { get; private set; }
    public string DeflectorName { get; private set; }="";
    public Vector3 DeflectorLocal { get; private set; }
    public Vector3 ContactNormal { get; private set; }
    public float DeflectorRodOffset { get; private set; }
    public Collider Deflector { get; private set; }
    public int PlayerBounceCount { get; private set; }
    public int WallBounceCount { get; private set; }
    public int ObservedContacts { get; private set; }
    public int TrialSeed { get; private set; }
    public float ActualTargetX => points==null?0:points[points.Length-1].x;
    public double GenerationMilliseconds { get; private set; }
    public string ContactTrace { get; private set; }="";
    public BabyFootReboundSolver.Contact[] Contacts { get; private set; }=Array.Empty<BabyFootReboundSolver.Contact>();
    Vector3 nominalStart;
    public string Error { get; private set; }="";
    public int VisibleRodCount { get { Cache(); int n=0; foreach(var r in rods) if(r.gameObject.activeSelf) n++; return n; } }
    void Cache()
    {
        if(rods!=null) return;
        var parent=exercise.goalkeeper.parent; var list=new List<Transform>();
        foreach(Transform r in parent) list.Add(r);
        list.Sort((a,b)=>a.localPosition.z.CompareTo(b.localPosition.z));
        rods=list.ToArray(); positions=new Vector3[rods.Length]; rotations=new Quaternion[rods.Length]; active=new bool[rods.Length];
        for(int i=0;i<rods.Length;i++) { positions[i]=rods[i].localPosition; rotations[i]=rods[i].localRotation; active[i]=rods[i].gameObject.activeSelf; }
        reactionAttacker=presentation.attackingRod;
        reactionSpawn=exercise.shotSpawn?exercise.shotSpawn.localPosition:exercise.ball.localPosition;
        foreach(var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if(cam.isActiveAndEnabled)
            {
                bool tracked=false;
                foreach(var component in cam.GetComponents<MonoBehaviour>())
                    if(component && component.GetType().Name.Contains("TrackedPoseDriver")) tracked=true;
                if(!tracked && !UnityEngine.XR.XRSettings.isDeviceActive) { desktopCamera=cam; cameraPosition=cam.transform.position; cameraRotation=cam.transform.rotation; cameraFov=cam.fieldOfView; break; }
            }
        distantAttacker=rods[rods.Length-2];
        foreach(Transform child in distantAttacker)
            if(child.name.StartsWith("Player ") && (sourcePlayer==null || child.localPosition.x<sourcePlayer.localPosition.x)) sourcePlayer=child;
    }
    public void Configure(bool longShot)
    {
        Cache();
        presentation.SetAttackingRod(null);
        for(int i=0;i<rods.Length;i++)
        {
            rods[i].localPosition=positions[i]; rods[i].localRotation=rotations[i]; rods[i].gameObject.SetActive(longShot || active[i]);
            if(longShot && rods[i]!=exercise.goalkeeper && rods[i]!=distantAttacker && i<rods.Length-1)
                rods[i].localRotation=rotations[i]*Quaternion.AngleAxis(75,Vector3.right);
        }
        configured=longShot;
        presentation.SetAttackingRod(longShot?distantAttacker:reactionAttacker);
        if(longShot)
        {
            var local=exercise.ball.parent.InverseTransformPoint(sourcePlayer.position);
            local.y=reactionSpawn.y; local.z-=.032f; StartLocal=local;
        }
        else StartLocal=reactionSpawn;
        nominalStart=StartLocal;
        BounceObserved=false;
        if(desktopCamera && !UnityEngine.XR.XRSettings.isDeviceActive)
        {
            if(longShot)
            {
                desktopCamera.transform.position=exercise.ball.parent.TransformPoint(new Vector3(1.05f,1.85f,-.35f));
                desktopCamera.transform.LookAt(exercise.ball.parent.TransformPoint(new Vector3(0,.77f,0))); desktopCamera.fieldOfView=55;
            }
            else { desktopCamera.transform.position=cameraPosition; desktopCamera.transform.rotation=cameraRotation; desktopCamera.fieldOfView=cameraFov; }
        }
        Physics.SyncTransforms();
    }
    public bool Prepare(float target,bool bank,int wallSide,bool playerBounce=false,int seed=0)
    {
        if(!configured) Configure(true);
        if(playerBounce) return PrepareRandom(target,seed);
        TrialSeed=seed; GenerationMilliseconds=0; PlayerBounceCount=WallBounceCount=ObservedContacts=0; Contacts=Array.Empty<BabyFootReboundSolver.Contact>(); ContactTrace="";
        distantAttacker.localPosition=positions[rods.Length-2]; StartLocal=nominalStart;
        // Restore the same open lanes before constructing each route, including retries.
        for(int i=1;i<rods.Length-2;i++)
        {
            rods[i].localPosition=positions[i];
            rods[i].localRotation=rotations[i]*Quaternion.AngleAxis(75,Vector3.right);
        }
        Deflector=null; DeflectorName=""; DeflectorLocal=ContactNormal=Vector3.zero; DeflectorRodOffset=0;
        Error=""; RouteKind=bank?"one_wall":"direct"; WallSide=bank?(wallSide<0?-1:1):0;
        BounceObserved=false; BounceDistance=-1;
        Vector3 end=new Vector3(target,StartLocal.y,exercise.goalkeeper.localPosition.z);
        if(bank)
        {
            float wall=WallSide*(innerHalfWidth-ballRadius);
            float mirrored=2*wall-target;
            float t=(wall-StartLocal.x)/(mirrored-StartLocal.x);
            if(t<=0 || t>=1) { Error="Invalid reflection geometry."; return false; }
            var bounce=new Vector3(wall,StartLocal.y,Mathf.Lerp(StartLocal.z,end.z,t));
            points=new[]{StartLocal,bounce,end}; BounceDistance=Vector3.Distance(StartLocal,bounce);
        }
        else points=new[]{StartLocal,end};
        Physics.SyncTransforms();
        Length=0; for(int i=1;i<points.Length;i++) Length+=Vector3.Distance(points[i-1],points[i]);
        // Check the complete swept sphere against every intermediate player collider.
        foreach(var rod in rods)
        {
            if(rod==exercise.goalkeeper || rod==distantAttacker || !rod.gameObject.activeSelf) continue;
            foreach(var collider in rod.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled || collider.isTrigger || collider==Deflector) continue;
                for(int i=1;i<points.Length;i++)
                {
                    var a=exercise.ball.parent.TransformPoint(points[i-1]); var b=exercise.ball.parent.TransformPoint(points[i]);
                    var expanded=collider.bounds; expanded.Expand(ballRadius*2);
                    if(expanded.Contains(a) || (expanded.IntersectRay(new Ray(a,(b-a).normalized),out float distance) && distance<Vector3.Distance(a,b)))
                    { Error="Trajectory blocked by "+rod.name+" / "+collider.name; return false; }
                }
            }
        }
        WallBounceCount=bank?1:0;
        return true;
    }
    bool PrepareRandom(float target,int seed)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var random=new System.Random(seed); TrialSeed=seed;
        Error=""; RouteKind="random_players"; WallSide=0; BounceObserved=false; ObservedContacts=0;
        Deflector=null; DeflectorName=""; DeflectorLocal=ContactNormal=Vector3.zero; DeflectorRodOffset=0;
        PlayerBounceCount=WallBounceCount=0; ContactTrace=""; Contacts=Array.Empty<BabyFootReboundSolver.Contact>();
        var colliders=new List<Collider>();
        for(int i=1;i<rods.Length-2;i++) foreach(var c in rods[i].GetComponentsInChildren<Collider>())
            if(c.enabled && !c.isTrigger) colliders.Add(c);
        var solver=new BabyFootReboundSolver(exercise.ball.parent,colliders,ballRadius,innerHalfWidth,exercise.goalkeeper.localPosition.z);
        int required=(seed&3)==0?2:1;
        for(int layout=0;layout<16;layout++)
        {
            for(int i=1;i<rods.Length-2;i++)
            {
                // Random poses are independent of requested left/right target. No bar moves in flight.
                float slide=(float)(random.NextDouble()*2-1)*(i==1?.075f:.035f);
                float angle=(float)(random.NextDouble()*44-22);
                rods[i].localPosition=positions[i]+Vector3.right*slide;
                rods[i].localRotation=rotations[i]*Quaternion.AngleAxis(angle,Vector3.right);
            }
            distantAttacker.localPosition=positions[rods.Length-2]+Vector3.right*(float)(random.NextDouble()*.12-.06);
            var source=sourcePlayer;
            if(random.Next(2)==1) foreach(Transform child in distantAttacker)
                if(child.name.StartsWith("Player ") && child.localPosition.x>source.localPosition.x) source=child;
            var start=exercise.ball.parent.InverseTransformPoint(source.position);
            start.y=nominalStart.y; start.z-=.032f; StartLocal=start;
            Physics.SyncTransforms();
            for(int attempt=0;attempt<160;attempt++)
            {
                float angle=(float)(random.NextDouble()*140-70)*Mathf.Deg2Rad;
                var direction=new Vector3(Mathf.Sin(angle),0,-Mathf.Cos(angle));
                if(!solver.Trace(StartLocal,direction,target<0?-1:1,required)) continue;
                points=solver.points.ToArray(); Contacts=solver.contacts.ToArray();
                PlayerBounceCount=solver.PlayerCount; WallBounceCount=solver.WallCount;
                Length=0; for(int i=1;i<points.Length;i++) Length+=Vector3.Distance(points[i-1],points[i]);
                BounceDistance=Contacts.Length>0?Contacts[0].distance:-1;
                var trace=new System.Text.StringBuilder();
                foreach(var hit in Contacts)
                {
                    if(hit.collider && !Deflector)
                    {
                        Deflector=hit.collider; DeflectorName=hit.collider.transform.parent.name+" / "+hit.collider.name;
                        DeflectorLocal=exercise.ball.parent.InverseTransformPoint(hit.collider.bounds.center); ContactNormal=hit.normal;
                        var rod=hit.collider.transform; while(rod.parent!=exercise.goalkeeper.parent) rod=rod.parent;
                        DeflectorRodOffset=rod.localPosition.x;
                    }
                    if(trace.Length>0) trace.Append('|');
                    trace.Append(hit.wall?"wall":hit.collider.transform.parent.name+"/"+hit.collider.name);
                    trace.Append(':').Append(hit.distance.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
                    trace.Append(':').Append(hit.center.x.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
                    trace.Append(':').Append(hit.center.z.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
                }
                ContactTrace=trace.ToString(); GenerationMilliseconds=timer.Elapsed.TotalMilliseconds;
                return true;
            }
        }
        GenerationMilliseconds=timer.Elapsed.TotalMilliseconds;
        Error="No defendable random path found. Please try the next trial again."; return false;
    }
    public Vector3 Evaluate(float distance)
    {
        if(points==null) return StartLocal;
        float remaining=distance;
        for(int i=1;i<points.Length;i++)
        {
            float length=Vector3.Distance(points[i-1],points[i]);
            if(remaining<=length) return Vector3.Lerp(points[i-1],points[i],remaining/length);
            remaining-=length;
        }
        return points[points.Length-1]+(points[points.Length-1]-points[points.Length-2]).normalized*remaining;
    }
    public void Observe(float distance)
    {
        if(BounceDistance>=0 && distance>=BounceDistance) BounceObserved=true;
        ObservedContacts=Contacts.Length==0 && BounceObserved?1:0; foreach(var c in Contacts) if(distance>=c.distance) ObservedContacts++;
    }
    public Vector3 AtZ(float z)
    {
        for(int i=1;i<points.Length;i++)
            if((z>=Mathf.Min(points[i-1].z,points[i].z) && z<=Mathf.Max(points[i-1].z,points[i].z)) || i==points.Length-1)
                return Vector3.LerpUnclamped(points[i-1],points[i],(z-points[i-1].z)/(points[i].z-points[i-1].z));
        return points[points.Length-1];
    }
    public void ResetObservation() { BounceObserved=false; ObservedContacts=0; }
    public Vector3[] CopyPoints() => points==null?Array.Empty<Vector3>():(Vector3[])points.Clone();
}
