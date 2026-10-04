# SOY Temperature

Windows 10/11 x64용 C# WinForms 온도 모니터. LibreHardwareMonitorLib 0.9.6으로 CPU·GPU·SSD/HDD 온도를 2초마다 읽습니다.

## 실행

[최신 배포 다운로드](https://github.com/soylab-edu/soylab_hw_temperature/releases/latest)에서 exe를 직접 받거나 ZIP의 `1.0.0` 폴더를 풀어 실행하세요. 로컬 최종 배포 파일은 `1.0.0/SoyTemperature.exe`입니다. 폴더에는 exe 하나만 있으며 .NET 런타임·아이콘·글꼴이 모두 포함되어 있습니다.

- exe를 더블클릭하면 Windows 관리자 승인(UAC)을 요청합니다. **예**를 누르면 CPU 센서에 필요한 권한으로 시작합니다.
- 화면에는 **CPU·GPU·SSD 현재 온도와 트레이로 내리기 버튼만** 표시됩니다. 제목, 장치명, 그래프, 안내 문구와 기타 버튼은 삭제했습니다.
- 숫자와 분류명에는 exe에 내장한 **Ubuntu Bold**, 한글 버튼에는 **맑은 고딕 Bold**를 사용합니다. 창 크기와 DPI에 맞춰 글자 크기를 조절합니다.
- 최소화·X·트레이 버튼은 창을 트레이로 숨깁니다. CPU·GPU 트레이 아이콘은 반올림한 온도 숫자와 C/G를 표시합니다. 클릭하면 창이 돌아옵니다. 완전히 끝내려면 우클릭 → 종료를 선택하세요.
- 창이나 트레이를 우클릭하면 최저·최고값 확인, 기록 초기화, CSV 저장, CPU 드라이버 설치와 종료를 사용할 수 있습니다. 최저·최고 기록은 화면에 노출하지 않고 내부에서 유지합니다.
- 다른 PC에 PawnIO가 없다면 우클릭 → CPU 드라이버 설치를 선택하세요. 공식 PawnIO 2.2.0 설치 파일의 SHA-256을 확인하고 설치합니다. 설치에는 인터넷과 관리자 권한이 필요합니다.
- Windows가 트레이 아이콘을 숨기면 작업 표시줄의 `^`에서 확인하세요.

대표 온도는 CPU Package와 GPU Core를 우선 선택합니다. 여러 장치가 있으면 대표 센서 중 현재 온도가 가장 높은 장치를 표시합니다. 실제 온도가 아닌 Distance to TjMax는 제외하고, AMD 미지원 보조 센서의 0을 실제 온도로 표시하지 않습니다.

SSD 온도는 하드웨어와 Windows 저장장치 드라이버의 SMART/NVMe 지원에 따라 달라집니다. 테스트 PC의 **Apple SSD AP2048 / Boot Camp 드라이버**는 온도를 제공하지 않아 **—°**로 표시됩니다. 장치에 마우스를 올리면 미지원 상태를 확인할 수 있습니다.

SOYLAB Comfy Router의 짙은 보라색 배경과 코랄·라벤더·초록 포인트를 사용했습니다. SOYLAB 로고의 컬러 무늬와 중앙 온도계 아이콘을 exe·창·작업 표시줄에 적용했습니다. 원본 자산은 `img`, 제작 프롬프트는 [icon-design.md](img/icon-design.md)에 있습니다.

## 빌드 및 검증

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\launch.ps1
Start-Process .\1.0.0\SoyTemperature.exe -ArgumentList '--self-test' -Wait
Start-Process .\1.0.0\SoyTemperature.exe -ArgumentList '--verify C:\Temp\SoyVerification --verify-exit' -Wait
```

빌드 스크립트는 SDK가 없으면 Microsoft 공식 설치 스크립트로 `.tools/dotnet`에 .NET 10 SDK를 설치합니다. 시스템 PATH는 변경하지 않습니다. 패키지 버전은 `packages.lock.json`에 기록합니다. PNG 변경 후 `export-icon.ps1`로 ICO를 갱신한 뒤 빌드하세요.

`--self-test`는 센서별 최저·최고, null·NaN·무한대 제외와 초기화를 검증합니다. `--verify`는 실제 WinForms 창에서 6회 측정하고 CPU 센서, 트레이 숨기기/복원, 기록 초기화, 작은 창의 글자 맞춤과 Ubuntu 글꼴을 확인합니다. `verification.json`, `samples.jsonl`, `hardware-report.txt`와 실제 창·트레이 이미지를 저장합니다. 읽지 못하는 온도는 null이며 가상 데이터는 사용하지 않습니다.

오류 로그: `%LOCALAPPDATA%/SoyTemperature/errors.log`.

참조: [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [Microsoft 단일 파일 배포](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview), [Ubuntu 글꼴](https://design.ubuntu.com/font), [디자인 색상](https://github.com/soylab-edu/ComfyUI-soylab-router/blob/main/web/router.js). 글꼴 라이선스는 우클릭 메뉴에서 볼 수 있습니다.
