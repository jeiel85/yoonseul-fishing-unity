# 윤슬낚시 (Unity Port) — Yoonseul Fishing

![License](https://img.shields.io/badge/License-MIT-blue.svg)
![Unity](https://img.shields.io/badge/Unity-6000.5%20LTS-black?logo=unity)
![Platform](https://img.shields.io/badge/platform-Android%20%7C%20Windows-lightgrey)
![Language](https://img.shields.io/badge/C%23-Built--in%20RP-239120?logo=csharp)

원본 Android 앱([`yoonseul-fishing`](https://github.com/jeiel85/yoonseul-fishing),
Kotlin/Jetpack Compose)을 **Unity 6 LTS / C#** 으로 포팅하는 저장소입니다.
타깃 플랫폼은 **Android(재출시)** 와 **PC(Windows)** 입니다.

> 🌊 바쁜 일상 속에서 잔잔한 물결(윤슬)을 바라보며 지친 마음을 녹이는 힐링 낚시 게임.
> 절차적으로 그려지는 비주얼과 실시간 합성 사운드가 특징입니다.

## 현재 상태

**Phase 1 — 게임 로직 코어 이식 중.** 데이터 레이어 + 관찰형 상태(`GameState`) +
상태머신·확률 추첨·진행도·퀘스트를 담은 `GameController` C# 포팅 완료
(UnityEngine 스텁 대상 오프라인 컴파일 검증 통과, 에디터 실 검증은 설치 후).
다음은 EditMode 유닛테스트 + 저장 레이어(Phase 2). 전체 로드맵은
[docs/PORTING-PLAN.md](docs/PORTING-PLAN.md).

## 개발 환경 세팅

1. **Unity Hub 설치** — `UnityHubSetup.exe` 실행 (이미 `다운로드` 폴더에 받아둠).
2. **Unity 6 LTS 에디터 설치** — Hub 에서 "Recommended/LTS" 표시 버전. 설치 시
   아래 모듈 체크:
   - **Android Build Support** (+ Android SDK & NDK Tools, OpenJDK)
   - **Windows Build Support (IL2CPP)** — PC 빌드용 (기본 포함)
3. **프로젝트 열기** — Hub → `Add` → `Add project from disk` →
   이 폴더(`yoonseul-fishing-unity`) 선택. 버전 불일치 경고가 뜨면 설치한
   Unity 6 버전으로 열기(Open with) 선택.

자세한 단계별 안내는 [docs/SETUP.md](docs/SETUP.md).

## 기술 스택

| 영역 | 선택 |
|---|---|
| 엔진 | Unity 6 LTS (Built-in Render Pipeline) |
| 언어 | C# |
| 렌더링/UI | UI Toolkit `Painter2D` (절차적 드로잉 — 원본 Compose Canvas 대응) |
| 오디오 | `OnAudioFilterRead` PCM 실시간 합성 (원본 AudioTrack 대응) |
| 저장 | JSON 세이브 파일 (`Application.persistentDataPath`) |

## 라이선스

[MIT](LICENSE) · © 2026 jeiel85
