# Pulse Widget

Windows 작업 표시줄에 OpenAI(Codex)·Claude Desktop/Code 구독 한도와 PC 자원을 표시하는 작은 네이티브 앱입니다.

이 저장소에는 소스 코드·테스트·설치 스크립트를 보관합니다. 실행 파일은 빌드 후 생성되며, 배포할 때는 GitHub/GitLab의 Release 첨부 파일로 제공합니다. 계정 로그인 정보와 개인 사용량 기록은 저장소에 포함하지 않습니다.

## 저장소 구조

| 경로 | 내용 |
| --- | --- |
| `src/` | WPF 위젯, 사용량 수집, 테마, 작업 표시줄·디스플레이 처리 |
| `bridge/` | Claude Code 상태줄·hook 연동 프로그램 |
| `installer/` | Inno Setup 설치본 정의와 설치 안내 |
| `scripts/` | 포터블·설치본 빌드, Claude 연동 설치·해제 |
| `tests/` | 데이터 처리와 설치·연동 검증 |
| `tests-window/` | 창 배치, 세션 복구, 설정 UI 검증 |
| `dist/`, `artifacts/` | 생성되는 실행·설치 파일과 검증 결과 — Git 제외 |

## 원격 저장소에 올리기

GitHub 또는 GitLab에 빈 저장소를 만든 다음, 아래 예시 주소를 실제 저장소 주소로 바꿔 연결합니다. 최초 업로드 시에는 원격 저장소에서 README·라이선스·`.gitignore`를 자동 생성하지 않아야 로컬 첫 커밋과 충돌하지 않습니다.

```powershell
git remote add origin https://github.com/OWNER/pulse-widget.git
git push -u origin main
```

`.gitignore`는 빌드 폴더·설치 EXE·로그·환경 파일·서명 키 등을 제외합니다. `.gitattributes`는 텍스트 줄바꿈과 바이너리 아이콘을 구분하고, `.editorconfig`는 기본 편집 형식을 맞춥니다. 설치 EXE와 `.sha256` 파일은 빌드 후 Release에 첨부하세요.

## 실행

### 설치본

`artifacts/installer/PulseWidget-1.0.0-Setup-x64.exe` 하나로 설치할 수 있습니다. Windows x64용이며 .NET 런타임을 포함합니다. 한국어 설치 화면을 지원하며 관리자 권한 없이 현재 사용자에게 설치합니다. 기본 설치 위치는 `%LOCALAPPDATA%\Programs\Pulse Widget`입니다.

1. 기존 위젯이 켜져 있으면 위젯/트레이 우클릭 → **종료**합니다.
2. 설치 EXE를 실행하고 설치 위치, **Windows 로그인 시 자동 실행**, **바탕 화면 바로가기**를 선택합니다.
3. 완료 화면에서 **Pulse Widget 실행**을 선택합니다. 이후 시작 메뉴의 **Pulse Widget**으로 실행할 수 있습니다.

기존 포터블 버전의 위치·테마·사용자 색상·디스플레이 설정을 그대로 읽습니다. 자동 실행을 선택하면 등록 경로를 새 설치 폴더로 변경합니다. 이후 새 설치본을 같은 위치에 실행하면 업데이트되며, 위젯에서 바꾼 자동 실행 켜기/끄기도 기본 선택에 반영합니다. 설치 프로그램은 실행 중인 위젯을 강제 종료하지 않습니다.

제거: 위젯을 종료한 뒤 Windows **설정 → 앱 → 설치된 앱 → Pulse Widget → 제거**. 프로그램, 바로가기, 해당 설치본의 자동 실행 등록을 제거합니다. `%LOCALAPPDATA%\PulseWidget`의 사용자 설정·사용량 기록과 기존 Claude Code 연동은 보존합니다. Claude Code 연동까지 해제하려면 프로그램 제거 전에 설치 폴더의 `scripts/Uninstall-Claude.ps1`을 실행합니다.

설치 파일에는 계정 인증 정보, 사용자 설정, 사용량 기록이 포함되지 않습니다. 다른 PC에서는 해당 PC의 Codex/Claude 로그인과 로컬 사용 기록을 사용합니다. 현재 설치 EXE에는 코드 서명이 적용되지 않아 Windows가 게시자를 확인하지 못할 수 있습니다. 함께 제공되는 `.sha256` 파일로 파일 해시를 확인할 수 있습니다.

### 포터블 실행

`dist/PulseWidget.exe`를 실행합니다. 배포본은 Windows x64용이며 .NET 설치 없이 실행됩니다. 다른 폴더로 옮길 때는 DLL·bridge·scripts를 포함한 **dist 폴더 전체**를 복사하세요. 트레이의 Pulse 아이콘을 클릭하면 상세창이 열립니다. 같은 앱을 여러 번 실행해도 하나만 유지됩니다.

- OpenAI: 주간 사용 비율 / 남은 비율, 이 PC의 Codex 작업 상태
- Claude: 주간 사용 비율 / 남은 비율, 5시간 사용 비율, 작업 상태. 상세창에서는 5시간 잔여 비율도 표시
- CPU·메모리: 2초 간격 갱신. 메모리 상세는 사용 GB / 전체 GB
- 사용량: OpenAI 60초마다 조회. Claude Desktop의 로컬 사용량 기록 또는 Code 상태줄 데이터를 6초마다 확인
- 사용량 막대: 작업 표시줄과 상세창에 Codex 주간·Claude 주간·5시간 사용 비율을 표시. Claude 막대는 왼쪽이 주간, 오른쪽이 5시간이며 마우스를 올리면 사용·잔여 비율 표시
- 상세창: 초기화 시각, 수신 시각, 위치 슬라이더와 % 직접 입력, 새로고침, Claude 연결
- 위젯 우클릭: 좌우 이동, 기본 위치, 작업 표시줄 위로 이동, 자동 실행, Codex 경로, 종료
- 화면만 사라졌다면: 위젯 자리 우클릭 → **화면 다시 표시**로 위치와 테마를 유지한 채 재시작
- 테마: 위젯 우클릭 → **테마 → 종류**, 상세창의 **테마 선택** 버튼 또는 트레이 우클릭 → **테마 → 종류**에서 즉시 변경. 선택은 다음 실행에도 유지
- 사용자 지정 색상: 위젯 우클릭 → **테마 → 색상 편집…**, 상세창 또는 트레이의 **색상 편집…**
- 디스플레이: 위젯 우클릭 → **디스플레이**, 상세창의 **디스플레이 설정**, 트레이의 **디스플레이**

사용량은 토큰 수가 아닌 **구독 한도의 사용 비율**입니다. 남은 비율은 `100 - 사용 비율`입니다. 한도가 없는 경우, 아직 수신하지 않은 경우, 초기화 시각이 지난 값은 `—`입니다. 10분 이상 갱신되지 않은 값은 `*`와 수신 시각으로 구분합니다.

## 테마

| 테마 | 색상 |
| --- | --- |
| 그라파이트 | 차콜 배경과 민트 포인트 · 기본 |
| 페이퍼 | 밝은 아이보리와 짙은 글자 |
| 미드나이트 | 네이비와 아이스 블루 |
| 포레스트 | 깊은 녹색과 세이지 |
| 플럼 | 자주색과 라벤더 |
| Claude · 라이트 / 다크 | 크림·테라코타 / 따뜻한 차콜·살구색 |
| OpenAI · 라이트 / 다크 | 흰색·검정 / 중성 회색 레이어 |
| Material · 라이트 / 다크 | 라벤더 / 짙은 보라, 둥근 카드와 알약형 버튼 |
| Fluent · 라이트 / 다크 | 밝은 회청색 / 짙은 청회색, 얕은 레이어와 파란 포인트 |
| Apple HIG · 라이트 / 다크 | 밝은 회색·흰 카드 / 검정·회색 카드, 시스템 블루 |

총 15종이며, 상세창의 선택창에서 색상 미리보기와 종류별 라이트·다크 모드를 고를 수 있습니다. 목록은 스크롤할 수 있고 Esc 또는 바깥 클릭으로 닫힙니다. 위젯·상세창·메뉴·툴팁·사용량 막대·작업 상태·트레이 아이콘에 함께 적용됩니다. 테마마다 모서리와 버튼 모양, 카드 그림자도 다릅니다. 설정 파일의 `ThemeId`에 선택이 저장되며, 이전 버전의 설정이나 알 수 없는 테마 ID는 기본 테마로 열립니다. 위치와 자동 실행 설정은 테마와 별개로 유지됩니다.

브랜드 및 디자인 시스템에서 영감을 얻은 위젯용 테마입니다. Fluent의 표면은 가독성과 작업 표시줄 호환성을 위한 불투명 그라데이션으로 표현하며, 실제 Windows Acrylic/Mica나 macOS의 배경 블러를 사용하지 않습니다. 디자인 참고: [UI 스타일 모음](https://miny.tistory.com/30), [Material](https://m3.material.io/), [Windows 디자인](https://learn.microsoft.com/windows/apps/design/), [Apple HIG](https://developer.apple.com/design/human-interface-guidelines/).

기본 테마 15종 외에 **사용자 지정** 색상 한 세트를 저장할 수 있습니다. 배경·카드·글자·테두리·OpenAI·Claude·상태·버튼 등 15개 색상을 Windows 색상 선택창에서 고릅니다. 작업 표시줄과 **실제 상세창과 동일한 구성의 미리보기**가 색상 변경에 따라 함께 갱신됩니다. 상세 미리보기에는 카드·사용량 막대·입력란·버튼까지 표시하며 스크롤해 전체를 확인할 수 있습니다. 편집창은 크기 조절이 가능하고 좁은 화면에서는 미리보기와 색상 선택 영역을 위아래로 배치합니다. 미리보기에는 예시 수치를 쓰며 **적용**을 눌러야 실제 위젯과 설정에 반영됩니다. 취소는 기존 설정을 유지합니다. **현재 테마 색상 복사** 또는 **기준 테마의 기본색**으로 다시 시작할 수 있습니다. 모양·폰트·크기는 편집하지 않으며, 불투명 RGB 색상만 지원합니다. 다른 기본 테마로 바꿔도 사용자 색상은 남아 있습니다.

개발 시 기본 테마는 `src/Core/ThemeCatalog.cs`, 사용자 색상은 `src/Core/CustomTheme.cs`, 편집창은 `src/Theming/ThemeEditorWindow.cs`에서 관리합니다. `WidgetController`가 데이터 조회·트레이·세션 복구를 한 번씩 실행하고, 디스플레이별 `WidgetWindow`는 같은 데이터와 테마를 표시합니다.

## 디스플레이 선택

- **한 디스플레이에만 표시**: 원하는 디스플레이를 선택하거나 **주 디스플레이 (자동)**을 사용합니다.
- **모든 디스플레이에 표시**: 연결된 화면마다 하나씩 표시합니다. 트레이와 사용량 조회는 늘어나지 않습니다.

화면 목록에는 해상도와 주 화면 기준 방향이 함께 표시됩니다. 선택은 재실행·로그온 후에도 유지됩니다. 선택한 화면이 분리되면 주 화면에 임시 표시하고 선택 정보는 유지하므로 다시 연결하면 돌아갑니다. 모든 화면 모드에서는 연결된 화면을 따라 창을 추가하거나 제거합니다. 테마와 가로 위치 비율(%)은 모든 위젯에 공통 적용합니다. 가로 위치는 각 화면에서 위젯이 이동할 수 있는 범위를 기준으로 하며, 0%는 왼쪽 끝, 50%는 중앙, 100%는 오른쪽 끝입니다. 해상도나 배율이 달라도 같은 정렬을 유지합니다. 위젯보다 좁은 화면에서는 왼쪽 끝에 맞춥니다. 각 화면의 배율과 음수 좌표를 고려합니다. Windows에서 보조 작업 표시줄을 숨긴 화면은 화면 아래쪽에 독립 창으로 표시합니다.

상세창의 **가로 위치** 슬라이더(0~100%)나 옆의 **% 입력란**, 또는 우클릭 → **가로 위치**에서 조정합니다. 입력란은 소수를 지원하며 Enter, **적용**, 다른 항목으로 포커스 이동 시 저장합니다. 잘못된 값은 안내를 표시하고 기존 위치를 유지합니다. 입력 중 Esc는 저장된 값으로 되돌립니다. 입력란과 슬라이더는 모든 상세창에서 함께 갱신됩니다. 메뉴에서 1%씩 이동하거나 0%·50%·100%로 바로 맞출 수 있습니다. 기존 픽셀 설정은 최초 실행 시 선택된 디스플레이(모든 화면 모드에서는 주 화면)를 기준으로 한 번만 변환하여 저장합니다. 이후에는 해상도가 바뀌어도 저장된 비율을 사용합니다.

## 작업 표시줄 표시 방식

작업 표시줄 안에서는 위젯 창을 작업 표시줄의 자식 창으로 붙여, 시작 메뉴·검색창이 작업 표시줄을 더 높은 표시 계층으로 올려도 함께 보이도록 합니다. Explorer에 코드를 주입하거나 Explorer 창의 스타일을 바꾸지 않습니다. DPI 모드가 다르거나 연결에 실패하면 독립적인 최상위 창으로 표시하며 다시 연결을 시도합니다. Windows의 정식 도구 모음이나 공간을 예약하는 DeskBand는 아닙니다.

선택한 모니터의 작업 표시줄을 따라갑니다. 기본값인 **항상 표시**에서는 화면 크기의 창이 활성화되어도 위젯을 스스로 숨기지 않습니다. **작업 표시줄 위에 표시** 또는 자동 숨김 상태에서는 독립 창으로 돌아가 화면 안에 유지합니다. 우클릭 메뉴에서 항상 표시 옵션을 끄면 이전처럼 전체 화면과 자동 숨김에 맞춰 숨깁니다. 해상도 변경은 다음 주기에 위치를 다시 계산합니다. Explorer 종료로 위젯 창이 무효화되면 위젯이 자동 재실행되어 다시 연결합니다. 시작 메뉴가 이미 열려 연결이 거절된 경우에는 메뉴를 닫으면 자동으로 연결됩니다.

일반 데스크톱의 최상위 창 방식을 사용하므로 잠금 화면·UAC 보안 화면·일부 독점 전체 화면이나 Windows가 더 높은 계층에 표시하는 UI 위까지 보장하지는 않습니다.

원격 데스크톱 접속·로컬 복귀·잠금 해제·디스플레이 변경·절전 복귀 시에는 이벤트가 약 2초간 안정된 뒤 위젯을 자동 재시작합니다. 창이 클릭되지만 그림만 사라지는 경우에도 WPF 렌더링 표면과 작업 표시줄 연결을 새로 만듭니다. 연결 해제나 잠금 중에는 복구를 대기하며, 연속된 알림은 하나로 합칩니다. 창을 만들기 전 이 앱에만 소프트웨어 렌더링을 적용하여 원격/로컬 GPU 전환의 영향을 줄입니다. 설정과 Windows 자동 실행 등록은 변경하지 않으며, 복구 기록은 `%LOCALAPPDATA%\PulseWidget\recovery.log`에 남습니다.

작업 표시줄을 클릭해 창 순서가 바뀌면 Windows 이벤트를 받아 즉시 위젯의 표시 순서를 유지합니다. 데이터의 2초 갱신 주기를 기다리지 않으며, 위치·크기·표시 순서가 같으면 불필요한 재배치를 하지 않습니다. 키보드 포커스는 가져오지 않습니다.

작업 표시줄 아이콘의 공간을 자동으로 확보하지는 않습니다. 기존 버튼과 겹치면 상세창의 **가로 위치** 슬라이더 또는 우클릭 메뉴로 빈 공간에 옮기세요. 여유 공간이 없으면 **작업 표시줄 위에 표시**를 사용할 수 있습니다. 가로 작업 표시줄을 지원하며 세로 작업 표시줄은 지원하지 않습니다.

## OpenAI 연결

기존 Codex 로그인과 공식 `codex app-server`의 `account/rateLimits/read`를 사용합니다. `PATH` 및 Codex 데스크톱 앱의 설치 경로에서 `codex.exe`를 찾습니다. 찾지 못하면 위젯 우클릭 → **Codex 실행 파일 선택**을 사용하세요. API 키를 입력할 필요가 없으며 위젯은 인증 토큰을 읽거나 저장하지 않습니다. 추론 요청, 크레딧 구매, 한도 초기화 요청을 보내지 않습니다.

`rateLimitsByLimitId.codex`를 우선 사용하며 `windowDurationMins=10080`인 항목만 주간으로 처리합니다. 해당 계정이 주간 한도를 반환하지 않으면 임의의 다른 한도를 주간으로 표시하지 않습니다.

작업 상태는 `$CODEX_HOME/sessions`(기본 `~/.codex/sessions`)의 로컬 실행 이벤트를 읽습니다. 동시에 실행 중인 세션을 합산하고, 종료 이벤트 없이 15분 이상 소식이 없으면 **확인 필요**로 표시합니다. 원격 PC의 작업은 감지하지 않습니다. Codex 로그 형식이 변경되면 감지 규칙을 조정해야 합니다. 프롬프트나 응답을 저장·전송하지 않습니다.

## Claude Desktop 사용량

Claude Desktop이 저장하는 `plan-usage-history.json`을 자동으로 읽습니다. 별도의 CLI 로그인이나 새 대화가 필요하지 않고, 위젯 재실행 시에도 마지막 기록이 표시됩니다. 일반 설치의 `%APPDATA%/Claude`와 Microsoft Store/MSIX 설치의 `%LOCALAPPDATA%/Packages/Claude_*/LocalCache/Roaming/Claude`를 탐색합니다.

Desktop 기록의 `sd`는 주간 전체 모델 사용 비율, `fh`는 5시간 사용 비율이며 단위는 이미 %입니다. 버전 1과 2를 지원하고, 가장 최근 시각의 한 기록만 사용해 계정/조직 사이의 값을 섞지 않습니다. 조직 식별자는 위젯에서 저장하거나 출력하지 않습니다. Desktop 기록이 있으면 터미널 상태줄보다 우선합니다.

위젯은 파일 변경을 6초 안에 반영하지만 서버 조회는 Desktop 앱이 담당합니다. 확인한 Desktop 3.6556.2.0은 활동에 따라 약 5~15분 간격으로 조회하고, 기록 저장은 최소 4분 30초 간격으로 제한합니다. 앱이 꺼져 있거나 조회가 중단되면 마지막 기록이 유지되며, 10분 이상 지난 값에는 `*`가 붙습니다. 빠른 확인이 필요하면 Claude Desktop의 사용량 메뉴를 여세요. 위젯의 새로고침은 Desktop 서버 조회를 강제하지 않습니다.

이 기록에는 초기화 시각이 없어 상세창에 **초기화 시각 정보 없음**으로 표시합니다. 오래된 기록을 현재 한도나 새 0%로 추정하지 않습니다. 비공개 로컬 파일 형식이므로 Desktop 업데이트에 따라 대응이 필요할 수 있습니다. 위젯은 Desktop 인증 토큰이나 쿠키를 읽지 않고, 사용량 조회를 위한 추론 요청도 보내지 않습니다.

## Claude 작업 상태 / 터미널 Code 연결

상세창의 **Claude 연결** 또는 `dist/scripts/Install-Claude.ps1`을 실행하세요. 기존 설정을 `.pulse-backup-날짜`로 백업한 뒤 공식 `statusLine`과 hook을 추가합니다. 이 작업은 연동 설정 설치이며 실제 사용량 수신 성공을 뜻하지 않습니다. 터미널 Claude Code는 지원 구독의 첫 응답 후 `rate_limits`를 상태줄로 전달합니다. 구독 종류·클라이언트에 따라 제공되지 않을 수 있습니다. 일반 Claude 웹 채팅은 작업 감지 대상이 아닙니다.

이미 실행 중인 Claude 세션에는 설정 변경이 즉시 적용되지 않을 수 있습니다. 기존 세션을 방해하지 말고 다음 새 세션에서 확인하세요. Desktop Code의 작업 상태는 hook으로, Desktop 구독 사용량은 위의 로컬 기록으로 따로 읽습니다. 작업 상태만 있고 사용량 기록이 없으면 Desktop 사용량 메뉴를 열어 확인하라는 안내가 표시됩니다.

터미널 상태줄에서 수신한 한도는 `%LOCALAPPDATA%/PulseWidget/claude/usage.json`에 저장되고 Desktop 기록이 없을 때 사용합니다. **위젯을 켤 때마다 새 Claude 작업을 할 필요는 없습니다.** 일부 한도만 전달되거나 빈 응답이 오면 기존 한도를 보존하며 각 한도의 수신 시각도 유지합니다. 초기화 시각이 지난 한도는 새 데이터가 올 때까지 `—`로 표시하며, 이를 0%로 추정하지 않습니다. 이 파일이 없어도 Desktop 사용량 기록이 있으면 표시됩니다.

`PulseBridge`는 구독 한도 숫자, 초기화 시각, 수신 시각과 세션의 작업 상태만 `%LOCALAPPDATA%/PulseWidget/claude`에 기록합니다. 세션 식별자는 해시 처리하고 프롬프트·응답·토큰·도구 입력을 기록하지 않습니다. 여러 세션은 개별 상태 파일로 처리합니다. 진행 중인 작업이 이벤트 없이 15분 이상 지속되면 확인 필요로 표시합니다.

다른 PC나 Windows 계정에서 가져온 기존 Pulse 연동은 **Claude 연결**을 다시 실행하면 백업 후 현재 계정의 `%LOCALAPPDATA%/PulseWidget/bridge/PulseBridge.exe` 경로로 갱신합니다. 기존 Pulse hook은 중복을 정리해 이벤트별 하나씩 등록하며, 같은 그룹에 있는 다른 hook과 그 설정은 보존합니다. `-DataDirectory`를 지정하면 해당 경로를 사용합니다.

Pulse 이외의 사용자 정의 상태줄이 있으면 자동 설치는 변경하지 않고 중단합니다. 자동 갱신 대상은 절대 경로의 `PulseBridge.exe`를 `statusline` 또는 `hook` 인수 하나로 직접 실행하는 설정입니다. 추가 명령이나 래퍼 스크립트가 있는 사용자 정의 명령은 변경하지 않습니다. 그 경우 기존 상태줄 스크립트가 받은 JSON을 `PulseBridge.exe statusline`의 stdin에도 전달하고 출력은 무시하도록 수동으로 연결할 수 있습니다. 프로젝트 설정에서 사용자 설정을 덮어쓰면 해당 프로젝트에도 별도 연동이 필요합니다.

해제: `dist/scripts/Uninstall-Claude.ps1`. 이전 PC·계정 경로를 포함한 위의 Pulse 직접 실행 항목을 제거하며 다른 설정은 보존합니다. 자동 실행은 위젯 우클릭에서 별도로 해제합니다.

## 빌드 및 검증

Windows x64와 .NET 9 SDK:

```powershell
dotnet build src/PulseWidget.csproj -c Release
dotnet run --project tests/PulseWidget.Tests.csproj -c Release
dotnet run --project tests-window/PulseWidget.WindowTests.csproj -c Release
powershell -NoProfile -File scripts/Build.ps1
powershell -NoProfile -File tests/Test-Bridge.ps1
powershell -NoProfile -File tests/Test-ClaudeIntegration.ps1
```

기존 .NET Desktop Runtime 9를 사용할 경량 배포본은 `scripts/Build.ps1 -FrameworkDependent`로 만듭니다.

설치본은 [Inno Setup 7.1.1 이상](https://jrsoftware.org/isdl.php)이 추가로 필요합니다. 아래 명령은 별도 `artifacts/installer-payload-버전` 폴더에 빌드하므로 실행 중인 `dist` 파일을 덮어쓰지 않습니다.

```powershell
.\scripts\Build-Installer.ps1 -Version 1.0.0
# 컴파일러 경로를 직접 지정하는 경우:
.\scripts\Build-Installer.ps1 -Version 1.0.1 -CompilerPath 'C:\Program Files\Inno Setup 7\ISCC.exe'
# 현재 사용자 설정과 실행 중인 위젯을 보존하는 설치/재설치/제거 검증:
.\tests\Test-Installer.ps1 -Version 1.0.0
```

결과는 `artifacts/installer/PulseWidget-버전-Setup-x64.exe`와 SHA-256 파일입니다. `installer/PulseWidget.iss`의 제품 ID는 업데이트 호환성을 위해 유지합니다. 테스트 설치본은 별도의 제품 ID·자동 실행 이름·바로가기를 사용하며 검증 후 제거합니다. 아이콘은 기존 Pulse 표시를 바탕으로 생성한 `src/Assets/PulseWidget.ico`이며 `scripts/New-AppIcon.ps1`로 재생성할 수 있습니다.

실제 계정과 시스템 진단(추론 요청 없음):

```powershell
Start-Process -FilePath ./dist/PulseWidget.exe -ArgumentList '--diagnose' -WindowStyle Hidden -Wait
Get-Content "$env:LOCALAPPDATA/PulseWidget/diagnostics.json"
```

설정·오류 로그는 `%LOCALAPPDATA%/PulseWidget`에 저장합니다. UI는 WPF, 트레이는 NotifyIcon, PC 수집은 Windows `GetSystemTimes`/`GlobalMemoryStatusEx`를 사용합니다. 외부 NuGet 패키지는 없습니다.

## 연동 근거

- [OpenAI 공식 Codex App Server](https://learn.chatgpt.com/docs/app-server)
- [Claude Code 공식 상태줄 데이터](https://code.claude.com/docs/en/statusline)
- [Claude Code 공식 작업 이벤트](https://code.claude.com/docs/en/hooks)
