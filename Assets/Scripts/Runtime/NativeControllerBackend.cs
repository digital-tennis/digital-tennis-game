using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DigitalTennis.Controller;
using DigitalTennis.Protocol;
using UnityEngine;

namespace DigitalTennis
{
    public interface IControllerInputReadiness
    {
        bool ButtonInputMode {get;}bool GameplayReady {get;}
    }
    public interface IControllerCalibration
    {
        bool Calibrated {get;}bool CalibrationPending {get;}void Calibrate(double now);
    }
    public interface IControllerLinkDiagnostics
    {
        bool LinkProbePending {get;}bool LinkProbePassed {get;}string LinkStatus {get;}
        void ProbeLink(double now);
    }
    public sealed class NativeControllerBackend:IControllerBackend,IControllerCalibration,IControllerLinkDiagnostics,IControllerInputReadiness
    {
        private readonly BleControllerClient client;
        private readonly List<ControllerDevice> devices=new List<ControllerDevice>();
        public NativeControllerBackend(IBleTransport transport){client=new BleControllerClient(transport);}
        public bool Available=>client.Available;
        public string Status=>client.Status;
        public string ConnectedDeviceId=>client.ConnectedDeviceId;
        public ulong ConnectionEpoch=>client.ConnectionEpoch;
        public bool BondVerified=>client.BondVerified;
        public byte SecurityLevel=>client.SecurityLevel;
        public bool ButtonInputMode=>client.InputProfile==ControllerInputProfile.Buttons;
        public bool GameplayReady=>client.GameplayReady;
        public bool Calibrated=>client.Calibrated;
        public bool CalibrationPending=>client.CalibrationPending;
        public bool LinkProbePending=>client.LinkProbePending;
        public bool LinkProbePassed=>client.LinkProbePassed;
        public string LinkStatus=>client.LinkStatus;
        public void ProbeLink(double now)=>client.ProbeLink(now);
        public IReadOnlyList<ControllerDevice> Devices=>devices;
        public ControllerLinkState State
        {
            get
            {
                switch(client.State)
                {
                    case BleLinkState.Unavailable:return ControllerLinkState.Unavailable;
                    case BleLinkState.Scanning:return ControllerLinkState.Scanning;
                    case BleLinkState.Connecting:return ControllerLinkState.Connecting;
                    case BleLinkState.Pairing:return ControllerLinkState.Pairing;
                    case BleLinkState.Connected:return ControllerLinkState.Connected;
                    case BleLinkState.Error:return ControllerLinkState.Error;
                    default:return ControllerLinkState.Disconnected;
                }
            }
        }
        public void Tick(double now){var previous=client.State;client.Tick(now);if(previous!=BleLinkState.Connected&&client.State==BleLinkState.Connected)client.ProbeLink(now);devices.Clear();foreach(var d in client.Devices)devices.Add(new ControllerDevice(d.Id,d.Name));}
        public void Scan()=>client.Scan();public void Connect(string id)=>client.Connect(id);public void Disconnect()=>client.Disconnect();
        public void Calibrate(double now)=>client.Calibrate(now);
        public void ClearInput()=>client.ClearInput();public void Dispose()=>client.Dispose();
        public bool TryRead(out ControllerEvent sample)
        {
            if(!client.TryRead(out var intent)){sample=default;return false;}
            PlayerAction action;
            switch(intent.Kind)
            {
                case SwingKind.Forehand:action=PlayerAction.Forehand;break;case SwingKind.Backhand:action=PlayerAction.Backhand;break;case SwingKind.Serve:action=PlayerAction.Serve;break;
                case SwingKind.Smash:action=PlayerAction.Smash;break;case SwingKind.Volley:action=PlayerAction.Volley;break;case SwingKind.Slice:action=PlayerAction.Slice;break;case SwingKind.Lob:action=PlayerAction.Lob;break;case SwingKind.Skill:action=PlayerAction.Skill;break;
                default:sample=default;return false;
            }
            sample=new ControllerEvent(action,intent.Sequence,intent.Epoch,intent.ReceivedAt,intent.Power,intent.AimX,intent.AimZ,intent.UseGameShotSettings);return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()=>ControllerBackendFactory.Create=Create;
        private static IControllerBackend Create()
        {
            if(Application.platform!=RuntimePlatform.OSXPlayer&&Application.platform!=RuntimePlatform.WindowsPlayer)return new UnavailableControllerBackend();
            string name=Application.platform==RuntimePlatform.WindowsPlayer?"digital-tennis-ble.exe":"digital-tennis-ble";
            string executable=Path.Combine(Application.streamingAssetsPath,"ControllerNative",name);
            if(!File.Exists(executable))return new UnavailableControllerBackend();
            // Capture Unity's time origin on the main thread. Worker receipts use
            // Stopwatch deltas only and never call a Unity API off-thread.
            double origin=Time.realtimeSinceStartupAsDouble;long timestamp=Stopwatch.GetTimestamp();
            Action<string> diagnostic=null;string[] args=Environment.GetCommandLineArgs();int observe=Array.IndexOf(args,"-dt-input-observe");
            if(observe>=0&&observe+1<args.Length&&!args[observe+1].StartsWith("-",StringComparison.Ordinal))
            {
                string directory=args[observe+1];Directory.CreateDirectory(directory);string path=Path.Combine(directory,"native-pipe.txt");object gate=new object();
                // Only line sizes/event categories, never addresses, raw messages, keys or OS stderr.
                diagnostic=value=>{try{lock(gate)File.AppendAllText(path,value+"\n");}catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){}};
            }
            return new NativeControllerBackend(new NativeBleProcessTransport(executable,()=>origin+(Stopwatch.GetTimestamp()-timestamp)/(double)Stopwatch.Frequency,diagnostic));
        }
    }
}
