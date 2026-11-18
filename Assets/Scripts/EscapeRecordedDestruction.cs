using System;
using UnityEngine;

/// <summary>Playback of original PositionsRecords; no replacement explosion simulation.</summary>
public sealed class EscapeRecordedDestruction : MonoBehaviour
{
    [Serializable] public sealed class Part
    {
        public Transform target;
        public AnimationCurve[] curves;
        public Vector3 initialPosition;
        public Quaternion initialRotation;
        public void Sample(float time)
        {
            target.localPosition=new Vector3(curves[0].Evaluate(time),curves[1].Evaluate(time),0);
            target.localRotation=new Quaternion(curves[2].Evaluate(time),curves[3].Evaluate(time),curves[4].Evaluate(time),curves[5].Evaluate(time));
        }
        public void Reset(){target.localPosition=initialPosition;target.localRotation=initialRotation;}
    }
    [Serializable] public sealed class Group
    {
        public int goId;
        public float speed;
        public EscapeOriginalTimeline.Channel gate;
        public Part[] parts;
        public float PlaybackTime(float zoneTime)
        {
            float elapsed=0;
            var keys=gate.keys;
            for(int i=0;i<keys.Length;i++)
            {
                float end=i+1<keys.Length?keys[i+1].time:zoneTime;
                if(keys[i].d>=.5f)elapsed+=Mathf.Max(0,Mathf.Min(zoneTime,end)-keys[i].time);
            }
            return elapsed*speed;
        }
        public void Evaluate(float zoneTime)
        {
            float t=PlaybackTime(zoneTime);
            bool begun=false;
            foreach(var k in gate.keys)if(k.time<=zoneTime && k.d>=.5f){begun=true;break;}
            foreach(var p in parts)if(begun)p.Sample(t);else p.Reset();
        }
    }
    public Group[] groups;
    public void Evaluate(float time){foreach(var g in groups)g.Evaluate(time);}
}
