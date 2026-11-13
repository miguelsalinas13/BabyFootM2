using UnityEngine;

// Both teams use this controller, including the opponent in human free play.
// Plans only drive bounded rod poses. All shots still require the foot sweep.
public sealed class BabyFootMatchAI
{
    readonly BabyFootFreeMatch match;
    readonly float[][] figures;
    readonly float[] target, phaseAge, holdAge, recovery, nextThink, aimOffset,carryTarget;
    readonly int[] phase;
    readonly bool[] striking,feint;
    readonly float[] followEnd,followSpeed;
    float nextFeint;
    bool skipPass;
    public int SkipAttempts {get;private set;}
    public int SkipCompletions {get;private set;}
    readonly float[] pinStart,pinFeint;
    public bool IsStriking(int rod)=>striking[rod];
    readonly Plan[] plans;
    int previousOwner=-1, lastPassRod=-1, lastPassReceiver=-1;
    float passDeadline;
    bool bankPending;
    float progressZ,zoneTime;
    int outletRod=-1;
    float outletX;
    bool NeedsOutlet=>zoneTime>3.2f;
    int lateralRod=-1,lastLateralRod=-1;
    string lateralSender;
    float lateralDirection,lateralAngle,lateralUntil,nextLateral,lateralReceiverX;
    public int LateralAttempts {get;private set;}
    public int LateralCompletions {get;private set;}
    public int BlueStrikes {get;private set;}
    public int CoralStrikes {get;private set;}
    public int Receptions {get;private set;}
    public int CompletedPasses {get;private set;}
    public int Feints {get;private set;}
    public int BankAttempts {get;private set;}
    public int ConfirmedBanks {get;private set;}
    public string BlueAction {get;private set;}="Covering lanes";
    public string CoralAction {get;private set;}="Covering lanes";
    public struct Plan
    {
        public int Receiver;
        public float TargetX, TargetZ, Speed, Offset;
        public bool Bank;
        public string Label;
    }
    public BabyFootMatchAI(BabyFootFreeMatch owner)
    {
        match=owner;int n=match.rods.Length;
        feint=new bool[n];followEnd=new float[n];followSpeed=new float[n];striking=new bool[n];pinStart=new float[n];pinFeint=new float[n];
        figures=new float[n][];target=new float[n];phaseAge=new float[n];holdAge=new float[n];
        recovery=new float[n];nextThink=new float[n];aimOffset=new float[n];carryTarget=new float[n];phase=new int[n];plans=new Plan[n];
        for(int i=0;i<n;i++)
        {
            var list=new System.Collections.Generic.List<float>();
            foreach(Transform t in match.rods[i])if(t.name.StartsWith("Player "))list.Add(t.localPosition.x);
            figures[i]=list.ToArray();target[i]=match.rods[i].localPosition.x;
        }
    }
    public void Reset(bool statistics=false)
    {
        lateralRod=lastLateralRod=-1;nextLateral=0;
        if(statistics)nextFeint=match.MatchTime;
        if(statistics)SkipAttempts=SkipCompletions=LateralAttempts=LateralCompletions=0;
        zoneTime=0;outletRod=-1;progressZ=match.ball.localPosition.z;
        previousOwner=lastPassRod=lastPassReceiver=-1;passDeadline=0;bankPending=false;
        for(int i=0;i<phase.Length;i++){followSpeed[i]=0;striking[i]=false;phase[i]=0;holdAge[i]=phaseAge[i]=recovery[i]=nextThink[i]=aimOffset[i]=0;target[i]=carryTarget[i]=match.rods[i].localPosition.x;plans[i]=default;}
        if(statistics){BlueStrikes=CoralStrikes=Receptions=CompletedPasses=Feints=BankAttempts=ConfirmedBanks=0;}
        BlueAction=CoralAction="Covering lanes";
    }
    static float Forward(int i)=>BabyFootFreeMatch.IsBlue(i)?1:-1;
    static int Receiver(int i)=>i==0?1:i==7?6:i==1?3:i==3?5:i==6?4:i==4?2:-1;
    float Align(int i,float x,float offset=0)
    {
        Vector2 range=match.TravelRange(i);float best=match.rods[i].localPosition.x,error=float.MaxValue;
        foreach(float figure in figures[i])
        {
            float pose=Mathf.Clamp(x-offset-figure,range.x,range.y);
            float miss=Mathf.Abs(x-offset-figure-pose)+Mathf.Abs(pose-match.rods[i].localPosition.x)*.08f;
            if(miss<error){error=miss;best=pose;}
        }
        return best;
    }
    float Clearance(int shooter,float x,float z,float endX,float endZ,bool bank)
    {
        float min=.12f;
        for(int i=0;i<match.rods.Length;i++)if(BabyFootFreeMatch.IsBlue(i)!=BabyFootFreeMatch.IsBlue(shooter))
        {
            float t=(match.rods[i].localPosition.z-z)/(endZ-z);
            if(t<=0||t>1.0001f)continue; // Include the goalkeeper on the target goal plane.
            float pathX=Mathf.Lerp(x,endX,t);
            if(bank)pathX=BabyFootAttackMotion.FoldX(pathX);
            foreach(float figure in figures[i])min=Mathf.Min(min,Mathf.Abs(pathX-match.rods[i].localPosition.x-figure)-.029f);
        }
        return min;
    }
    public Plan ChoosePlan(int i,float x)
    {
        int receiver=NeedsOutlet?-1:Receiver(i);float z=match.ball.localPosition.z;
        float endZ=receiver<0?Forward(i)*BabyFootFreeMatch.GoalZ:match.rods[receiver].localPosition.z;
        Plan best=new Plan{Receiver=receiver,TargetX=x,TargetZ=endZ,Speed=receiver<0?2.8f:1.15f,Label=receiver<0?"Shooting a gap":"Passing forward"};
        float score=float.NegativeInfinity;
        for(int sample=0;sample<9;sample+=match.difficulty==0?2:1)
        {
            float destination=receiver<0?Mathf.Lerp(-.075f,.075f,sample/8f):Mathf.Lerp(-.27f,.27f,sample/8f);
            if(receiver>=0&&Mathf.Abs(Align(receiver,destination)+NearestFigure(receiver,destination)-destination)>.025f)continue;
            for(int route=0;route<(match.difficulty==0?1:3);route++)
            {
                bool bank=route!=0;
                float virtualX=route==1?2*(BabyFootFreeMatch.HalfWidth-BabyFootFreeMatch.Radius)-destination:route==2?-2*(BabyFootFreeMatch.HalfWidth-BabyFootFreeMatch.Radius)-destination:destination;
                float angle=Mathf.Atan2(virtualX-x,Mathf.Abs(endZ-z))*Mathf.Rad2Deg;
                if(Mathf.Abs(angle)>48)continue;
                float merit=Clearance(i,x,z,virtualX,endZ,bank)-Mathf.Abs(angle)*.00035f-(bank?.007f:0);
                if(receiver>=0)merit-=Mathf.Abs(Align(receiver,destination)-match.rods[receiver].localPosition.x)*.04f;
                if(merit<=score)continue;score=merit;
                best.TargetX=destination;best.Bank=bank;
                best.Offset=Mathf.Abs(angle)<2?0:Mathf.Sign(angle)*(.008f+.021f*Mathf.Sqrt(Mathf.Abs(angle)/55f));
                best.Label=bank?(receiver<0?"Bank shot":"Wall pass"):receiver<0?"Shooting a gap":"Passing to "+(receiver==3||receiver==4?"midfield":"attack");
            }
        }
        return best;
    }
    float NearestFigure(int i,float x)
    {
        float slide=Align(i,x),best=0,error=float.MaxValue;
        foreach(float figure in figures[i])if(Mathf.Abs(slide+figure-x)<error){error=Mathf.Abs(slide+figure-x);best=figure;}
        return best;
    }
    float InterceptX(float travel,int level)
    {
        float x=match.ball.localPosition.x;
        float projected=x+match.Velocity.x*travel*(level==0?.55f:1f);
        float wall=BabyFootFreeMatch.HalfWidth-BabyFootFreeMatch.Radius;
        if(level<2 || Mathf.Abs(projected)<=wall)return Mathf.Clamp(projected,-wall,wall);
        float edge=Mathf.Sign(projected)*wall;
        return Mathf.Clamp(edge-(projected-edge)*.88f,-wall,wall);
    }
    void Action(int i,string value){if(BabyFootFreeMatch.IsBlue(i))BlueAction=value;else CoralAction=value;}
    float OpenCarryLane(int i)
    {
        if(Receiver(i)<0&&Mathf.Abs(match.ball.localPosition.x)>.07f)
            return Mathf.Clamp(match.rods[i].localPosition.x+Mathf.Clamp(match.ball.localPosition.x,-.055f,.055f)-match.ball.localPosition.x,match.TravelRange(i).x,match.TravelRange(i).y);
        float best=match.rods[i].localPosition.x,bestScore=float.NegativeInfinity;
        float endZ=Receiver(i)<0?Forward(i)*BabyFootFreeMatch.GoalZ:match.rods[Receiver(i)].localPosition.z;
        for(int sample=-3;sample<=3;sample++)
        {
            float slide=Mathf.Clamp(match.rods[i].localPosition.x+sample*.025f,match.TravelRange(i).x,match.TravelRange(i).y);
            float x=match.ball.localPosition.x+slide-match.rods[i].localPosition.x;
            if(Mathf.Abs(x)>.29f)continue;
            float score=Clearance(i,x,match.ball.localPosition.z,x,endZ,false)-Mathf.Abs(slide-match.rods[i].localPosition.x)*.02f;
            if(Receiver(i)<0)score-=Mathf.Max(0,Mathf.Abs(x)-.075f)*2;
            if(score>bestScore){bestScore=score;best=slide;}
        }
        return best;
    }
    public void OnStrike(int i,float speed,float endAngle)
    {
        if(!match.IsAutomatedRod(i))return;
        if(BabyFootFreeMatch.IsBlue(i))BlueStrikes++;else CoralStrikes++;
        lastLateralRod=-1;var p=plans[i];if(p.Label==null)p=ChoosePlan(i,match.ball.localPosition.x);
        if(Mathf.Abs(speed)>1100)
        {
            float rotation=Mathf.Abs(speed)>1500?360:Mathf.Repeat(Mathf.Sign(speed)*(180-endAngle),360);
            followEnd[i]=endAngle+Mathf.Sign(speed)*rotation;
            followSpeed[i]=Mathf.Abs(speed);
        }
        if(p.Bank)BankAttempts++;
        bankPending=p.Bank;
        lastPassRod=i;lastPassReceiver=p.Receiver;passDeadline=match.MatchTime+2;
        Action(i,p.Label);phase[i]=0;recovery[i]=.12f;holdAge[i]=0;
    }
    public void OnWallContact(){if(bankPending){ConfirmedBanks++;bankPending=false;}}
    public void OnPlayerContact(int index){if(index!=lastPassRod)bankPending=false;}
    bool BeginLateral(int i)
    {
        var rod=match.rods[i];
        float x=match.ball.localPosition.x;
        float best=float.PositiveInfinity;
        skipPass=false;
        foreach(float figure in figures[i])
        {
            float distance=rod.localPosition.x+figure-x;
            float direction=Mathf.Sign(distance);
            float slide=rod.localPosition.x+direction*.02f;
            var range=match.TravelRange(i);
            if(Mathf.Abs(distance)<.055f || slide<range.x || slide>range.y || Mathf.Abs(figure+slide)>.304f)continue;
            bool skips=false;
            foreach(float other in figures[i])
            {
                float along=(rod.localPosition.x+other-x)*direction;
                if(along>.045f && along<Mathf.Abs(distance)-.04f){skips=true;break;}
            }
            if(skipPass && !skips || skips==skipPass && Mathf.Abs(distance)>=best)continue;
            skipPass=skips;
            best=Mathf.Abs(distance);lateralDirection=direction;lateralReceiverX=figure;
        }
        if(float.IsInfinity(best))return false;
        lastLateralRod=lateralRod=i;lateralSender=match.BallControl.Figure;lateralAngle=match.RodAngle(i);
        lateralUntil=match.MatchTime+1;nextLateral=match.MatchTime+1.25f;
        LateralAttempts++;if(skipPass)SkipAttempts++;return true;
    }
    float KeeperLane(int i,float dz)
    {
        float ballX=match.ball.localPosition.x;
        float targetX=ballX*.25f;
        if(dz*match.Velocity.z<0 && Mathf.Abs(match.Velocity.z)>.1f)
        {
            float time=Mathf.Abs(dz/match.Velocity.z);
            if(time<.65f)targetX=InterceptX(time,Mathf.Clamp(match.difficulty,0,3));
        }
        return Mathf.Clamp(targetX,-BabyFootFreeMatch.GoalHalf,BabyFootFreeMatch.GoalHalf);
    }
    public void Step(float dt)
    {
        if(!match.aiEnabled)return;
        int owner=match.ControlledRod;
        var ball=match.ball.localPosition;
        if(Mathf.Abs(ball.z-progressZ)>.12f)
        {progressZ=ball.z;zoneTime=0;outletRod=-1;}
        else zoneTime+=dt;
        if(NeedsOutlet && owner<0)
        {
            int nearest=-1;
            float distance=.085f;
            for(int r=0;r<match.rods.Length;r++)
            {
                float gap=Mathf.Abs(match.rods[r].localPosition.z-ball.z);
                if(gap<distance && match.IsAutomatedRod(r)){nearest=r;distance=gap;}
            }
            if(nearest!=outletRod && (outletRod<0 || Mathf.Abs(match.rods[outletRod].localPosition.z-ball.z)>.082f))
            {
                outletRod=nearest;
                if(nearest>=0)outletX=Align(nearest,BabyFootAttackMotion.FoldX(ball.x+match.Velocity.x*.16f));
            }
        }
        if(lateralRod>=0 && (match.MatchTime>lateralUntil || (owner>=0 && owner!=lateralRod)))lateralRod=-1;
        if(owner==lateralRod && owner>=0 && match.BallControl.Figure!=lateralSender && match.PossessionSecured &&
            Mathf.Abs(match.ball.localPosition.x-match.rods[owner].localPosition.x-lateralReceiverX)<.035f)
        {LateralCompletions++;if(skipPass)SkipCompletions++;lateralRod=-1;holdAge[owner]=.36f;}
        if(owner!=previousOwner)
        {
            if(owner>=0&&match.IsAutomatedRod(owner))
            {
                if(owner!=lastLateralRod)lastLateralRod=-1;
                Receptions++;holdAge[owner]=0;phase[owner]=0;
                feint[owner]=!BabyFootFreeMatch.IsKeeper(owner) && match.MatchTime>=nextFeint &&
                    Clearance(owner,match.ball.localPosition.x,match.ball.localPosition.z,
                        match.ball.localPosition.x,Forward(owner)*BabyFootFreeMatch.GoalZ,false)<.012f;
                if(feint[owner])nextFeint=match.MatchTime+15;
                pinStart[owner]=match.ball.localPosition.x;
                float slide=match.rods[owner].localPosition.x;
                var travel=match.TravelRange(owner);
                float sign=slide-travel.x>travel.y-slide?-1:1;
                carryTarget[owner]=Mathf.Clamp(slide+sign*.024f,travel.x,travel.y);
                pinFeint[owner]=Mathf.Clamp(slide-sign*.013f,travel.x,travel.y);
                if(lastPassRod>=0&&BabyFootFreeMatch.IsBlue(owner)!=BabyFootFreeMatch.IsBlue(lastPassRod))lastPassRod=lastPassReceiver=-1;
                plans[owner]=ChoosePlan(owner,match.ball.localPosition.x);
            }
            previousOwner=owner;
        }
        if(owner>=0&&match.PossessionSecured&&owner==lastPassReceiver&&lastPassRod>=0&&match.MatchTime<passDeadline)
        {CompletedPasses++;lastPassRod=lastPassReceiver=-1;}
        int level=Mathf.Clamp(match.difficulty,0,3);
        float observation=level==0?.20f:level==1?.12f:level==2?.08f:.045f;
        for(int i=0;i<match.rods.Length;i++)if(match.IsAutomatedRod(i))
        {
            striking[i]=false;
            var rod=match.rods[i];float angle=match.RodAngle(i);
            if(followSpeed[i]>0)
            {
                float next=Mathf.MoveTowards(angle,followEnd[i],followSpeed[i]*dt);
                match.SetRodPose(i,rod.localPosition.x,next);
                if(Mathf.Abs(next-followEnd[i])<.01f)followSpeed[i]=0;
                continue;
            }
            float dz=match.ball.localPosition.z-rod.localPosition.z;
            if(match.MatchTime>=nextThink[i])
            {
                nextThink[i]=match.MatchTime+observation;
                float travel=Mathf.Clamp(Mathf.Abs(dz)/Mathf.Max(.3f,Mathf.Abs(match.Velocity.z)),0,.4f);
                float predicted=InterceptX(travel,level);
                if(BabyFootFreeMatch.IsKeeper(i) && Mathf.Abs(dz)>.13f)
                {
                    float lane=KeeperLane(i,dz);
                    if(Mathf.Abs(lane-rod.localPosition.x)>.012f)target[i]=Align(i,lane);
                    else target[i]=rod.localPosition.x;
                }
                else target[i]=Align(i,predicted,aimOffset[i]);
            }
            float desiredAngle=0,turnSpeed=95;
            bool backLine=i==1||i==6;
            if(backLine && owner!=i && dz*Forward(i)<-.008f && Mathf.Abs(match.ball.localPosition.x)<.13f)
            {
                // Let an outlet from behind the defence cross the row. An angled toe
                // presented to its own goalkeeper can redirect the outlet into goal.
                phase[i]=0;aimOffset[i]=0;
                match.SetRodPose(i,rod.localPosition.x,Mathf.MoveTowardsAngle(angle,Forward(i)*82,110*dt));
                continue;
            }
            if(BabyFootFreeMatch.IsKeeper(i) && owner!=i && Mathf.Abs(dz)>.14f)
            {
                phase[i]=0;aimOffset[i]=0;
                float cover=Mathf.MoveTowards(rod.localPosition.x,target[i],.32f*dt);
                match.SetRodPose(i,cover,Mathf.MoveTowardsAngle(angle,0,150*dt));
                continue;
            }
            if(!NeedsOutlet && lateralRod<0 && lastLateralRod!=i && (Receiver(i)>=0 || Mathf.Abs(match.ball.localPosition.x)>.08f) && owner==i && holdAge[i]>=(feint[i]?.72f:.24f) && match.PossessionSecured && match.MatchTime>=nextLateral && BeginLateral(i))
            {
                match.SetRodPose(i,rod.localPosition.x+lateralDirection*.85f*dt,angle);
                Action(i,skipPass?"Skip pass along the rod":"Side pass to adjacent figure");
                continue;
            }
            if(lateralRod==i)
            {
                float remaining=(rod.localPosition.x+lateralReceiverX-match.ball.localPosition.x)*lateralDirection;
                bool clearMiddle=skipPass && remaining>Mathf.Max(.055f,Mathf.Abs(match.Velocity.x)*.085f);
                float pose=clearMiddle?lateralAngle+Mathf.Sign(Mathf.DeltaAngle(0,lateralAngle))*55:lateralAngle;
                match.SetRodPose(i,rod.localPosition.x,Mathf.MoveTowards(angle,pose,850*dt));
                Action(i,"Receiving lateral pass");continue;
            }
            if(owner==i)
            {
                holdAge[i]+=dt;
                // Carry the real contact sideways to draw a defender before releasing it.
                desiredAngle=angle;
                target[i]=rod.localPosition.x;
                if(holdAge[i]<.25f || (!match.PossessionSecured && phase[i]==0))
                    Action(i,"Pinning the ball");
                else if((feint[i] || plans[i].Receiver>=0) && holdAge[i]<(feint[i]?.51f:.42f))
                {
                    target[i]=carryTarget[i];
                    Action(i,feint[i]?"Drawing the defender":"Moving into the passing lane");
                }
                else if(feint[i] && holdAge[i]<.78f)
                {
                    target[i]=pinFeint[i];
                    if(phase[i]!=4 && Mathf.Abs(match.ball.localPosition.x-pinStart[i])>.012f)
                    {phase[i]=4;Feints++;}
                    Action(i,"Reversing under the foot");
                }
                else if(Receiver(i)<0 && Mathf.Abs(match.ball.localPosition.x)>.079f && holdAge[i]<1.6f)
                {
                    target[i]=OpenCarryLane(i);
                    Action(i,"Pulling into the shooting lane");
                }
                else
                {
                    if(phase[i]!=3){plans[i]=ChoosePlan(i,match.ball.localPosition.x);phase[i]=3;}
                    striking[i]=true;desiredAngle=-Forward(i)*82;turnSpeed=plans[i].Speed*Mathf.Rad2Deg/.0975f;
                    Action(i,plans[i].Label);
                }

            }
            else if(recovery[i]>0){recovery[i]-=dt;phase[i]=0;aimOffset[i]=0;}
            else
            {
                float vz=match.Velocity.z;
                bool approaching=dz*vz<0 && Mathf.Abs(dz)/Mathf.Max(.05f,Mathf.Abs(vz))<.65f;
                bool reachable=Mathf.Abs(dz)<.065f;
                bool keeper=BabyFootFreeMatch.IsKeeper(i);
                bool clearance=Forward(i)*rod.localPosition.z<-.30f;
                bool goalLane=Receiver(i)<0 && Mathf.Abs(match.ball.localPosition.x)<.08f;
                float finishAngle=-Forward(i)*82;
                bool outlet=NeedsOutlet && i==outletRod;
                if(NeedsOutlet && owner<0 && !outlet && Mathf.Abs(dz)<.16f)
                {
                    match.SetRodPose(i,rod.localPosition.x,Mathf.MoveTowardsAngle(angle,Forward(i)*75,100*dt));
                    continue;
                }
                if(outlet)target[i]=outletX;
                bool shotAvailable=owner<0 && reachable && (goalLane||clearance||outlet) &&
                    (goalLane || outlet || match.Velocity.z*Forward(i)<-.12f) &&
                    Mathf.Abs(Mathf.DeltaAngle(angle,finishAngle))>15 &&
                    match.FootInStroke(i,angle,Mathf.MoveTowardsAngle(angle,finishAngle,25));
                if(shotAvailable)
                {
                    plans[i]=ChoosePlan(i,match.ball.localPosition.x);
                    striking[i]=true;desiredAngle=finishAngle;
                    turnSpeed=plans[i].Speed*Mathf.Rad2Deg/.0975f;
                    Action(i,clearance?"Clearing the goal mouth":"Finishing an open ball");
                }
                else if(keeper && owner<0 && reachable && dz*Forward(i)<0)
                {
                    // Behind the keeper, sliding or lowering the toe pushes toward the goal.
                    desiredAngle=angle;target[i]=rod.localPosition.x;
                }
                else if(owner<0 && (approaching || reachable))
                {
                    // Present the sole on the ball side. A loose ball is received before winding up.
                    aimOffset[i]=0;
                    desiredAngle=dz>=0?-32:32;
                    turnSpeed=150;
                    if(reachable && match.Velocity.sqrMagnitude<.0225f)
                        target[i]=Align(i,match.ball.localPosition.x);
                    if(outlet && zoneTime>5f)
                    {
                        // Bring the foot to the ball from the side used for a forward stroke.
                        // Stop chasing a ball laterally once a figure covers its path.
                        desiredAngle=Forward(i)*35;
                        if(match.FootInStroke(i,angle,angle-Forward(i)*55))
                        {striking[i]=true;desiredAngle=angle-Forward(i)*65;turnSpeed=1250;}
                    }
                    if(outlet && Mathf.Abs(match.Velocity.x)<.09f)
                    {target[i]=Align(i,ball.x);outletX=target[i];}
                    Action(i,outlet?"Intercepting to play forward":"Preparing the receiving foot");
                }
                phase[i]=0;
            }
            float speed=owner==i?.17f:(level==0?.28f:level==1?.45f:level==2?.60f:.78f);
            float x=Mathf.MoveTowards(rod.localPosition.x,target[i],speed*dt);
            float continuousTarget=angle+Mathf.DeltaAngle(angle,desiredAngle);
            match.SetRodPose(i,x,Mathf.MoveTowards(angle,continuousTarget,turnSpeed*dt));
        }
    }
}
