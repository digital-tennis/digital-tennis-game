using System;
using System.Collections;
using NUnit.Framework;
using DigitalTennis.Controller;
using DigitalTennis.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class PacketControllerSceneTests
    {
        private sealed class RadioFixture:IBleTransport
        {
            public bool Physical,ButtonProfile;public bool Available=>true;public ulong Operation;private ushort sequence;
            public event Action<BleTransportEvent> Received;
            public void Scan(ulong operation){Operation=operation;Event("device","fixture");}
            public void Connect(string id,ulong operation){Operation=operation;Event("pairing");Received?.Invoke(new BleTransportEvent{Operation=operation,Kind="security",Id=id,Packet=new byte[]{0x44,1,2,3},ReceivedAt=Time.realtimeSinceStartupAsDouble});Event("connected",id);}
            public void Write(byte[] data,ulong operation){Assert.That(PacketCodec.Decode(data,out var command),Is.EqualTo(DecodeError.None));if(command.Type==PacketType.LinkProbe){Send(Physical?(ButtonProfile?(byte)3:(byte)2):(ButtonProfile?(byte)1:(byte)0),command.EventId,PacketType.LinkReply,0);return;}Assert.That(command.Type,Is.EqualTo(PacketType.Calibrate));Send(0,command.EventId,PacketType.Ack);}
            public void SendFirmwareOverhead(ushort id)
            {
                string hex=System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,"Tests/Fixtures/physical-overhead.txt")).Trim();
                var bytes=new byte[hex.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);
                Assert.That(PacketCodec.Decode(bytes,out var p),Is.EqualTo(DecodeError.None));Assert.That(p.Type,Is.EqualTo(PacketType.Motion));
                // Preserve actual C payload; fresh transport IDs are required for each fixture event.
                Received?.Invoke(new BleTransportEvent{Kind="packet",Operation=Operation,ReceivedAt=Time.realtimeSinceStartupAsDouble,Packet=PacketCodec.Encode(new ControllerPacket(p.Version,p.Type,p.Flags,++sequence,p.DeviceTimeMs,id,p.Kind,p.Power,p.YawCdeg,p.PitchCdeg,p.Battery))});
            }
            public void Disconnect(){}public void Dispose(){}
            public void Event(string kind,string id=null)=>Received?.Invoke(new BleTransportEvent{Kind=kind,Id=id,Name="MOCK PACKET FIXTURE",Operation=Operation,ReceivedAt=Time.realtimeSinceStartupAsDouble});
            public void Send(byte kind,ushort eventId,PacketType type=PacketType.Swing,byte flags=3)
            {Received?.Invoke(new BleTransportEvent{Kind="packet",Operation=Operation,ReceivedAt=Time.realtimeSinceStartupAsDouble,Packet=PacketCodec.Encode(new ControllerPacket(Physical?(byte)3:(byte)2,type,flags,++sequence,100,eventId,kind,type==PacketType.Swing||type==PacketType.Motion?(byte)70:(byte)0,0,0,255))});}
        }
        private static void Click(FrontEndController f,string id)=>Assert.That(f.View.Invoke(id),Is.True,id);
        [UnityTest]public IEnumerator PhysicalV3SetupConsumesNeutralInputsAndGameInterpretsFirmwareOverheadAsServe()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(FrontEndController.Active?.Ready!=true&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var f=FrontEndController.Active;var radio=new RadioFixture{Physical=true};var backend=new NativeControllerBackend(radio);f.Match.InputAdapter.ConfigureDevices(backend,new MemoryInputDevicePreference());
            if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();f.MenuChoice(3);Click(f,"controllers.options");Click(f,"device.sensor");Click(f,"device.scan");
            deadline=Time.realtimeSinceStartupAsDouble+6;while((!f.View.Buttons.TryGetValue("device.connect.0",out var connect)||!connect.interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Click(f,"device.connect.0");yield return null;yield return null;yield return null;
            Assert.That(backend.PhysicalInputMode,Is.True);Assert.That(backend.Calibrated,Is.False);Click(f,"device.calibrate");yield return null;yield return null;
            Click(f,"device.test");radio.SendFirmwareOverhead(901);radio.Send(1,902,PacketType.Motion);radio.Send(4,903,PacketType.ButtonDown);radio.Send(4,904,PacketType.ButtonUp);yield return null;yield return null;
            Assert.That(f.InputDevices.Setup.WirelessTestPassed,Is.True);Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero,"setup never serves");Assert.That(f.InputDevices.Setup.LastInput,Does.Contain("버튼4"));
            Click(f,"device.save");Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Sensor));Click(f,"back");f.MenuChoice(0);f.Confirm();f.Confirm();f.Confirm();
            deadline=Time.realtimeSinceStartupAsDouble+6;while(f.Model.Stage!=FrontEndStage.Playing&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Playing));radio.SendFirmwareOverhead(905);yield return null;yield return null;
            Assert.That(f.Match.Match.CurrentShot.ShotId,Is.GreaterThan(0),"same neutral C pattern is Serve only in game context");
            Assert.That(f.Match.InputAdapter.LastAction,Is.EqualTo(PlayerAction.MotionOverheadDown),"transport/adapter must preserve the physical identity");
            f.View.UpdateLive();Assert.That(f.View.Texts["input.feedback"].text,Does.Contain("위→아래 동작 입력됨"),"physical inputs must keep visible gameplay feedback");
            radio.Event("disconnected");yield return null;yield return null;Assert.That(f.InputDevices.Ready,Is.False);Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause));
        }
        [UnityTest]public IEnumerator WirePacketsMustCalibrateTestAndSaveBeforeTheyCanServeThroughTheOrdinaryAdapter()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(FrontEndController.Active?.Ready!=true&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var f=FrontEndController.Active;Assert.That(f.Ready,Is.True);var radio=new RadioFixture();var backend=new NativeControllerBackend(radio);var store=new MemoryInputDevicePreference();f.Match.InputAdapter.ConfigureDevices(backend,store);
            if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();f.MenuChoice(3);Click(f,"controllers.options");Click(f,"device.sensor");Click(f,"device.scan");
            deadline=Time.realtimeSinceStartupAsDouble+6;while((!f.View.Buttons.TryGetValue("device.connect.0",out var connect)||!connect.interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(backend.State,Is.EqualTo(ControllerLinkState.Disconnected));Click(f,"device.connect.0");yield return null;yield return null;
            Assert.That(backend.State,Is.EqualTo(ControllerLinkState.Connected));Assert.That(f.View.Buttons["device.test"].interactable,Is.False);Assert.That(f.View.Buttons["device.save"].interactable,Is.False);
            deadline=Time.realtimeSinceStartupAsDouble+5;while((backend.LinkProbePending||!f.View.Buttons["device.probe"].interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(f.View.Buttons["device.probe"].interactable,Is.True);
            Click(f,"device.probe");yield return null;yield return null;
            Assert.That(backend.LinkProbePassed,Is.True);Assert.That(backend.Calibrated,Is.False,"A real reply without sensor readiness never calibrates");
            Assert.That(f.InputDevices.Setup.CanSave,Is.False);Assert.That(f.View.Buttons["device.test"].interactable,Is.False);
            Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Keyboard));
            Click(f,"device.calibrate");yield return null;yield return null;Assert.That(backend.Calibrated,Is.True);Click(f,"device.test");
            radio.Send(3,101);radio.Send(1,102);radio.Send(8,103);yield return null;yield return null;
            Assert.That(f.InputDevices.Setup.WirelessTestPassed,Is.True);Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero,"Setup packets must not play the game");Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Keyboard));
            Click(f,"device.calibrate");Assert.That(f.InputDevices.Setup.WirelessTestPassed,Is.False,"Recalibration invalidates the test even on the same connection");Assert.That(f.InputDevices.Setup.CanSave,Is.False);yield return null;yield return null;
            Click(f,"device.test");radio.Send(3,104);radio.Send(2,105);radio.Send(8,106);yield return null;yield return null;Click(f,"device.save");Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Sensor));Assert.That(store.Load().WirelessDeviceId,Is.EqualTo("fixture"));Click(f,"back");
            f.MenuChoice(2);f.Confirm();f.Confirm();f.Confirm();deadline=Time.realtimeSinceStartupAsDouble+5;while(f.Model.Stage!=FrontEndStage.Playing&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Playing));
            radio.Send(3,107);yield return null;yield return null;Assert.That(f.Match.Match.CurrentShot.ShotId,Is.GreaterThan(0),"Decoded wire packet serves through the usual game adapter");
            radio.Event("disconnected");yield return null;yield return null;Assert.That(f.Match.InputAdapter.DeviceReady,Is.False);Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Sensor),"No silent keyboard fallback");Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause));ulong frozen=f.Match.Ball.PhysicsTicks;yield return new WaitForSeconds(.2f);Assert.That(f.Match.Ball.PhysicsTicks,Is.EqualTo(frozen));f.Back();Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause),"Cannot resume without ready input");
        }
        [UnityTest]public IEnumerator ExplicitButtonProfileTestsSavesAndPlaysWithoutClaimingImuCalibration()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(FrontEndController.Active?.Ready!=true&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var f=FrontEndController.Active;var radio=new RadioFixture{ButtonProfile=true};var backend=new NativeControllerBackend(radio);
            f.Match.InputAdapter.ConfigureDevices(backend,new MemoryInputDevicePreference());
            if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();f.MenuChoice(3);Click(f,"controllers.options");Click(f,"device.sensor");Click(f,"device.scan");
            deadline=Time.realtimeSinceStartupAsDouble+6;while((!f.View.Buttons.TryGetValue("device.connect.0",out var connect)||!connect.interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Click(f,"device.connect.0");yield return null;yield return null;yield return null;
            deadline=Time.realtimeSinceStartupAsDouble+5;while((!backend.ButtonInputMode||!f.View.Buttons["device.test"].interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(backend.ButtonInputMode,Is.True);Assert.That(backend.Calibrated,Is.False);
            Assert.That(f.InputDevices.Setup.CalibrationReady,Is.False);Assert.That(f.View.Buttons.ContainsKey("device.calibrate"),Is.False);
            Assert.That(f.InputDevices.Setup.CanSave,Is.False);Click(f,"device.test");
            radio.Send(4,701,PacketType.Button,0);radio.Send(5,702,PacketType.Button,0);yield return null;yield return null;
            Assert.That(f.InputDevices.Setup.CanSave,Is.False);radio.Send(7,703,PacketType.Button,0);yield return null;yield return null;
            Assert.That(f.InputDevices.Setup.WirelessProgress,Is.EqualTo(3));Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);
            Click(f,"device.save");Assert.That(f.InputDevices.Ready,Is.True);Click(f,"back");f.MenuChoice(0);f.Confirm();f.Confirm();f.Confirm();
            deadline=Time.realtimeSinceStartupAsDouble+6;while(f.Model.Stage!=FrontEndStage.Playing&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Playing));Assert.That(f.Match.InputAdapter.UsesSensor,Is.True);
            radio.Send(4,704,PacketType.Button,0);yield return null;yield return null;
            Assert.That(f.Match.Match.CurrentShot.ShotId,Is.GreaterThan(0));
            // Skill stock is a unit-test precondition here, never physical charging evidence.
            f.Match.Characters.Player(CourtSide.Near).Meter.Gain(100);
            radio.Send(7,705,PacketType.Button,0);yield return null;yield return null;
            Assert.That(f.Match.Characters.Player(CourtSide.Near).ActivationId,Is.EqualTo(1));
            radio.Send(7,705,PacketType.Button,0);yield return null;yield return null;
            Assert.That(f.Match.Characters.Player(CourtSide.Near).ActivationId,Is.EqualTo(1),"radio retransmission cannot spend twice");
            ushort eventId=800;bool forehand=false,backhand=false;deadline=Time.realtimeSinceStartupAsDouble+40;
            while((!forehand||!backhand)&&Time.realtimeSinceStartupAsDouble<deadline)
            {
                int before=f.Match.NearHits;byte kind=0;
                if(f.Match.ReadyToServe&&f.Match.Match.Server==CourtSide.Near)kind=4;
                else if(f.Match.CanReturn(CourtSide.Near))kind=forehand?(byte)6:(byte)5;
                if(kind!=0)radio.Send(kind,++eventId,PacketType.Button,0);
                yield return new WaitForSeconds(.08f);
                if(f.Match.NearHits>before){if(kind==5)forehand=true;if(kind==6)backhand=true;}
            }
            Assert.That(forehand&&backhand,Is.True,"button forehand and backhand pass ordinary reach/timing rules");
            radio.Event("disconnected");yield return null;yield return null;
            Assert.That(f.InputDevices.Ready,Is.False);Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause));
        }
    }
}
