using UnityEngine;

// Planar approximation of foot/field friction. Never reparents or teleports the ball.
// Independent of a match/session so passing drills can use the same contact rules.
public sealed class BabyFootBallControl
{
    public int Rod { get; private set; } = -1;
    public string Figure { get; private set; } = "";
    public float StableSeconds { get; private set; }
    public bool Secured => Rod >= 0 && StableSeconds >= .18f;
    public bool SidePush {get;private set;}
    Vector3 anchor;
    float lockout;

    public void Release(float delay = .12f)
    { Rod = -1; Figure = ""; StableSeconds = 0; lockout = delay; }

    public bool TryReceive(int index, Transform rod, BabyFootFootContact feet,
        Vector3 position, Vector3 velocity, Vector3 rodVelocity, float angularSpeed, bool eligible)
    {
        if (Rod >= 0 || lockout > 0 || !eligible || Mathf.Abs(angularSpeed) > 160 || rodVelocity.magnitude > .65f)
            return false;
        if ((velocity - rodVelocity).magnitude > 2.1f || !feet.Supports(rod.parent.TransformPoint(position), .0185f, out string figure))
            return false;
        Rod = index; Figure = figure; StableSeconds = 0;
        anchor = Quaternion.Inverse(rod.localRotation) * (position - rod.localPosition);
        return true;
    }

    public Vector3 Step(float dt, Transform rod, BabyFootFootContact feet, Vector3 position,
        Vector3 velocity, Vector3 rodVelocity, float angularSpeed, bool eligible)
    {
        SidePush=false;
        if (Rod < 0) { lockout = Mathf.Max(0, lockout - dt); return velocity; }
        Vector3 target = rod.localPosition + rod.localRotation * anchor;
        target.y = position.y;
        // A quick sideways push on an already supported ball rolls it along the rod.
        if (eligible && Mathf.Abs(rodVelocity.x)>.65f && Mathf.Abs(angularSpeed)<100 &&
            feet.Sweep(rod.parent.TransformPoint(position),.0215f,BabyFootFootContact.Angle(rod),BabyFootFootContact.Angle(rod),out _,out string touching) && touching==Figure)
        {
            SidePush=true;Release(.055f);
            return new Vector3(Mathf.Clamp(rodVelocity.x*.80f,-1.3f,1.3f),0,velocity.z);
        }
        if (!eligible || Mathf.Abs(angularSpeed) > 160 || rodVelocity.magnitude > .65f ||
            Vector3.Distance(position, target) > .028f ||
            !feet.Supports(rod.parent.TransformPoint(position), .0215f, out string supportingFigure) || supportingFigure != Figure)
        { Release(); return velocity; }
        Vector3 desired = rodVelocity + (target - position) * 18f;
        desired = Vector3.ClampMagnitude(desired, .75f); desired.y = 0;
        velocity = Vector3.MoveTowards(velocity, desired, 6f * dt);
        StableSeconds = (velocity - rodVelocity).magnitude < .15f ? StableSeconds + dt : 0;
        return velocity;
    }
}
