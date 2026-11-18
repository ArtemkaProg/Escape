using System;
using UnityEngine;

/// <summary>Evaluates verified original cubic segments. Contains no game assets.</summary>
public sealed class EscapeOriginalTimeline : MonoBehaviour
{
    [Serializable] public sealed class Key
    {
        public float time, a, b, c, d;
    }
    [Serializable] public sealed class Channel
    {
        public Key[] keys;
        public float Evaluate(float time)
        {
            int lo = 0, hi = keys.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (keys[mid].time <= time) lo = mid; else hi = mid - 1;
            }
            Key k = keys[lo];
            float dt = Mathf.Max(0, time - k.time);
            return ((k.a * dt + k.b) * dt + k.c) * dt + k.d;
        }
    }
    [Serializable] public sealed class Track
    {
        public int goId, kind;
        public string path;
        public Transform target;
        public Animator pump;
        public Channel[] channels;
        [NonSerialized] public bool pumpPlaying;
    }
    public float duration, speed;
    public Track[] tracks;
    public EscapePumpRigPlayback pumpRig;

    public void ResetTimeline()
    {
        foreach (Track t in tracks)
        {
            if (t.pump == null) continue;
            t.pump.enabled = true;
            t.pump.Rebind();
            t.pump.Update(0);
            t.pump.speed = 0;
            t.pumpPlaying = false;
        }
        Evaluate(0, false);
    }

    public void Evaluate(float time, bool playing)
    {
        time = Mathf.Clamp(time, 0, duration);
        foreach (Track t in tracks)
        {
            var c = t.channels;
            switch (t.kind)
            {
                case 1:
                    t.target.localPosition = new Vector3(c[0].Evaluate(time), c[1].Evaluate(time), c[2].Evaluate(time));
                    break;
                case 2:
                    t.target.localRotation = new Quaternion(c[0].Evaluate(time), c[1].Evaluate(time),
                        c[2].Evaluate(time), c[3].Evaluate(time)).normalized;
                    break;
                case 3:
                    t.target.localScale = new Vector3(c[0].Evaluate(time), c[1].Evaluate(time), c[2].Evaluate(time));
                    break;
                case 4:
                    bool active = c[0].Evaluate(time) >= .5f;
                    if (t.target.gameObject.activeSelf != active) t.target.gameObject.SetActive(active);
                    break;
                case 5:
                    if (pumpRig != null) break;
                    if (t.pump == null) break;
                    bool run = c[0].Evaluate(time) >= .5f;
                    if (run && !t.pumpPlaying) { t.pump.enabled = true; t.pump.Play(0, 0, 0); t.pump.Update(0); }
                    t.pump.speed = run && playing ? 1 : 0;
                    t.pumpPlaying = run;
                    break;
            }
        }
        if (pumpRig != null) pumpRig.Evaluate(time);
    }
}
