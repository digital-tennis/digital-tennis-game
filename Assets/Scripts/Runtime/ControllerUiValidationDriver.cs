using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Opt-in development survey. Simulated wireless transport is labelled; setup never writes match state.</summary>
    public sealed class ControllerUiValidationDriver:MonoBehaviour
    {
        private FrontEndController f;private string folder;private int errors,captures;
        private readonly HashSet<KeyCode> setupKeys=new HashSet<KeyCode>(),gameKeys=new HashSet<KeyCode>();
        private readonly MemoryInputDevicePreference store=new MemoryInputDevicePreference();
        private void Error(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-controller-ui-verify");
            if(flag<0||flag+1>=args.Length||!Debug.isDebugBuild){enabled=false;yield break;}
            folder=args[flag+1];Directory.CreateDirectory(folder);Application.logMessageReceived+=Error;Application.runInBackground=true;
            f=GetComponent<FrontEndController>();double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(!f.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Require(f.Ready,"Front end ready");
            Inject(new UnavailableControllerBackend());
            if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();f.MenuChoice(3);Click("controllers.options");
            yield return Capture("01-category-keyboard-active");
            Require(!f.View.Buttons.ContainsKey("device.scan"),"Category has no mixed pairing panel");
            Click("device.keyboard");yield return Capture("02-keyboard-detail");
            Require(!f.View.Buttons.ContainsKey("device.scan")&&!f.View.Texts.ContainsKey("device.link"),"Keyboard has no wireless functions");
            Click("key.bind.0");yield return Capture("03-keyboard-capture-key");setupKeys.Add(KeyCode.T);yield return null;yield return null;
            Require(f.InputDevices.Setup.Keyboard[0]==KeyCode.T&&f.InputDevices.SavedKeyboard[0]==KeyCode.Space,"Draft does not change active bindings");
            yield return Capture("04-keyboard-draft-unsaved");Click("device.test");setupKeys.UnionWith(new[]{KeyCode.T,KeyCode.Q,KeyCode.K});yield return null;yield return null;
            yield return Capture("05-keyboard-test-no-gameplay");Require(f.Match.Match.CurrentShot.ShotId==0,"Keyboard testing cannot serve");
            Click("back");Require(f.InputDevices.SavedKeyboard[0]==KeyCode.Space,"Cancel keeps saved bindings");yield return Capture("06-cancel-keeps-keyboard");
            Click("device.sensor");yield return Capture("07-wireless-unavailable");
            Require(!f.View.Buttons["device.save"].interactable&&f.InputDevices.Selected==InputDeviceKind.Keyboard,"Unsupported wireless cannot replace saved keyboard");Click("back");
            var backend=new SurveyBackend();Inject(backend);f.RefreshView();Click("device.sensor");yield return Capture("08-MOCK-wireless-detail");
            Click("device.scan");yield return Capture("09-MOCK-scanning");while(f.InputDevices.State==ControllerLinkState.Scanning)yield return null;
            yield return Capture("10-MOCK-device-found");Click("device.connect.0");yield return Capture("11-MOCK-connection-request");
            while(f.InputDevices.State==ControllerLinkState.Connecting)yield return null;Require(f.InputDevices.State==ControllerLinkState.Pairing,"Separate pairing state");yield return Capture("12-MOCK-pairing");
            while(f.InputDevices.State==ControllerLinkState.Pairing)yield return null;yield return null;yield return Capture("13-MOCK-connected-test-required");
            Require(!f.View.Buttons["device.save"].interactable,"Connection alone is not a passed test");Click("device.test");yield return Capture("14-MOCK-test-started");
            backend.Emit(PlayerAction.Serve);yield return null;yield return null;yield return Capture("15-MOCK-test-partial");
            Require(!f.View.Buttons["device.save"].interactable,"One action is insufficient");backend.Emit(PlayerAction.Forehand);backend.Emit(PlayerAction.Skill);yield return null;yield return null;
            yield return Capture("16-MOCK-test-complete-before-save");Require(f.InputDevices.Selected==InputDeviceKind.Keyboard&&f.Match.Match.CurrentShot.ShotId==0,"Wireless setup cannot change active device or score");
            Click("device.save");Require(f.Model.Stage==FrontEndStage.Options&&f.InputDevices.Selected==InputDeviceKind.Sensor,"Explicit save activates device and exits");yield return Capture("17-MOCK-saved-parent");
            Click("back");f.MenuChoice(0);f.Pick(0);f.Confirm();f.Pick(1);f.Confirm();f.Confirm();while(f.Model.Stage!=FrontEndStage.Playing)yield return null;
            backend.Emit(PlayerAction.Serve);deadline=Time.realtimeSinceStartupAsDouble+20;
            while(f.Match.NearHits<1&&Time.realtimeSinceStartupAsDouble<deadline)
            {
                if(f.Match.ReadyToServe&&f.Match.Match.Server==CourtSide.Near)backend.Emit(PlayerAction.Serve);
                else if(f.Match.CanReturn(CourtSide.Near))backend.Emit(PlayerAction.Forehand);
                yield return new WaitForSeconds(.08f);
            }
            Require(f.Match.NearHits>0,"Saved wireless input goes through real hit gates");yield return Capture("18-MOCK-actual-rally");
            f.Pause();Click("controllers.options");Click("device.sensor");Click("device.test");backend.Emit(PlayerAction.Serve);backend.Emit(PlayerAction.Forehand);backend.Emit(PlayerAction.Skill);yield return null;yield return null;
            Require(f.InputDevices.Setup.WirelessTestPassed,"Second setup test passes current connection");yield return Capture("19-MOCK-current-connection-retest");
            backend.Disconnect();yield return null;yield return null;Require(!f.InputDevices.Setup.CanSave&&!f.Match.InputAdapter.DeviceReady,"Lost connection invalidates test");yield return Capture("20-MOCK-disconnect-invalidates-test");
            Click("back");Click("device.keyboard");Require(f.InputDevices.Selected==InputDeviceKind.Sensor,"Opening keyboard is only a draft");Click("key.bind.0");setupKeys.Add(KeyCode.T);yield return null;yield return null;yield return Capture("21-keyboard-draft-sensor-still-active");
            Click("device.save");Require(f.Model.Stage==FrontEndStage.Pause&&f.InputDevices.Selected==InputDeviceKind.Keyboard&&f.InputDevices.SavedKeyboard[0]==KeyCode.T,"Save uses new bindings and returns to pause");yield return Capture("22-keyboard-saved-pause");f.Confirm();
            deadline=Time.realtimeSinceStartupAsDouble+20;while(!f.Match.ReadyToServe&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Require(f.Match.ReadyToServe,"Next ordinary serve is ready");
            uint previous=f.Match.Match.CurrentShot.ShotId;gameKeys.Add(KeyCode.Space);yield return null;yield return null;Require(f.Match.Match.CurrentShot.ShotId==previous,"Unsaved old key does not serve");yield return Capture("23-saved-binding-old-key-ignored");
            gameKeys.Add(KeyCode.T);yield return null;yield return null;Require(f.Match.Match.CurrentShot.ShotId>previous,"Saved T binding serves through keyboard adapter");yield return Capture("24-saved-binding-T-serves");
            f.Pause();
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(new Report{passed=errors==0,errors=errors,captures=captures,nearHits=f.Match.NearHits,draftSaveFlow=true,wirelessTestRequired=true,scope="Actual Unity GPU UI and ordinary adapter. Keyboard keys are injected test fixtures; wireless transport is explicitly labelled MOCK. No actual BLE/OS pairing or native physical-input claim."},true));
            Application.Quit(errors==0?0:1);
        }
        private void Inject(IControllerBackend backend)=>f.Match.InputAdapter.ConfigureDevices(backend,store,new KeyboardPlayerInputSource(k=>gameKeys.Remove(k)),setupKeys:k=>setupKeys.Remove(k));
        private void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private void Click(string id){Require(f.View.Invoke(id),"UI button "+id);}
        private IEnumerator Capture(string name)
        {while(f.StageAge<.3f)yield return null;yield return null;yield return new WaitForEndOfFrame();f.RenderUiNow();GameCameraCapture.Save(Camera.main,Path.Combine(folder,name+".png"),1600,900,f.UiCamera);captures++;}
        private void OnDestroy(){Application.logMessageReceived-=Error;}
        [Serializable]private sealed class Report{public bool passed,draftSaveFlow,wirelessTestRequired;public int errors,captures,nearHits;public string scope;}
        private sealed class SurveyBackend:IControllerBackend
        {
            public bool Available=>true;public ControllerLinkState State{get;private set;}=ControllerLinkState.Disconnected;
            public string Status=>"모의 기기 · 검증 전용 · 실제 BLE/OS 페어링이 아닙니다.";
            private readonly List<ControllerDevice> devices=new List<ControllerDevice>();public IReadOnlyList<ControllerDevice> Devices=>devices;
            public string ConnectedDeviceId=>State==ControllerLinkState.Connected?"development-fixture":null;public ulong ConnectionEpoch{get;private set;}
            private double clock,until;private uint sequence;private readonly Queue<ControllerEvent> queue=new Queue<ControllerEvent>();
            public void Tick(double now)
            {
                clock=now;if(now<until)return;
                if(State==ControllerLinkState.Scanning){devices.Clear();devices.Add(new ControllerDevice("development-fixture","모의 NU40-DK [검증 전용]"));State=ControllerLinkState.Disconnected;}
                else if(State==ControllerLinkState.Connecting){State=ControllerLinkState.Pairing;until=now+.6;}
                else if(State==ControllerLinkState.Pairing){State=ControllerLinkState.Connected;ConnectionEpoch++;}
            }
            public void Scan(){State=ControllerLinkState.Scanning;until=clock+.5;}
            public void Connect(string id){State=ControllerLinkState.Connecting;until=clock+.5;}
            public void Disconnect(){State=ControllerLinkState.Disconnected;ClearInput();}
            public void Emit(PlayerAction action){if(State!=ControllerLinkState.Connected)return;if(++sequence==0)sequence=1;queue.Enqueue(new ControllerEvent(action,sequence,ConnectionEpoch,Time.realtimeSinceStartupAsDouble,.5f));}
            public bool TryRead(out ControllerEvent sample){if(queue.Count>0){sample=queue.Dequeue();return true;}sample=default;return false;}
            public void ClearInput()=>queue.Clear();public void Dispose()=>ClearInput();
        }
    }
}
