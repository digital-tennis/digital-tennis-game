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
            public bool Available=>true;
            public ControllerLinkState State{get;set;}=ControllerLinkState.Disconnected;
            public string Status=>State.ToString();
            public readonly List<ControllerDevice> Found=new List<ControllerDevice>{new ControllerDevice("test","TEST ONLY")};
            public IReadOnlyList<ControllerDevice> Devices=>Found;
            public string Id="test";
            public string ConnectedDeviceId=>State==ControllerLinkState.Connected?Id:null;
            public ulong ConnectionEpoch{get;set;}
            public readonly Queue<ControllerEvent> Events=new Queue<ControllerEvent>();
            public double LastTick=-1;
            public void Tick(double now){LastTick=now;}
            public void Scan(){State=ControllerLinkState.Scanning;}
            public void Connect(string id){Id=id;State=ControllerLinkState.Connecting;}
            public void Disconnect(){State=ControllerLinkState.Disconnected;ClearInput();}
            public bool TryRead(out ControllerEvent e){if(Events.Count>0){e=Events.Dequeue();return true;}e=default;return false;}
            public void ClearInput()=>Events.Clear();public void Dispose(){}
        }
        private sealed class Store:IInputDevicePreference
        {
            public InputDeviceConfiguration Value=new InputDeviceConfiguration();public int Saves;public bool Fail;
            public InputDeviceConfiguration Load()=>Value;
            public void Save(InputDeviceConfiguration value){Saves++;if(Fail)throw new IOException("TEST failure");Value=value;}
        }
        private static void ConnectedSetup(InputDeviceManager m,Backend b,double now=10)
        {
            m.Tick(now);m.BeginSetup(InputDeviceKind.Sensor);m.Setup.Scan();Assert.That(b.State,Is.EqualTo(ControllerLinkState.Scanning));
            b.State=ControllerLinkState.Disconnected;m.Tick(now);m.Setup.Connect("test");Assert.That(b.State,Is.EqualTo(ControllerLinkState.Connecting));
            b.State=ControllerLinkState.Pairing;m.Tick(now);Assert.That(m.Setup.CanSave,Is.False);
            b.State=ControllerLinkState.Connected;b.ConnectionEpoch++;m.Tick(now);m.Setup.StartTest();
        }
        private static void Emit(InputDeviceManager m,Backend b,PlayerAction action,uint seq,double now=10,ulong? epoch=null,float power=.5f,double? stamp=null)
        {b.Events.Enqueue(new ControllerEvent(action,seq,epoch??b.ConnectionEpoch,stamp??now,power));m.Tick(now);}
        private static void PassTest(InputDeviceManager m,Backend b,double now=10)
        {m.Setup.StartTest();Emit(m,b,PlayerAction.Serve,10,now);Emit(m,b,PlayerAction.Forehand,11,now);Emit(m,b,PlayerAction.Skill,12,now);Assert.That(m.Setup.WirelessTestPassed,Is.True);}
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

        [Test]public void OpeningAndCancellingDraftsNeverSavesOrChangesGameplaySelection()
        {
            var b=new Backend();var store=new Store();var keys=new HashSet<KeyCode>();
            using(var m=new InputDeviceManager(b,new KeyboardPlayerInputSource(),store,k=>keys.Remove(k)))
            {
                m.BeginSetup(InputDeviceKind.Keyboard);m.Setup.CaptureBinding(0);keys.Add(KeyCode.T);m.Tick(10);
                Assert.That(m.Setup.Keyboard[0],Is.EqualTo(KeyCode.T));Assert.That(m.SavedKeyboard[0],Is.EqualTo(KeyCode.Space));Assert.That(store.Saves,Is.Zero);
                m.CancelSetup();Assert.That(m.SavedKeyboard[0],Is.EqualTo(KeyCode.Space));
                m.BeginSetup(InputDeviceKind.Sensor);Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Keyboard));m.Setup.Scan();m.CancelSetup();
                Assert.That(b.State,Is.EqualTo(ControllerLinkState.Disconnected));Assert.That(store.Saves,Is.Zero);Assert.That(m.Ready,Is.True);
            }
        }
        [Test]public void OnlySuccessfulSaveAppliesDraftBindingsAndClearsOldBacklogs()
        {
            var b=new Backend();var store=new Store();var keys=new HashSet<KeyCode>();var keyboard=new KeyboardPlayerInputSource(k=>keys.Remove(k));
            using(var m=new InputDeviceManager(b,keyboard,store,k=>keys.Remove(k)))
            {
                var router=new PlayerInputRouter();m.SourceChanged+=router.SetSource;router.SetSource(m.Source);uint before=router.SessionId;
                m.BeginSetup(InputDeviceKind.Keyboard);m.Setup.CaptureBinding(0);keys.Add(KeyCode.T);m.Tick(10);
                keyboard.Enqueue(new PlayerInputSample(PlayerAction.Serve,999,10));store.Fail=true;
                Assert.That(m.SaveSetup(),Is.False);Assert.That(m.SavedKeyboard[0],Is.EqualTo(KeyCode.Space));Assert.That(m.Setup,Is.Not.Null);
                m.Tick(11);Assert.That(m.Setup.Error,Does.Contain("저장하지 못"));
                store.Fail=false;Assert.That(m.SaveSetup(),Is.True);Assert.That(m.Setup,Is.Null);Assert.That(router.SessionId,Is.Not.EqualTo(before));
                Assert.That(keyboard.TryRead(out _),Is.False);Assert.That(store.Value.Keyboard[0],Is.EqualTo(KeyCode.T));
                keys.UnionWith(new[]{KeyCode.Space,KeyCode.T});m.Source.Poll(11,.5f,0,0);int count=0;router.Drain(11,(sample,_)=>{Assert.That(sample.Action,Is.EqualTo(PlayerAction.Serve));count++;});Assert.That(count,Is.EqualTo(1));
            }
        }
        [Test]public void BindingCaptureRejectsReservedAndDuplicateKeysAndEscapeCancelsOnlyCapture()
        {
            var keys=new HashSet<KeyCode>();using(var m=new InputDeviceManager(new Backend(),new KeyboardPlayerInputSource(),new Store(),k=>keys.Remove(k)))
            {
                m.BeginSetup(InputDeviceKind.Keyboard);m.Setup.CaptureBinding(0);keys.Add(KeyCode.Escape);m.Tick(10);
                Assert.That(m.Setup.BindingCapture,Is.EqualTo(-1));Assert.That(m.Setup.SuppressFrameInput,Is.True);Assert.That(m.Setup,Is.Not.Null);
                m.Setup.CaptureBinding(0);keys.Add(KeyCode.Return);m.Tick(11);Assert.That(m.Setup.BindingCapture,Is.Zero);Assert.That(m.Setup.CanSave,Is.False);
                keys.Add(KeyCode.Q);m.Tick(12);Assert.That(m.Setup.Error,Does.Contain("이미"));Assert.That(m.Setup.Keyboard[0],Is.EqualTo(KeyCode.Space));
                keys.Add(KeyCode.T);m.Tick(13);Assert.That(m.Setup.Keyboard[0],Is.EqualTo(KeyCode.T));m.Setup.RestoreKeyboard();Assert.That(m.Setup.Keyboard[0],Is.EqualTo(KeyCode.Space));
            }
        }
        [Test]public void KeyboardTestConsumesSetupInputWithoutSelectingOrSaving()
        {
            var keys=new HashSet<KeyCode>();var store=new Store();using(var m=new InputDeviceManager(new Backend(),new KeyboardPlayerInputSource(),store,k=>keys.Remove(k)))
            {
                m.BeginSetup(InputDeviceKind.Keyboard);m.Setup.StartTest();keys.UnionWith(new[]{KeyCode.Space,KeyCode.Q});m.Tick(10);
                Assert.That(m.Setup.SuppressFrameInput,Is.True);Assert.That(m.Setup.KeyboardStep(0)&&m.Setup.KeyboardStep(1),Is.True);
                Assert.That(store.Saves,Is.Zero);Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Keyboard));
            }
        }
        [Test]public void WirelessSaveRequiresCurrentConnectionAndAllThreeValidatedTestActions()
        {
            var b=new Backend();var store=new Store();using(var m=new InputDeviceManager(b,new KeyboardPlayerInputSource(),store))
            {
                ConnectedSetup(m,b);Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Keyboard));Assert.That(m.SaveSetup(),Is.False);
                Emit(m,b,PlayerAction.Serve,1);Assert.That(m.Setup.WirelessProgress,Is.EqualTo(1));
                Emit(m,b,PlayerAction.Skill,1);Emit(m,b,PlayerAction.Skill,2,epoch:0);Emit(m,b,PlayerAction.Skill,3,stamp:8);Emit(m,b,PlayerAction.Skill,4,power:float.NaN);
                Assert.That(m.Setup.WirelessProgress,Is.EqualTo(1));Assert.That(m.SaveSetup(),Is.False);
                Emit(m,b,PlayerAction.Backhand,5);Emit(m,b,PlayerAction.Skill,6);Assert.That(m.Setup.WirelessTestPassed,Is.True);
                Assert.That(m.SaveSetup(),Is.True);Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Sensor));Assert.That(m.Ready,Is.True);
                Assert.That(store.Saves,Is.EqualTo(1));Assert.That(store.Value.WirelessDeviceId,Is.EqualTo("test"));Assert.That(m.Source,Is.TypeOf<SensorPlayerInputSource>());
            }
        }
        [Test]public void InputBeforeStartingWirelessTestDoesNotCountAndCannotLeakAfterSave()
        {
            var b=new Backend();using(var m=new InputDeviceManager(b,new BufferedPlayerInputSource("keyboard"),new Store()))
            {
                ConnectedSetup(m,b);b.Events.Enqueue(new ControllerEvent(PlayerAction.Skill,9,b.ConnectionEpoch,10,.5f));m.Setup.StartTest();m.Tick(10);
                Assert.That(m.Setup.WirelessProgress,Is.Zero);PassTest(m,b);Assert.That(m.SaveSetup(),Is.True);
                m.Source.Poll(10,.5f,0,0);Assert.That(m.Source.TryRead(out _),Is.False);
            }
        }
        [Test]public void ConnectionLossOrReplacementInvalidatesPassedTestAndSavedIdentityGuardsGameplay()
        {
            var b=new Backend();var store=new Store();using(var m=new InputDeviceManager(b,new KeyboardPlayerInputSource(),store))
            {
                ConnectedSetup(m,b);PassTest(m,b);b.State=ControllerLinkState.Disconnected;m.Tick(10);Assert.That(m.Setup.CanSave,Is.False);
                b.State=ControllerLinkState.Connected;b.ConnectionEpoch++;m.Tick(10);Assert.That(m.Setup.WirelessTestPassed,Is.False);
                PassTest(m,b);Assert.That(m.SaveSetup(),Is.True);b.Id="other";m.Tick(10);Assert.That(m.Ready,Is.False);Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Sensor));
                b.Id="test";m.Tick(10);m.BeginSetup(InputDeviceKind.Sensor);PassTest(m,b);m.CancelSetup();Assert.That(b.State,Is.EqualTo(ControllerLinkState.Connected),"Cancel does not destroy a pre-existing active connection");
            }
        }
        [Test]public void CancelNewWirelessConnectionDiscardsItAndPreservesSavedKeyboard()
        {
            var b=new Backend();var store=new Store();using(var m=new InputDeviceManager(b,new KeyboardPlayerInputSource(),store))
            {
                ConnectedSetup(m,b);PassTest(m,b);m.CancelSetup();Assert.That(b.State,Is.EqualTo(ControllerLinkState.Disconnected));Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Keyboard));Assert.That(store.Saves,Is.Zero);
            }
        }
        [Test]public void UnavailableWirelessCannotBeSavedOrSilentlyReplaceKeyboard()
        {
            var store=new Store();using(var m=new InputDeviceManager(new UnavailableControllerBackend(),new KeyboardPlayerInputSource(),store))
            {
                m.BeginSetup(InputDeviceKind.Sensor);m.Setup.Scan();m.Setup.Connect("fake");m.Setup.StartTest();m.Tick(10);
                Assert.That(m.SaveSetup(),Is.False);Assert.That(m.Available,Is.False);Assert.That(m.Devices,Is.Empty);Assert.That(m.Selected,Is.EqualTo(InputDeviceKind.Keyboard));Assert.That(m.Ready,Is.True);Assert.That(store.Saves,Is.Zero);
            }
        }
        [Test]public void DiscoveryTimeoutCanBeRetriedAndCommandsNeverTickWithFabricatedClock()
        {
            var b=new Backend();using(var m=new InputDeviceManager(b,new KeyboardPlayerInputSource(),new Store()))
            {
                m.Tick(12);m.BeginSetup(InputDeviceKind.Sensor);m.Setup.Scan();Assert.That(b.LastTick,Is.EqualTo(12));m.Tick(33);
                Assert.That(b.State,Is.EqualTo(ControllerLinkState.Disconnected));Assert.That(m.Setup.Error,Does.Contain("시간"));
                m.Setup.Scan();Assert.That(m.Setup.Error,Is.Null);m.Setup.Disconnect();m.Setup.Connect("unknown");Assert.That(b.State,Is.EqualTo(ControllerLinkState.Disconnected));m.CancelSetup();
            }
        }
        [Test]public void DisconnectReconnectInvalidateOldEpochAndResetInputNamespace()
        {
            var b=new Backend{State=ControllerLinkState.Connected,ConnectionEpoch=1};var store=new Store{Value=new InputDeviceConfiguration(InputDeviceKind.Sensor,wirelessDeviceId:"test")};
            using(var m=new InputDeviceManager(b,new KeyboardPlayerInputSource(),store))
            {
                var router=new PlayerInputRouter();m.SourceChanged+=router.SetSource;router.SetSource(m.Source);int count=0;
                void Send(ulong epoch,uint seq){b.Events.Enqueue(new ControllerEvent(PlayerAction.Forehand,seq,epoch,10,.5f));m.Source.Poll(10,.5f,0,0);router.Drain(10,(_,__)=>count++);}
                Send(1,1);uint before=router.SessionId;b.State=ControllerLinkState.Disconnected;m.Tick(10);Assert.That(m.Ready,Is.False);Assert.That(router.SessionId,Is.Not.EqualTo(before));
                b.State=ControllerLinkState.Connected;b.ConnectionEpoch=2;m.Tick(10);Send(1,2);Send(2,1);Assert.That(count,Is.EqualTo(2));
            }
        }
        [Test]public void SensorIngressPreservesDuplicateStaleFutureNaNAndActionGuards()
        {
            var b=new Backend{State=ControllerLinkState.Connected,ConnectionEpoch=3};var source=new SensorPlayerInputSource(b);var router=new PlayerInputRouter();router.SetSource(source);
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Forehand,1,3,10,.6f));b.Events.Enqueue(new ControllerEvent(PlayerAction.Backhand,1,3,10,.6f));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Serve,2,3,9,.6f));b.Events.Enqueue(new ControllerEvent(PlayerAction.Serve,3,3,11,.6f));b.Events.Enqueue(new ControllerEvent(PlayerAction.Forehand,4,3,10,float.NaN));
            b.Events.Enqueue(new ControllerEvent(PlayerAction.Restart,5,3,10,.5f));b.Events.Enqueue(new ControllerEvent(PlayerAction.SelectCharacter,6,3,10,.5f));b.Events.Enqueue(new ControllerEvent(PlayerAction.Skill,7,2,10,.5f));
            source.Poll(10,.5f,0,0);int count=0;router.Drain(10,(_,__)=>count++);Assert.That(count,Is.EqualTo(1));Assert.That(router.Rejected,Is.EqualTo(4));
        }
        [Test]public void FileConfigurationRoundTripsBindingsAndDeviceAndMigratesVersionOne()
        {
            string dir=Path.Combine(Path.GetTempPath(),"dt-controller-"+Guid.NewGuid()),path=Path.Combine(dir,"input.json");
            try
            {
                var store=new FileInputDevicePreference(path);Assert.That(store.Load().Selected,Is.EqualTo(InputDeviceKind.Keyboard));
                new KeyboardBindingSet().TryChange(0,KeyCode.T,out var keys,out _);store.Save(new InputDeviceConfiguration(InputDeviceKind.Sensor,keys,"test"));
                var loaded=new FileInputDevicePreference(path).Load();Assert.That(loaded.Selected,Is.EqualTo(InputDeviceKind.Sensor));Assert.That(loaded.Keyboard[0],Is.EqualTo(KeyCode.T));Assert.That(loaded.WirelessDeviceId,Is.EqualTo("test"));
                File.WriteAllText(path,"{\"version\":1,\"selected\":\"Sensor\"}");loaded=store.Load();Assert.That(loaded.Selected,Is.EqualTo(InputDeviceKind.Sensor));Assert.That(loaded.WirelessDeviceId,Is.Empty);Assert.That(loaded.Keyboard[0],Is.EqualTo(KeyCode.Space));
                File.WriteAllText(path,"{ broken");Assert.That(store.Load().Selected,Is.EqualTo(InputDeviceKind.Keyboard));
                File.WriteAllText(path,"{\"version\":99,\"selected\":\"Sensor\"}");Assert.That(store.Load().Selected,Is.EqualTo(InputDeviceKind.Keyboard));
                store.Save(new InputDeviceConfiguration());Assert.That(store.Load().Selected,Is.EqualTo(InputDeviceKind.Keyboard));Assert.That(File.Exists(path+".pending"),Is.False);
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test]public void KeypadBindingsAreValidAndNativeValidationPreferencesAreIsolated()
        {
            Assert.That(new KeyboardBindingSet().TryChange(0,KeyCode.Keypad1,out var keys,out _),Is.True);Assert.That(KeyboardBindingSet.KeyName(keys[0]),Is.EqualTo("Num 1"));
            string production=FileInputDevicePreference.PlayerPath("/test-data","com.digitaltennis.game"),validation=FileInputDevicePreference.PlayerPath("/test-data","com.digitaltennis.validation");
            Assert.That(production,Is.Not.EqualTo(validation));Assert.That(validation,Does.Contain("ControllerValidation"));
        }
        [Test]public void MenuKeyboardNeverGeneratesRacketActions()
        {
            var keys=new HashSet<KeyCode>{KeyCode.Space,KeyCode.Q,KeyCode.W,KeyCode.K,KeyCode.Return,KeyCode.Escape};var source=new KeyboardMenuInputSource(k=>keys.Remove(k));
            source.Poll(10,.5f,0,0);var actions=new List<PlayerAction>();while(source.TryRead(out var sample))actions.Add(sample.Action);Assert.That(actions,Is.EquivalentTo(new[]{PlayerAction.MenuConfirm,PlayerAction.MenuBack}));
        }
    }
}
