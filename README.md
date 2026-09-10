# WFT — 전자칠판 플로팅 툴

전자칠판(Windows) 환경에서 교사가 키보드 없이 터치 한 번으로 창 전환·브라우저 탭 전환·작업 보기·스냅을 실행할 수 있는 항상-위 플로팅 툴.

- 명세: `WFT.md` (기능 명세 F-01 ~ F-10)
- 디자인: `WFT_UI.md` (디자인 토큰·컴포넌트 규격)
- 5.2 추가 제안 기능(판서/가리기/스포트라이트/타이머 등)은 미구현 — 추후 업그레이드 예정

## 배포 파일

- `dist\WFT.exe` — 자체 포함 단일 실행 파일 (self-contained single-file, win-x64)
  - .NET 런타임 포함, 설치 불필요, 관리자 권한 불필요
  - USB로 복사해 어느 PC에서든 바로 실행 가능

## 기능

| ID | 기능 | 사용법 |
|---|---|---|
| F-01 | 창 전환 | "창 전환" 버튼 → 썸네일 목록에서 창 탭 |
| F-02 | 이전/다음 창 | 버튼 한 번 탭으로 즉시 전환 |
| F-03 | 브라우저 탭 목록 | Chrome/Edge 탭 목록에서 직접 이동 (UI Automation) |
| F-04 | 이전/다음 탭 | 활성 브라우저에서 즉시 탭 이동 |
| F-05 | 작업 보기 | Win+Tab 실행 |
| F-06 | 스냅 | 좌/우/상/하 — SetWindowPos 직접 배치 |
| F-07 | 스냅 레이아웃 | 자체 패널(2/1+2/3/4분할/전체) 또는 Win11 Win+Z |
| F-08 | 기능 선택 | 설정 > 기능: 토글로 on/off, ⋮⋮ 드래그로 순서 변경, 실시간 미리보기 |
| F-09 | 투명도 | Grip 길게 누르기(500ms) 또는 설정 → 20~100% |
| F-10 | 드래그 이동 | Grip 드래그, 화면 밖 클램프 + 모서리 자석 스냅, 더블 탭 접기 |

기타:
- 트레이 아이콘 (표시/숨김, 설정, 종료) + 바 내장 종료 버튼
- 화면 전환 메뉴 (가상 데스크톱): 새 화면 만들기 / 이전·다음 화면 전환 / 현재 화면 닫기 (Win+Ctrl+D/←/→/F4)
- 전역 단축키(기본 Ctrl+Alt+Space, 점유 시 사용 가능한 조합으로 자동 전환 후 안내)
- 시작 프로그램 등록/제거 — 이미 등록되어 있으면 "이미 시작 프로그램에 추가되어 있습니다" 안내
- 첫 실행 온보딩 3단계
- 다크/라이트/시스템/고대비 테마, 세로/가로 바, 버튼 44/56/72px, 라벨 표시 토글
- 패널 자동 닫힘 기본 꺼짐 (설정 > 동작에서 2/3/5초 가능)
- 창 제목·탭 제목 저장/전송 없음, 네트워크 통신 없음

## 데이터 위치

- 설정: `%APPDATA%\WhiteboardFloatingTool\settings.json` (손상 시 `settings.broken.json`으로 백업 후 기본값)
- 로그: `%APPDATA%\WhiteboardFloatingTool\logs\`

## 빌드

```
cd src\WhiteboardFloatingTool
dotnet publish -c Release
```

산출물: `bin\Release\net8.0-windows\win-x64\publish\WFT.exe`

## 기술 요약

- .NET 8 WPF, PerMonitorV2 DPI
- `WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW + WS_EX_TOPMOST` — 버튼을 눌러도 아래 창의 포커스 유지
- 창 전환/스냅은 Win32 API 직접 호출(`SetForegroundWindow`, `SetWindowPos`), 키 시뮬레이션은 폴백
- 브라우저 탭: UI Automation(`ControlType.TabItem` → `SelectionItemPattern.Select`), 재시도 3회
- 썸네일: STA 작업 스레드에서 `PrintWindow(PW_RENDERFULLCONTENT)`
- 트레이: WinForms `NotifyIcon` (WinForms 전역 using은 제거, 충돌 방지)
