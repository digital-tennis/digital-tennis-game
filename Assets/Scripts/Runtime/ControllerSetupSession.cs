using System;
using System.Linq;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>A disposable settings draft. It never selects a gameplay source or writes preferences.</summary>
    public sealed class ControllerSetupSession
    {
        private readonly IControllerBackend backend;
        private readonly InputDeviceConfiguration original;
        private readonly Func<KeyCode,bool> keyDown;
        private readonly PlayerInputRouter testRouter=new PlayerInputRouter();
        private readonly SensorPlayerInputSource testSensor;
        private static readonly KeyCode[] CaptureKeys=((KeyCode[])Enum.GetValues(typeof(KeyCode))).Where(k=>(int)k>0&&(int)k<(int)KeyCode.Mouse0).Distinct().ToArray();
        private readonly bool[] keyboardSeen=new bool[8], wirelessSeen=new bool[3];
        private ControllerLinkState lastState;
        private ulong lastEpoch, testEpoch;
        private string lastDevice, testDevice;
        private double now, operationStarted;
        private bool lastCalibrated,lastCalibrationPending,lastInputReady;
        private bool ownsConnectionWork;private string lastLinkStatus;private bool lastLinkPending;
        public event Action Changed;
        public InputDeviceKind Kind {get;}
        public KeyboardBindingSet Keyboard {get;private set;}
        public string WirelessDeviceId {get;private set;}
        public int BindingCapture {get;private set;}=-1;
        public bool Testing {get;private set;}
        public bool SuppressFrameInput {get;private set;}
        public string Error {get;private set;}
        public string LastInput {get;private set;}="아직 입력이 없습니다.";
        public int WirelessProgress=>wirelessSeen.Count(v=>v);
        public bool WirelessStep(int index)=>wirelessSeen[index];
        public bool KeyboardStep(int index)=>keyboardSeen[index];
        public bool ConnectionReady=>backend.State==ControllerLinkState.Connected&&!string.IsNullOrEmpty(WirelessDeviceId)&&backend.ConnectedDeviceId==WirelessDeviceId;
        public bool LinkDiagnosticsSupported=>backend is IControllerLinkDiagnostics;
        public bool LinkProbePending=>backend is IControllerLinkDiagnostics d&&d.LinkProbePending;
        public bool LinkProbePassed=>backend is IControllerLinkDiagnostics d&&d.LinkProbePassed;
        public string LinkStatus=>(backend as IControllerLinkDiagnostics)?.LinkStatus;
        public void ProbeLink(){if(Kind==InputDeviceKind.Sensor&&ConnectionReady&&backend is IControllerLinkDiagnostics d){d.ProbeLink(now);Notify();}}
        public bool ButtonInputMode=>backend is IControllerInputReadiness r&&r.ButtonInputMode;
        public bool InputReady=>backend is IControllerInputReadiness r?r.GameplayReady:CalibrationReady;
        public bool CalibrationSupported=>!ButtonInputMode&&backend is IControllerCalibration;
        public bool CalibrationReady=>!(backend is IControllerCalibration c)||c.Calibrated;
        public bool CalibrationPending=>backend is IControllerCalibration c&&c.CalibrationPending;
        public bool WirelessTestPassed=>WirelessProgress==3&&InputReady&&ConnectionReady&&testEpoch==backend.ConnectionEpoch&&testDevice==backend.ConnectedDeviceId;
        public bool CanSave=>BindingCapture<0&&(Kind==InputDeviceKind.Keyboard||WirelessTestPassed);
        public ControllerSetupSession(InputDeviceKind kind,InputDeviceConfiguration saved,IControllerBackend backend,Func<KeyCode,bool> keys,double clock)
        {
            Kind=kind;original=saved;this.backend=backend;keyDown=keys??Input.GetKeyDown;now=clock;
            Keyboard=saved.Keyboard;WirelessDeviceId=string.IsNullOrEmpty(saved.WirelessDeviceId)?backend.ConnectedDeviceId:saved.WirelessDeviceId;
            testSensor=new SensorPlayerInputSource(backend);testRouter.SetSource(testSensor);
            lastState=backend.State;lastEpoch=backend.ConnectionEpoch;lastDevice=backend.ConnectedDeviceId;lastCalibrated=CalibrationReady;lastCalibrationPending=CalibrationPending;lastInputReady=InputReady;
        }
        private void Notify()=>Changed?.Invoke();
        public void CaptureBinding(int index)
        {
            if(Kind!=InputDeviceKind.Keyboard||index<0||index>=8)return;
            BindingCapture=index;Testing=false;Error=null;Notify();
        }
        public void RestoreKeyboard()
        {
            if(Kind!=InputDeviceKind.Keyboard)return;
            Keyboard=new KeyboardBindingSet();BindingCapture=-1;Testing=false;LastInput="기본 키 배치로 복원했습니다. 테스트로 확인하세요.";Array.Clear(keyboardSeen,0,8);Error=null;Notify();
        }
        public void StartTest()
        {
            if(Kind==InputDeviceKind.Sensor&&(!ConnectionReady||!InputReady))return;
            BindingCapture=-1;Testing=true;Error=null;LastInput="입력을 기다립니다.";
            Array.Clear(keyboardSeen,0,8);ResetWirelessTest();
            if(Kind==InputDeviceKind.Sensor){testEpoch=backend.ConnectionEpoch;testDevice=backend.ConnectedDeviceId;testRouter.SetSource(testSensor);}
            Notify();
        }
        private void ResetWirelessTest()
        {Array.Clear(wirelessSeen,0,3);testEpoch=0;testDevice=null;}
        public void Tick(double clock)
        {
            now=clock;SuppressFrameInput=false;
            if(Kind==InputDeviceKind.Keyboard)
            {
                if(BindingCapture>=0)
                {
                    foreach(var key in CaptureKeys)if(keyDown(key))
                    {
                        SuppressFrameInput=true;
                        if(key==KeyCode.Escape){BindingCapture=-1;Error=null;}
                        else if(Keyboard.TryChange(BindingCapture,key,out var changed,out var error))
                        {Keyboard=changed;BindingCapture=-1;Error=null;LastInput="키 배치를 변경했습니다. 테스트로 확인하세요.";Array.Clear(keyboardSeen,0,8);}
                        else Error=error;
                        Notify();break;
                    }
                }
                else if(Testing)
                    for(int i=0;i<8;i++)if(keyDown(Keyboard[i]))
                    {SuppressFrameInput=true;keyboardSeen[i]=true;LastInput=KeyboardBindingSet.Labels[i]+"  /  "+KeyboardBindingSet.KeyName(Keyboard[i]);Notify();}
                return;
            }
            if(lastState!=backend.State||lastEpoch!=backend.ConnectionEpoch||lastDevice!=backend.ConnectedDeviceId||lastCalibrated!=CalibrationReady||lastCalibrationPending!=CalibrationPending||lastInputReady!=InputReady)
            {
                lastState=backend.State;lastEpoch=backend.ConnectionEpoch;lastDevice=backend.ConnectedDeviceId;lastCalibrated=CalibrationReady;lastCalibrationPending=CalibrationPending;lastInputReady=InputReady;
                Testing=false;ResetWirelessTest();testSensor.Clear();Notify();
            }
            if(lastLinkStatus!=LinkStatus||lastLinkPending!=LinkProbePending){lastLinkStatus=LinkStatus;lastLinkPending=LinkProbePending;Notify();}
            if(ownsConnectionWork&&(backend.State==ControllerLinkState.Scanning||backend.State==ControllerLinkState.Connecting||backend.State==ControllerLinkState.Pairing))
            {
                double timeout=backend.State==ControllerLinkState.Scanning?20:60;
                if(now-operationStarted>timeout)
                {backend.Disconnect();Testing=false;ResetWirelessTest();Error="시간 안에 연결하지 못했습니다. 전원과 거리를 확인한 뒤 다시 찾아 주세요.";Notify();}
            }
            // Setup packets are consumed here, never by gameplay. Starting a test discards older input.
            if(Testing&&ConnectionReady)
            {
                testSensor.Poll(now,0,0,0);testRouter.Drain(now,(sample,_)=>
                {
                    if(testEpoch!=backend.ConnectionEpoch||testDevice!=backend.ConnectedDeviceId)return;
                    int step=sample.Action==PlayerAction.Serve?0:sample.Action==PlayerAction.Forehand||sample.Action==PlayerAction.Backhand?1:sample.Action==PlayerAction.Skill?2:-1;
                    if(step<0)return;
                    wirelessSeen[step]=true;LastInput=step==0?"서브 입력 확인":step==2?"스킬 입력 확인":sample.Action==PlayerAction.Forehand?"포핸드 입력 확인":"백핸드 입력 확인";Notify();
                });
            }
            else backend.ClearInput();
        }
        public void Scan()
        {
            if(Kind!=InputDeviceKind.Sensor||!backend.Available||Busy||backend.State==ControllerLinkState.Connected)return;
            ownsConnectionWork=true;operationStarted=now;Testing=false;ResetWirelessTest();Error=null;backend.ClearInput();backend.Scan();Notify();
        }
        public bool Busy=>backend.State==ControllerLinkState.Scanning||backend.State==ControllerLinkState.Connecting||backend.State==ControllerLinkState.Pairing;
        public void Connect(string id)
        {
            if(Kind!=InputDeviceKind.Sensor||!backend.Available||Busy||backend.State==ControllerLinkState.Connected||!backend.Devices.Any(d=>d.Id==id))return;
            WirelessDeviceId=id;ownsConnectionWork=true;operationStarted=now;Testing=false;ResetWirelessTest();Error=null;backend.ClearInput();backend.Connect(id);Notify();
        }
        public void Disconnect()
        {
            if(Kind!=InputDeviceKind.Sensor)return;
            backend.Disconnect();Testing=false;ResetWirelessTest();Error=null;Notify();
        }
        public void Calibrate()
        {
            if(Kind!=InputDeviceKind.Sensor||ButtonInputMode||!ConnectionReady||CalibrationPending||!(backend is IControllerCalibration c))return;
            Testing=false;ResetWirelessTest();testSensor.Clear();Error=null;c.Calibrate(now);lastCalibrated=CalibrationReady;lastCalibrationPending=CalibrationPending;lastInputReady=InputReady;Notify();
        }
        public void SaveFailed(){Error="설정을 저장하지 못했습니다. 기존 설정을 유지합니다. 다시 시도해 주세요.";Notify();}
        public InputDeviceConfiguration Configuration()=>new InputDeviceConfiguration(Kind,Keyboard,Kind==InputDeviceKind.Sensor?WirelessDeviceId:original.WirelessDeviceId);
        public void Close(bool saved)
        {
            BindingCapture=-1;Testing=false;testSensor.Clear();
            if(!saved&&ownsConnectionWork)backend.Disconnect();
        }
    }
}
