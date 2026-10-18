using System;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    [Serializable] public sealed class OnlineConnectionSettings
    {
        public int version=1;
        public bool forceRelay;
        public string[] stunUrls=Array.Empty<string>();
        public static OnlineConnectionSettings Load()
        {
            string source=Path.Combine(Application.streamingAssetsPath,"Online/transport.json");
            string saved=Path.Combine(Application.persistentDataPath,"Online/transport.json");
            // Batch and validation never read or create the human's settings file.
            string path=Application.isBatchMode||Application.identifier=="com.digitaltennis.validation"?source:File.Exists(saved)?saved:source;
            try
            {
                var info=new FileInfo(path);if(info.Length>16384)throw new FormatException();
                var settings=JsonUtility.FromJson<OnlineConnectionSettings>(File.ReadAllText(path));
                if(settings==null||settings.version!=1||settings.stunUrls==null||settings.stunUrls.Length>8)throw new FormatException();
                foreach(var url in settings.stunUrls)if(string.IsNullOrWhiteSpace(url)||url.Length>256||!url.StartsWith("stun:",StringComparison.Ordinal)||url.IndexOfAny(new[]{'\r','\n','@'})>=0)throw new FormatException();
                return settings;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is FormatException)
            {Debug.LogWarning("온라인 연결 설정을 읽을 수 없어 로컬 ICE와 릴레이를 사용합니다.");return new OnlineConnectionSettings();}
        }
    }
}
