using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class ControllerSettingsSceneTests
    {
        private sealed class Backend:IControllerBackend
        {
            public bool Available=>true;public ControllerLinkState State{get;set;}=ControllerLinkState.Disconnected;
            public string Status=>"TEST ONLY";public IReadOnlyList<ControllerDevice> Devices=>new[]{new ControllerDevice("test","TEST ONLY")};
            public string ConnectedDeviceId=>State==ControllerLinkState.Connected?"test":null;public ulong ConnectionEpoch{get;set;}=1;
            public readonly Queue<ControllerEvent> Events=new Queue<ControllerEvent>();
            public void Tick(double now){}public void Scan(){State=ControllerLinkState.Scanning;}public void Connect(string id){State=ControllerLinkState.Connecting;}public void Disconnect(){State=ControllerLinkState.Disconnected;ClearInput();}
            public bool TryRead(out ControllerEvent sample){if(Events.Count>0){sample=Events.Dequeue();return true;}sample=default;return false;}
            public void ClearInput()=>Events.Clear();public void Dispose(){}
        }
        private sealed class Store:IInputDevicePreference
        {
            public int Saves;public InputDeviceConfiguration Value=new InputDeviceConfiguration();
            public InputDeviceConfiguration Load()=>Value;public void Save(InputDeviceConfiguration c){Saves++;Value=c;}
        }
        private static IEnumerator Ready()
        {yield return SceneManager.LoadSceneAsync("FrontEnd");double end=Time.realtimeSinceStartupAsDouble+10;while(FrontEndController.Active?.Ready!=true&&Time.realtimeSinceStartupAsDouble<end)yield return null;Assert.That(FrontEndController.Active.Ready,Is.True);}
        private static void Click(FrontEndController f,string id)=>Assert.That(f.View.Invoke(id),Is.True,id);
        private static void Settings(FrontEndController f)
        {if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();f.MenuChoice(3);Click(f,"controllers.options");}
        private static IEnumerator Play(FrontEndController f)
        {
            f.MenuChoice(2);f.Confirm();f.Confirm();f.Confirm();double end=Time.realtimeSinceStartupAsDouble+5;
            while(f.Model.Stage!=FrontEndStage.Playing&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Playing));
        }
        [UnityTest]public IEnumerator CategoryAndDetailsAreContextualAndOnlySaveExitsToTheParent()
        {
            yield return Ready();var f=FrontEndController.Active;Settings(f);
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.InputControllers));Assert.That(f.View.Buttons.ContainsKey("device.scan"),Is.False);
            Click(f,"device.keyboard");Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.KeyboardSettings));
            Assert.That(f.View.Buttons.ContainsKey("device.scan"),Is.False);Assert.That(f.View.Buttons.ContainsKey("device.disconnect"),Is.False);Assert.That(f.View.Texts.ContainsKey("device.link"),Is.False);
            f.Match.InputAdapter.Press(PlayerAction.Serve);Assert.That(f.InputDevices.Setup.BindingCapture,Is.EqualTo(-1));Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);
            Click(f,"back");Click(f,"device.sensor");Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.WirelessSettings));
            Assert.That(f.View.Buttons["device.scan"].interactable,Is.False);Assert.That(f.View.Buttons["device.save"].interactable,Is.False);Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Keyboard));
            Click(f,"back");Click(f,"device.keyboard");Click(f,"device.save");Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Options));Assert.That(f.ControllerNotice,Does.Contain("저장"));
            Click(f,"back");f.MenuChoice(0);foreach(int i in new[]{0,1,11}){Click(f,"country."+CharacterCatalog.Ids[i]);Assert.That(f.View.Flags["selection.flag"].texture,Is.SameAs(CountryFlags.Get(i)));}
        }
        [UnityTest]public IEnumerator KeyboardDraftCancelAndSaveControlTheActualGameplayBindingAndHud()
        {
            yield return Ready();var f=FrontEndController.Active;var store=new Store();var keys=new HashSet<KeyCode>();var setupKeys=new HashSet<KeyCode>();
            f.Match.InputAdapter.ConfigureDevices(new UnavailableControllerBackend(),store,new KeyboardPlayerInputSource(k=>keys.Remove(k)),setupKeys:k=>setupKeys.Remove(k));Settings(f);
            Click(f,"device.keyboard");Click(f,"key.bind.0");setupKeys.Add(KeyCode.T);yield return null;yield return null;
            Assert.That(f.InputDevices.Setup.Keyboard[0],Is.EqualTo(KeyCode.T));Assert.That(f.InputDevices.SavedKeyboard[0],Is.EqualTo(KeyCode.Space));Assert.That(store.Saves,Is.Zero);
            Click(f,"back");Assert.That(f.InputDevices.SavedKeyboard[0],Is.EqualTo(KeyCode.Space));
            Click(f,"device.keyboard");Click(f,"key.bind.0");setupKeys.Add(KeyCode.T);yield return null;yield return null;Click(f,"device.save");
            Assert.That(store.Saves,Is.EqualTo(1));Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Options));Click(f,"back");yield return Play(f);
            Assert.That(f.View.Texts["input.hint"].text,Does.Contain("T"));keys.Add(KeyCode.Space);yield return null;yield return null;Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);
            keys.Add(KeyCode.T);yield return null;yield return null;Assert.That(f.Match.Match.CurrentShot.ShotId,Is.GreaterThan(0));
            int onlineCalls=0;f.Match.InputAdapter.OnlineRoute=(sample,session)=>{Assert.That(sample.Action,Is.EqualTo(PlayerAction.Forehand));Assert.That(session,Is.GreaterThan(0));onlineCalls++;return null;};
            keys.Add(KeyCode.Q);yield return null;yield return null;Assert.That(onlineCalls,Is.EqualTo(1),"Saved keyboard input retains the ordinary online route");
        }
        [UnityTest]public IEnumerator KeyboardTestAndBindingEscapeCannotConfirmMenusSaveOrStartAShot()
        {
            yield return Ready();var f=FrontEndController.Active;var store=new Store();var keys=new HashSet<KeyCode>();
            f.Match.InputAdapter.ConfigureDevices(new UnavailableControllerBackend(),store,setupKeys:k=>keys.Remove(k));Settings(f);int onlineCalls=0;f.Match.InputAdapter.OnlineRoute=(_,__)=>{onlineCalls++;return null;};Click(f,"device.keyboard");Click(f,"device.test");
            keys.UnionWith(new[]{KeyCode.Space,KeyCode.Q});yield return null;yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.KeyboardSettings));Assert.That(f.InputDevices.Setup.KeyboardStep(0)&&f.InputDevices.Setup.KeyboardStep(1),Is.True);
            Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);Assert.That(f.Match.InputAdapter.Delivered,Is.Zero);Assert.That(onlineCalls,Is.Zero);Assert.That(store.Saves,Is.Zero);
            Click(f,"key.bind.0");keys.Add(KeyCode.Escape);yield return null;yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.KeyboardSettings));Assert.That(f.InputDevices.Setup.BindingCapture,Is.EqualTo(-1));
            f.Match.InputAdapter.Press(PlayerAction.MenuBack);Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.InputControllers));Assert.That(store.Saves,Is.Zero);
        }
        [UnityTest]public IEnumerator WirelessDiscoveryPairingAndTestAreRequiredBeforeSavedSensorCanPlay()
        {
            yield return Ready();var f=FrontEndController.Active;var backend=new Backend();var store=new Store();var keys=new HashSet<KeyCode>();var menuKeys=new HashSet<KeyCode>();
            f.Match.InputAdapter.ConfigureDevices(backend,store,new KeyboardPlayerInputSource(k=>keys.Remove(k)),k=>menuKeys.Remove(k));Settings(f);Click(f,"device.sensor");
            Click(f,"device.scan");Assert.That(backend.State,Is.EqualTo(ControllerLinkState.Scanning));backend.State=ControllerLinkState.Disconnected;yield return null;yield return null;Click(f,"device.connect.0");
            backend.State=ControllerLinkState.Pairing;yield return null;yield return null;Assert.That(f.View.Texts["device.link"].text,Does.Contain("페어링"));
            backend.State=ControllerLinkState.Connected;yield return null;yield return null;Click(f,"device.test");
            void Emit(PlayerAction action,uint seq){backend.Events.Enqueue(new ControllerEvent(action,seq,backend.ConnectionEpoch,Time.realtimeSinceStartupAsDouble,.5f));}
            Emit(PlayerAction.Serve,1);yield return null;yield return null;Assert.That(f.View.Buttons["device.save"].interactable,Is.False);
            Emit(PlayerAction.Forehand,2);Emit(PlayerAction.Skill,3);yield return null;yield return null;
            Assert.That(f.View.Buttons["device.save"].interactable,Is.True);Assert.That(store.Saves,Is.Zero);Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Keyboard));Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);
            Click(f,"device.save");Assert.That(store.Saves,Is.EqualTo(1));Assert.That(store.Value.WirelessDeviceId,Is.EqualTo("test"));Click(f,"back");yield return Play(f);
            keys.UnionWith(new[]{KeyCode.Space,KeyCode.Q,KeyCode.W});f.Match.InputAdapter.Press(PlayerAction.Serve);yield return null;yield return null;Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);
            Emit(PlayerAction.Serve,4);yield return null;yield return null;Assert.That(f.Match.Match.CurrentShot.ShotId,Is.GreaterThan(0));
            backend.State=ControllerLinkState.Disconnected;yield return null;yield return null;Assert.That(f.Match.InputAdapter.DeviceReady,Is.False);Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Sensor));
            menuKeys.Add(KeyCode.Escape);yield return null;yield return null;Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause));Click(f,"controllers.options");Click(f,"device.keyboard");
            Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Sensor));Click(f,"back");Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Sensor));
            Click(f,"device.keyboard");Click(f,"device.save");keys.Clear();Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause));Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Keyboard));
        }
    }
}
