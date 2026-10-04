# SOY Temperature

Windows 10/11 x64용 C# WinForms 온도 모니터. LibreHardwareMonitorLib 0.9.6으로 CPU·GPU 온도를 2초마다 읽습니다.

## 실행

[최신 배포 다운로드](https://github.com/soylab-edu/soylab_hw_temperature/releases/latest)에서 exe를 직접 받거나 ZIP의 `1.0.0` 폴더를 풀어 실행하세요. 로컬 최종 배포 파일은 `1.0.0/SoyTemperature.exe`입니다. 폴더에는 exe 하나만 있으며 .NET 런타임·아이콘·글꼴이 모두 포함되어 있습니다.

- exe를 더블클릭하면 Windows 관리자 승인(UAC)을 요청합니다. **예**를 누르면 CPU 센서에 필요한 권한으로 시작합니다.
- 화면에는 **CPU·GPU 현재 온도, 숫자 위의 작은 그래프와 좌상단의 원형 버튼 세 개**만 표시됩니다. SSD와 기본 창 제목 표시줄, 장치명과 안내 문구는 삭제했습니다.
- 둥근 카드와 창 모서리, 짙은 보라색 배경, 넓은 여백과 **Ubuntu Bold** 숫자를 사용합니다. 글꼴은 exe에 내장되며 창 크기와 DPI에 맞춰 크기를 조절합니다.
- 창 외곽에는 밝은 보라색 `#A779E6`의 굵은 테두리(논리 3.5 px)를 적용했습니다. DPI와 창 크기를 바꿔도 둥근 외곽선이 유지됩니다.
- CPU는 따뜻한 보라색 카드와 코랄 그래프, GPU는 차가운 보라색 카드와 라벤더 그래프입니다. 그래프는 실제 측정한 최근 60초(최대 30개 표본)를 표시하며, 트레이에서도 기록을 이어갑니다. 미지원 값은 연결하지 않습니다.
- 좌상단 버튼은 왼쪽부터 **빨강: 앱 종료**, **노랑: 트레이로 숨기기**, **초록: https://soylab.ai/ 열기**입니다. 초록 버튼은 기본 브라우저로 사이트를 엽니다. 버튼에 마우스를 올리면 아이콘과 기능 안내가 표시됩니다.
- CPU·GPU 트레이 아이콘은 온도 숫자와 C/G를 표시합니다. 클릭하면 창이 돌아옵니다. 우클릭 → 종료로도 앱을 끝낼 수 있습니다.
- 카드나 여백을 드래그해 창을 이동하고, 창 가장자리를 드래그해 크기를 조절하세요. Alt+F4는 앱을 종료합니다.
- 바깥 테두리와 오른쪽 아래의 작은 대각선 손잡이를 드래그하면 창을 줄이거나 늘릴 수 있습니다. 둥근 모서리에서는 대각선 크기 조절 커서가 표시됩니다. 최소 크기는 논리 260×180 px이며, 온도·그래프·좌상단 버튼이 함께 맞춰집니다.
- 창이나 트레이를 우클릭하면 최저·최고값 확인, 기록 초기화, CSV 저장, CPU 드라이버 설치와 종료를 사용할 수 있습니다. 최저·최고 기록은 화면에 노출하지 않고 내부에서 유지합니다.
- 다른 PC에 PawnIO가 없다면 우클릭 → CPU 드라이버 설치를 선택하세요. 공식 PawnIO 2.2.0 설치 파일의 SHA-256을 확인하고 설치합니다. 설치에는 인터넷과 관리자 권한이 필요합니다.
- Windows가 트레이 아이콘을 숨기면 작업 표시줄의 `^`에서 확인하세요.

대표 온도는 CPU Package와 GPU Core를 우선 선택합니다. 여러 장치가 있으면 대표 센서 중 현재 온도가 가장 높은 장치를 표시합니다. 실제 온도가 아닌 Distance to TjMax는 제외하고, AMD 미지원 보조 센서의 0을 실제 온도로 표시하지 않습니다.

SOYLAB Comfy Router의 짙은 보라색 배경과 코랄·라벤더·초록 포인트를 사용했습니다. SOYLAB 로고의 컬러 무늬와 중앙 온도계 아이콘을 exe·창·작업 표시줄에 적용했습니다. 원본 자산은 `img`, 제작 프롬프트는 [icon-design.md](img/icon-design.md)에 있습니다.

## 빌드 및 검증

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\launch.ps1
Start-Process .\1.0.0\SoyTemperature.exe -ArgumentList '--self-test' -Wait
Start-Process .\1.0.0\SoyTemperature.exe -ArgumentList '--verify C:\Temp\SoyVerification --verify-exit' -Wait
```

빌드 스크립트는 SDK가 없으면 Microsoft 공식 설치 스크립트로 `.tools/dotnet`에 .NET 10 SDK를 설치합니다. 시스템 PATH는 변경하지 않습니다. 패키지 버전은 `packages.lock.json`에 기록합니다. PNG 변경 후 `export-icon.ps1`로 ICO를 갱신한 뒤 빌드하세요.

`--self-test`는 센서별 최저·최고, null·NaN·무한대 제외와 초기화를 검증합니다. `--verify`는 실제 WinForms 창에서 6회 측정하고 CPU 센서, 트레이 숨기기/복원, 기록 초기화, 작은 창의 글자 맞춤, 가장자리 크기 조절과 Ubuntu 글꼴을 확인합니다. `verification.json`, `samples.jsonl`, `hardware-report.txt`와 실제 창·트레이 이미지를 저장합니다. 읽지 못하는 온도는 null이며 가상 데이터는 사용하지 않습니다.

오류 로그: `%LOCALAPPDATA%/SoyTemperature/errors.log`.

참조: [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [Microsoft 단일 파일 배포](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview), [Ubuntu 글꼴](https://design.ubuntu.com/font), [디자인 색상](https://github.com/soylab-edu/ComfyUI-soylab-router/blob/main/web/router.js). 글꼴 라이선스는 우클릭 메뉴에서 볼 수 있습니다.
