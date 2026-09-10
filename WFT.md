# Chat Export - Assistant

**Exported**: 2026-09-09 09:35:01
**Timezone**: UTC+09:00

\---

**User**:
\*제품명 : 전자칠판 플로팅 툴 \*기능 : 전자 칠판 사용시 윈도우 화면전환, 웹브라우저 탭 가능(크롬, 에지), 윈도우 작업보기, 윈도우 스냅, 윈도우 스냅레이아웃, 기능 창에서 해당 기능만 선택하도록 구현. 플로팅 툴의 투명도 조절 가능하도록 구현. 드래그 가능하도록 구현. \*목적 : 플로팅 툴에서 교사가 직접 해당 기능을 클릭하면 바로 전화 가능하도록 구현. 키보드를 사용하려면 시간이 걸리므로 이를 개선하기 위함. \* 이프로그램을 개발하기 위한 skill.md 문서를 작성해줘. 내가 기술하지 않았지만 교사에게 도움되는 기능 있으면 추가해줘.

\---

**Assistant** (Claude Fable 5.1):
아래는 요청하신 **전자칠판 플로팅 툴** 개발용 `SKILL.md` 문서입니다. 말씀하신 기능을 모두 포함하고, 교사 수업 환경에서 실제로 도움이 되는 기능들을 "추가 제안 기능" 섹션에 별도로 정리했습니다. 그대로 저장해서 사용하실 수 있도록 마크다운 파일 형태로 작성했습니다.

\---

````markdown
---
name: whiteboard-floating-tool
description: 전자칠판(Windows) 환경에서 교사가 키보드 없이 터치/클릭만으로 창 전환, 브라우저 탭 전환, 작업 보기, 스냅, 스냅 레이아웃 등을 즉시 실행할 수 있는 항상-위(Always-on-top) 플로팅 툴을 설계·구현할 때 사용하는 스킬. 투명도 조절, 드래그 이동, 기능 선택(커스터마이징), 수업 보조 기능(판서, 스포트라이트, 타이머 등) 구현 가이드를 포함한다.
---

# 전자칠판 플로팅 툴 (Whiteboard Floating Tool)

## 1. 개요

### 1.1 배경과 목적
전자칠판은 대부분 교실 앞에 설치되어 있고, 교사는 칠판 앞에 서서 수업을 진행한다.
이때 창 전환(Alt+Tab), 브라우저 탭 전환(Ctrl+Tab), 작업 보기(Win+Tab), 창 스냅(Win+방향키) 같은
작업은 모두 \*\*키보드 단축키\*\*에 의존하기 때문에, 교사가 교탁의 키보드로 돌아가거나
화면 키보드를 띄워야 하는 시간 손실이 발생한다.

이 스킬은 \*\*터치 한 번으로 해당 기능을 즉시 실행\*\*하는 플로팅 툴을 만들어
수업 흐름이 끊기지 않도록 하는 것을 목표로 한다.

### 1.2 핵심 원칙
1. \*\*원터치 즉시 실행\*\* : 어떤 기능도 2번 이상 탭하지 않도록 설계한다.
2. \*\*포커스를 빼앗지 않는다\*\* : 플로팅 툴을 눌러도 현재 작업 중인 창의 포커스가 유지되어야 한다. (가장 중요한 기술 요건, 4.1절 참고)
3. \*\*수업 방해 최소화\*\* : 작게, 투명하게, 원하는 곳으로 옮길 수 있어야 한다.
4. \*\*교사가 직접 구성\*\* : 필요한 기능만 골라서 표시할 수 있어야 한다.
5. \*\*터치 우선 UX\*\* : 손가락으로 조작하므로 버튼 최소 크기 44×44px(권장 56×56px)을 지킨다.

---

## 2. 사용 시점 (When to use this skill)
- 전자칠판/터치 모니터용 Windows 유틸리티(플로팅 런처, 퀵바, 도크)를 설계하거나 구현할 때
- 키보드 단축키를 터치 버튼으로 대체하는 기능을 구현할 때
- Always-on-top, 포커스 비활성(WS\_EX\_NOACTIVATE) 창을 WPF/WinUI/Electron 등으로 만들 때
- 브라우저(Chrome/Edge) 탭 목록을 외부 프로그램에서 읽고 전환하는 기능을 구현할 때

---

## 3. 기술 스택 선택

| 항목 | 1순위 (권장) | 대안 | 비고 |
|---|---|---|---|
| 언어/프레임워크 | \*\*C# / .NET 8 + WPF\*\* | WinUI 3, Electron, Python(PyQt6) | Win32 API 호출(P/Invoke)이 쉽고, 투명 창·Always-on-top 구현이 가장 안정적 |
| 창 제어 | Win32 API (`user32.dll`, `dwmapi.dll`) | – | `EnumWindows`, `SetForegroundWindow`, `SendInput` 등 |
| 브라우저 탭 제어 | \*\*UI Automation (System.Windows.Automation / FlaUI)\*\* | 키보드 시뮬레이션(Ctrl+Tab), 브라우저 확장 + Native Messaging | UIA는 탭 제목 목록을 읽어 특정 탭으로 직접 점프 가능 |
| 설정 저장 | JSON (`%APPDATA%\\WhiteboardFloatingTool\\settings.json`) | – | 사람이 읽고 고칠 수 있게 |
| 배포 | MSIX 또는 Inno Setup | 단일 exe(Self-contained) | 학교 전산 환경은 관리자 권한 없는 경우가 많으니 \*\*사용자 단위 설치\*\* 지원 |
| 로깅 | Serilog (파일 롤링) | – | 학교 현장 문제 재현이 어려우므로 로그 필수 |

> Electron을 선택할 경우 `alwaysOnTop`, `focusable: false`, `setIgnoreMouseEvents` 등으로 유사 구현이 가능하지만,
> 창 열거·스냅·UIA 접근을 위해 결국 네이티브 모듈이 필요하므로 WPF를 권장한다.

---

## 4. 아키텍처

```
WhiteboardFloatingTool/
├─ src/
│  ├─ App/                     # WPF 진입점, DI 구성, 트레이 아이콘
│  ├─ UI/
│  │  ├─ FloatingBar/          # 메인 플로팅 바 (드래그, 투명도, 접기/펼치기)
│  │  ├─ Panels/               # 창 목록 패널, 탭 목록 패널, 스냅 레이아웃 패널
│  │  ├─ Settings/             # 기능 선택, 투명도, 크기, 위치, 테마 설정 창
│  │  └─ Overlay/              # 판서, 스포트라이트, 블랙아웃 등 전체화면 오버레이
│  ├─ Core/
│  │  ├─ Actions/              # IAction 인터페이스 + 기능별 구현체 (아래 표)
│  │  ├─ Windowing/            # Win32 창 열거/전환/스냅 래퍼
│  │  ├─ Browser/              # Chrome/Edge 탭 열거·전환 (UIA)
│  │  ├─ Input/                # SendInput 단축키 시뮬레이터
│  │  └─ Settings/             # 설정 모델, 로드/저장, 마이그레이션
│  └─ Interop/                 # P/Invoke 선언 (NativeMethods.cs)
├─ tests/                      # 단위 테스트 (Action 로직, 설정 직렬화)
├─ docs/                       # 사용자 매뉴얼, 관리자 배포 가이드
└─ SKILL.md
```

### 4.1 반드시 지켜야 할 핵심 기술 요건: 포커스 비활성 창
플로팅 툴이 클릭될 때 \*\*포커스를 가져가면\*\* 이후 "창 전환", "탭 전환" 명령이
플로팅 툴 자신에게 전달되어 동작하지 않는다. 따라서 메인 창에 다음을 적용한다.

```csharp
// WPF Window의 SourceInitialized 이벤트에서
const int GWL\_EXSTYLE      = -20;
const int WS\_EX\_NOACTIVATE = 0x08000000;
const int WS\_EX\_TOOLWINDOW = 0x00000080;  // Alt+Tab, 작업 보기 목록에서 자기 자신을 숨김
const int WS\_EX\_TOPMOST    = 0x00000008;

var hwnd = new WindowInteropHelper(this).Handle;
int ex = GetWindowLong(hwnd, GWL\_EXSTYLE);
SetWindowLong(hwnd, GWL\_EXSTYLE, ex | WS\_EX\_NOACTIVATE | WS\_EX\_TOOLWINDOW | WS\_EX\_TOPMOST);
```

- `ShowActivated = false`, `Topmost = true`, `ShowInTaskbar = false`, `WindowStyle = None`, `AllowsTransparency = true`
- 버튼은 `PreviewMouseDown`/`TouchDown`에서 처리하고, 명령 실행 전 \*\*직전 활성 창 핸들\*\*(`GetForegroundWindow()`로 주기 캐싱)을 기억해 두었다가 그 창을 대상으로 동작시킨다.
- 전체화면 앱(PPT 슬라이드쇼 등) 위에서도 보이도록 `SetWindowPos(HWND\_TOPMOST)`를 주기적으로(예: 2초) 재적용한다.

---

## 5. 기능 명세

### 5.1 기본 요구 기능 (사용자 요청)

| ID | 기능 | 동작 방식 | 구현 방법 |
|---|---|---|---|
| F-01 | \*\*윈도우 화면 전환\*\* | 버튼 탭 → 열려 있는 창 목록(썸네일+제목) 팝업 → 탭하면 해당 창 전면으로 | `EnumWindows` + `IsWindowVisible` + `DwmGetWindowAttribute(DWMWA\_CLOAKED)`로 실제 창만 필터 → `SetForegroundWindow`(최소화 상태면 `ShowWindow(SW\_RESTORE)` 먼저). 썸네일은 `DwmRegisterThumbnail` 또는 `PrintWindow` |
| F-02 | \*\*이전/다음 창 빠른 전환\*\* | 목록 없이 즉시 Alt+Tab 1회 효과 | 캐싱된 Z-order 목록 기준 다음 창 `SetForegroundWindow`. 키 시뮬레이션(Alt+Tab)은 UIPI 문제로 불안정하므로 API 우선 |
| F-03 | \*\*브라우저 탭 전환 (Chrome/Edge)\*\* | 버튼 탭 → 현재 활성 브라우저의 탭 목록 팝업 → 탭 선택 시 이동 | UI Automation: 브라우저 창에서 `ControlType.TabItem` 열거 → `SelectionItemPattern.Select()`. 실패 시 폴백으로 `Ctrl+1\~8`, `Ctrl+Tab` SendInput |
| F-04 | \*\*이전/다음 탭\*\* | 즉시 전환 | 활성 창이 chrome.exe/msedge.exe인지 확인 후 `Ctrl+Shift+Tab` / `Ctrl+Tab` SendInput |
| F-05 | \*\*윈도우 작업 보기\*\* | Win+Tab 화면 열기 | `SendInput`으로 `VK\_LWIN + VK\_TAB`. (`keybd\_event`보다 `SendInput` 사용) |
| F-06 | \*\*윈도우 스냅\*\* | 좌/우/상/하 4개 버튼 | 대상 창 핸들에 `Win+←/→/↑/↓` SendInput, 또는 `MonitorFromWindow` + `GetMonitorInfo`로 작업영역 계산 후 `SetWindowPos` 직접 배치(더 안정적) |
| F-07 | \*\*스냅 레이아웃\*\* | Win11 스냅 레이아웃 팝업 또는 자체 레이아웃 패널(2분할, 3분할, 4분할) | Win11: `Win+Z` SendInput. Win10 호환/자체 패널: 레이아웃 그리드 탭 → 각 슬롯 탭 시 창 선택 후 `SetWindowPos` |
| F-08 | \*\*기능 선택(커스터마이징)\*\* | 설정에서 체크한 기능만 바에 표시, 순서 드래그 변경 | `settings.json`의 `enabledActions: \["switchWindow","tabList",...]` 배열, 순서 = 표시 순서 |
| F-09 | \*\*투명도 조절\*\* | 슬라이더(20\~100%), 마우스/터치가 올라오면 자동 100% 옵션 | `Window.Opacity` 바인딩. 클릭 무시 영역이 없도록 최소 20% 하한 |
| F-10 | \*\*드래그 이동\*\* | 손잡이(grip) 영역 드래그, 화면 밖으로 못 나가게 클램프, 모서리 근접 시 스냅 | `DragMove()`는 NOACTIVATE 창에서 불안정하므로 `PreviewMouseDown`→`MouseMove` 좌표 차이로 `Left/Top` 직접 갱신. 멀티모니터 경계는 `SystemParameters.VirtualScreen\*` 사용 |

### 5.2 추가 제안 기능 (교사 수업 편의)
추후 사용자가 구현 하라고 할때 구현


교사에게 실질적인 도움이 되는 기능으로, 우선순위(★)를 매겼다. F-08 기능 선택에 포함시켜 원하지 않으면 끌 수 있게 한다.

| ID | 우선순위 | 기능 | 설명 | 구현 힌트 |
|---|---|---|---|---|
| A-01 | ★★★ | \*\*화면 판서 (스크린 펜)\*\* | 어떤 화면 위에서든 펜/형광펜/지우개/전체 지우기. PPT, 웹, PDF 위에 바로 쓰기 | 전체화면 투명 오버레이 Window + InkCanvas. 판서 종료 시 오버레이 제거. 판서 중에는 하부 창 클릭 통과 안 됨(의도된 동작) |
| A-02 | ★★★ | \*\*화면 가리기(블랙아웃/커튼)\*\* | 정답 공개 전, 주의 집중 시 화면 전체 또는 일부를 검게 가림. 한 번 더 탭하면 해제 | 검은 전체화면 오버레이. 부분 가리기는 드래그로 사각형 영역 지정 |
| A-03 | ★★★ | \*\*스포트라이트 / 확대(돋보기)\*\* | 특정 영역만 밝게 강조하거나 2\~4배 확대 | 반투명 검정 오버레이에 원형 구멍(Geometry Combine). 확대는 `Magnification API` 또는 화면 캡처 후 확대 렌더 |
| A-04 | ★★★ | \*\*타이머 / 스톱워치\*\* | 활동 시간 제한(1/3/5/10분 프리셋), 종료 시 부드러운 알림음과 화면 깜빡임 | 항상-위 작은 위젯. 큰 숫자로 표시(교실 뒤에서도 보이게) |
| A-05 | ★★☆ | \*\*PPT 슬라이드 제어\*\* | 이전/다음/처음/슬라이드쇼 시작·종료 버튼. 활성 창이 PowerPoint일 때 자동 표시 | 활성 프로세스가 `POWERPNT.EXE`이면 컨텍스트 버튼 노출. `PageUp/PageDown/F5/Esc` SendInput. 한쇼(Hancom Show)도 동일 키 지원 |
| A-06 | ★★☆ | \*\*화면 캡처 + 판서 저장\*\* | 현재 화면(판서 포함)을 PNG로 캡처해 지정 폴더에 날짜별 저장, 클립보드 복사 | `Graphics.CopyFromScreen` 또는 `Windows.Graphics.Capture`. 수업 기록/학생 공유용 |
| A-07 | ★★☆ | \*\*바로가기 런처\*\* | 자주 쓰는 프로그램·폴더·URL을 등록해 원터치 실행(e-학습터, 구글 클래스룸, 교과서 뷰어 등) | `Process.Start` with `UseShellExecute=true`. 아이콘은 `ExtractAssociatedIcon` |
| A-08 | ★★☆ | \*\*화면 키보드 토글\*\* | 필요할 때만 Windows 터치 키보드 호출 | `TabTip.exe` 실행 또는 `ITipInvocation` COM 인터페이스 |
| A-09 | ★★☆ | \*\*볼륨 / 음소거\*\* | 동영상 재생 시 빠른 볼륨 조절 | `VK\_VOLUME\_UP/DOWN/MUTE` SendInput 또는 CoreAudio API |
| A-10 | ★★☆ | \*\*바탕화면 보기 / 모두 최소화\*\* | 수업 자료 창을 정리하고 바탕화면으로 | `Win+D` SendInput 또는 Shell `MinimizeAll` |
| A-11 | ★☆☆ | \*\*랜덤 학생 뽑기\*\* | 학급 명단(CSV) 또는 번호 범위에서 무작위 선택. 뽑힌 학생 제외 옵션 | 명단은 로컬 파일만 사용(개인정보 보호). 중복 방지 리스트 |
| A-12 | ★☆☆ | \*\*화면 회전/미러 없음 확인용 ‘창 정렬 초기화’\*\* | 여러 창이 어지러울 때 모든 창을 기본 크기로 정렬 | `EnumWindows` + 캐스케이드 배치 |
| A-13 | ★☆☆ | \*\*자동 숨김(도킹) 모드\*\* | 화면 가장자리로 밀면 손잡이만 남기고 숨김, 터치하면 다시 펼침 | 가장자리 근접 시 애니메이션으로 접기. 항상 5\~10px의 손잡이는 남긴다 |
| A-14 | ★☆☆ | \*\*프로필(수업 모드) 전환\*\* | ‘발표 모드’, ‘판서 모드’, ‘기본 모드’ 등 기능 세트를 한 번에 전환 | `settings.json`에 `profiles` 배열, 현재 프로필 ID |
| A-15 | ★☆☆ | \*\*멀티모니터 인식\*\* | 교사 PC 모니터와 전자칠판이 확장 모드일 때, 플로팅 툴을 전자칠판 쪽에 고정 | `Screen.AllScreens`, 마지막 위치를 모니터 DeviceName 기준으로 저장 |

---

## 6. UI/UX 가이드라인

### 6.1 레이아웃
- 기본 형태는 \*\*세로 또는 가로 막대(bar)\*\*이며 설정에서 방향 전환이 가능해야 한다.
- 최상단(또는 좌측)에 \*\*드래그 손잡이(grip)\*\*, 그 옆에 \*\*접기/펼치기 버튼\*\*을 둔다.
- 접힌 상태에서는 원형 버튼 하나(직경 56px)만 남는다.
- 버튼 크기 기본 56×56px, 설정에서 44/56/72px 선택. 버튼 간격 6\~8px.
- 아이콘은 단색 라인 아이콘 + 짧은 한글 라벨(설정으로 숨김 가능)을 함께 표시한다. 교사 연령대가 다양하므로 아이콘만으로 의미를 추측하게 하지 않는다.

### 6.2 상호작용
- 모든 버튼은 \*\*터치 다운 시점\*\*에 시각 피드백(배경색 변화)을 즉시 준다.
- 팝업 패널(창 목록, 탭 목록)은 플로팅 바 옆에 열리며, \*\*바깥을 탭하면 닫힌다\*\*. 열린 채 3초 이상 조작이 없으면 자동으로 닫는다(설정 가능).
- 창 목록은 썸네일 + 프로세스 아이콘 + 제목(최대 2줄)으로 구성하고, 최소 160×100px 썸네일을 사용한다.
- 위험 동작(창 닫기 등)은 기본 기능에 넣지 않는다. 넣을 경우 길게 누르기(long press)로만 실행한다.
- 투명도 슬라이더는 설정 창뿐 아니라 손잡이를 \*\*길게 누르면\*\* 바로 나타나게 한다.

### 6.3 시각
- 라이트/다크 두 테마, 고대비 모드 지원. 전자칠판은 반사가 심하므로 다크 테마를 기본으로 한다.
- 모서리 반경 12px 이하, 그림자는 사용하지 않는다(투명도 조절 시 지저분해짐).
- 텍스트 최소 13px, 타이머 등 교실 뒤에서 봐야 하는 숫자는 96px 이상.



\### 6.4 참조문서

&#x20;- 같은 폴더의 WFT\_UI.md

---

## 7. 설정 파일 스키마 (settings.json)

```json
{
  "version": 1,
  "position": { "monitor": "\\\\\\\\.\\\\DISPLAY2", "x": 1840, "y": 200, "orientation": "vertical" },
  "appearance": { "opacity": 0.85, "opacityOnHover": 1.0, "buttonSize": 56, "theme": "dark", "showLabels": true },
  "behavior": { "autoHide": false, "autoClosePanelSec": 3, "startWithWindows": true, "toggleHotkey": "Ctrl+Alt+Space" },
  "enabledActions": \[
    "switchWindow", "prevWindow", "nextWindow",
    "tabList", "prevTab", "nextTab",
    "taskView", "snapLeft", "snapRight", "snapLayout",
    "pen", "blackout", "spotlight", "timer"
  ],
  "profiles": \[
    { "id": "default", "name": "기본", "enabledActions": \["..."] },
    { "id": "presentation", "name": "발표", "enabledActions": \["pptPrev", "pptNext", "pen", "spotlight", "blackout"] }
  ],
  "launcher": \[
    { "name": "e학습터", "target": "https://cls.edunet.net", "icon": null }
  ],
  "timerPresets": \[60, 180, 300, 600]
}
```

- 로드 시 스키마 검증 후 누락 필드는 기본값으로 채운다.
- 손상된 파일은 `settings.broken.json`으로 백업하고 기본값으로 시작한다(교실에서 프로그램이 안 켜지는 사고 방지).

---

## 8. 구현 절차 (Step-by-step)

1. \*\*프로젝트 생성\*\* : .NET 8 WPF, `NativeMethods.cs`에 P/Invoke 선언(`EnumWindows`, `GetWindowText`, `IsWindowVisible`, `GetWindowLong/SetWindowLong`, `SetWindowPos`, `SetForegroundWindow`, `ShowWindow`, `GetForegroundWindow`, `SendInput`, `DwmGetWindowAttribute`, `MonitorFromWindow`, `GetMonitorInfo`).
2. \*\*메인 플로팅 창\*\* : 투명·테두리 없음·Topmost·NOACTIVATE·TOOLWINDOW 적용. 드래그, 접기, 투명도 슬라이더까지 먼저 완성한다.
3. \*\*IAction 추상화\*\* : `Id`, `DisplayName`, `Icon`, `IsAvailable(context)`, `Execute(context)`. `context`에는 직전 활성 창 핸들, 프로세스명, 모니터 정보가 들어간다.
4. \*\*창 열거/전환 (F-01, F-02)\*\* : 필터 규칙 — 보이지 않는 창, 제목 없는 창, `WS\_EX\_TOOLWINDOW`, DWM cloaked, 자기 자신 제외. 썸네일은 DWM Thumbnail 우선.
5. \*\*키 시뮬레이터 (F-04\~F-07 폴백)\*\* : `SendInput`으로 modifier down → key down → key up → modifier up. Win 키 조합은 `KEYEVENTF\_EXTENDEDKEY` 주의.
6. \*\*스냅 (F-06, F-07)\*\* : 작업영역 기준 좌표 계산 후 `SetWindowPos` 직접 배치를 기본으로, Win11에서만 `Win+Z` 옵션 제공.
7. \*\*브라우저 탭 (F-03)\*\* : UIA로 `chrome.exe`/`msedge.exe` 최상위 창에서 TabItem 열거. 탭 수 30개 이상이면 스크롤 목록. UIA 실패 시 폴백 키 시뮬레이션과 사용자 안내.
8. \*\*설정 창 + 기능 선택 (F-08)\*\* : 체크리스트 + 드래그 정렬. 저장 즉시 바 재구성.
9. \*\*추가 기능\*\* : A-01 판서 → A-02 블랙아웃 → A-03 스포트라이트 → A-04 타이머 순으로 오버레이 계층을 공유하며 구현.
10. \*\*트레이 아이콘, 시작 프로그램 등록, 전역 단축키(숨김/표시)\*\* : `RegisterHotKey`.
11. \*\*로깅·크래시 리포트\*\* : 예외 발생 시 로그 파일 + 조용한 재시작(교실에서 오류 창이 뜨지 않게).
12. \*\*패키징\*\* : 사용자 단위 설치, 자동 업데이트는 학교 네트워크 제한을 고려해 \*\*선택 사항\*\*으로.

---

## 9. 테스트 체크리스트

### 기능
- \[ ] 플로팅 툴 버튼을 눌러도 직전 활성 창의 포커스(캐럿 깜빡임)가 유지된다.
- \[ ] PPT 슬라이드쇼, 전체화면 동영상, 전체화면 브라우저 위에서도 플로팅 툴이 보인다.
- \[ ] 창 목록에 자기 자신·숨김 창·설정 앱 잔여 창이 나타나지 않는다.
- \[ ] 최소화된 창을 목록에서 선택하면 복원 후 전면에 온다.
- \[ ] Chrome, Edge 각각에서 탭 목록이 정확히 읽히고 선택 시 전환된다(탭 20개 이상 포함).
- \[ ] 브라우저가 아닌 창이 활성일 때 탭 버튼은 비활성 상태로 표시된다.
- \[ ] 스냅 좌/우/상/하가 작업표시줄 영역을 침범하지 않는다.
- \[ ] Windows 10과 Windows 11에서 스냅 레이아웃이 각각 동작한다(10은 자체 패널).
- \[ ] 투명도 20%에서도 모든 버튼이 터치에 반응한다.
- \[ ] 드래그로 화면 밖으로 완전히 나가지 않으며, 모니터 해제/연결 후 재시작 시 보이는 위치로 복귀한다.
- \[ ] 기능 선택 변경이 즉시 반영되고 재시작 후 유지된다.

### 환경
- \[ ] 100% / 150% / 200% DPI 배율에서 레이아웃이 깨지지 않는다.
- \[ ] 관리자 권한 없이 설치·실행된다.
- \[ ] 터치 전용(마우스 없음) 환경에서 모든 기능을 끝까지 수행할 수 있다.
- \[ ] 8시간 연속 실행 시 메모리 증가가 없다(썸네일 핸들 해제 확인).

---

## 10. 알려진 함정과 해결 방법

| 문제 | 원인 | 해결 |
|---|---|---|
| Alt+Tab, Win+Tab 시뮬레이션이 간헐적으로 무시됨 | 포그라운드 잠금(`ForegroundLockTimeout`), UIPI | 창 전환은 `SetForegroundWindow` API로, 필요 시 `AttachThreadInput` 트릭 또는 `keybd\_event(VK\_MENU)` 선행 |
| `SetForegroundWindow`가 실패하고 작업표시줄 아이콘만 깜빡임 | 호출 프로세스가 포그라운드가 아님 | `AllowSetForegroundWindow`, 또는 Alt 키 눌렀다 떼기 후 호출 |
| 창 목록에 보이지 않는 UWP 창이 잡힘 | Suspended UWP 앱 | `DWMWA\_CLOAKED` 값이 0이 아닌 창 제외 |
| 관리자 권한 앱(작업 관리자 등) 위에서 SendInput 무시 | UIPI | 사용자에게 안내 메시지, 해당 창은 스냅을 `SetWindowPos`로 대체 시도 |
| 투명 창에서 클릭이 뒤로 통과 | 투명 픽셀은 히트테스트 통과 | 배경에 `#01000000`처럼 거의 투명한 색을 깔아 히트테스트 유지 |
| 판서 오버레이가 켜진 상태에서 창 전환 불가 | 오버레이가 입력을 가로챔 | 판서 모드에서는 플로팅 바를 오버레이보다 위에 두고 "판서 종료" 버튼을 항상 노출 |
| Edge/Chrome UIA 탭 열거가 비어 있음 | 브라우저의 접근성 트리 지연 로딩 | 첫 요청 시 `WM\_GETOBJECT` 트리거를 위해 UIA 루트 한 번 접근 후 재시도(최대 3회, 200ms 간격) |
| 멀티모니터에서 저장된 좌표가 화면 밖 | 모니터 구성 변경 | 시작 시 저장 좌표가 어떤 모니터 작업영역에도 없으면 주 모니터 우측 중앙으로 리셋 |

---

## 11. 개인정보·보안 고려
- 창 제목, 탭 제목은 화면에만 표시하고 \*\*저장하거나 전송하지 않는다.\*\*
- 학생 명단(A-11)은 로컬 파일만 사용하고 로그에 기록하지 않는다.
- 캡처 이미지(A-06)는 사용자가 지정한 폴더에만 저장하고 기본 경로는 `문서\\전자칠판 캡처\\YYYY-MM-DD\\`로 한다.
- 네트워크 통신은 업데이트 확인(선택) 외에 없다.

---

## 12. 산출물 정의 (Definition of Done)
1. `F-01 \~ F-10` 전 기능이 9절 체크리스트를 통과한다.
2. `A-01 \~ A-04`(판서, 블랙아웃, 스포트라이트, 타이머)가 포함되어 있다.
3. 사용자 매뉴얼(한글, 그림 포함, 4쪽 이내)과 관리자 배포 가이드가 `docs/`에 있다.
4. 설치 파일(사용자 단위)과 포터블 단일 exe 두 가지 형태로 제공된다.
5. 첫 실행 시 60초 이내의 온보딩(드래그, 접기, 기능 선택 안내 3단계)이 표시된다.
````

\---

### 문서 작성 시 반영한 포인트

1. **가장 중요한 기술 요건을 앞에 배치했습니다.** 플로팅 툴이 클릭 시 포커스를 빼앗으면 "창 전환/탭 전환" 명령이 플로팅 툴 자기 자신에게 가버려 동작하지 않기 때문에, `WS\_EX\_NOACTIVATE` 적용을 4.1절에서 필수 요건으로 명시했습니다.
2. **키 시뮬레이션보다 Win32 API 직접 호출을 우선**하도록 했습니다. Alt+Tab 시뮬레이션은 Windows 보안 정책 때문에 간헐적으로 무시되므로, 창 전환은 `SetForegroundWindow`, 스냅은 `SetWindowPos`로 직접 처리하고 키 시뮬레이션은 폴백으로 두는 구조입니다.
3. **추가 제안 기능**은 교사 수업 흐름에서 자주 필요한 순서로 우선순위를 매겼습니다. 특히 화면 판서(스크린 펜), 화면 가리기, 스포트라이트, 타이머는 전자칠판 교실에서 거의 매 수업 쓰이는 기능이라 ★★★로 두었고, PPT 슬라이드 제어처럼 활성 창에 따라 자동으로 나타나는 컨텍스트 버튼도 넣었습니다.
4. **학교 현장 제약**(관리자 권한 없음, 네트워크 제한, 터치 전용, 다양한 DPI)을 배포·테스트 항목에 반영했습니다.

필요하시면 이 SKILL.md를 기준으로 `NativeMethods.cs`(P/Invoke 선언)나 창 열거·전환 모듈 같은 실제 코드 초안도 이어서 작성해 드릴 수 있습니다.

\---

