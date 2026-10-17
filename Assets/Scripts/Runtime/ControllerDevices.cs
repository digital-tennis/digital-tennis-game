using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    public enum InputDeviceKind { Keyboard, Sensor }
    public enum ControllerLinkState { Unavailable, Disconnected, Scanning, Connecting, Connected, Error }
    public sealed class ControllerDevice
    {
        public string Id { get; }
        public string Name { get; }
        public ControllerDevice(string id,string name){Id=id;Name=name;}
    }
    /// <summary>Decoded intent, stamped on the PC's monotonic receipt clock by a transport adapter.
    /// ConnectionEpoch belongs to a connection, not the board packet. No court/player role is accepted.</summary>
    public readonly struct ControllerEvent
    {
        public readonly PlayerAction Action;
        public readonly uint Sequence;
        public readonly ulong ConnectionEpoch;
        public readonly double ReceivedAt;
        public readonly float Power,AimX,AimZ;
        public ControllerEvent(PlayerAction action,uint sequence,ulong epoch,double receivedAt,float power,float aimX=0,float aimZ=0)
        {Action=action;Sequence=sequence;ConnectionEpoch=epoch;ReceivedAt=receivedAt;Power=power;AimX=aimX;AimZ=aimZ;}
        public bool Allowed => Action==PlayerAction.Serve||Action==PlayerAction.Forehand||Action==PlayerAction.Backhand||Action==PlayerAction.Smash||Action==PlayerAction.Volley||Action==PlayerAction.Slice||Action==PlayerAction.Lob||Action==PlayerAction.Skill;
    }
    /// <summary>Transport port. Future BLE plugin owns OS pairing, discovery, cancellation and decoding.
    /// Tick publishes worker results on the Unity thread; ClearInput must discard all pending callbacks/events.</summary>
    public interface IControllerBackend:IDisposable
    {
        bool Available {get;}
        ControllerLinkState State {get;}
        string Status {get;}
        IReadOnlyList<ControllerDevice> Devices {get;}
        string ConnectedDeviceId {get;}
        ulong ConnectionEpoch {get;}
        void Tick(double now);
        void Scan();
        void Connect(string deviceId);
        void Disconnect();
        bool TryRead(out ControllerEvent sample);
        void ClearInput();
    }
    public sealed class UnavailableControllerBackend:IControllerBackend
    {
        public bool Available=>false;
        public ControllerLinkState State=>ControllerLinkState.Unavailable;
        public string Status=>"센서 연결 모듈 준비 중입니다. 현재는 키보드로 플레이할 수 있습니다.";
        public IReadOnlyList<ControllerDevice> Devices=>Array.Empty<ControllerDevice>();
        public string ConnectedDeviceId=>null;public ulong ConnectionEpoch=>0;
        public void Tick(double now){} public void Scan(){} public void Connect(string deviceId){} public void Disconnect(){}
        public bool TryRead(out ControllerEvent sample){sample=default;return false;}
        public void ClearInput(){}public void Dispose(){}
    }
    /// <summary>Composition root seam. A platform plugin registers its adapter before a court is loaded.</summary>
    public static class ControllerBackendFactory
    {
        public static Func<IControllerBackend> Create {get;set;}=()=>new UnavailableControllerBackend();
    }
    public interface IInputDevicePreference { InputDeviceKind Load(); void Save(InputDeviceKind selected); }
    public sealed class MemoryInputDevicePreference:IInputDevicePreference
    {
        private InputDeviceKind value;public InputDeviceKind Load()=>value;public void Save(InputDeviceKind selected)=>value=selected;
    }
    public sealed class FileInputDevicePreference:IInputDevicePreference
    {
        [Serializable]private sealed class Data{public int version=1;public string selected;}
        private readonly string path;
        public FileInputDevicePreference(string path){this.path=path;}
        public InputDeviceKind Load()
        {
            try {if(!File.Exists(path))return InputDeviceKind.Keyboard;var data=JsonUtility.FromJson<Data>(File.ReadAllText(path));return data!=null&&data.version==1&&data.selected=="Sensor"?InputDeviceKind.Sensor:InputDeviceKind.Keyboard;}
            catch(IOException){return InputDeviceKind.Keyboard;}catch(UnauthorizedAccessException){return InputDeviceKind.Keyboard;}catch(ArgumentException){return InputDeviceKind.Keyboard;}
        }
        public void Save(InputDeviceKind selected)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending=path+".pending";File.WriteAllText(pending,JsonUtility.ToJson(new Data{selected=selected.ToString()},true));
            if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);
        }
    }
    public interface IInputDeviceSettings
    {
        InputDeviceKind Selected {get;}
        bool Ready {get;}
        bool Available {get;}
        ControllerLinkState State {get;}
        string Status {get;}
        string SelectedName {get;}
        IReadOnlyList<ControllerDevice> Devices {get;}
        string ConnectedDeviceId {get;}
        int Revision {get;}
        bool Select(InputDeviceKind kind);
        void Scan();void Connect(string id);void Disconnect();
    }
    /// <summary>Strategy selection and connection lifecycle. UI depends on settings, never a BLE SDK.</summary>
    public sealed class InputDeviceManager:IInputDeviceSettings,IDisposable
    {
        private readonly IControllerBackend backend;private readonly IInputDevicePreference preference;
        private readonly IPlayerInputSource keyboard;private readonly SensorPlayerInputSource sensor;
        private ControllerLinkState beforeState;private ulong beforeEpoch;private int beforeCount;
        public event Action<IPlayerInputSource> SourceChanged;
        public InputDeviceKind Selected {get;private set;}
        public bool Ready=>Selected==InputDeviceKind.Keyboard||backend.State==ControllerLinkState.Connected;
        public bool Available=>backend.Available;public ControllerLinkState State=>backend.State;
        public string Status {get;private set;}
        public string SelectedName=>Selected==InputDeviceKind.Keyboard?"키보드":"센서 컨트롤러";
        public IReadOnlyList<ControllerDevice> Devices=>backend.Devices;
        public string ConnectedDeviceId=>backend.ConnectedDeviceId;
        public int Revision {get;private set;}
        public IPlayerInputSource Source=>Selected==InputDeviceKind.Keyboard?keyboard:sensor;
        public InputDeviceManager(IControllerBackend backend,IPlayerInputSource keyboard,IInputDevicePreference preference)
        {
            this.backend=backend??throw new ArgumentNullException(nameof(backend));this.keyboard=keyboard??throw new ArgumentNullException(nameof(keyboard));this.preference=preference??throw new ArgumentNullException(nameof(preference));
            sensor=new SensorPlayerInputSource(backend);Selected=preference.Load();beforeState=backend.State;beforeEpoch=backend.ConnectionEpoch;beforeCount=backend.Devices.Count;Status=backend.Status;
        }
        public bool Select(InputDeviceKind kind)
        {
            if(!Enum.IsDefined(typeof(InputDeviceKind),kind))return false;
            try{preference.Save(kind);}catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){Status="설정을 저장하지 못했습니다. 다시 시도해 주세요.";Revision++;return false;}
            keyboard.Clear();sensor.Clear();Selected=kind;Status=backend.Status;Revision++;SourceChanged?.Invoke(Source);return true;
        }
        public void Tick(double now)
        {
            backend.Tick(now);Observe();
            if(Selected!=InputDeviceKind.Sensor)sensor.Clear();
        }
        private void Observe()
        {
            bool changed=beforeState!=backend.State||beforeEpoch!=backend.ConnectionEpoch;
            if(changed)
            {
                keyboard.Clear();sensor.Clear();beforeState=backend.State;beforeEpoch=backend.ConnectionEpoch;
                if(Selected==InputDeviceKind.Sensor)SourceChanged?.Invoke(Source);
            }
            if(changed||beforeCount!=backend.Devices.Count||Status!=backend.Status){beforeCount=backend.Devices.Count;Status=backend.Status;Revision++;}
        }
        public void Scan(){if(!backend.Available||backend.State==ControllerLinkState.Connecting||backend.State==ControllerLinkState.Scanning||backend.State==ControllerLinkState.Connected)return;backend.Scan();Observe();}
        public void Connect(string id)
        {if(!backend.Available||backend.State==ControllerLinkState.Connecting||backend.State==ControllerLinkState.Scanning||backend.State==ControllerLinkState.Connected)return;bool found=false;foreach(var d in backend.Devices)if(d.Id==id)found=true;if(!found)return;backend.Connect(id);Observe();}
        public void Disconnect(){backend.Disconnect();Observe();}
        public void Dispose(){keyboard.Clear();sensor.Clear();backend.Disconnect();backend.Dispose();}
    }
    public sealed class SensorPlayerInputSource:BufferedPlayerInputSource
    {
        private readonly IControllerBackend backend;
        public SensorPlayerInputSource(IControllerBackend backend):base("Sensor controller"){this.backend=backend;}
        public override void Poll(double now,float power01,float aimX,float aimZ)
        {
            if(backend.State!=ControllerLinkState.Connected){Clear();return;}
            for(int i=0;i<32&&backend.TryRead(out var e);i++)
                if(e.ConnectionEpoch==backend.ConnectionEpoch&&e.Allowed)
                    Enqueue(new PlayerInputSample(e.Action,e.Sequence,e.ReceivedAt,e.Power,e.AimX,e.AimZ));
        }
        public override void Clear(){base.Clear();backend.ClearInput();}
    }
    /// <summary>Keyboard menus always remain reachable, even with a missing/disconnected sensor.
    /// These samples may only reach MenuRoute and are never delivered as gameplay.</summary>
    public sealed class KeyboardMenuInputSource:BufferedPlayerInputSource
    {
        private readonly Func<KeyCode,bool> keyDown;private uint sequence;
        public KeyboardMenuInputSource(Func<KeyCode,bool> keyDown=null):base("Menu keyboard"){this.keyDown=keyDown??Input.GetKeyDown;}
        public override void Poll(double now,float power,float x,float z)
        {
            Key(KeyCode.Return,PlayerAction.MenuConfirm);Key(KeyCode.KeypadEnter,PlayerAction.MenuConfirm);Key(KeyCode.Escape,PlayerAction.MenuBack);
            Key(KeyCode.LeftArrow,PlayerAction.MenuLeft);Key(KeyCode.RightArrow,PlayerAction.MenuRight);Key(KeyCode.UpArrow,PlayerAction.MenuUp);Key(KeyCode.DownArrow,PlayerAction.MenuDown);Key(KeyCode.C,PlayerAction.Characters);Key(KeyCode.F5,PlayerAction.ReloadBalance);
            void Key(KeyCode key,PlayerAction action){if(keyDown(key)){if(++sequence==0)sequence=1;Enqueue(new PlayerInputSample(action,sequence,now,power));}}
        }
    }
}
