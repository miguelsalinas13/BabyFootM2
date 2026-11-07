using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

[InitializeOnLoad]
public static class BabyFootHeightProbe
{
    const string Pending="Library/BabyFootHeightProbe.pending";
    static double start,next;
    static string report;
    static BabyFootHeightProbe(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        File.WriteAllText(Pending,"run");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Pending))return;
        start=EditorApplication.timeSinceStartup;next=start+2;report="Height probe / no transforms changed\n";EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        next+=1;
        var origin=UnityEngine.Object.FindAnyObjectByType<XROrigin>();
        if(origin)
        {
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            head.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked);head.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 pose);
            report+=$"t={EditorApplication.timeSinceStartup-start:F1} XR={XRSettings.isDeviceActive} tracked={tracked} rawHead={pose:F4} cameraWorld={origin.Camera.transform.position:F4} cameraLocal={origin.Camera.transform.localPosition:F4} origin={origin.transform.position:F4} scale={origin.transform.lossyScale:F3} floorOffset={origin.CameraFloorOffsetObject.transform.localPosition:F4} requested={origin.RequestedTrackingOriginMode} current={origin.CurrentTrackingOriginMode}\n";
            var list=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(list);
            foreach(var sub in list)report+=$"  Input running={sub.running} mode={sub.GetTrackingOriginMode()} supported={sub.GetSupportedTrackingOriginModes()}\n";
        }
        File.WriteAllText("Library/BabyFootHeightProbe.txt",report);
        if(EditorApplication.timeSinceStartup-start<10)return;
        EditorApplication.update-=Tick;File.Delete(Pending);EditorApplication.isPlaying=false;
    }
}
