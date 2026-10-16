using UnityEngine;
using UnityEngine.Rendering;
namespace DigitalTennis
{
    public enum PresentationQuality { Low, Medium, High }
    /// <summary>Local rendering preferences only; never changes physics or authoritative tuning.</summary>
    public sealed class GamePresentationQuality : MonoBehaviour
    {
        public static PresentationQuality Current {get;private set;}=PresentationQuality.High;
        private static bool IsValidation => Application.isBatchMode;
        private void Start(){Apply((PresentationQuality)Mathf.Clamp(IsValidation?2:PlayerPrefs.GetInt("DigitalTennis.Graphics",2),0,2));}
        public static void Apply(PresentationQuality quality)
        {
            if(!System.Enum.IsDefined(typeof(PresentationQuality),quality))return;
            Current=quality;if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            QualitySettings.antiAliasing=quality==PresentationQuality.High?4:quality==PresentationQuality.Medium?2:0;
            QualitySettings.shadows=quality==PresentationQuality.Low?ShadowQuality.Disable:quality==PresentationQuality.Medium?ShadowQuality.HardOnly:ShadowQuality.All;
            QualitySettings.shadowResolution=quality==PresentationQuality.High?ShadowResolution.High:ShadowResolution.Low;
            QualitySettings.shadowDistance=quality==PresentationQuality.High?55:30;
            QualitySettings.vSyncCount=Application.isBatchMode?0:1;Application.targetFrameRate=60;
            foreach(var stadium in Object.FindObjectsByType<TennisStadium>(FindObjectsSortMode.None))
            {var crowd=stadium.transform.Find("Seated Audience");if(crowd!=null)crowd.GetComponent<Renderer>().enabled=quality!=PresentationQuality.Low;}
            if(!IsValidation){PlayerPrefs.SetInt("DigitalTennis.Graphics",(int)quality);PlayerPrefs.Save();}
        }
    }
}
