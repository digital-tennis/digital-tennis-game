using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Explicit development-only UI/port survey. Mock pairing is labelled and never installed in normal play.</summary>
    public sealed class ControllerUiValidationDriver:MonoBehaviour
    {
        private FrontEndController f;private string folder;private int errors,captures;private bool complete;
        private void Error(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-controller-ui-verify");
            if(flag<0||flag+1>=args.Length||!Debug.isDebugBuild){enabled=false;yield break;}
            folder=args[flag+1];Directory.CreateDirectory(folder);Application.logMessageReceived+=Error;Application.runInBackground=true;
            f=GetComponent<FrontEndController>();double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(!f.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Require(f.Ready,"Front end ready");
            if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();f.MenuChoice(3);Click("controllers.options");
            Require(f.InputDevices.State==ControllerLinkState.Unavailable,"Production build must report unavailable transport honestly");
            yield return Capture("01-controller-keyboard");Click("device.sensor");yield return Capture("02-sensor-unavailable");
            Require(!f.InputDevices.Ready&&f.Match.InputAdapter.UsesSensor,"Selected sensor must not silently use keyboard");
            f.Match.InputAdapter.Press(PlayerAction.Serve);Require(f.Match.Match.CurrentShot.ShotId==0,"Disconnected sensor cannot serve");
            Click("device.keyboard");Click("back");Click("back");f.MenuChoice(0);
            for(int i=0;i<15;i++)
            {
                Click("country."+CharacterCatalog.Ids[i]);Require(f.View.Flags["selection.flag"].texture==CountryFlags.Get(i),"Selected country flag");
                yield return Capture("roster-"+CharacterCatalog.Ids[i]);
            }
            f.Pick(0);yield return Capture("03-world-korea");f.Pick(11);yield return Capture("04-world-netherlands");
            f.Back();f.MenuChoice(3);Click("controllers.options");
            var backend=new SurveyBackend();f.Match.InputAdapter.ConfigureDevices(backend);f.RefreshView();
            Click("device.scan");yield return Capture("05-MOCK-scanning");
            while(f.InputDevices.State==ControllerLinkState.Scanning)yield return null;
            yield return Capture("06-MOCK-discovered");Click("device.connect.0");
            while(f.InputDevices.State==ControllerLinkState.Connecting)yield return null;
            Click("device.sensor");yield return Capture("07-MOCK-connected");
            Click("back");Click("back");f.MenuChoice(0);f.Pick(0);f.Confirm();f.Pick(1);f.Confirm();f.Confirm();
            while(f.Model.Stage!=FrontEndStage.Playing)yield return null;
            backend.Emit(PlayerAction.Serve);deadline=Time.realtimeSinceStartupAsDouble+20;
            while(f.Match.NearHits<1&&Time.realtimeSinceStartupAsDouble<deadline)
            {
                if(f.Match.ReadyToServe&&f.Match.Match.Server==CourtSide.Near)backend.Emit(PlayerAction.Serve);
                else if(f.Match.CanReturn(CourtSide.Near))backend.Emit(PlayerAction.Forehand);
                yield return new WaitForSeconds(.08f);
            }
            Require(f.Match.NearHits>0,"Mock decoded intent must produce an actual accepted hit through the ordinary adapter");
            yield return Capture("08-MOCK-actual-rally");backend.Disconnect();yield return null;yield return null;
            Require(!f.Match.InputAdapter.DeviceReady,"Disconnect blocks sensor input");f.Match.InputAdapter.Press(PlayerAction.MenuBack);Click("controllers.options");yield return Capture("09-MOCK-disconnected");
            f.Match.InputAdapter.ConfigureDevices(new UnavailableControllerBackend());f.RefreshView();
            Click("device.keyboard");Click("back");f.Confirm(); // Return to the real match via its paused menu.
            complete=true;File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(new Report{passed=errors==0,errors=errors,captures=captures,rosterFlags=15,borderRegions=WorldCountries.Regions.Length,nearHits=f.Match.NearHits,scope="Actual Unity GPU UI/flags/country geometry and ordinary input pipeline. Pairing transport is an explicitly labelled development mock; no real BLE or OS pairing claimed."},true));
            Application.Quit(errors==0?0:1);
        }
        private void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private void Click(string id){Require(f.View.Invoke(id),"UI button "+id);}
        private IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();f.RenderUiNow();
            GameCameraCapture.Save(Camera.main,Path.Combine(folder,name+".png"),1600,900,f.UiCamera);captures++;
        }
        private void OnDestroy(){Application.logMessageReceived-=Error;}
        [Serializable]private sealed class Report{public bool passed;public int errors,captures,rosterFlags,borderRegions,nearHits;public string scope;}
        private sealed class SurveyBackend:IControllerBackend
        {
            public bool Available=>true;public ControllerLinkState State{get;private set;}=ControllerLinkState.Disconnected;
            public string Status=>"모의 기기 · UI/입력 경로 검증 전용 · 실제 BLE 연결이 아닙니다.";
            private readonly List<ControllerDevice> devices=new List<ControllerDevice>();public IReadOnlyList<ControllerDevice> Devices=>devices;
            public string ConnectedDeviceId=>State==ControllerLinkState.Connected?"development-fixture":null;
            public ulong ConnectionEpoch{get;private set;}private double clock,until;private uint sequence;
            private readonly Queue<ControllerEvent> queue=new Queue<ControllerEvent>();
            public void Tick(double now)
            {
                clock=now;
                if(now<until)return;
                if(State==ControllerLinkState.Scanning){devices.Clear();devices.Add(new ControllerDevice("development-fixture","모의 NU40-DK [검증 전용]"));State=ControllerLinkState.Disconnected;}
                else if(State==ControllerLinkState.Connecting){State=ControllerLinkState.Connected;ConnectionEpoch++;}
            }
            public void Scan(){State=ControllerLinkState.Scanning;until=clock+.4;}
            public void Connect(string id){State=ControllerLinkState.Connecting;until=clock+.4;}
            public void Disconnect(){State=ControllerLinkState.Disconnected;ClearInput();}
            public void Emit(PlayerAction action){if(State!=ControllerLinkState.Connected)return;if(++sequence==0)sequence=1;queue.Enqueue(new ControllerEvent(action,sequence,ConnectionEpoch,Time.realtimeSinceStartupAsDouble,.4f));}
            public bool TryRead(out ControllerEvent sample){if(queue.Count>0){sample=queue.Dequeue();return true;}sample=default;return false;}
            public void ClearInput()=>queue.Clear();public void Dispose()=>ClearInput();
        }
    }
}
