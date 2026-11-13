using System;
using System.Globalization;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.OpenXR.Features.Interactions;

public sealed class BabyFootMatchGaze : MonoBehaviour
{
    StreamWriter output;
    double lastEvent=-1;
    int attempt, observations, validFrames;
    bool recoveryStarted;
    public string Status {get;private set;}="Record gaze";
    public string Coverage => observations==0 ? "No gaze samples" : $"Valid gaze {100f*validFrames/observations:F0}%";
    public void Toggle()
    {
        if(output!=null){Close();return;}
        try
        {
            string folder=Path.Combine(Application.persistentDataPath,"MatchAnalysis");
            Directory.CreateDirectory(folder);
            string name=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv";
            output=new StreamWriter(Path.Combine(folder,name));
            output.WriteLine("app_s,match_s,mode,difficulty,lane_cue_enabled,lane_cue_visible,play_hints_enabled,lane_mode_0auto_1always_2off,attempt,bounce_s,secured_s,closed,recoveries,owner,stable_s,ball_x,ball_z,rod1_x,rod1_deg,wall_hits,player_contacts,input_s,fresh,valid,table_hit_x,table_hit_z,ball_error_deg,lane_error_deg");
            output.Flush();lastEvent=-1;recoveryStarted=false;observations=validFrames=0;
            Status="Stop recording";
        }
        catch(Exception e){Fail(e);}
    }
    public void Observe(BabyFootFreeMatch m,XROrigin origin,bool cueEnabled,bool cueVisible,bool hintsEnabled,int laneMode)
    {
        if(output==null||m.Paused)return;
        if(m.RecoveryPractice && (!recoveryStarted||attempt!=m.Attempt))
        {attempt=m.Attempt;observations=validFrames=0;}
        recoveryStarted=m.RecoveryPractice;
        EyeGazeInteraction.EyeGazeDevice eye=null;
        foreach(var device in InputSystem.devices)
            if(device is EyeGazeInteraction.EyeGazeDevice gaze && gaze.enabled){eye=gaze;break;}
        double stamp=eye==null?-1:eye.lastUpdateTime;
        bool fresh=stamp>=0&&stamp!=lastEvent;
        lastEvent=stamp;
        bool valid=false;
        string gx="",gz="",ballError="",laneError="";
        if(eye!=null && Application.isFocused && BabyFootEyeTrackingProbe.HeadIsTracked())
        {
            var pose=eye.pose.ReadValue();
            valid=BabyFootGazeMath.ValidPose(pose.isTracked,(int)pose.trackingState,pose.position,pose.rotation,InputState.currentTime-stamp,.25);
            if(valid)
            {
                Transform space=origin.Camera.transform.parent,table=m.ball.parent;
                Ray ray=new Ray(space.TransformPoint(pose.position),space.TransformDirection(pose.rotation*Vector3.forward));
                if(BabyFootGazeMath.PlaneIntersection(ray,m.ball.position,table.up,out var hit))
                {var local=table.InverseTransformPoint(hit);gx=N(local.x);gz=N(local.z);}
                ballError=N(BabyFootGazeMath.ErrorDegrees(ray,m.ball.position));
                if(m.RecoveryPractice && m.BounceTime>=0)
                    laneError=N(BabyFootGazeMath.ErrorDegrees(ray,table.TransformPoint(m.RecoveryTarget)));
            }
        }
        if(!m.RecoveryPractice || !m.AttemptClosed){observations++;if(valid)validFrames++;}
        string mode=m.RecoveryPractice?"recovery":m.ControlWarmup?"retention":m.Spectator?"spectator":"match";
        try
        {
            output.WriteLine(string.Join(",",N(Time.realtimeSinceStartupAsDouble),N(m.MatchTime),mode,m.difficulty,cueEnabled?1:0,cueVisible?1:0,hintsEnabled?1:0,laneMode,m.RecoveryPractice?m.Attempt:0,
                m.RecoveryPractice?N(m.BounceTime):"",m.RecoveryPractice?N(m.SecuredTime):"",m.RecoveryPractice&&m.AttemptClosed?1:0,
                m.RecoveryPractice?m.Recoveries:0,m.ControlledRod,N(m.BallControl.StableSeconds),N(m.ball.localPosition.x),N(m.ball.localPosition.z),
                N(m.rods[1].localPosition.x),N(m.RodAngle(1)),m.WallContacts,m.PlayerContacts,N(stamp),fresh?1:0,valid?1:0,gx,gz,ballError,laneError));
            if(Time.frameCount%90==0)output.Flush();
        }
        catch(Exception e){Fail(e);}
    }
    static string N(double x)=>x.ToString("F5",CultureInfo.InvariantCulture);
    void Fail(Exception error){Debug.LogWarning("Match analysis: "+error.Message);Close();Status="Recording error / retry";}
    void Close()
    {
        try{output?.Dispose();}catch(IOException error){Debug.LogWarning(error.Message);}
        output=null;Status="Record gaze";
    }
    void OnDisable()=>Close();
}
