using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Bounded game-only output limiter. Opt-in validation captures this game's mix, never a microphone or OS audio.</summary>
    public sealed class AudioOutputLimiter : MonoBehaviour
    {
        private readonly object captureSync=new object();
        private float[] recording;
        private int count,channels=2,rate;
        private long processed;
        private float peak;
        private bool muteMonitor;
        public bool MonitorMuted=>muteMonitor;
        public long ProcessedSamples=>Interlocked.Read(ref processed);
        public float Peak=>Volatile.Read(ref peak);
        public bool Capturing=>recording!=null;
        private void Awake()
        {
            rate=AudioSettings.outputSampleRate;muteMonitor=Application.isBatchMode;
            if(FrontEndController.HasArgument("-dt-audio-validation"))recording=new float[Math.Max(22050,rate)*2*20];
        }
        private void OnAudioFilterRead(float[] data,int channelCount)
        {
            lock(captureSync)
            {
                channels=channelCount;int at=count;float maximum=peak;
                for(int i=0;i<data.Length;i++)
                {
                    float value=data[i],absolute=Math.Abs(value);
                    if(absolute>.8f)value=Math.Sign(value)*(.8f+.199f*(1-(float)Math.Exp(-(absolute-.8f)/.199f)));
                    maximum=Math.Max(maximum,Math.Abs(value));
                    if(recording!=null){if(at<recording.Length)recording[at++]=value;}data[i]=muteMonitor?0:value;
                }
                Volatile.Write(ref peak,maximum);Volatile.Write(ref count,at);Interlocked.Add(ref processed,data.Length);
            }
        }
        public void RestartCapture(){lock(captureSync){if(recording!=null)count=0;}}
        public bool Save(string path)
        {
            if(recording==null||rate<=0)return false;
            float[] captured;int capturedChannels;
            lock(captureSync)
            {
                int size=count-count%channels;if(size==0)return false;
                captured=new float[size];Array.Copy(recording,captured,size);capturedChannels=channels;
            }
            int length=captured.Length;
            using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream))
            {
                void Text(string text)=>writer.Write(System.Text.Encoding.ASCII.GetBytes(text));
                Text("RIFF");writer.Write(36+length*2);Text("WAVEfmt ");writer.Write(16);writer.Write((short)1);writer.Write((short)capturedChannels);
                writer.Write(rate);writer.Write(rate*capturedChannels*2);writer.Write((short)(capturedChannels*2));writer.Write((short)16);Text("data");writer.Write(length*2);
                for(int i=0;i<length;i++)writer.Write((short)(Math.Max(-1,Math.Min(1,captured[i]))*32767));
            }
            return true;
        }
    }
}
