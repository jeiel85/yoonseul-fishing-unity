# 개발 환경 세팅 가이드 (Unity 설치 → 프로젝트 열기)

> ✅ **현재 상태:** 사수 PC에 **Unity 6000.5.0f1** 설치 + 프로젝트 오픈 완료
> (스크립트 컴파일 에러 0). 아래는 새 PC에서 클론해 셋업할 때의 재현 가이드입니다.

**1~2번(설치·라이선스)은 GUI 작업이라 직접 하셔야 합니다.**

## 1. Unity Hub 설치

`다운로드` 폴더의 인스톨러를 실행합니다.

```
C:\Users\jeiel\Downloads\UnityHubSetup.exe
```

더블클릭 → 약관 동의 → 설치. 끝나면 Unity Hub 가 열립니다.
(Unity 계정 로그인 + **Personal 라이선스 무료 활성화** 가 필요합니다.)

## 2. Unity 6 LTS 에디터 설치

Unity Hub → 왼쪽 **Installs** → **Install Editor**

- 버전: **Unity 6 LTS** (목록에서 `LTS` / `Recommended` 표시된 6000.x)
- **Add modules** 에서 반드시 체크:
  - ✅ **Android Build Support**
    - ✅ Android SDK & NDK Tools
    - ✅ OpenJDK
  - ✅ **Windows Build Support (IL2CPP)** — 보통 기본 포함
  - (선택) **Documentation**

> 설치 용량이 수 GB 입니다. Android 모듈 포함 시 더 큽니다.

## 3. 프로젝트 열기

Unity Hub → **Projects** → **Add** → **Add project from disk** →

```
D:\Project\yoonseul-fishing-unity
```

선택 후 프로젝트를 클릭해 엽니다.

- "이 프로젝트는 다른 버전(6000.1.13f1)에서 만들어졌습니다" 같은 **버전 경고가
  뜨면**, 설치한 Unity 6 버전으로 **Open with** / **Continue** 를 누르세요.
  (스켈레톤의 버전 표기는 placeholder 이며, 처음 열 때 설치 버전으로 갱신됩니다.)
- 첫 오픈 시 Unity 가 `Library/` 와 누락된 `ProjectSettings` 를 자동 생성합니다.
  (수 분 소요될 수 있음 — 정상입니다.)

## 4. 버전 동기화 (완료)

설치 버전 `6000.5.0f1` 에 맞춰 `ProjectSettings/ProjectVersion.txt` 를 핀했고,
첫 오픈 때 Unity 가 리비전 해시까지 채웠습니다. 첫 오픈으로 생성된
`ProjectSettings/*.asset`·`.meta`·`Packages/packages-lock.json` 도 커밋되어,
다른 PC에서 클론하면 동일하게 열립니다. 현재 Phase 1(게임 로직 코어) 진행 중 —
로드맵은 [PORTING-PLAN.md](PORTING-PLAN.md).

## 코드 에디터 (선택)

C# 작성/디버깅용으로 아래 중 하나:
- **Visual Studio Community** (Unity 워크로드) — Hub 모듈에서 같이 설치 가능
- **VS Code** + C# Dev Kit 확장
- **Rider** (유료, JetBrains)

없어도 코드 이식 자체는 제가 진행합니다. 에디터는 사수가 Play/디버그할 때 편합니다.
