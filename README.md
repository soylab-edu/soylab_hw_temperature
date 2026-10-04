# SOY Temperature

Windows 10/11 x64용 C# WinForms 온도 모니터. LibreHardwareMonitorLib 0.9.6으로 CPU·GPU·SSD/HDD의 현재·최저·최고 온도를 2초마다 읽습니다.

## 실행

[Windows 실행 파일 다운로드](https://github.com/soylab-edu/soylab_hw_temperature/releases/latest)에서 ZIP을 풀고 `SoyTemperature.exe`를 실행하세요. 로컬 빌드 결과는 `dist/SoyTemperature.exe`입니다. .NET 런타임이 들어 있는 단일 exe이므로 사용자는 .NET을 따로 설치할 필요가 없습니다. 재빌드에는 .NET 10 SDK가 필요합니다.

- CPU 온도는 **관리자 권한**과 **PawnIO 드라이버**가 필요합니다. exe를 우클릭해 관리자 권한으로 실행하거나, 앱의 **더 보기 → 관리자 실행**을 사용하세요.
- PawnIO가 없는 다른 PC에서는 **더 보기 → CPU 드라이버 설치**로 공식 PawnIO 2.2.0 설치 파일을 다운로드합니다. SHA-256을 확인한 뒤 UAC 승인을 요청하며, 드라이버 설치에는 인터넷이 필요합니다.
- **최소화**, **X**, **트레이로 내리기**는 창을 트레이로 숨깁니다. CPU와 GPU 아이콘은 각각 반올림한 온도 숫자와 C/G 표시를 보여줍니다. 아이콘에 마우스를 올리면 소수점 온도를 볼 수 있습니다.
- 트레이 아이콘을 클릭하면 창이 열립니다. 완전히 끝내려면 트레이 우클릭 → **종료**, 또는 더 보기 → **종료**를 사용하세요.
- Windows가 아이콘을 숨기면 작업 표시줄의 `^`에서 확인하거나 작업 표시줄 설정에서 항상 표시하도록 선택하세요.
- **기록 초기화** 후 다음 측정부터 최저·최고를 다시 기록합니다. 최저·최고값은 실행 세션의 관측값입니다.
- **센서 상세**는 코어·핫스팟 등 개별 센서를 펼칩니다. **더 보기 → CSV 저장**은 모든 센서의 현재 기록을 저장합니다.

기본 카드는 CPU Package와 GPU Core를 우선 표시합니다. 여러 장치가 있으면 대표 센서 중 현재 온도가 가장 높은 장치를 보여주며, 전체 장치는 센서 상세에서 확인할 수 있습니다. `Distance to TjMax`는 실제 온도가 아니므로 제외합니다. AMD 보조 센서에서 미지원 의미로 반환하는 0은 ‘미지원’으로 표시합니다.

저장장치 온도는 하드웨어와 Windows 저장장치 드라이버의 SMART/NVMe 지원에 따라 달라집니다. 이번 테스트 PC의 **Apple SSD AP2048 / Boot Camp 드라이버**에서는 LibreHardwareMonitor가 온도를 제공하지 않아 **온도 정보 미제공**으로 표시합니다. 장치명은 Windows 장치 목록에서 확인합니다.

## 화면

SOYLAB Comfy Router의 짙은 보라색과 코랄·라벤더·초록 포인트를 사용하고, [ZIRKA](https://www.cssdesignawards.com/sites/zirka-interceptor/50133/)의 큰 흰색 타이포그래피, 얇은 구분선, 눈금과 모서리 포인트를 참고했습니다. 각 카드에는 실제 측정값으로 그린 최근 60초 온도 추이가 표시됩니다. 설치된 SF Pro를 우선 선택하고, 없으면 Segoe UI 계열로 표시합니다. 창 크기와 Windows DPI에 맞춰 글꼴·여백·카드 크기를 조정하며, 온도와 최저·최고값은 공간에 맞게 줄여 표시합니다. 기본 화면에서는 상세 표를 그리지 않고, 트레이에서는 카드와 표를 갱신하지 않습니다.

## 빌드

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

빌드 스크립트는 SDK가 없으면 Microsoft 공식 설치 스크립트로 `.tools/dotnet`에 .NET 10 SDK를 설치합니다. 시스템 PATH를 변경하지 않습니다. 패키지 버전은 `packages.lock.json`에 기록합니다. 결과는 `dist/SoyTemperature.exe`입니다.

관리자 실행:

```powershell
powershell -ExecutionPolicy Bypass -File .\launch.ps1
```

## 검증

```powershell
.\dist\SoyTemperature.exe --self-test
.\dist\SoyTemperature.exe --verify C:\Temp\SoyVerification --verify-exit
```

`--self-test`는 센서별 최저·최고, 값 없음, NaN, 기록 초기화를 검증합니다. `--verify`는 실제 WinForms 창을 띄워 6회 측정하면서 트레이로 숨기기/복원, 기록 초기화, 센서 상세, 작은 창의 글자 맞춤을 확인합니다. `verification.json`, `samples.jsonl`, `hardware-report.txt`, 실행 중인 창의 `DrawToBitmap` 이미지와 트레이 아이콘 이미지를 저장합니다. 실제 센서 데이터만 사용하며, 읽을 수 없는 값은 null입니다.

오류 로그: `%LOCALAPPDATA%/SoyTemperature/errors.log`.

참조: [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [Microsoft 단일 파일 배포](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview), [디자인 색상](https://github.com/soylab-edu/ComfyUI-soylab-router/blob/main/web/router.js).
