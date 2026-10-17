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
            public bool Available=>true;public ControllerLinkState State{get;set;}=ControllerLinkState.Connected;
            public string Status=>"TEST ONLY";public IReadOnlyList<ControllerDevice> Devices=>new[]{new ControllerDevice("test","TEST ONLY")};
            public string ConnectedDeviceId=>"test";public ulong ConnectionEpoch{get;set;}=1;
            public readonly Queue<ControllerEvent> Events=new Queue<ControllerEvent>();
            public void Tick(double now){}public void Scan(){State=ControllerLinkState.Scanning;}public void Connect(string id){State=ControllerLinkState.Connecting;}public void Disconnect(){State=ControllerLinkState.Disconnected;}
            public bool TryRead(out ControllerEvent sample){if(Events.Count>0){sample=Events.Dequeue();return true;}sample=default;return false;}
            public void ClearInput()=>Events.Clear();public void Dispose(){}
        }
        private static IEnumerator Ready()
        {yield return SceneManager.LoadSceneAsync("FrontEnd");double end=Time.realtimeSinceStartupAsDouble+10;while(FrontEndController.Active?.Ready!=true&&Time.realtimeSinceStartupAsDouble<end)yield return null;Assert.That(FrontEndController.Active.Ready,Is.True);}
        private static void Click(FrontEndController f,string id)=>Assert.That(f.View.Invoke(id),Is.True,id);
        private static void Menu(FrontEndController f){if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();}
        [UnityTest]public IEnumerator FlagsBordersAndUnsupportedControllerScreenUseRealResourcesAndReturnSafely()
        {
            yield return Ready();var f=FrontEndController.Active;Menu(f);f.MenuChoice(3);Click(f,"controllers.options");
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.InputControllers));Assert.That(f.View.Buttons["device.scan"].interactable,Is.False);
            Click(f,"device.sensor");Assert.That(f.InputDevices.Ready,Is.False);Assert.That(f.Match.InputAdapter.UsesSensor,Is.True);
            f.Match.InputAdapter.Press(PlayerAction.Serve);Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);
            f.Match.InputAdapter.Press(PlayerAction.MenuBack);Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Options));
            Click(f,"controllers.options");Click(f,"device.keyboard");Click(f,"back");Click(f,"back");f.MenuChoice(0);
            for(int i=0;i<15;i++)
            {
                Click(f,"country."+CharacterCatalog.Ids[i]);Assert.That(f.View.Flags["selection.flag"].texture,Is.SameAs(CountryFlags.Get(i)));
                var map=UnityEngine.Object.FindFirstObjectByType<CountryMapGraphic>();Assert.That(map.Selected,Is.EqualTo(i));Assert.That(WorldCountries.Pick(WorldRoster.Position(i)),Is.EqualTo(i));
            }
            Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);Assert.That(f.Match.InputAdapter.GameplayEnabled,Is.False);
        }
        [UnityTest]public IEnumerator SensorSwingsInSettingsNeverActAsMenuConfirmation()
        {
            yield return Ready();var f=FrontEndController.Active;Menu(f);f.MenuChoice(3);Click(f,"controllers.options");
            var backend=new Backend();f.Match.InputAdapter.ConfigureDevices(backend);f.Match.InputAdapter.Devices.Select(InputDeviceKind.Sensor);
            backend.Events.Enqueue(new ControllerEvent(PlayerAction.Serve,1,1,Time.realtimeSinceStartupAsDouble,.5f));yield return null;yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.InputControllers));Assert.That(f.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Sensor));
            Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);Assert.That(f.Match.InputAdapter.Delivered,Is.Zero);
        }
        [UnityTest]public IEnumerator SelectedSensorExcludesKeyboardGameplayAndMouseButtonsWhileEscapeRemainsUsable()
        {
            yield return Ready();var f=FrontEndController.Active;Menu(f);f.MenuChoice(2);f.Confirm();f.Confirm();f.Confirm();
            double end=Time.realtimeSinceStartupAsDouble+5;while(f.Model.Stage!=FrontEndStage.Playing&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            var c=f.Match;var backend=new Backend();var keys=new HashSet<KeyCode>();var menuKeys=new HashSet<KeyCode>();
            c.InputAdapter.ConfigureDevices(backend,new MemoryInputDevicePreference(),new KeyboardPlayerInputSource(key=>keys.Remove(key)),key=>menuKeys.Remove(key));
            c.InputAdapter.Devices.Select(InputDeviceKind.Sensor);keys.UnionWith(new[]{KeyCode.Space,KeyCode.Q,KeyCode.W});
            c.InputAdapter.Press(PlayerAction.Serve);yield return null;yield return null;
            Assert.That(c.Match.CurrentShot.ShotId,Is.Zero,"Keyboard and HUD cannot serve while sensor is selected");
            backend.Events.Enqueue(new ControllerEvent(PlayerAction.Serve,1,1,Time.realtimeSinceStartupAsDouble,.5f));yield return null;yield return null;
            Assert.That(c.Match.CurrentShot.ShotId,Is.GreaterThan(0));Assert.That(c.InputAdapter.LastAction,Is.EqualTo(PlayerAction.Serve));
            int delivered=c.InputAdapter.Delivered;backend.State=ControllerLinkState.Disconnected;yield return null;yield return null;
            Assert.That(c.InputAdapter.DeviceReady,Is.False);Assert.That(c.InputAdapter.Devices.Selected,Is.EqualTo(InputDeviceKind.Sensor));Assert.That(c.InputAdapter.Delivered,Is.EqualTo(delivered));
            menuKeys.Add(KeyCode.Escape);yield return null;yield return null;Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause));
            Click(f,"controllers.options");Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.InputControllers));
            Click(f,"device.keyboard");keys.Clear();Assert.That(c.InputAdapter.SourceName,Is.EqualTo("Keyboard"));Click(f,"back");Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause));
        }
    }
}
