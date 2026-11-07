using System;
using System.Collections.Generic;
using UnityEngine;

// Tabletop (XZ) swept-sphere solver. All queries happen before launch, not every VR frame.
// Candidate paths are accepted only after real collider contacts reach a defendable goal.
public sealed class BabyFootReboundSolver
{
    public struct Contact
    {
        public Vector3 center,normal;
        public Collider collider;
        public float distance;
        public bool wall;
    }
    readonly RaycastHit[] hits=new RaycastHit[128];
    readonly Collider[] overlaps=new Collider[128];
    readonly HashSet<Collider> players=new HashSet<Collider>();
    readonly Transform table;
    readonly float radius,halfWidth,goalZ;
    public readonly List<Vector3> points=new List<Vector3>();
    public readonly List<Contact> contacts=new List<Contact>();
    public int PlayerCount,WallCount;
    public float Length;
    public BabyFootReboundSolver(Transform table,IEnumerable<Collider> players,float radius,float halfWidth,float goalZ)
    {
        this.table=table;this.radius=radius;this.halfWidth=halfWidth;this.goalZ=goalZ;
        foreach(var c in players) this.players.Add(c);
    }
    public bool Trace(Vector3 start,Vector3 direction,int wantedSide,int minimumPlayers)
    {
        points.Clear(); contacts.Clear(); PlayerCount=WallCount=0; Length=0;
        points.Add(start); var position=start; direction.y=0; direction.Normalize();
        const float epsilon=.00015f;
        Collider previous=null;
        for(int bounce=0;bounce<=14;bounce++)
        {
            int overlapping=Physics.OverlapSphereNonAlloc(table.TransformPoint(position),radius-.00005f,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(overlapping==overlaps.Length) return false;
            for(int i=0;i<overlapping;i++) if(players.Contains(overlaps[i])) return false;
            float goal=direction.z<-.0001f?(goalZ-position.z)/direction.z:float.PositiveInfinity;
            float far=direction.z>.0001f?(start.z+.005f-position.z)/direction.z:float.PositiveInfinity;
            float wall=Mathf.Abs(direction.x)>.0001f?((direction.x>0?halfWidth-radius:-halfWidth+radius)-position.x)/direction.x:float.PositiveInfinity;
            float limit=Mathf.Min(goal,far,wall,2.8f-Length);
            if(limit<=0 || float.IsInfinity(limit)) return false;
            float nearest=limit; Collider hitCollider=null; Vector3 normal=Vector3.zero;
            int count=Physics.SphereCastNonAlloc(table.TransformPoint(position),radius,table.TransformDirection(direction),hits,limit,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length) return false; // never silently lose a possible nearest collision
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(!players.Contains(hit.collider) || (hit.collider==previous && hit.distance<epsilon*3)) continue;
                if(hit.distance<nearest && hit.distance>0)
                {
                    var n=table.InverseTransformDirection(hit.normal); n.y=0;
                    if(n.sqrMagnitude<.01f) continue;
                    nearest=hit.distance; hitCollider=hit.collider; normal=n.normalized;
                }
            }
            var point=position+direction*nearest;
            Length+=nearest;
            if(hitCollider)
            {
                if(bounce==14 || Vector3.Dot(direction,normal)>-.001f) return false;
                points.Add(point); PlayerCount++;
                contacts.Add(new Contact{center=point,normal=normal,collider=hitCollider,distance=Length});
                direction=Vector3.Reflect(direction,normal).normalized;
                previous=hitCollider;
            }
            else if(goal<=limit+.00001f)
            {
                if(Mathf.Abs(point.x)<.055f || Mathf.Abs(point.x)>.082f || Mathf.Sign(point.x)!=wantedSide || PlayerCount<minimumPlayers) return false;
                points.Add(point); return true;
            }
            else if(wall<=limit+.00001f)
            {
                if(bounce==14) return false;
                normal=direction.x>0?Vector3.left:Vector3.right;
                points.Add(point); WallCount++;
                contacts.Add(new Contact{center=point,normal=normal,distance=Length,wall=true});
                direction=Vector3.Reflect(direction,normal); previous=null;
            }
            else return false; // opposite end or excessive length; do not turn the ball invisibly
            // Tiny separation prevents repeated zero-distance contacts. Keep arc-length
            // coordinates exact by adding this travelled distance to the current segment.
            position=point+direction*epsilon; Length+=epsilon;
        }
        return false;
    }
}
