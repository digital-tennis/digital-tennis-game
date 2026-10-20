using System;
using UnityEngine;

namespace DigitalTennis
{
    public sealed partial class FrontEndView
    {
        private void ControllerOptions()
        {
            var d=flow.InputDevices;
            Label("controllers.title","입력 컨트롤러",100,153,1400,64,48,White);
            Label("controllers.current","현재 사용 중  /  "+d.SelectedName,108,239,1380,38,25,Lime);
            Label("controllers.note","설정할 기기를 고르세요. 상세 화면에서 ‘저장하고 사용’을 누르기 전에는 변경되지 않습니다.",108,292,1380,62,22,Muted);
            Button("device.keyboard","키보드\n키 배치 변경 · 입력 테스트",100,388,674,232,()=>flow.ConfigureController(InputDeviceKind.Keyboard),false,32);
            Button("device.sensor","무선 컨트롤러\n기기 찾기 · 연결 · 동작 테스트",810,388,690,232,()=>flow.ConfigureController(InputDeviceKind.Sensor),false,32);
            Label("controllers.keyboard.active",d.Selected==InputDeviceKind.Keyboard?"사용 중":"",128,649,610,37,21,Lime);
            Label("controllers.wireless.active",d.Selected==InputDeviceKind.Sensor?"사용 중"+(d.Ready?" · 연결됨":" · 미연결"):"",838,649,610,37,21,Lime);
            if(!string.IsNullOrEmpty(flow.ControllerNotice))Label("controllers.notice",flow.ControllerNotice,108,714,1380,42,20,Muted);
            Button("back","←  뒤로",100,792,320,54,()=>flow.Back());
        }
        private void KeyboardOptions()
        {
            var edit=flow.InputDevices.Setup;
            Label("keyboard.title","키보드 설정",100,147,1400,64,47,White);
            Label("keyboard.note","바꿀 키 버튼을 선택한 뒤 새 키를 누르세요. 변경 사항은 저장할 때 게임에 적용됩니다.",108,229,1380,52,22,Muted);
            Image(page.transform,"Keyboard Bindings",100,291,680,438,Panel);
            for(int i=0;i<8;i++)
            {
                int binding=i;float y=309+i*48;
                Label("key.label."+i,KeyboardBindingSet.Labels[i],124,y+7,296,33,23,White);
                string key=edit.BindingCapture==i?"새 키를 누르세요…":KeyboardBindingSet.KeyName(edit.Keyboard[i]);
                var keyButton=Button("key.bind."+i,key,438,y,317,40,()=>flow.CaptureControllerKey(binding),edit.BindingCapture==i,21);
                if(edit.BindingCapture!=i)keyButton.targetGraphic.color=new Color(.025f,.075f,.10f);
            }
            Image(page.transform,"Keyboard Test",814,291,686,438,Panel);
            Label("keyboard.test.title","입력 테스트",842,309,630,42,29,White);
            Label("keyboard.test.note","테스트 중에는 경기 동작이 실행되지 않습니다.\n설정한 키를 눌러 반응을 확인하세요.",842,365,630,65,21,Muted);
            Label("keyboard.test.last",edit.LastInput,842,456,630,53,26,Lime);
            string seen="";for(int i=0;i<8;i++)seen+=(edit.KeyboardStep(i)?"✓ ":"○ ")+KeyboardBindingSet.Labels[i]+(i%2==0?"     ":"\n");
            Label("keyboard.test.seen",seen,842,523,630,135,20,White);
            Button("device.test",edit.Testing?"입력 테스트 다시 시작":"입력 테스트 시작",842,665,630,43,()=>flow.TestController(),false,21);
            Button("key.defaults","기본 키 배치로 복원",100,744,420,39,()=>flow.RestoreControllerKeys(),false,20);
            Label("keyboard.fixed","메뉴: 방향키 · Enter · ESC (고정)\n메뉴·특수 조작 키는 고정되며 같은 키를 중복 지정할 수 없습니다.",552,738,948,53,18,Muted);
            DetailActions(edit,edit.BindingCapture>=0?"새 키를 누르세요. ESC를 누르면 키 변경만 취소합니다.":"취소하면 기존 키 배치와 사용 기기를 유지합니다.");
        }
        private void WirelessOptions()
        {
            var d=flow.InputDevices;var edit=d.Setup;
            Label("wireless.title","무선 컨트롤러 설정",100,147,1400,64,47,White);
            Label("wireless.note",edit.ButtonInputMode?"NU-54 버튼 입력 모드 · IMU 사용 안 함  /  연결 → 버튼 테스트 → 저장":"NU40-DK + 별도 부착 외장 IMU  /  기기 찾기 → 연결·페어링 → 보정·테스트 → 저장",108,229,1380,45,22,Muted);
            Image(page.transform,"Wireless Discovery",100,291,680,438,Panel);
            Label("wireless.discovery","1  기기 찾기와 연결",126,309,628,45,29,White);
            Button("device.scan",d.State==ControllerLinkState.Scanning?"찾는 중…":"주변 기기 찾기",126,375,383,47,()=>flow.ScanControllers(),false,22).interactable=d.Available&&!edit.Busy&&d.State!=ControllerLinkState.Connected;
            Button("device.disconnect",edit.Busy?"요청 취소":"연결 해제",529,375,223,47,()=>flow.DisconnectController(),false,21).interactable=d.Available&&(edit.Busy||d.State==ControllerLinkState.Connected);
            string title=d.State==ControllerLinkState.Unavailable?(d.Available?"Bluetooth를 사용할 수 없습니다.":"현재 버전은 무선 연결 준비 중입니다."):d.State==ControllerLinkState.Scanning?"가까운 기기를 찾고 있습니다…":d.State==ControllerLinkState.Connecting?"연결을 요청했습니다…":d.State==ControllerLinkState.Pairing?"페어링 승인 대기 중":d.State==ControllerLinkState.Connected?"연결 완료":d.State==ControllerLinkState.Error?"연결하지 못했습니다":"전원을 켠 뒤 기기를 찾아 주세요.";
            Label("device.link",title,126,445,628,56,24,Lime);
            if(d.Devices.Count==0)
                Label("device.empty",d.Available?"검색이 끝나면 아래에 기기가 표시됩니다.\n찾지 못하면 전원·거리를 확인하고 다시 시도하세요.":"기기 검색과 페어링은 아직 사용할 수 없습니다.\n현재 사용 중인 입력 설정을 유지합니다.",126,521,628,119,21,Muted);
            int current=Mathf.Min(flow.ControllerPage,Math.Max(0,(d.Devices.Count-1)/2));
            for(int row=0;row<2;row++)
            {
                int i=current*2+row;if(i>=d.Devices.Count)break;var device=d.Devices[i];bool connected=d.ConnectedDeviceId==device.Id&&d.State==ControllerLinkState.Connected;
                Button("device.connect."+i,device.Name+(connected?" · 연결됨":" · 연결 요청"),126,521+row*59,626,48,()=>flow.ConnectController(device.Id),connected,21).interactable=!edit.Busy&&d.State!=ControllerLinkState.Connected;
            }
            if(d.Devices.Count>2)
            {
                Button("device.previous","← 이전",126,653,292,35,()=>flow.MoveControllerPage(-1),false,18).interactable=current>0;
                Button("device.next","다음 →",440,653,312,35,()=>flow.MoveControllerPage(1),false,18).interactable=(current+1)*2<d.Devices.Count;
            }
            Label("device.status",d.Status,126,693,628,30,15,Muted);
            Image(page.transform,"Wireless Input Test",814,291,686,438,Panel);
            bool diagnostics=edit.LinkDiagnosticsSupported;
            Label("wireless.test.title",diagnostics?edit.ButtonInputMode?"2  버튼 입력 테스트 · IMU 사용 안 함":"2  통신 확인과 센서 테스트":"2  보정과 동작 테스트",842,309,630,45,29,White);
            if(diagnostics)
            {
                Button("device.probe",edit.LinkProbePending?"응답 확인 중…":"연결 통신 확인",842,375,630,43,()=>flow.ProbeControllerLink(),false,21).interactable=edit.ConnectionReady&&!edit.LinkProbePending;
                Label("wireless.link.result",edit.LinkStatus,842,424,630,45,18,edit.LinkProbePassed?Lime:Muted);
            }
            Label("wireless.test.note",edit.WirelessTestPassed?"연결과 동작 확인이 완료됐습니다. 저장하면 경기에 사용합니다.":edit.Testing?"아래 세 동작을 입력해 반응을 확인하세요.":edit.PhysicalInputMode?(!edit.InputReady?"라켓을 정지하고 먼저 센서를 보정하세요.":"물리 입력 확인 · 경기 액션은 게임이 해석합니다."):edit.ButtonInputMode?"SW1 서브 · SW2 포핸드 · SW3 백핸드 · SW4 스킬":edit.ConnectionReady&&!edit.InputReady?"라켓을 정지하고 먼저 센서를 보정하세요.":edit.ConnectionReady?"연결됐습니다. 테스트를 시작하고 세 동작을 확인하세요.":"연결을 완료하면 동작 테스트를 진행할 수 있습니다.",842,diagnostics?475:375,630,diagnostics?45:62,diagnostics?18:22,Muted);
            string[] steps=edit.PhysicalInputMode?new[]{"위→아래 동작 또는 버튼1","횡회전 또는 버튼2 / 버튼3","버튼4 눌림"}:edit.ButtonInputMode?new[]{"SW1 · 서브","SW2 포핸드 / SW3 백핸드","SW4 · 고유 스킬"}:new[]{"서브 버튼 또는 서브 동작","포핸드 또는 백핸드 스윙","고유 스킬 버튼 또는 동작"};
            for(int i=0;i<3;i++)Label("wireless.test.step."+i,(edit.WirelessStep(i)?"✓  ":"○  ")+steps[i],842,(diagnostics?531:458)+i*(diagnostics?35:48),630,diagnostics?29:35,diagnostics?19:23,edit.WirelessStep(i)?Lime:White);
            Label("wireless.test.result",edit.WirelessTestPassed?"동작 확인 완료 · 저장할 수 있습니다.":edit.Testing?edit.LastInput:"테스트 입력은 경기에 전달되지 않습니다.",842,diagnostics?635:609,630,diagnostics?26:41,diagnostics?16:21,edit.WirelessTestPassed?Lime:Muted);
            Button("device.test",edit.Testing?"동작 테스트 다시 시작":"동작 테스트 시작",842,665,edit.CalibrationSupported?304:630,43,()=>flow.TestController(),false,21).interactable=edit.ConnectionReady&&edit.InputReady;
            if(edit.CalibrationSupported)Button("device.calibrate",edit.CalibrationPending?"보정 응답 대기…":edit.CalibrationReady?"다시 보정":"정지 상태에서 보정",1162,665,310,43,()=>flow.CalibrateController(),false,20).interactable=edit.ConnectionReady&&!edit.CalibrationPending;
            Label("wireless.safety","본인 기기를 선택하세요. OS 승인 요청이 있으면 기기와 컴퓨터에서 승인해 주세요. 연결이 끊기거나 기기가 바뀌면 다시 테스트합니다.",108,741,1380,47,18,Muted);
            DetailActions(edit,edit.WirelessTestPassed?"연결과 동작이 확인됐습니다. 저장하면 이 기기를 경기에 사용합니다.":"연결과 세 동작 테스트를 마쳐야 ‘저장하고 사용’을 누를 수 있습니다.");
        }
        private void DetailActions(ControllerSetupSession edit,string hint)
        {
            Label("setup.status",edit.Error??hint,440,797,720,51,18,edit.Error==null?Muted:new Color(1,.64f,.49f));
            Button("back","취소",100,795,302,53,()=>flow.Back(),false,23);
            Button("device.save",edit.CanSave?"저장하고 사용":"저장할 수 없음",1191,795,309,53,()=>flow.SaveControllerSettings(),edit.CanSave,23).interactable=edit.CanSave;
        }
    }
}
