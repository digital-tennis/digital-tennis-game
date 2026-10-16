using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Bounded game-only output limiter. Opt-in validation captures this game's mix, never a microphone or OS audio.</summary>
    public sealed class AudioOutputLimiter : MonoBehaviour
    {
        private float[] recording;
        private int count,channels=2,rate;
        private long processed;
        private float peak;
        public long ProcessedSamples=>Interlocked.Read(ref processed);
        public float Peak=>Volatile.Read(ref peak);
        public bool Capturing=>recording!=null;
        private void Awake()
        {
            rate=AudioSettings.outputSampleRate;
            if(FrontEndController.HasArgument("-dt-audio-validation"))recording=new float[Math.Max(22050,rate)*2*20];
        }
        private void OnAudioFilterRead(float[] data,int channelCount)
        {
            channels=channelCount;int at=count;float maximum=peak;
            for(int i=0;i<data.Length;i++)
            {
                float value=data[i],absolute=Math.Abs(value);
                if(absolute>.8f)value=Math.Sign(value)*(.8f+.199f*(1-(float)Math.Exp(-(absolute-.8f)/.199f)));
                maximum=Math.Max(maximum,Math.Abs(value));
                if(recording!=null){if(at<recording.Length)recording[at++]=value;data[i]=0;}else data[i]=value;
            }
            Volatile.Write(ref peak,maximum);Volatile.Write(ref count,at);Interlocked.Add(ref processed,data.Length);
        }
        public bool Save(string path)
        {
            if(recording==null||rate<=0)return false;
            int length=Volatile.Read(ref count);length-=length%channels;if(length==0)return false;
            using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream))
            {
                void Text(string text)=>writer.Write(System.Text.Encoding.ASCII.GetBytes(text));
                Text("RIFF");writer.Write(36+length*2);Text("WAVEfmt ");writer.Write(16);writer.Write((short)1);writer.Write((short)channels);
                writer.Write(rate);writer.Write(rate*channels*2);writer.Write((short)(channels*2));writer.Write((short)16);Text("data");writer.Write(length*2);
                for(int i=0;i<length;i++)writer.Write((short)(Math.Max(-1,Math.Min(1,recording[i]))*32767));
            }
            return true;
        }
    }
}
