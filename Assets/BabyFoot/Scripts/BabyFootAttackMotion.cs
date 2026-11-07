using System;
using UnityEngine;

// Reactive defenders: delayed observations of visible ball motion, finite speed and
// acceleration. Never reads the shot command, striker alignment or future outcome.
public sealed class BabyFootAttackMotion
{
    public struct Pose { public float line,keeper; }
    struct Observation { public double ready;public float line,keeper; }
    readonly Observation[] observations=new Observation[64];
    int first,count;
    double now,nextObservation;
    float lineVelocity,keeperVelocity;
    readonly float patrolPhase,patrolFrequency,keeperPatrolAmplitude;
    public Pose Current { get; private set; }
    public float TargetLine { get; private set; }
    public float TargetKeeper { get; private set; }
    public float MaxSpeed { get; private set; }
    public float MaxAcceleration { get; private set; }
    public float ReactionDelay { get; private set; }
    public float OpportunitySeconds => 0; // legacy scheduled-window field, no longer applicable
    public string Profile => "active_cover_sweep_and_delayed_interception";
    public double Time => now;
    public BabyFootAttackMotion(int seed,int difficulty)
    {
        int d=Mathf.Clamp(difficulty,0,2);
        MaxSpeed=new[]{.23f,.34f,.46f}[d];MaxAcceleration=new[]{.8f,1.3f,1.9f}[d];ReactionDelay=new[]{.34f,.24f,.16f}[d];
        var random=new System.Random(seed);
        patrolPhase=(float)random.NextDouble()*Mathf.PI*2;
        patrolFrequency=new[]{.35f,.50f,.65f}[d];keeperPatrolAmplitude=new[]{.038f,.045f,.05f}[d];
        Current=new Pose{line=(float)(random.NextDouble()*.14-.07),keeper=(float)(random.NextDouble()*.14-.07)};
        TargetLine=Current.line;TargetKeeper=Current.keeper;
    }
    public static float FoldX(float x,float halfWidth=.3225f)
    {
        float width=halfWidth*2;
        float t=Mathf.Repeat(x+halfWidth,width*2);
        return t<=width?t-halfWidth:width*2-t-halfWidth;
    }
    static float Intercept(Vector3 ball,Vector3 velocity,float z,bool flying)
    {
        if(flying && velocity.z<-.0001f && ball.z>z)
            return FoldX(ball.x+velocity.x*((z-ball.z)/velocity.z));
        // Before a visible shot, cover the cone between the ball and the goal;
        // do not peek at which edge of a striker the user is about to use.
        float fraction=Mathf.Clamp01((ball.z-z)/Mathf.Max(.001f,ball.z+.5565f));
        return Mathf.Lerp(ball.x,0,fraction);
    }
    void Observe(Vector3 ball,Vector3 velocity,bool flying)
    {
        float threat=Intercept(ball,velocity,-.375f,flying);
        // While waiting, actively cover adjacent lanes rather than parking on a
        // single intercept. On visible flight, delayed observations switch to pursuit.
        if(!flying)threat+=.065f*Mathf.Sin((float)now*patrolFrequency*Mathf.PI*2+patrolPhase);
        float best=float.MaxValue,slide=0;
        foreach(float playerX in new[]{-.1175f,.1175f})
        {
            float x=Mathf.Clamp(threat-playerX,-.09f,.09f);
            float cost=Mathf.Abs(x+playerX-threat)+.08f*Mathf.Abs(x-Current.line);
            if(cost<best){best=cost;slide=x;}
        }
        float keeper=Mathf.Clamp(Intercept(ball,velocity,-.5565f,flying),-.09f,.09f);
        if(!flying)keeper=Mathf.Clamp(keeper+keeperPatrolAmplitude*Mathf.Sin((float)now*patrolFrequency*.83f*Mathf.PI*2+patrolPhase+1.1f),-.09f,.09f);
        if(count==observations.Length){first=(first+1)%observations.Length;count--;}
        observations[(first+count)%observations.Length]=new Observation{ready=now+ReactionDelay,line=slide,keeper=keeper};count++;
    }
    public Pose AdvanceTo(double elapsed,Vector3 ball,Vector3 visibleVelocity,bool flying)
    {
        // Calls are monotonic; analysis queries must never fast-forward live defenders.
        if(elapsed<=now)return Current;
        while(now<elapsed)
        {
            float dt=(float)Math.Min(1.0/240,elapsed-now);
            if(now>=nextObservation){Observe(ball,visibleVelocity,flying);nextObservation=now+.04;}
            while(count>0 && observations[first].ready<=now)
            {
                TargetLine=observations[first].line;TargetKeeper=observations[first].keeper;
                first=(first+1)%observations.Length;count--;
            }
            var pose=Current;
            pose.line=Move(pose.line,TargetLine,ref lineVelocity,dt);
            pose.keeper=Move(pose.keeper,TargetKeeper,ref keeperVelocity,dt);
            Current=pose;now+=dt;
        }
        return Current;
    }
    float Move(float position,float target,ref float velocity,float dt)
    {
        float error=target-position;
        float desired=Mathf.Sign(error)*Mathf.Min(MaxSpeed,Mathf.Sqrt(2*MaxAcceleration*Mathf.Abs(error)));
        velocity=Mathf.MoveTowards(velocity,desired,MaxAcceleration*dt);
        float next=position+velocity*dt;
        if(Mathf.Abs(error)<.0001f && Mathf.Abs(velocity)<MaxAcceleration*dt){next=target;velocity=0;}
        if(next<-.09f || next>.09f){next=Mathf.Clamp(next,-.09f,.09f);velocity=0;}
        return next;
    }
}
