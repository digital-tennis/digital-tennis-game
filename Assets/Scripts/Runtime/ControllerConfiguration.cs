using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Saved gameplay bindings. Menu/recovery keys are fixed and cannot be rebound.</summary>
    public sealed class KeyboardBindingSet
    {
        public static readonly IReadOnlyList<PlayerAction> Actions = Array.AsReadOnly(new[] {
            PlayerAction.Serve, PlayerAction.Forehand, PlayerAction.Backhand, PlayerAction.Smash,
            PlayerAction.Volley, PlayerAction.Slice, PlayerAction.Lob, PlayerAction.Skill });
        public static readonly IReadOnlyList<string> Labels = Array.AsReadOnly(new[] { "서브", "포핸드", "백핸드", "스매시", "발리", "슬라이스", "로브", "고유 스킬" });
        private static readonly KeyCode[] Defaults = { KeyCode.Space, KeyCode.Q, KeyCode.W, KeyCode.S, KeyCode.V, KeyCode.X, KeyCode.B, KeyCode.K };
        private readonly KeyCode[] keys;
        public KeyboardBindingSet() { keys = (KeyCode[])Defaults.Clone(); }
        private KeyboardBindingSet(KeyCode[] values) { keys = (KeyCode[])values.Clone(); }
        public KeyCode this[int index] => keys[index];
        public KeyCode KeyFor(PlayerAction action) { for(int i=0;i<Actions.Count;i++)if(Actions[i]==action)return keys[i]; return KeyCode.None; }
        public static string KeyName(KeyCode key) => key==KeyCode.Space?"Space":key.ToString().Replace("Alpha", "").Replace("Keypad", "Num ");
        public bool TryChange(int index, KeyCode key, out KeyboardBindingSet changed, out string error)
        {
            changed=this;
            if(index<0||index>=Actions.Count) { error="변경할 동작을 선택해 주세요."; return false; }
            if(!Allowed(key)) { error="메뉴·특수 조작에 사용 중인 키입니다. 알파벳·숫자·Space 중 다른 키를 선택하세요."; return false; }
            for(int i=0;i<keys.Length;i++) if(i!=index&&keys[i]==key) { error=KeyName(key)+"는 이미 "+Labels[i]+"에 사용 중입니다."; return false; }
            var copy=(KeyCode[])keys.Clone(); copy[index]=key; changed=new KeyboardBindingSet(copy); error=null; return true;
        }
        public static bool Allowed(KeyCode key)
        {
            int code=(int)key;
            if(key!=KeyCode.Space && !(code>=(int)KeyCode.A&&code<=(int)KeyCode.Z) && !(code>=(int)KeyCode.Alpha0&&code<=(int)KeyCode.Alpha9) && !(code>=(int)KeyCode.Keypad0&&code<=(int)KeyCode.Keypad9))return false;
            // Existing menu, restart, serve-lab and practice-opponent commands keep their own keys.
            return key!=KeyCode.C&&key!=KeyCode.R&&key!=KeyCode.F&&key!=KeyCode.L&&key!=KeyCode.N&&key!=KeyCode.E&&key!=KeyCode.D&&key!=KeyCode.G;
        }
        public string[] Serialize() { var result=new string[keys.Length]; for(int i=0;i<keys.Length;i++)result[i]=keys[i].ToString(); return result; }
        public static bool TryParse(string[] values, out KeyboardBindingSet bindings)
        {
            bindings=null; if(values==null||values.Length!=Actions.Count)return false;
            var parsed=new KeyCode[values.Length]; var used=new HashSet<KeyCode>();
            for(int i=0;i<values.Length;i++)if(!Enum.TryParse(values[i],out parsed[i])||!Allowed(parsed[i])||!used.Add(parsed[i]))return false;
            bindings=new KeyboardBindingSet(parsed); return true;
        }
    }
    public sealed class InputDeviceConfiguration
    {
        public InputDeviceKind Selected {get;}
        public KeyboardBindingSet Keyboard {get;}
        public string WirelessDeviceId {get;}
        public InputDeviceConfiguration(InputDeviceKind selected=InputDeviceKind.Keyboard, KeyboardBindingSet keyboard=null, string wirelessDeviceId=null)
        { Selected=selected; Keyboard=keyboard??new KeyboardBindingSet(); WirelessDeviceId=wirelessDeviceId??""; }
    }
    public interface IInputDevicePreference
    {
        InputDeviceConfiguration Load();
        void Save(InputDeviceConfiguration configuration);
    }
    public sealed class MemoryInputDevicePreference:IInputDevicePreference
    {
        private InputDeviceConfiguration value;
        public MemoryInputDevicePreference(InputDeviceConfiguration configuration=null) { value=configuration??new InputDeviceConfiguration(); }
        public InputDeviceConfiguration Load()=>value;
        public void Save(InputDeviceConfiguration configuration)=>value=configuration;
    }
    public sealed class FileInputDevicePreference:IInputDevicePreference
    {
        [Serializable]private sealed class Data
        { public int version; public string selected; public string[] keyboard; public string wirelessDeviceId; }
        private readonly string path;
        public FileInputDevicePreference(string path) { this.path=path; }
        public static string PlayerPath(string root,string appId)=>Path.Combine(appId=="com.digitaltennis.validation"?Path.Combine(root,"ControllerValidation"):root,"input-controller.json");
        public InputDeviceConfiguration Load()
        {
            try
            {
                if(!File.Exists(path))return new InputDeviceConfiguration();
                var data=JsonUtility.FromJson<Data>(File.ReadAllText(path));
                if(data==null||!(data.selected=="Keyboard"||data.selected=="Sensor"))return new InputDeviceConfiguration();
                var kind=data.selected=="Sensor"?InputDeviceKind.Sensor:InputDeviceKind.Keyboard;
                // Old sensor selection has no device identity: retain the choice, but it cannot be Ready.
                if(data.version==1)return new InputDeviceConfiguration(kind);
                if(data.version!=2||!KeyboardBindingSet.TryParse(data.keyboard,out var keyboard))return new InputDeviceConfiguration();
                return new InputDeviceConfiguration(kind,keyboard,data.wirelessDeviceId);
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException) { return new InputDeviceConfiguration(); }
        }
        public void Save(InputDeviceConfiguration configuration)
        {
            if(!Enum.IsDefined(typeof(InputDeviceKind),configuration.Selected)||configuration.Selected==InputDeviceKind.Sensor&&string.IsNullOrWhiteSpace(configuration.WirelessDeviceId))throw new ArgumentException("Incomplete controller configuration");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending=path+".pending";
            File.WriteAllText(pending,JsonUtility.ToJson(new Data { version=2,selected=configuration.Selected.ToString(),keyboard=configuration.Keyboard.Serialize(),wirelessDeviceId=configuration.WirelessDeviceId },true));
            if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);
        }
    }
}
