using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class ControllerDeviceTests
    {
        private sealed class Backend:IControllerBackend
        {
            public bool Available=>true;public ControllerLinkState State{get;set;}=ControllerLinkState.Disconnected;
            public string Status=>State.ToString();public readonly List<ControllerDevice> Found=new List<ControllerDevice>();
            public IReadOnlyList<ControllerDevice> Devices=>Found;public string ConnectedDeviceId=>State==ControllerLinkState.Connected?"test":null;
            public ulong ConnectionEpoch{get;set;}
            public readonly Queue<ControllerEvent> Events=new Queue<ControllerEvent>();
            public double LastTick=-1;public void Tick(double now){LastTick=now;}
            public void Scan(){State=ControllerLinkState.Scanning;}public void Connect(string id){State=ControllerLinkState.Connecting;}
            public void Disconnect(){State=ControllerLinkState.Disconnected;}
            public bool TryRead(out ControllerEvent e){if(Events.Count>0){e=Events.Dequeue();return true;}e=default;return false;}
            public void ClearInput()=>Events.Clear();public void Dispose(){}
        }
        [Test]public void EveryRosterFlagAndCountryRegionExistsAndCityPickingIsGeographic()
        {
            var ids=new HashSet<int>();
            for(int i=0;i<15;i++)
            {
                var flag=CountryFlags.Get(i);Assert.That(flag,Is.Not.Null,CharacterCatalog.Ids[i]);
                Assert.That(flag.width,Is.EqualTo(512));Assert.That(flag.height,Is.EqualTo(384));Assert.That(ids.Add(flag.GetInstanceID()),Is.True);
                Assert.That(WorldCountries.Pick(WorldRoster.Position(i)),Is.EqualTo(i),CharacterCatalog.Ids[i]+" city maps to its country");
            }
            Assert.That(WorldCountries.Pick(new Vector2(.1f,.7f)),Is.EqualTo(-1),"Ocean is not a selectable country");
            Assert.That(WorldCountries.Regions.Length,Is.GreaterThan(170));
            int vertices=0;foreach(var region in WorldCountries.Regions)foreach(var ring in region.rings){vertices+=ring.xy.Length*2;foreach(float value in ring.xy)Assert.That(float.IsNaN(value)||float.IsInfinity(value),Is.False);}
            Assert.That(vertices,Is.LessThan(64000),"Borders occupy a separate bounded UI mesh");
        }
        [Test]public void SelectingSensorDiscardsBothBacklogsAndNeverUsesInactiveKeyboard()
        {
            var backend=new Backend{State=ControllerLinkState.Connected,ConnectionEpoch=1};var keyboard=new BufferedPlayerInputSource("test keyboard");
            using(var manager=new InputDeviceManager(backend,keyboard,new MemoryInputDevicePreference()))
            {
                var router=new PlayerInputRouter();manager.SourceChanged+=router.SetSource;router.SetSource(manager.Source);
                keyboard.Enqueue(new PlayerInputSample(PlayerAction.Serve,1,10));backend.Events.Enqueue(new ControllerEvent(PlayerAction.Backhand,1,1,10,.5f));
                uint before=router.SessionId;manager.Select(InputDeviceKind.Sensor);Assert.That(router.SessionId,Is.Not.EqualTo(before));
                router.Source.Poll(10,.5f,0,0);int count=0;router.Drain(10,(_,__)=>count++);Assert.That(count,Is.Zero);
                keyboard.Enqueue(new PlayerInputSample(PlayerAction.Serve,2,10));backend.Events.Enqueue(new ControllerEvent(PlayerAction.Forehand,2,1,10,.7f,-.2f,.3f));
                manager.Tick(10);router.Source.Poll(10,.5f,0,0);router.Drain(10,(sample,session)=>{count++;Assert.That(sample.Action,Is.EqualTo(PlayerAction.Forehand));Assert.That(sample.PracticeOpponent,Is.False);Assert.That(sample.Power01,Is.EqualTo(.7f));});
                Assert.That(count,Is.EqualTo(1));manager.Select(InputDeviceKind.Keyboard);Assert.That(keyboard.TryRead(out _),Is.False);
            }
        }
        [Test]public void DisconnectAndReconnectInvalidateOldEpochAndCreateFreshInputNamespace()
        {
            var backend=new Backend{State=ControllerLinkState.Connected,ConnectionEpoch=1};var prefs=new MemoryInputDevicePreference();prefs.Save(InputDeviceKind.Sensor);
            using(var manager=new InputDeviceManager(backend,new BufferedPlayerInputSource("keyboard"),prefs))
            {
                var router=new PlayerInputRouter();manager.SourceChanged+=router.SetSource;router.SetSource(manager.Source);
                void Send(ulong epoch,uint seq,double stamp){backend.Events.Enqueue(new ControllerEvent(PlayerAction.Forehand,seq,epoch,stamp,.5f));router.Source.Poll(10,.5f,0,0);}
                int count=0;Send(1,1,10);router.Drain(10,(_,__)=>count++);uint session=router.SessionId;
                backend.State=ControllerLinkState.Disconnected;manager.Tick(10);Assert.That(manager.Ready,Is.False);Assert.That(router.SessionId,Is.Not.EqualTo(session));
                backend.State=ControllerLinkState.Connected;backend.ConnectionEpoch=2;manager.Tick(10);
                Send(1,2,10);Send(2,1,10);router.Drain(10,(_,__)=>count++);Assert.That(count,Is.EqualTo(2),"Reused sequence is valid only in the new PC session; old epoch is ignored");
            }
        }
        [Test]public void SensorIngressKeepsValidationAndRejectsDeviceRolesAdministrativeActionsAndDuplicates()
        {
            var b=new Backend{State=ControllerLinkState.Connected,ConnectionEpoch=3};var source=new SensorPlayerInputSource(b);var router=new PlayerInputRouter();router.SetSource(source);
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Forehand,1,3,10,.6f));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Backhand,1,3,10,.6f));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Serve,2,3,9,.6f));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Serve,3,3,11,.6f));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Forehand,4,3,10,float.NaN));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Restart,5,3,10,.5f));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.SelectCharacter,6,3,10,.5f));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Skill,7,2,10,.5f));
            source.Poll(10,.5f,0,0);int count=0;router.Drain(10,(_,__)=>count++);
            Assert.That(count,Is.EqualTo(1));Assert.That(router.Rejected,Is.EqualTo(4));
        }
        [Test]public void UnavailableBackendDoesNotInventDevicesOrFallBackToKeyboard()
        {
            using(var m=new InputDeviceManager(new UnavailableControllerBackend(),new BufferedPlayerInputSource("keyboard"),new MemoryInputDevicePreference()))
            {
                m.Select(InputDeviceKind.Sensor);m.Scan();m.Connect("fabricated");m.Tick(1);
                Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Sensor));Assert.That(m.Ready,Is.False);Assert.That(m.Available,Is.False);
                Assert.That(m.Devices,Is.Empty);Assert.That(m.Source,Is.TypeOf<SensorPlayerInputSource>());Assert.That(m.State,Is.EqualTo(ControllerLinkState.Unavailable));
            }
        }
        [Test]public void DiscoveryCancellationNeverTicksWithAFabricatedClockOrImplicitlySelectsADevice()
        {
            var b=new Backend();b.Found.Add(new ControllerDevice("test","TEST ONLY"));
            using(var m=new InputDeviceManager(b,new BufferedPlayerInputSource("keyboard"),new MemoryInputDevicePreference()))
            {
                m.Tick(12);m.Scan();Assert.That(m.State,Is.EqualTo(ControllerLinkState.Scanning));m.Disconnect();Assert.That(m.State,Is.EqualTo(ControllerLinkState.Disconnected));
                m.Connect("unknown");Assert.That(m.State,Is.EqualTo(ControllerLinkState.Disconnected));m.Connect("test");Assert.That(m.State,Is.EqualTo(ControllerLinkState.Connecting));
                Assert.That(b.LastTick,Is.EqualTo(12));Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Keyboard));m.Disconnect();
            }
        }
        [Test]public void DevicePreferenceRoundTripsAndInvalidFilesDefaultToKeyboard()
        {
            string dir=Path.Combine(Path.GetTempPath(),"digital-tennis-device-"+Guid.NewGuid());string path=Path.Combine(dir,"input.json");
            try
            {
                var store=new FileInputDevicePreference(path);Assert.That(store.Load(),Is.EqualTo(InputDeviceKind.Keyboard));
                store.Save(InputDeviceKind.Sensor);Assert.That(new FileInputDevicePreference(path).Load(),Is.EqualTo(InputDeviceKind.Sensor));
                store.Save(InputDeviceKind.Keyboard);Assert.That(store.Load(),Is.EqualTo(InputDeviceKind.Keyboard));
                File.WriteAllText(path,"{ broken");Assert.That(store.Load(),Is.EqualTo(InputDeviceKind.Keyboard));
                File.WriteAllText(path,"{\"version\":99,\"selected\":\"Sensor\"}");Assert.That(store.Load(),Is.EqualTo(InputDeviceKind.Keyboard));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test]public void MenuKeyboardNeverGeneratesRacketActions()
        {
            var keys=new HashSet<KeyCode>{KeyCode.Space,KeyCode.Q,KeyCode.W,KeyCode.K,KeyCode.Return,KeyCode.Escape};var source=new KeyboardMenuInputSource(key=>keys.Remove(key));
            source.Poll(10,.5f,0,0);var actions=new List<PlayerAction>();while(source.TryRead(out var sample))actions.Add(sample.Action);
            Assert.That(actions,Is.EquivalentTo(new[]{PlayerAction.MenuConfirm,PlayerAction.MenuBack}));
        }
    }
}
