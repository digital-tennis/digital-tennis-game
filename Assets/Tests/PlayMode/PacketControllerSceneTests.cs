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
            public bool Available=>true;public ulong Operation;private ushort sequence;
            public event Action<BleTransportEvent> Received;
            public void Scan(ulong operation){Operation=operation;Event("device","fixture");}
            public void Connect(string id,ulong operation){Operation=operation;Event("pairing");Event("connected",id);}
            public void Write(byte[] data,ulong operation){Assert.That(PacketCodec.Decode(data,out var command),Is.EqualTo(DecodeError.None));if(command.Type==PacketType.LinkProbe){Send(0,command.EventId,PacketType.LinkReply,0);return;}Assert.That(command.Type,Is.EqualTo(PacketType.Calibrate));Send(0,command.EventId,PacketType.Ack);}
            public void Disconnect(){}public void Dispose(){}
            public void Event(string kind,string id=null)=>Received?.Invoke(new BleTransportEvent{Kind=kind,Id=id,Name="MOCK PACKET FIXTURE",Operation=Operation,ReceivedAt=Time.realtimeSinceStartupAsDouble});
            public void Send(byte kind,ushort eventId,PacketType type=PacketType.Swing,byte flags=3)
            {Received?.Invoke(new BleTransportEvent{Kind="packet",Operation=Operation,ReceivedAt=Time.realtimeSinceStartupAsDouble,Packet=PacketCodec.Encode(new ControllerPacket(2,type,flags,++sequence,100,eventId,kind,type==PacketType.Swing?(byte)70:(byte)0,0,0,255))});}
        }
        private static void Click(FrontEndController f,string id)=>Assert.That(f.View.Invoke(id),Is.True,id);
        [UnityTest]public IEnumerator WirePacketsMustCalibrateTestAndSaveBeforeTheyCanServeThroughTheOrdinaryAdapter()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(FrontEndController.Active?.Ready!=true&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var f=FrontEndController.Active;Assert.That(f.Ready,Is.True);var radio=new RadioFixture();var backend=new NativeControllerBackend(radio);var store=new MemoryInputDevicePreference();f.Match.InputAdapter.ConfigureDevices(backend,store);
            if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();f.MenuChoice(3);Click(f,"controllers.options");Click(f,"device.sensor");Click(f,"device.scan");
            deadline=Time.realtimeSinceStartupAsDouble+6;while((!f.View.Buttons.TryGetValue("device.connect.0",out var connect)||!connect.interactable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(backend.State,Is.EqualTo(ControllerLinkState.Disconnected));Click(f,"device.connect.0");yield return null;yield return null;
            Assert.That(backend.State,Is.EqualTo(ControllerLinkState.Connected));Assert.That(f.View.Buttons["device.test"].interactable,Is.False);Assert.That(f.View.Buttons["device.save"].interactable,Is.False);
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
    }
}
