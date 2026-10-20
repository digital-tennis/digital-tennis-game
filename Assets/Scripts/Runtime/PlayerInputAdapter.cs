using System;
using UnityEngine;

namespace DigitalTennis
{
    [DisallowMultipleComponent, RequireComponent(typeof(TennisMatchController))]
    public sealed class PlayerInputAdapter : MonoBehaviour
    {
        private readonly PlayerInputRouter router = new PlayerInputRouter();
        private TennisMatchController controller;
        private uint buttonSequence;
        private InputDeviceManager devices;
        private KeyboardMenuInputSource menuKeyboard=new KeyboardMenuInputSource();
        private bool sourceOverride;
        public IInputDeviceSettings Devices=>devices;
        public bool UsesSensor=>!sourceOverride&&devices?.Selected==InputDeviceKind.Sensor;
        public bool DeviceReady=>sourceOverride||devices==null||devices.Ready;
        public float Power01 { get; set; }
        public float AimX { get; set; }
        public float AimZ { get; set; }
        public bool InputEnabled { get; set; } = true;
        public bool GameplayEnabled { get; set; } = true;
        public bool Typing { get; set; }
        public string SourceName => router.Source?.Name ?? "None";
        public Func<PlayerInputSample, uint, SwingResult?> OnlineRoute { get; set; }
        public Func<PlayerInputSample, bool> MenuRoute { get; set; }
        public int Delivered { get; private set; }
        public int Rejected => router.Rejected;
        public PlayerAction? LastAction { get; private set; }
        public SwingResult? LastSwingResult { get; private set; }
        public bool LastActionSentOnline { get; private set; }

        private void Awake()
        {
            controller = GetComponent<TennisMatchController>();
            IInputDevicePreference preference=Application.isBatchMode?(IInputDevicePreference)new MemoryInputDevicePreference():new FileInputDevicePreference(FileInputDevicePreference.PlayerPath(Application.persistentDataPath,Application.identifier));
            ConfigureDevices(ControllerBackendFactory.Create(),preference);
        }

        // Explicit injected sources remain available to existing opt-in validation and tests.
        public void UseSource(IPlayerInputSource source) {sourceOverride=true;router.SetSource(source);ClearFeedback();}
        public void ConfigureDevices(IControllerBackend backend,IInputDevicePreference preference=null,IPlayerInputSource keyboard=null,Func<KeyCode,bool> menuKeys=null,Func<KeyCode,bool> setupKeys=null)
        {
            menuKeyboard=new KeyboardMenuInputSource(menuKeys);
            devices?.Dispose();devices=new InputDeviceManager(backend,keyboard??new KeyboardPlayerInputSource(),preference??new MemoryInputDevicePreference(),setupKeys);
            devices.SourceChanged+=ApplyDevice;ApplyDevice(devices.Source);
        }
        private void ApplyDevice(IPlayerInputSource source){sourceOverride=false;router.SetSource(source);menuKeyboard.Clear();ClearFeedback();}
        private void OnDestroy(){devices?.Dispose();}
        // Explicit developer/test reset; user-facing screens use their own SaveSetup action.
        public void UseKeyboard(){devices.BeginSetup(InputDeviceKind.Keyboard);devices.SaveSetup();}
        public void ClearFeedback() {LastAction=null;LastSwingResult=null;LastActionSentOnline=false;}
        public void MarkInputSent() => LastActionSentOnline=true;

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;devices?.Tick(now);
            if(devices?.Setup?.SuppressFrameInput==true){menuKeyboard.Clear();router.Source?.Clear();return;}
            if(!(router.Source is KeyboardPlayerInputSource)&&MenuRoute!=null)
            {
                menuKeyboard.Poll(now,Power01,AimX,AimZ);bool consumed=false;
                while(menuKeyboard.TryRead(out var menu))
                {if(menu.Action==PlayerAction.ReloadBalance){Deliver(menu,998);consumed=true;}else consumed|=MenuRoute(menu);}
                if(consumed){router.Source?.Clear();return;} // No held confirmation leaks into the newly selected device.
            }
            if ((!InputEnabled && MenuRoute == null) || Typing) { router.Source?.Clear(); return; }
            router.Source?.Poll(now, Power01, AimX, AimZ);
            router.Drain(now, Deliver);
        }

        /// <summary>On-screen buttons use the same command route as keyboard and future hardware.</summary>
        public void Press(PlayerAction action, bool practiceOpponent = false, int choice = -1)
        {
            if (!InputEnabled && MenuRoute == null) return;
            if(UsesSensor&&IsGameplayAction(action))return;
            if (++buttonSequence == 0) buttonSequence = 1;
            Deliver(new PlayerInputSample(action, buttonSequence, Time.realtimeSinceStartupAsDouble,
                Power01, AimX, AimZ, practiceOpponent, choice), 999);
        }

        public static bool IsGameplayAction(PlayerAction action)=>PhysicalTennisInputResolver.IsPhysical(action)||action==PlayerAction.Serve||action==PlayerAction.Forehand||action==PlayerAction.Backhand||action==PlayerAction.Smash||action==PlayerAction.Volley||action==PlayerAction.Slice||action==PlayerAction.Lob||action==PlayerAction.Skill||action==PlayerAction.Restart||action==PlayerAction.FaultServe||action==PlayerAction.LetServe||action==PlayerAction.NetServe;
        private void Deliver(PlayerInputSample sample, uint session)
        {
            if (!sample.IsValid) return;
            if((devices?.Setup!=null||UsesSensor&&!GameplayEnabled)&&IsGameplayAction(sample.Action))return;
            if (MenuRoute != null && MenuRoute(sample)) return;
            if(sample.Action==PlayerAction.ReloadBalance){controller.Effects?.Reload();GetComponent<TennisAudio>()?.Reload();}
            if (!InputEnabled || (!DeviceReady&&IsGameplayAction(sample.Action))) return;
            if (!GameplayEnabled && sample.Action != PlayerAction.SelectCharacter && sample.Action != PlayerAction.SelectCourt) return;
            Delivered++;
            LastAction=sample.Action;
            LastActionSentOnline=false;
            LastSwingResult=OnlineRoute!=null?OnlineRoute(sample,session):controller.ApplyPlayerInput(sample,session,CourtSide.Near);
        }
    }
}
