using UnityEngine;

// Both teams use this controller, including the opponent in human free play.
// Plans only drive bounded rod poses. All shots still require the foot sweep.
public sealed class BabyFootMatchAI
{
    readonly BabyFootFreeMatch match;
    readonly float[][] figures;
    readonly float[] target, phaseAge, holdAge, recovery, nextThink, aimOffset,carryTarget;
    readonly int[] phase;
    readonly Plan[] plans;
    int previousOwner=-1, lastPassRod=-1, lastPassReceiver=-1;
    float passDeadline;
    bool bankPending;
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
        previousOwner=lastPassRod=lastPassReceiver=-1;passDeadline=0;bankPending=false;
        for(int i=0;i<phase.Length;i++){phase[i]=0;holdAge[i]=phaseAge[i]=recovery[i]=nextThink[i]=aimOffset[i]=0;target[i]=carryTarget[i]=match.rods[i].localPosition.x;plans[i]=default;}
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
        int receiver=Receiver(i);float z=match.ball.localPosition.z;
        float endZ=receiver<0?Forward(i)*BabyFootFreeMatch.GoalZ:match.rods[receiver].localPosition.z;
        Plan best=new Plan{Receiver=receiver,TargetX=x,TargetZ=endZ,Speed=receiver<0?2.8f:1.15f,Label=receiver<0?"Shooting a gap":"Passing forward"};
        float score=float.NegativeInfinity;
        for(int sample=0;sample<9;sample++)
        {
            float destination=receiver<0?Mathf.Lerp(-.075f,.075f,sample/8f):Mathf.Lerp(-.27f,.27f,sample/8f);
            if(receiver>=0&&Mathf.Abs(Align(receiver,destination)+NearestFigure(receiver,destination)-destination)>.025f)continue;
            for(int route=0;route<3;route++)
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
    public void OnStrike(int i)
    {
        if(!match.IsAutomatedRod(i))return;
        if(BabyFootFreeMatch.IsBlue(i))BlueStrikes++;else CoralStrikes++;
        var p=plans[i];if(p.Label==null)p=ChoosePlan(i,match.ball.localPosition.x);
        if(p.Bank)BankAttempts++;
        bankPending=p.Bank;
        lastPassRod=i;lastPassReceiver=p.Receiver;passDeadline=match.MatchTime+2;
        Action(i,p.Label);phase[i]=0;recovery[i]=.12f;holdAge[i]=0;
    }
    public void OnWallContact(){if(bankPending){ConfirmedBanks++;bankPending=false;}}
    public void OnPlayerContact(int index){if(index!=lastPassRod)bankPending=false;}
    public void Step(float dt)
    {
        if(!match.aiEnabled)return;
        int owner=match.ControlledRod;
        if(owner!=previousOwner)
        {
            if(owner>=0&&match.IsAutomatedRod(owner))
            {
                Receptions++;holdAge[owner]=0;phase[owner]=0;
                carryTarget[owner]=OpenCarryLane(owner);
                if(lastPassRod>=0&&BabyFootFreeMatch.IsBlue(owner)!=BabyFootFreeMatch.IsBlue(lastPassRod))lastPassRod=lastPassReceiver=-1;
                plans[owner]=ChoosePlan(owner,match.ball.localPosition.x);
            }
            previousOwner=owner;
        }
        if(owner>=0&&match.PossessionSecured&&owner==lastPassReceiver&&lastPassRod>=0&&match.MatchTime<passDeadline)
        {CompletedPasses++;lastPassRod=lastPassReceiver=-1;}
        int level=Mathf.Clamp(match.difficulty,0,2);
        float observation=level==0?.20f:level==1?.12f:.08f;
        for(int i=0;i<match.rods.Length;i++)if(match.IsAutomatedRod(i))
        {
            var rod=match.rods[i];float angle=match.RodAngle(i);
            float dz=match.ball.localPosition.z-rod.localPosition.z;
            if(match.MatchTime>=nextThink[i])
            {
                nextThink[i]=match.MatchTime+observation;
                float travel=Mathf.Clamp(Mathf.Abs(dz)/Mathf.Max(.3f,Mathf.Abs(match.Velocity.z)),0,.4f);
                float predicted=BabyFootAttackMotion.FoldX(match.ball.localPosition.x+match.Velocity.x*travel);
                target[i]=Align(i,predicted,aimOffset[i]);
            }
            float desiredAngle=0,turnSpeed=220;
            if(owner==i)
            {
                holdAge[i]+=dt;
                // Carry the real contact sideways to draw a defender before releasing it.
                target[i]=carryTarget[i];
                desiredAngle=angle;
                if(holdAge[i]<.18f)Action(i,"Controlling / drawing defender");
                else if(holdAge[i]<.36f)
                {
                    if(phase[i]!=4){carryTarget[i]=OpenCarryLane(i);phase[i]=4;Feints++;}
                    Action(i,"Changing lane / feint");
                }
                else if(Receiver(i)>=0||Mathf.Abs(match.ball.localPosition.x)<.081f||holdAge[i]>1.25f)
                {
                    if(phase[i]!=3){plans[i]=ChoosePlan(i,match.ball.localPosition.x);phase[i]=3;}
                    desiredAngle=-Forward(i)*82;turnSpeed=plans[i].Speed*Mathf.Rad2Deg/.0975f;
                    Action(i,plans[i].Label);
                }
                else Action(i,"Pulling ball into a shooting lane");
            }
            else if(recovery[i]>0){recovery[i]-=dt;phase[i]=0;aimOffset[i]=0;}
            else
            {
                bool incoming=dz*match.Velocity.z<0&&Mathf.Abs(dz)<.40f;
                bool slow=incoming&&match.Velocity.magnitude<2.1f;
                if(slow&&phase[i]==0)
                {
                    aimOffset[i]=0;target[i]=Align(i,match.ball.localPosition.x+match.Velocity.x*Mathf.Abs(dz)/Mathf.Max(.1f,Mathf.Abs(match.Velocity.z)));
                    desiredAngle=dz>0?-32:32;turnSpeed=150;Action(i,"Receiving a pass");
                }
                else if(Mathf.Abs(dz)<.085f||phase[i]!=0)
                {
                    if(phase[i]==0){phase[i]=1;phaseAge[i]=0;plans[i]=ChoosePlan(i,match.ball.localPosition.x);aimOffset[i]=plans[i].Offset;}
                    phaseAge[i]+=dt;
                    target[i]=Align(i,match.ball.localPosition.x,aimOffset[i]);
                    if(phase[i]==1)
                    {
                        desiredAngle=Forward(i)*38;turnSpeed=170;
                        if(Mathf.Abs(Mathf.DeltaAngle(angle,desiredAngle))<3||phaseAge[i]>.30f){phase[i]=2;phaseAge[i]=0;}
                    }
                    else
                    {
                        desiredAngle=-Forward(i)*82;turnSpeed=plans[i].Speed*Mathf.Rad2Deg/.0975f;
                        if(phaseAge[i]>.32f){phase[i]=0;recovery[i]=.12f;}
                    }
                    Action(i,plans[i].Label);
                }
                else{aimOffset[i]=0;phase[i]=0;}
            }
            float speed=owner==i?.32f:(level==0?.28f:level==1?.45f:.60f);
            float x=Mathf.MoveTowards(rod.localPosition.x,target[i],speed*dt);
            float continuousTarget=angle+Mathf.DeltaAngle(angle,desiredAngle);
            match.SetRodPose(i,x,Mathf.MoveTowards(angle,continuousTarget,turnSpeed*dt));
        }
    }
}
