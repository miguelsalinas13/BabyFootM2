using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// No head-pose substitute, eye-camera images, pupil diameter or inferred fixation labels.
public static class BabyFootGazeMath
{
    public static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    public static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
    public static bool ValidPose(bool tracked,int state,Vector3 p,Quaternion q,double age,double maximumAge)
    {
        float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
        return tracked&&(state&3)==3&&Finite(p)&&Finite(norm)&&norm>.5f&&norm<1.5f&&age>=-.01&&age<=maximumAge;
    }
    public static bool PlaneIntersection(Ray ray,Vector3 point,Vector3 normal,out Vector3 hit)
    {
        hit=Vector3.zero;float denominator=Vector3.Dot(ray.direction,normal);
        if(Mathf.Abs(denominator)<.00001f)return false;
        float distance=Vector3.Dot(point-ray.origin,normal)/denominator;
        if(distance<=0||distance>10)return false;
        hit=ray.GetPoint(distance);return Finite(hit);
    }
    public static float ErrorDegrees(Ray ray,Vector3 target)=>Vector3.Angle(ray.direction,target-ray.origin);
}

public struct BabyFootGazeSample
{
    public double time,inputTime,age;
    public int frame,target;
    public bool valid,newInput,settled,headTracked,planeHit;
    public string status;
    public Ray ray;
    public Vector3 headPosition,headForward,targetPosition,hit;
    public float angularError,planeError;
}

// Frame-observation logger. Input event time is not a sensor exposure timestamp.
public sealed class BabyFootGazeLog : IDisposable
{
    public const string Header="app_time_s,frame,input_event_time_s,input_age_s,new_input_event,eye_valid,status,head_tracked,target,settled,gaze_origin_x,gaze_origin_y,gaze_origin_z,gaze_direction_x,gaze_direction_y,gaze_direction_z,head_x,head_y,head_z,head_forward_x,head_forward_y,head_forward_z,target_x,target_y,target_z,plane_hit,hit_x,hit_y,hit_z,angular_error_deg,plane_error_m";
    [Serializable] public class Metadata
    {
        public string protocol="BabyFoot eye validation v1",id,startedUtc,endedUtc,status="recording";
        public string source="OpenXR XR_EXT_eye_gaze_interaction combined gaze; no head fallback";
        public string timing="One observation per Unity Update. input_event_time_s is InputSystem device lastUpdateTime, not sensor time. Repeated events flagged. Not tracker sampling frequency or calibrated latency.";
        public string interpretation="Look at each numbered amber dot. Per-target error estimates after settling; no accuracy pass threshold, fixation or saccade classification. System eye calibration must be completed separately.";
        public string units="World-space metres; angles degrees. Local scene axes: +Z toward board, +Y up. Eye poses transformed through XR camera parent.";
        public float secondsPerTarget,settlingSeconds,maximumInputAgeSeconds;
        public Vector3[] targets;
        public string unityVersion,deviceModel;
        public int rows,validRows,uniqueSettledValidRows;
    }
    readonly StreamWriter writer;
    readonly string folder;
    readonly Metadata metadata;
    readonly int[] total,valid,unique;
    readonly double[] sum,squares;
    bool closed;
    public string Folder=>folder;
    public int Rows=>metadata.rows;
    public int ValidRows=>metadata.validRows;
    static string N(double n)=>n.ToString("F6",CultureInfo.InvariantCulture);
    static string B(bool b)=>b?"true":"false";
    public BabyFootGazeLog(string parent,Metadata details)
    {
        metadata=details;metadata.id=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
        metadata.startedUtc=DateTime.UtcNow.ToString("O");metadata.unityVersion=Application.unityVersion;metadata.deviceModel=SystemInfo.deviceModel;
        folder=Path.Combine(parent,metadata.id);Directory.CreateDirectory(folder);
        int count=metadata.targets.Length;total=new int[count];valid=new int[count];unique=new int[count];sum=new double[count];squares=new double[count];
        File.WriteAllText(Path.Combine(folder,"test.json"),JsonUtility.ToJson(metadata,true));
        writer=new StreamWriter(Path.Combine(folder,"gaze.csv"));writer.WriteLine(Header);writer.Flush();
    }
    public void Append(BabyFootGazeSample s)
    {
        if(closed)throw new ObjectDisposedException(nameof(BabyFootGazeLog));
        string[] row=new string[31];
        row[0]=N(s.time);row[1]=s.frame.ToString(CultureInfo.InvariantCulture);row[2]=s.inputTime>=0?N(s.inputTime):"";row[3]=s.inputTime>=0?N(s.age):"";
        row[4]=B(s.newInput);row[5]=B(s.valid);row[6]=s.status;row[7]=B(s.headTracked);row[8]=(s.target+1).ToString(CultureInfo.InvariantCulture);row[9]=B(s.settled);
        if(s.valid){Put(row,10,s.ray.origin);Put(row,13,s.ray.direction);}
        if(s.headTracked){Put(row,16,s.headPosition);Put(row,19,s.headForward);}
        Put(row,22,s.targetPosition);row[25]=B(s.valid&&s.planeHit);
        if(s.valid&&s.planeHit){Put(row,26,s.hit);row[30]=N(s.planeError);}
        if(s.valid)row[29]=N(s.angularError);
        writer.WriteLine(string.Join(",",row));metadata.rows++;if(s.valid)metadata.validRows++;
        if(s.target>=0&&s.target<total.Length)
        {
            total[s.target]++;if(s.valid)valid[s.target]++;
            if(s.valid&&s.settled&&s.newInput){unique[s.target]++;sum[s.target]+=s.angularError;squares[s.target]+=s.angularError*s.angularError;metadata.uniqueSettledValidRows++;}
        }
    }
    static void Put(string[] row,int i,Vector3 p){row[i]=N(p.x);row[i+1]=N(p.y);row[i+2]=N(p.z);}
    public void Flush(){if(!closed)writer.Flush();}
    public void End(string status)
    {
        if(closed)return;closed=true;
        try{writer.Flush();}finally{writer.Dispose();}
        metadata.status=status;metadata.endedUtc=DateTime.UtcNow.ToString("O");
        File.WriteAllText(Path.Combine(folder,"test.json"),JsonUtility.ToJson(metadata,true));
        using(var report=new StreamWriter(Path.Combine(folder,"summary.csv")))
        {
            report.WriteLine("target,observed_frames,valid_frames,valid_frame_fraction,unique_settled_valid_observations,mean_angular_error_deg,rms_angular_error_deg");
            for(int i=0;i<total.Length;i++)report.WriteLine(string.Join(",",i+1,total[i],valid[i],total[i]>0?N((double)valid[i]/total[i]):"",unique[i],unique[i]>0?N(sum[i]/unique[i]):"",unique[i]>0?N(Math.Sqrt(squares[i]/unique[i])):""));
        }
    }
    public void Dispose(){End("interrupted");}
}
