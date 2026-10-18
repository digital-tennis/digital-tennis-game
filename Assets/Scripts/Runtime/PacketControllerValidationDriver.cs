using System;
using System.Collections;
using System.IO;
using DigitalTennis.Controller;
using DigitalTennis.Protocol;
using UnityEngine;

namespace DigitalTennis
{
    // Explicit development fixture. Native production discovery never uses it.
    public sealed class PacketControllerValidationDriver:MonoBehaviour
    {
        private FrontEndController flow;private PacketRadio radio;private NativeControllerBackend backend;private string folder;private int errors,captures;
        private void Error(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-controller-packet-verify");if(flag<0||flag+1>=args.Length||!Debug.isDebugBuild){enabled=false;yield break;}
            folder=args[flag+1];Directory.CreateDirectory(folder);Application.runInBackground=true;Application.logMessageReceived+=Error;
            flow=GetComponent<FrontEndController>();double deadline=Time.realtimeSinceStartupAsDouble+20;while(!flow.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Require(flow.Ready,"Front end ready");
            radio=new PacketRadio();backend=new NativeControllerBackend(radio);flow.Match.InputAdapter.ConfigureDevices(backend,new MemoryInputDevicePreference());
            if(flow.Model.Stage==FrontEndStage.Logo)flow.Confirm();flow.Confirm();flow.MenuChoice(3);yield return Click("controllers.options");yield return Click("device.sensor");yield return Capture("01-MOCK-wireless-detail");
            yield return Click("device.scan");yield return Capture("02-MOCK-searching");yield return WaitButton("device.connect.0");yield return Capture("03-MOCK-found");
            yield return Click("device.connect.0");yield return Capture("04-MOCK-requested");radio.Event("pairing");yield return Capture("05-MOCK-pairing");radio.Event("connected",PacketRadio.Id);yield return Capture("06-MOCK-connected-not-calibrated");Require(!flow.View.Buttons["device.test"].interactable&&!flow.View.Buttons["device.save"].interactable,"Connection alone cannot test/save");
            yield return Click("device.probe");yield return Capture("06b-MOCK-sensorless-link-confirmed");Require(backend.LinkProbePassed&&!backend.Calibrated&&!flow.InputDevices.Setup.CanSave&&!flow.View.Buttons["device.test"].interactable,"Sensorless reply never grants calibration/save/gameplay");
            yield return Click("device.calibrate");yield return Capture("07-MOCK-calibrating");radio.Ack();yield return WaitButton("device.test");yield return Capture("08-MOCK-calibrated");yield return Click("device.test");
            radio.Send(3);yield return Capture("09-MOCK-partial-test");Require(!flow.InputDevices.Setup.CanSave,"Partial test cannot save");radio.Send(1);radio.Send(8);yield return Capture("10-MOCK-tested-before-save");Require(flow.InputDevices.Setup.WirelessTestPassed&&flow.Match.Match.CurrentShot.ShotId==0&&flow.InputDevices.Selected==InputDeviceKind.Keyboard,"Wire tests are isolated from game and saved configuration");
            yield return Click("device.save");Require(flow.Model.Stage==FrontEndStage.Options&&flow.InputDevices.Selected==InputDeviceKind.Sensor,"Save applies source and exits");yield return Capture("11-MOCK-saved-options");
            yield return Click("back");flow.MenuChoice(0);flow.Pick(0);flow.Confirm();flow.Pick(1);flow.Confirm();flow.Confirm();deadline=Time.realtimeSinceStartupAsDouble+10;while(flow.Model.Stage!=FrontEndStage.Playing&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Require(flow.Model.Stage==FrontEndStage.Playing,"Ordinary single player");
            deadline=Time.realtimeSinceStartupAsDouble+40;
            while((flow.Match.NearHits<1||flow.Match.FarHits<1)&&Time.realtimeSinceStartupAsDouble<deadline)
            {
                if(flow.Match.ReadyToServe&&flow.Match.Match.Server==CourtSide.Near)radio.Send(3);
                else if(flow.Match.CanReturn(CourtSide.Near))radio.Send(1);
                yield return new WaitForSeconds(.08f);
            }
            Require(flow.Match.NearHits>0&&flow.Match.FarHits>0,"Wire input and AI both hit through ordinary rules");yield return Capture("12-MOCK-actual-rally");
            deadline=Time.realtimeSinceStartupAsDouble+20;
            while((flow.Match.Match.Progress.NearTotalPoints+flow.Match.Match.Progress.FarTotalPoints==0||!flow.Match.ReadyToServe)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Require(flow.Match.ReadyToServe&&flow.Match.Match.Progress.NearTotalPoints+flow.Match.Match.Progress.FarTotalPoints>0,"Actual point and next serve");yield return Capture("13-MOCK-next-serve");
            bool nextServeReached=flow.Match.ReadyToServe;radio.Event("disconnected");yield return null;yield return null;Require(flow.Model.Stage==FrontEndStage.Pause,"Offline device loss pauses the game");ulong frozenAt=flow.Match.Ball.PhysicsTicks;yield return new WaitForSeconds(.2f);Require(flow.Match.Ball.PhysicsTicks==frozenAt,"Device-loss pause freezes physics");Require(!flow.Match.InputAdapter.DeviceReady&&flow.InputDevices.Selected==InputDeviceKind.Sensor,"Disconnect never falls back to keyboard");yield return Capture("14-MOCK-disconnected");
            var report=new Report{passed=errors==0,captures=captures,errors=errors,nearHits=flow.Match.NearHits,farHits=flow.Match.FarHits,points=flow.Match.Match.Progress.NearTotalPoints+flow.Match.Match.Progress.FarTotalPoints,nextServe=nextServeReached,pauseOnDisconnect=flow.Model.Stage==FrontEndStage.Pause,scope="Real Unity GPU UI, packet codec, BLE SDK and ordinary adapter with explicitly MOCK radio; no physical pairing/IMU claim."};
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);
        }
        private void Require(bool value,string message){if(value)return;File.WriteAllText(Path.Combine(folder,"failure.txt"),message);throw new InvalidOperationException(message);}
        private IEnumerator Click(string id)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(flow.StageAge<.3f&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            yield return new WaitForEndOfFrame();
            Require(flow.View.Invoke(id),"UI button "+id+" / "+flow.View.PointerProblem);
            yield return null;
        }
        private IEnumerator WaitButton(string id){double deadline=Time.realtimeSinceStartupAsDouble+10;while((!flow.View.Buttons.TryGetValue(id,out var button)||!button.interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Require(flow.View.Buttons.TryGetValue(id,out var result)&&result.interactable,"Enabled button "+id);}
        private IEnumerator Capture(string name){yield return null;yield return null;while(flow.StageAge<.3f)yield return null;yield return new WaitForEndOfFrame();GameCameraCapture.Save(Camera.main,Path.Combine(folder,name+".png"),1600,900,flow.UiCamera);captures++;}
        private void OnDestroy(){Application.logMessageReceived-=Error;}
        [Serializable]private sealed class Report{public bool passed,nextServe,pauseOnDisconnect;public int captures,errors,nearHits,farHits;public long points;public string scope;}
        private sealed class PacketRadio:IBleTransport
        {
            public const string Id="MOCK-PACKET-RADIO";public bool Available=>true;private ulong operation;private ushort sequence,eventId,commandId;
            public event Action<BleTransportEvent> Received;
            public void Scan(ulong op){operation=op;Event("device",Id);}public void Connect(string id,ulong op){operation=op;}
            public void Write(byte[] bytes,ulong op){if(op!=operation||PacketCodec.Decode(bytes,out var p)!=DecodeError.None)return;if(p.Type==PacketType.Calibrate)commandId=p.EventId;else if(p.Type==PacketType.LinkProbe)Packet(PacketType.LinkReply,0,p.EventId,0);}
            public void Ack()=>Packet(PacketType.Ack,0,commandId);
            public void Send(byte kind)=>Packet(PacketType.Swing,kind,++eventId);
            private void Packet(PacketType type,byte kind,ushort id,byte flags=3)=>Received?.Invoke(new BleTransportEvent{Operation=operation,Kind="packet",ReceivedAt=Time.realtimeSinceStartupAsDouble,Packet=PacketCodec.Encode(new ControllerPacket(2,type,flags,++sequence,100,id,kind,type==PacketType.Swing?(byte)50:(byte)0,0,0,255))});
            public void Event(string kind,string id=null)=>Received?.Invoke(new BleTransportEvent{Operation=operation,Kind=kind,Id=id,Name="MOCK PACKET RADIO · 실제 기기 아님",ReceivedAt=Time.realtimeSinceStartupAsDouble});
            public void Disconnect(){}public void Dispose(){}
        }
    }
}
