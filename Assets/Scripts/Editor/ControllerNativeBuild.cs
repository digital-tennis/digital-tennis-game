using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;

namespace DigitalTennis.Editor
{
    public static class ControllerNativeBuild
    {
        [PostProcessBuild(20)]
        public static void IncludeHelper(BuildTarget target,string output)
        {
            if(target!=BuildTarget.StandaloneOSX&&target!=BuildTarget.StandaloneWindows64)return;
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.digital-tennis.controller");
            if(package==null)throw new InvalidOperationException("Controller package is missing");
            string platform=target==BuildTarget.StandaloneOSX?"mac":"windows";
            string generated=Path.GetFullPath(".work/controller-native/"+platform);
            // These fixed .work build outputs are disposable. Start fresh so
            // prior publishes/conflict copies cannot enter the next product.
            if(Directory.Exists(generated))Directory.Delete(generated,true);
            Directory.CreateDirectory(generated);
            var process=new Process{StartInfo=new ProcessStartInfo("python3",Quote(Path.Combine(package.resolvedPath,"tools/build-native.py"))+" --platform "+platform+" --out "+Quote(generated)){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}};
            process.Start();string stdout=null,stderr=null;
            var outTask=process.StandardOutput.ReadToEndAsync();var errorTask=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(300000)){process.Kill();throw new InvalidOperationException("Controller native build timed out");}
            stdout=outTask.GetAwaiter().GetResult();stderr=errorTask.GetAwaiter().GetResult();
            if(process.ExitCode!=0)throw new InvalidOperationException("Controller native build failed: "+stderr+stdout);
            string destination=target==BuildTarget.StandaloneOSX?Path.Combine(output,"Contents/Resources/Data/StreamingAssets/ControllerNative"):Path.Combine(Path.GetDirectoryName(output),Path.GetFileNameWithoutExtension(output)+"_Data/StreamingAssets/ControllerNative");
            if(Directory.Exists(destination))Directory.Delete(destination,true);
            Directory.CreateDirectory(destination);
            foreach(var path in Directory.GetFiles(generated,"*",SearchOption.AllDirectories)){string copy=Path.Combine(destination,path.Substring(generated.Length+1));Directory.CreateDirectory(Path.GetDirectoryName(copy));File.Copy(path,copy,true);}
            var webRtc=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.webrtc");
            if(webRtc==null)throw new InvalidOperationException("WebRTC package is missing");
            string notices=Path.Combine(destination,"Licenses","Unity-WebRTC");Directory.CreateDirectory(notices);
            foreach(string name in new[]{"LICENSE.md","Third Party Notices.md"})File.Copy(Path.Combine(webRtc.resolvedPath,name),Path.Combine(notices,name),true);
            // Preserve original project and managed-package Apache notices in binary delivery.
            CopyNotices(Path.GetFullPath("."),Path.Combine(destination,"Licenses","DigitalTennis.Game"));
            foreach(string id in new[]{"com.digital-tennis.protocol","com.digital-tennis.controller","com.digital-tennis.ai"})
            {
                var dependency=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/"+id);
                if(dependency==null)throw new InvalidOperationException("Missing licensed package: "+id);
                CopyNotices(dependency.resolvedPath,Path.Combine(destination,"Licenses",id));
            }
            foreach(string relative in new[]{"Assets/Resources/FrontEnd/Fonts/OFL.txt","Assets/ThirdParty/Quaternius/0/License_Standard.txt","Assets/ThirdParty/Quaternius/1/License.txt","Assets/ThirdParty/Selection/FlagIcons/LICENSE.txt","Assets/ThirdParty/Selection/NaturalEarth-Terms.html"})
            {
                string copy=Path.Combine(destination,"Licenses","Assets",relative.Substring("Assets/".Length));
                Directory.CreateDirectory(Path.GetDirectoryName(copy));File.Copy(relative,copy,true);
            }
            // Set executable permission on the copy without changing source assets.
            if(target==BuildTarget.StandaloneOSX)
            {
                var chmod=Process.Start(new ProcessStartInfo("/bin/chmod","755 "+Quote(Path.Combine(destination,"digital-tennis-ble"))){UseShellExecute=false});chmod.WaitForExit();if(chmod.ExitCode!=0)throw new InvalidOperationException("Controller helper permission failure");
                string plist=Path.Combine(output,"Contents/Info.plist");var document=new XmlDocument();document.Load(plist);var dictionary=document.SelectSingleNode("/plist/dict");
                SetPurpose(document,dictionary,"NSBluetoothAlwaysUsageDescription","선택한 테니스 라켓을 찾아 연결하고 입력을 받습니다.");
                SetPurpose(document,dictionary,"NSLocalNetworkUsageDescription","상대와 테니스 경기를 연결하고 경기 상태를 동기화합니다.");
                // XmlDocument serializes an empty DTD subset as `[]`, which
                // Apple's plist parser rejects. A plist does not require a DTD.
                if(document.DocumentType!=null)document.RemoveChild(document.DocumentType);
                using(var writer=XmlWriter.Create(plist,new XmlWriterSettings{Encoding=new System.Text.UTF8Encoding(false),Indent=true}))document.Save(writer);
                var lint=Process.Start(new ProcessStartInfo("/usr/bin/plutil","-lint "+Quote(plist)){UseShellExecute=false});lint.WaitForExit();if(lint.ExitCode!=0)throw new InvalidOperationException("Invalid player Info.plist");
                SealMac(output);
            }
            UnityEngine.Debug.Log("DIGITAL_TENNIS_NATIVE_CONTROLLER_BUILD_OK "+platform);
        }
        public static void FinalizeBuiltProduct(BuildTarget target,string output)
        {
            string native=target==BuildTarget.StandaloneOSX?Path.Combine(output,"Contents/Resources/Data/StreamingAssets/ControllerNative"):Path.Combine(Path.GetDirectoryName(output),Path.GetFileNameWithoutExtension(output)+"_Data/StreamingAssets/ControllerNative");
            foreach(string file in new[]{target==BuildTarget.StandaloneOSX?"digital-tennis-ble":"digital-tennis-ble.exe","native-build.json","Licenses/Unity-WebRTC/LICENSE.md","Licenses/Unity-WebRTC/Third Party Notices.md","Licenses/DigitalTennis.Game/LICENSE","Licenses/com.digital-tennis.protocol/NOTICE","Licenses/com.digital-tennis.controller/NOTICE","Licenses/com.digital-tennis.ai/NOTICE"})
                if(!File.Exists(Path.Combine(native,file)))throw new InvalidOperationException("Missing controller build output: "+file);
            if(target!=BuildTarget.StandaloneOSX)return;
            // Unity adds Finder icon metadata after managed callbacks. Seal the
            // completed bundle after BuildPipeline returns, then verify it.
            SealMac(output);
        }
        private static void SealMac(string output)
        {
            // Finder artwork metadata can be inherited by local build copies.
            // Remove only these two metadata attributes from our output;
            // never remove quarantine or change OS security settings.
            foreach(string attribute in new[]{"com.apple.FinderInfo","com.apple.ResourceFork"})
            {
                using(var clean=Process.Start(new ProcessStartInfo("/usr/bin/xattr","-dr "+attribute+" "+Quote(output)){UseShellExecute=false,RedirectStandardError=true}))
                {clean.StandardError.ReadToEnd();clean.WaitForExit();}
            }
            // Unity signs before this callback. Added resources and purpose
            // strings must be covered by a fresh signature of our build.
            var sign=Process.Start(new ProcessStartInfo("/usr/bin/codesign","--force --deep --sign - "+Quote(output)){UseShellExecute=false});sign.WaitForExit();if(sign.ExitCode!=0)throw new InvalidOperationException("Controller app signing failure");
            // File Provider may restore Finder artwork while codesign walks a .nib.
            // Remove only artwork metadata once more before strict verification.
            foreach(string attribute in new[]{"com.apple.FinderInfo","com.apple.ResourceFork"})
            {
                using(var clean=Process.Start(new ProcessStartInfo("/usr/bin/xattr","-dr "+attribute+" "+Quote(output)){UseShellExecute=false,RedirectStandardError=true}))
                {clean.StandardError.ReadToEnd();clean.WaitForExit();}
            }
            var verify=Process.Start(new ProcessStartInfo("/usr/bin/codesign","--verify --deep --strict "+Quote(output)){UseShellExecute=false});verify.WaitForExit();if(verify.ExitCode!=0)throw new InvalidOperationException("Controller app signature verification failure");
        }
        private static void CopyNotices(string source,string destination)
        {
            Directory.CreateDirectory(destination);
            foreach(string name in new[]{"LICENSE","NOTICE","THIRD_PARTY_NOTICES.md"})
                File.Copy(Path.Combine(source,name),Path.Combine(destination,name),true);
        }
        private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
        private static void SetPurpose(XmlDocument document,XmlNode dictionary,string key,string text)
        {
            if(dictionary==null)throw new InvalidOperationException("Invalid player Info.plist");
            foreach(XmlNode node in dictionary.ChildNodes)if(node.Name=="key"&&node.InnerText==key&&node.NextSibling!=null){node.NextSibling.InnerText=text;return;}
            var name=document.CreateElement("key");name.InnerText=key;dictionary.AppendChild(name);var value=document.CreateElement("string");value.InnerText=text;dictionary.AppendChild(value);
        }
    }
}
