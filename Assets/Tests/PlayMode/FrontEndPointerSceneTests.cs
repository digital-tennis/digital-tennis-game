using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    // Test-only input at the uGUI mouse boundary, never a Button callback shortcut.
    public sealed class FrontEndPointerTestInput : BaseInput
    {
        public Vector2 Position;
        public bool Down, Up, Held;
        public override bool mousePresent => true;
        public override Vector2 mousePosition => Position;
        public override Vector2 mouseScrollDelta => Vector2.zero;
        public override bool GetMouseButtonDown(int button) => button==0&&Down;
        public override bool GetMouseButtonUp(int button) => button==0&&Up;
        public override bool GetMouseButton(int button) => button==0&&Held;
    }

    public sealed class FrontEndPointerTestModule : StandaloneInputModule
    {
        // The test supplies focus-independent mouse frames; native focus is verified in the Mac player.
        public void ProcessMouseFrame() => ProcessMouseEvent();
    }

    public sealed class FrontEndPointerSceneTests
    {
        private FrontEndController flow;
        private FrontEndPointerTestInput input;
        private FrontEndPointerTestModule module;

        private IEnumerator Click(string id)
        {
            double end=Time.realtimeSinceStartupAsDouble+3;
            while(flow.StageAge<.3f&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            flow.RenderUiNow();Canvas.ForceUpdateCanvases();
            Assert.That(flow.View.Buttons.TryGetValue(id,out var button)&&button.interactable,Is.True,id);
            var rect=button.GetComponent<RectTransform>();
            input.Position=RectTransformUtility.WorldToScreenPoint(flow.UiCamera,rect.TransformPoint(rect.rect.center));
            var before=flow.Model.Stage;
            input.Down=true;input.Held=true;input.Up=false;module.ProcessMouseFrame();
            input.Down=false;
            yield return null;
            Assert.That(flow.Model.Stage,Is.EqualTo(before),id+" must not activate on mouse-down");
            input.Held=false;input.Up=true;module.ProcessMouseFrame();input.Up=false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerPressAndReleaseNavigateMenuRosterAndTransactionalControllerSettings()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Pointer raycasts require a rendered Canvas: run bash tools/unity.sh test-mouse");
            yield return SceneManager.LoadSceneAsync("FrontEnd");
            double end=Time.realtimeSinceStartupAsDouble+12;
            while(FrontEndController.Active?.Ready!=true&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            flow=FrontEndController.Active;Assert.That(flow?.Ready,Is.True);
            flow.Match.InputAdapter.ConfigureDevices(new UnavailableControllerBackend(),new MemoryInputDevicePreference());
            var events=EventSystem.current;Assert.That(events,Is.Not.Null);
            events.GetComponent<StandaloneInputModule>().enabled=false;
            input=events.gameObject.AddComponent<FrontEndPointerTestInput>();
            module=events.gameObject.AddComponent<FrontEndPointerTestModule>();module.inputOverride=input;module.enabled=false;
            if(flow.Model.Stage==FrontEndStage.Logo)yield return Click("opening.skip");
            Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.Title));
            yield return Click("continue");Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.Menu));
            yield return Click("menu.single");Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.Player));
            yield return Click("country.JPN");Assert.That(flow.Model.Focus,Is.EqualTo(1));
            yield return Click("confirm");Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.Opponent),"One release must not confirm the next page");
            yield return Click("back");yield return Click("back");
            yield return Click("menu.options");yield return Click("controllers.options");
            yield return Click("device.keyboard");Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.KeyboardSettings));
            Assert.That(flow.View.Buttons.ContainsKey("device.scan"),Is.False);
            yield return Click("device.test");Assert.That(flow.InputDevices.Setup.Testing,Is.True);
            yield return Click("back");Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.InputControllers));
            yield return Click("device.sensor");Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.WirelessSettings));
            Assert.That(flow.View.Buttons["device.save"].interactable,Is.False);
            yield return Click("back");yield return Click("device.keyboard");yield return Click("device.save");
            Assert.That(flow.Model.Stage,Is.EqualTo(FrontEndStage.Options));
            Assert.That(flow.InputDevices.Selected,Is.EqualTo(InputDeviceKind.Keyboard));
            Assert.That(flow.Match.Match.CurrentShot.ShotId,Is.Zero);
            Assert.That(flow.View.ButtonActivations,Is.Zero,"Do not use View.Invoke or direct callbacks as pointer evidence");
        }
    }
}
