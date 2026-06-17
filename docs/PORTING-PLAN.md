# 윤슬낚시 → Unity 포팅 플랜 (Yoonseul Fishing — Unity Port Plan)

원본: `D:\Project\yoonseul-fishing` (Kotlin / Jetpack Compose, Android)
대상: 이 저장소 (Unity 6 LTS / C#) — 타깃 **Android(재출시) + PC(Windows)**

> 이건 "기계적 변환"이 아니라 **프레임워크 경계를 다시 짜는 재작성**이다.
> 게임 로직(상태머신·퀘스트·확률·합성 수식)은 1:1로 옮기고, 그릇(렌더링·UI·
> 저장·오디오 출력)만 Unity 것으로 갈아끼운다.

---

## 0. 왜 이 게임은 Unity 바이브코딩에 잘 맞나

- **에셋이 거의 없는 절차적 게임.** 그래픽은 코드로 Canvas에 그리고, 사운드는
  PCM으로 실시간 합성한다. 스프라이트·오디오 파일·애니메이션 클립·머티리얼이
  사실상 없다.
- 유니티 바이브코딩의 최대 장애물인 **씬/프리팹/인스펙터 드래그 연결**이 거의
  필요 없다 → C# 스크립트 중심으로 99% 처리 가능.
- 단, **에디터 GUI 작업(라이선스 활성화, 빌드 세팅, Android 모듈, Play 버튼
  플레이테스트)은 사람이 해야 한다.** 거기선 "코드는 내가, Play는 사수가" 협업.

---

## 1. 원본 구조 → Unity 대응 매핑

| 원본 (Kotlin/Compose) | 줄수 | Unity / C# 대응 | 에디터 필요? |
|---|---:|---|:---:|
| `MainActivity.kt` | 65 | 부트스트랩 씬 + `GameBootstrap` MonoBehaviour (오디오·상태·UI 와이어링) | 씬 1개 |
| `ui/FishingViewModel.kt` | 1683 | `Game/GameController.cs` + `Game/GameState.cs` — 순수 C#. StateFlow → C# `event`/`Action` | ❌ |
| `ui/HealingFishingGame.kt` | 7325 | (a) 절차적 씬 렌더링 + (b) 메뉴 UI 로 분리 → **UI Toolkit `Painter2D`** | 부분 |
| `ui/AudioSynthesizer.kt` | 415 | `Audio/ProceduralAudio.cs` — `OnAudioFilterRead` 로 PCM 합성 | AudioSource 1개 |
| `data/FishSpecies.kt` | 232 | ✅ **완료** → `Assets/Scripts/Data/FishSpecies.cs` | ❌ |
| `data/CaughtFishEntity.kt` | 14 | `Data/CaughtFish.cs` (`[Serializable]`) | ❌ |
| `data/AppDatabase/DAO/Repository` (Room) | 71 | `Data/SaveService.cs` — JSON 파일(`Application.persistentDataPath`) | ❌ |
| `ui/theme/*` (Color/Type/Theme) | 99 | `UI/Palette.cs` + TMP/폰트 에셋 | 폰트 import |
| `res/values*`, `res/values-en` | xml | `UI/Localization.cs` (ko/en/ja 딕셔너리) | ❌ |

**핵심 판단 — 렌더링/UI는 UI Toolkit `Painter2D` 로 간다.**
Compose의 `DrawScope`(drawCircle/drawPath/gradient…)와 Unity 6의 `Painter2D`
(BeginPath/LineTo/Arc/Fill/Stroke/gradient)는 API가 거의 1:1로 대응한다.
100% 코드 드리븐이라 프리팹 와이어링이 없어 바이브코딩에 최적이고, 원본
Canvas 코드를 거의 직역할 수 있다. (대안: 스프라이트/파티클로 재구성 → 더
"유니티스럽"지만 에셋·씬 작업이 늘고 원본 충실도가 떨어진다. 채택 안 함.)

**렌더 파이프라인:** Built-in(기본). 절차적으로 그리므로 URP 불필요 →
렌더 파이프라인 에셋 와이어링이 없어 더 단순. (이 스켈레톤은 Built-in 기준.)

**저장:** Room(SQLite) → **JSON 세이브 파일** 1개로 단순화. 잡은 물고기
리스트 + 진행도(코인·레벨·해금·퀘스트·미끼). 사용자 데이터는 그대로 유지된다는
체감은 동일.

---

## 2. 단계별 진행 (Phases)

- [x] **Phase 0 — 세팅** (현재): 폴더·git·`.gitignore`·플랜·`FishSpecies.cs`,
      Unity Hub 인스톨러 준비. *(에디터 불필요)*
- [~] **Phase 1 — 게임 로직 코어** *(진행 중)*: `FishingViewModel.kt` →
      `GameState` + `GameController` 순수 C# 포팅.
  - [x] **타입 토대**: `Core/Observable.cs`(StateFlow 대응), `Data/Enums.cs`
        (TimeOfDay/Weather/NatureSound/FishingState/AppLanguage),
        `Data/BaitType.cs`, `Data/FishingSpot.cs`, `Data/CaughtFish.cs`,
        `Data/Celebration.cs`(CelebrationReward/Data, NotificationAlert)
  - [x] **`Core/GameState.cs`**: ViewModel의 모든 StateFlow를 Observable 로 이식
  - [x] **`Core/GameController.cs`**: 상태머신(IDLE→CASTING→…→CAUGHT/LOST),
        확률 기반 어종 추첨(`RollFishSpecies`/`ComputeSpeciesWeight`), 시간/날씨/
        자연음 사이클, 레벨/XP·코인·낚싯대 업그레이드, 일일 퀘스트·업적·도감
        마일스톤·축하 이벤트. Kotlin 코루틴(`viewModelScope.launch{delay()}`) →
        Unity 6 `Awaitable` + `CancellationTokenSource`(=`Job.cancel()`) 로 모델링.
        경계(seam) 인터페이스 신설: `Audio/IAudioSynthesizer`(Phase 3 구현),
        `Core/ISaveService`(Phase 2 구현 — `prefs`/Room 흩뿌린 저장을 GameState
        JSON 스냅샷 1개로 통합). 안드로이드 `prefs.apply()` 지점마다 `_save.Save()`.
        **오프라인 컴파일 검증 통과**(UnityEngine 스텁 대상 `dotnet build`,
        LangVersion 9.0, 0 errors) — 단, 에디터 실 컴파일·플레이 검증은 Unity
        설치 후로 남음.
  - [ ] **EditMode 유닛테스트**: 확률 가중치(`ComputeSpeciesWeight`)·레벨업 경계·
        퀘스트 클레임 조건 검증. *(Test Framework asmdef 필요 → 에디터에서 실행)*
- [ ] **Phase 2 — 저장**: `SaveService` (JSON, persistentDataPath). 잡은
      물고기 + 진행도 직렬화/역직렬화.
- [ ] **Phase 3 — 오디오**: `ProceduralAudio` — 펜타토닉 벨/바람/물/빗/벌레
      합성 수식을 `OnAudioFilterRead` 로 이식.
- [ ] **Phase 4 — 절차적 씬 렌더링**: `Painter2D` 로 하늘/시간·날씨 그라데이션,
      윤슬(물결 반짝임), 수면, 찌(bobber), 물고기 점프, 파문(ripple).
- [ ] **Phase 5 — UI**: 상점(미끼)·도감·퀘스트·업적·결과 카드 다이얼로그를
      UI Toolkit 으로.
- [ ] **Phase 6 — 입력·통합**: 탭 리듬(찌 가라앉을 때 탭 → 좁혀지는 도넛 링
      타이밍). 부트스트랩 씬에서 전체 와이어링.
- [ ] **Phase 7 — 빌드**: Android(재출시용 패키지명·서명) + Windows 빌드 세팅,
      플레이테스트. *(에디터 GUI — 사수 협업)*
- [ ] **Phase 8 — 스토어 재출시**: 기존 워크플로(`Desktop\Build\`, 릴리스 노트).

순서 근거: Phase 1~3 은 에디터 없이도 작성·검증 가능 → Unity 설치를 기다리는
동안/병행해 진도를 뺄 수 있다. Phase 4~7 은 에디터에서 눈으로 확인하며 조율.

---

## 3. 바이브코딩이 잘 안 되는 지점 (정직한 한계)

1. **라이선스 활성화 / 에디터·Android 모듈 설치** — GUI, 사수만 가능.
2. **Play 모드 플레이테스트 / 시각 조율** — 에디터에서 눈으로 봐야 함. 나는
   코드를 주고, 사수가 Play 눌러 결과(스크린샷/말로)를 알려주는 루프.
3. **빌드·서명·스토어 업로드** — 에디터 빌드 세팅 + Play Console GUI.
4. **폰트 import** — 한글 폰트를 TMP 에셋으로 굽는 건 에디터 작업(드물게 1회).

그 외 로직·렌더링·오디오·저장·UI 의 **코드 작성은 전부 내가 진행**한다.

---

## 4. 패키지/식별자 메모

- 원본 패키지: `com.jeiel85.healingfishing`
- Unity C# 네임스페이스: `YoonseulFishing.*`
- Android 재출시 시 applicationId 는 원본과 **동일 패키지로 업데이트 출시**할지,
  **신규 앱으로 별도 출시**할지 결정 필요 (스토어 정책·기존 사용자 영향) → 사수 확인.
