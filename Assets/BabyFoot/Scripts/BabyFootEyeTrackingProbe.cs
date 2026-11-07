using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Unity.XR.CoreUtils;

// Combined gaze from the actual OpenXR eye device. Head direction is diagnostic only.
public class BabyFootEyeTrackingProbe : MonoBehaviour
{
    public XROrigin origin;
    public Transform board;
    public Transform[] targets;
    public Renderer[] targetRenderers;
    public Material idleMaterial,activeMaterial;
    public Transform gazeMarker;
    public LineRenderer gazeLine;
    public float secondsPerTarget=2.5f,settlingSeconds=.7f,maximumInputAgeSeconds=.25f;
    public bool showDebugGaze;
    public bool IsRecording=>log!=null;
    public bool EyeValid { get; private set; }
    public string Status { get; private set; }="Waiting for eye device";
    public string LastFolder { get; private set; }="";
    public string LastResult { get; private set; }="No validation recorded";
    public int ActiveTarget { get; private set; }=-1;
    public int RecordedRows { get; private set; }
    public int ValidRows { get; private set; }
    public float CurrentError { get; private set; }
    public bool HeadTracked { get; private set; }
    public string ControllerStatus { get; private set; }="";
    public string dataDirectoryOverride;
    BabyFootGazeLog log;
    BabyFootGazeSample current;
    double began,nextFlush,lastLoggedInput=-1;
    int lastHighlighted=-2;
    public static bool HeadIsTracked()
    {
        var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        return device.isValid&&device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool tracked)&&tracked;
    }
    void ReadGaze()
    {
        HeadTracked=HeadIsTracked();EyeValid=false;
        current=new BabyFootGazeSample{time=Time.realtimeSinceStartupAsDouble,frame=Time.frameCount,inputTime=-1,status="no_eye_device",headTracked=HeadTracked};
        if(HeadTracked){current.headPosition=origin.Camera.transform.position;current.headForward=origin.Camera.transform.forward;}
        EyeGazeInteraction.EyeGazeDevice eye=null;
        foreach(var d in InputSystem.devices)if(d is EyeGazeInteraction.EyeGazeDevice candidate&&candidate.added&&candidate.enabled){eye=candidate;break;}
        if(eye==null)Status="NO EYE DEVICE - enable eye tracking and Link forwarding";
        else
        {
            var pose=eye.pose.ReadValue();current.inputTime=eye.lastUpdateTime;current.age=InputState.currentTime-eye.lastUpdateTime;
            bool poseValid=BabyFootGazeMath.ValidPose(pose.isTracked,(int)pose.trackingState,pose.position,pose.rotation,current.age,maximumInputAgeSeconds);
            EyeValid=HeadTracked&&Application.isFocused&&poseValid;
            current.status=!HeadTracked?"head_not_tracked":!Application.isFocused?"app_not_focused":!pose.isTracked?"eye_not_tracked":(int)pose.trackingState%4!=3?"pose_incomplete":current.age>maximumInputAgeSeconds?"stale_input":!poseValid?"invalid_pose":"valid";
            Status=EyeValid?"EYE DATA VALID":current.status.ToUpperInvariant().Replace('_',' ');
            if(EyeValid)
            {
                // Eye position/rotation are tracking-space poses, not head-local poses.
                var space=origin.Camera.transform.parent;
                Vector3 position=space.TransformPoint(pose.position);
                Vector3 direction=space.TransformDirection(pose.rotation*Vector3.forward).normalized;
                current.ray=new Ray(position,direction);
                current.planeHit=BabyFootGazeMath.PlaneIntersection(current.ray,board.position,board.forward,out current.hit);
            }
        }
        current.valid=EyeValid;
        bool visible=showDebugGaze&&!IsRecording&&EyeValid;
        gazeMarker.gameObject.SetActive(visible&&current.planeHit);gazeLine.enabled=visible;
        if(visible){gazeLine.SetPosition(0,current.ray.origin+current.ray.direction*.1f);gazeLine.SetPosition(1,current.planeHit?current.hit:current.ray.GetPoint(2.5f));if(current.planeHit)gazeMarker.position=current.hit-board.forward*.008f;}
    }
    void Update()
    {
        ReadGaze();
        var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        ControllerStatus="Controllers: "+(left.isValid?"L connected":"L missing")+" / "+(right.isValid?"R connected":"R missing");
        if(IsRecording)
        {
            double elapsed=Time.realtimeSinceStartupAsDouble-began;
            int target=(int)(elapsed/secondsPerTarget);
            if(target>=targets.Length){StopValidation("completed");return;}
            ActiveTarget=target;current.target=target;current.settled=elapsed-target*secondsPerTarget>=settlingSeconds;
            current.targetPosition=targets[target].position;current.newInput=current.inputTime>=0&&current.inputTime!=lastLoggedInput;
            if(current.inputTime>=0)lastLoggedInput=current.inputTime;
            if(EyeValid){current.angularError=BabyFootGazeMath.ErrorDegrees(current.ray,current.targetPosition);current.planeError=Vector3.Distance(current.hit,current.targetPosition);CurrentError=current.angularError;}
            try
            {
                log.Append(current);RecordedRows=log.Rows;ValidRows=log.ValidRows;
                if(Time.realtimeSinceStartupAsDouble>=nextFlush){log.Flush();nextFlush=Time.realtimeSinceStartupAsDouble+1;}
            }
            catch(Exception e){FailStorage(e);}
        }
        Highlight();
    }
    void Highlight()
    {
        if(lastHighlighted==ActiveTarget)return;
        for(int i=0;i<targetRenderers.Length;i++)targetRenderers[i].sharedMaterial=i==ActiveTarget?activeMaterial:idleMaterial;
        lastHighlighted=ActiveTarget;
    }
    public bool StartValidation()
    {
        if(IsRecording)return false;
        if(!EyeValid){LastResult="Cannot start: valid eye data required. No head-gaze substitute.";return false;}
        try
        {
            var positions=Array.ConvertAll(targets,t=>t.position);
            string root=string.IsNullOrEmpty(dataDirectoryOverride)?Path.Combine(Application.persistentDataPath,"EyeTrackingTests"):dataDirectoryOverride;
            log=new BabyFootGazeLog(root,new BabyFootGazeLog.Metadata{targets=positions,secondsPerTarget=secondsPerTarget,settlingSeconds=settlingSeconds,maximumInputAgeSeconds=maximumInputAgeSeconds});
            LastFolder=log.Folder;RecordedRows=ValidRows=0;began=Time.realtimeSinceStartupAsDouble;nextFlush=began+1;lastLoggedInput=-1;
            ActiveTarget=0;LastResult="Recording: look at each amber target. Gaze marker hidden.";Highlight();return true;
        }
        catch(Exception e){FailStorage(e);return false;}
    }
    public void StopValidation(string reason="aborted")
    {
        if(log==null)return;
        var ending=log;log=null;ActiveTarget=-1;
        try{ending.End(reason);LastResult=$"Validation {reason}: {ValidRows}/{RecordedRows} valid frame observations.\nSaved test: {Path.GetFileName(LastFolder)}";Debug.Log("Eye test files: "+LastFolder);}
        catch(Exception e){LastResult="Could not finalize files: "+e.Message;Debug.LogError(LastResult);}
        Highlight();
    }
    void FailStorage(Exception e){Debug.LogError("Eye test storage: "+e.Message);StopValidation("storage_error");LastResult="Recording stopped: "+e.Message;}
    public void AlignBoard()
    {
        if(IsRecording||!HeadTracked){LastResult="Align only between tests with headset tracking active.";return;}
        var head=origin.Camera.transform;
        var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);
        if(forward.sqrMagnitude>.01f)origin.transform.RotateAround(head.position,Vector3.up,Vector3.SignedAngle(forward,Vector3.forward,Vector3.up));
        var shift=new Vector3(-head.position.x,0,-head.position.z);origin.transform.position+=shift;
        var position=board.position;position.y=head.position.y;board.position=position;
        LastResult="Board aligned to tracked eye height. This does not recalibrate the eye tracker.";
    }
    void OnDisable(){StopValidation("interrupted");}
    void OnApplicationQuit(){StopValidation("interrupted");}
}
