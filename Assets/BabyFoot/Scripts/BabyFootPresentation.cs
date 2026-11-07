using UnityEngine;

// Presentation-only events: never reads target direction or changes gameplay timing.
public class BabyFootPresentation : MonoBehaviour
{
    public BabyFootDefense exercise;
    public Transform attackingRod;
    public ParticleSystem goalSparks;
    public float strikeDuration=.24f;
    Quaternion restRotation;
    double strikeTime=-1;
    AudioSource feedbackSource;
    AudioClip contactClip,airClip,blockClip,goalClip,missClip;
    public int wallSoundCount { get; private set; }
    void WallFeedback(){if(exercise.IsAttack && PlayFeedback(blockClip))wallSoundCount++;}
    public int contactSoundCount { get; private set; }
    public int airSoundCount { get; private set; }
    public int blockSoundCount { get; private set; }
    public int goalSoundCount { get; private set; }
    public int missSoundCount { get; private set; }
    public bool FeedbackReady => contactClip && airClip && blockClip && goalClip && missClip;
    void PrepareFeedback()
    {
        if(!feedbackSource)
        {
            var sound=new GameObject("Shot Feedback Audio");sound.transform.SetParent(transform,false);
            feedbackSource=sound.AddComponent<AudioSource>();feedbackSource.playOnAwake=false;
            feedbackSource.spatialBlend=1;feedbackSource.rolloffMode=AudioRolloffMode.Linear;
            feedbackSource.minDistance=.3f;feedbackSource.maxDistance=5;feedbackSource.dopplerLevel=0;feedbackSource.volume=.7f;
        }
        contactClip=Resources.Load<AudioClip>("BabyFootAudio/contact");airClip=Resources.Load<AudioClip>("BabyFootAudio/air");
        blockClip=Resources.Load<AudioClip>("BabyFootAudio/block");goalClip=Resources.Load<AudioClip>("BabyFootAudio/goal");missClip=Resources.Load<AudioClip>("BabyFootAudio/miss");
    }
    bool PlayFeedback(AudioClip clip)
    {
        if(!clip || !feedbackSource)return false;
        feedbackSource.transform.position=exercise.ball.position;feedbackSource.PlayOneShot(clip);return true;
    }
    void ContactFeedback(bool made)
    {
        if(!exercise.IsAttack)return;
        if(made){if(PlayFeedback(contactClip))contactSoundCount++;}
        else if(PlayFeedback(airClip))airSoundCount++;
    }
    public int strikeCount { get; private set; }
    public int goalEffectCount { get; private set; }
    public bool strikeActive => strikeTime>=0;
    public double lastStrikeTime { get; private set; }=-1;
    public void SetAttackingRod(Transform rod)
    {
        ResetPresentation(); attackingRod=rod;
        if(rod) restRotation=rod.localRotation;
    }
    void OnEnable()
    {
        if(!exercise || !attackingRod) return;
        PrepareFeedback();
        restRotation=attackingRod.localRotation;
        exercise.TrialReset+=ResetPresentation;
        exercise.ShotLaunched+=Strike;
        exercise.TrialCompleted+=Complete;
        exercise.AttackContact+=ContactFeedback;exercise.AttackWall+=WallFeedback;
    }
    void OnDisable()
    {
        if(exercise)
        {
            exercise.TrialReset-=ResetPresentation;
            exercise.ShotLaunched-=Strike;
            exercise.TrialCompleted-=Complete;
            exercise.AttackContact-=ContactFeedback;exercise.AttackWall-=WallFeedback;
        }
        ResetPresentation();
    }
    void Strike(double onset)
    {
        if(exercise.IsAttack && exercise.externalControls)return;
        strikeTime=onset; lastStrikeTime=onset; strikeCount++;
        // Same fixed stroke for both targets. No pre-shot movement, audio or lighting changes.
        attackingRod.localRotation=restRotation*Quaternion.AngleAxis(exercise.IsAttack?0:6,Vector3.right);
    }
    void LateUpdate()
    {
        if(strikeTime<0) return;
        if(exercise.IsAttack)
        {
            float elapsed=(float)(Time.realtimeSinceStartupAsDouble-strikeTime);
            float attackAngle;
            if(elapsed<.04f)attackAngle=Mathf.Lerp(0,-20,Mathf.SmoothStep(0,1,elapsed/.04f));
            else if(elapsed<BabyFootAttack.StrikeContactDelay)attackAngle=Mathf.Lerp(-20,18,Mathf.SmoothStep(0,1,(elapsed-.04f)/(BabyFootAttack.StrikeContactDelay-.04f)));
            else if(elapsed<.14f)attackAngle=Mathf.Lerp(18,60,Mathf.SmoothStep(0,1,(elapsed-BabyFootAttack.StrikeContactDelay)/(.14f-BabyFootAttack.StrikeContactDelay)));
            else if(elapsed<.28f)attackAngle=Mathf.Lerp(60,0,Mathf.SmoothStep(0,1,(elapsed-.14f)/.14f));
            else{attackingRod.localRotation=restRotation;strikeTime=-1;return;}
            attackingRod.localRotation=restRotation*Quaternion.AngleAxis(attackAngle,Vector3.right);return;
        }
        float t=(float)(Time.realtimeSinceStartupAsDouble-strikeTime)/Mathf.Max(.05f,strikeDuration);
        if(t>=1) { attackingRod.localRotation=restRotation; strikeTime=-1; return; }
        float angle=t<.35f?Mathf.Lerp(6,52,Mathf.SmoothStep(0,1,t/.35f)):Mathf.Lerp(52,0,Mathf.SmoothStep(0,1,(t-.35f)/.65f));
        attackingRod.localRotation=restRotation*Quaternion.AngleAxis(angle,Vector3.right);
    }
    void Complete(string result)
    {
        if(exercise.IsAttack)
        {
            if(result=="goal" && PlayFeedback(goalClip))goalSoundCount++;
            else if(result=="blocked" && PlayFeedback(blockClip))blockSoundCount++;
            else if(result=="miss" && exercise.attack.ContactMade && PlayFeedback(missClip))missSoundCount++;
        }
        if(result!="goal") return;
        goalEffectCount++;
        goalSparks.Play(true);
    }
    void ResetPresentation()
    {
        strikeTime=-1;
        if(feedbackSource)feedbackSource.Stop();
        if(attackingRod) attackingRod.localRotation=restRotation;
        if(goalSparks) goalSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
