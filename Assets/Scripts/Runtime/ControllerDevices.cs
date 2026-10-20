using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    public enum InputDeviceKind { Keyboard, Sensor }
    public enum ControllerLinkState { Unavailable, Disconnected, Scanning, Connecting, Connected, Error, Pairing }
    public sealed class ControllerDevice
    {
        public string Id { get; }
        public string Name { get; }
        public ControllerDevice(string id,string name){Id=id;Name=name;}
    }
    /// <summary>Decoded intent, stamped on the PC's monotonic receipt clock by a transport adapter.
    /// ConnectionEpoch belongs to a connection, not the board packet. No court/player role is accepted.</summary>
    public enum ControllerMotion : byte { HorizontalPositive=1,HorizontalNegative=2,OverheadDown=3,ShortRotation=4,ObliqueDown=5,ObliqueUp=6 }
    public readonly struct ControllerEvent
    {
        public readonly bool IsPhysical,ButtonPressed;public readonly ControllerMotion Motion;public readonly byte ButtonNumber;
        public readonly PlayerAction Action;
        public readonly uint Sequence;
        public readonly ulong ConnectionEpoch;
        public readonly double ReceivedAt;
        public readonly float Power,AimX,AimZ;public readonly bool UseGameShotSettings;
        public ControllerEvent(PlayerAction action,uint sequence,ulong epoch,double receivedAt,float power,float aimX=0,float aimZ=0,bool useGameShotSettings=false):this()
        {Action=action;Sequence=sequence;ConnectionEpoch=epoch;ReceivedAt=receivedAt;Power=power;AimX=aimX;AimZ=aimZ;UseGameShotSettings=useGameShotSettings;}
        public ControllerEvent(ControllerMotion motion,uint sequence,ulong epoch,double receivedAt,float power,float aimX,float aimZ):this()
        {IsPhysical=true;Motion=motion;Sequence=sequence;ConnectionEpoch=epoch;ReceivedAt=receivedAt;Power=power;AimX=aimX;AimZ=aimZ;}
        public ControllerEvent(byte button,bool pressed,uint sequence,ulong epoch,double receivedAt):this()
        {IsPhysical=true;ButtonNumber=button;ButtonPressed=pressed;Sequence=sequence;ConnectionEpoch=epoch;ReceivedAt=receivedAt;UseGameShotSettings=true;}
        public bool Allowed => IsPhysical?(ButtonNumber>=1&&ButtonNumber<=5||Enum.IsDefined(typeof(ControllerMotion),Motion)):Action==PlayerAction.Serve||Action==PlayerAction.Forehand||Action==PlayerAction.Backhand||Action==PlayerAction.Smash||Action==PlayerAction.Volley||Action==PlayerAction.Slice||Action==PlayerAction.Lob||Action==PlayerAction.Skill;
    }
    /// <summary>Transport port. Future BLE plugin owns OS pairing, discovery, cancellation and decoding.
    /// Tick publishes worker results on the Unity thread. ClearInput drops only motion events/callbacks;
    /// Disconnect cancels discovery/connection work as well, including its late callbacks.</summary>
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
    public interface IInputDeviceSettings
    {
        InputDeviceKind Selected {get;}
        bool Ready {get;}
        bool ButtonInputMode {get;}
        bool Available {get;}
        ControllerLinkState State {get;}
        string Status {get;}
        string SelectedName {get;}
        string SavedWirelessDeviceId {get;}
        KeyboardBindingSet SavedKeyboard {get;}
        IReadOnlyList<ControllerDevice> Devices {get;}
        string ConnectedDeviceId {get;}
        ControllerSetupSession Setup {get;}
        int Revision {get;}
        void BeginSetup(InputDeviceKind kind);
        bool SaveSetup();
        void CancelSetup();
    }
    /// <summary>Saved runtime selection is independent of the open setup draft and connection transport.</summary>
    public sealed class InputDeviceManager:IInputDeviceSettings,IDisposable
    {
        private readonly IControllerBackend backend;
        private readonly IInputDevicePreference preference;
        private readonly IPlayerInputSource keyboard;
        private readonly SensorPlayerInputSource sensor;
        private readonly Func<KeyCode,bool> setupKeys;
        private InputDeviceConfiguration saved;
        private ControllerLinkState beforeState;
        private ulong beforeEpoch;
        private string beforeDevice;
        private int beforeCount;
        private double clock;
        public event Action<IPlayerInputSource> SourceChanged;
        public InputDeviceKind Selected=>saved.Selected;
        public bool ButtonInputMode=>backend is IControllerInputReadiness r&&r.ButtonInputMode;
        public bool Ready=>Selected==InputDeviceKind.Keyboard||backend.State==ControllerLinkState.Connected&&!string.IsNullOrEmpty(saved.WirelessDeviceId)&&backend.ConnectedDeviceId==saved.WirelessDeviceId&&(backend is IControllerInputReadiness ready?ready.GameplayReady:!(backend is IControllerCalibration calibration)||calibration.Calibrated);
        public bool Available=>backend.Available;
        public ControllerLinkState State=>backend.State;
        public string Status {get;private set;}
        public string SelectedName=>Selected==InputDeviceKind.Keyboard?"키보드":"무선 컨트롤러";
        public string SavedWirelessDeviceId=>saved.WirelessDeviceId;
        public KeyboardBindingSet SavedKeyboard=>saved.Keyboard;
        public IReadOnlyList<ControllerDevice> Devices=>backend.Devices;
        public string ConnectedDeviceId=>backend.ConnectedDeviceId;
        public ControllerSetupSession Setup {get;private set;}
        public int Revision {get;private set;}
        public IPlayerInputSource Source=>Selected==InputDeviceKind.Keyboard?keyboard:sensor;
        public InputDeviceManager(IControllerBackend backend,IPlayerInputSource keyboard,IInputDevicePreference preference,Func<KeyCode,bool> setupKeys=null)
        {
            this.backend=backend??throw new ArgumentNullException(nameof(backend));
            this.keyboard=keyboard??throw new ArgumentNullException(nameof(keyboard));
            this.preference=preference??throw new ArgumentNullException(nameof(preference));this.setupKeys=setupKeys;
            sensor=new SensorPlayerInputSource(backend);saved=preference.Load();ApplyKeyboard();
            beforeState=backend.State;beforeEpoch=backend.ConnectionEpoch;beforeDevice=backend.ConnectedDeviceId;
            beforeCount=backend.Devices.Count;Status=backend.Status;
        }
        private void ApplyKeyboard(){if(keyboard is KeyboardPlayerInputSource keys)keys.SetBindings(saved.Keyboard);}
        private void Changed()=>Revision++;
        public void BeginSetup(InputDeviceKind kind)
        {
            if(!Enum.IsDefined(typeof(InputDeviceKind),kind))return;
            CancelSetup();Setup=new ControllerSetupSession(kind,saved,backend,setupKeys,clock);Setup.Changed+=Changed;Revision++;
        }
        public bool SaveSetup()
        {
            if(Setup==null||!Setup.CanSave)return false;
            var configuration=Setup.Configuration();
            try{preference.Save(configuration);}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){Setup.SaveFailed();return false;}
            var finished=Setup;Setup=null;finished.Changed-=Changed;finished.Close(true);
            saved=configuration;ApplyKeyboard();keyboard.Clear();sensor.Clear();Revision++;SourceChanged?.Invoke(Source);return true;
        }
        public void CancelSetup()
        {
            if(Setup==null)return;
            var cancelled=Setup;Setup=null;cancelled.Changed-=Changed;cancelled.Close(false);keyboard.Clear();sensor.Clear();Revision++;
        }
        public void Tick(double now)
        {
            clock=now;backend.Tick(now);Observe();Setup?.Tick(now);
            if(Selected!=InputDeviceKind.Sensor)sensor.Clear();
        }
        private void Observe()
        {
            bool changed=beforeState!=backend.State||beforeEpoch!=backend.ConnectionEpoch||beforeDevice!=backend.ConnectedDeviceId;
            if(changed)
            {
                keyboard.Clear();sensor.Clear();beforeState=backend.State;beforeEpoch=backend.ConnectionEpoch;beforeDevice=backend.ConnectedDeviceId;
                if(Selected==InputDeviceKind.Sensor)SourceChanged?.Invoke(Source);
            }
            if(changed||beforeCount!=backend.Devices.Count||Status!=backend.Status)
            {beforeCount=backend.Devices.Count;Status=backend.Status;Revision++;}
        }
        public void Dispose(){CancelSetup();keyboard.Clear();sensor.Clear();backend.Disconnect();backend.Dispose();}
    }
    public sealed class SensorPlayerInputSource:BufferedPlayerInputSource
    {
        private readonly IControllerBackend backend;
        public SensorPlayerInputSource(IControllerBackend backend):base("Sensor controller"){this.backend=backend;}
        public override void Poll(double now,float power01,float aimX,float aimZ)
        {
            if(backend.State!=ControllerLinkState.Connected){Clear();return;}
            for(int i=0;i<32&&backend.TryRead(out var e);i++)
            {
                if(e.ConnectionEpoch!=backend.ConnectionEpoch||!e.Allowed)continue;
                PlayerAction action=e.Action;
                if(e.IsPhysical)
                {
                    if(e.ButtonNumber!=0)
                    {
                        if(!e.ButtonPressed)continue; // Release is observable at the backend; no repeated gameplay command.
                        switch(e.ButtonNumber){case 1:action=PlayerAction.ControllerPrimary;break;case 2:action=PlayerAction.MotionHorizontalPositive;break;case 3:action=PlayerAction.MotionHorizontalNegative;break;case 4:action=PlayerAction.Skill;break;default:continue;}
                    }
                    else if(!PhysicalTennisInputResolver.TryPhysicalAction(e.Motion,out action))continue;
                }
                Enqueue(new PlayerInputSample(action,e.Sequence,e.ReceivedAt,e.UseGameShotSettings?power01:e.Power,e.AimX,e.AimZ,isPhysicalMotion:e.IsPhysical&&e.ButtonNumber==0));
            }
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
