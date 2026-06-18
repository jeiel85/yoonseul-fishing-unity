using UnityEngine;
using YoonseulFishing.Audio;
using YoonseulFishing.Core;
using YoonseulFishing.Data;
using YoonseulFishing.Rendering;

namespace YoonseulFishing.Game
{
    /// <summary>
    /// Phase 6 boot wiring — the Unity equivalent of MainActivity.kt. Assembles the
    /// whole game from the parts built in earlier phases:
    /// <list type="bullet">
    ///   <item>GameState + SaveService(load) + GameController (Phases 1–2)</item>
    ///   <item>ProceduralAudio as the controller's IAudioSynthesizer (Phase 3)</item>
    ///   <item>FishingSceneRenderer subscribed to the state observables (Phase 4)</item>
    ///   <item>tap input → GameController.HandleScreenTap</item>
    /// </list>
    ///
    /// SETUP (editor, one-time): make a GameObject with a <c>UIDocument</c> (assign a
    /// Panel Settings asset; leave Source Asset empty), plus <c>FishingSceneRenderer</c>,
    /// <c>ProceduralAudio</c> (auto-adds an AudioSource), and this component. Assign the
    /// two references below (or leave empty to auto-GetComponent). Keep the default
    /// Main Camera (it carries the AudioListener) and press Play.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Wiring (auto-found on this GameObject if left empty)")]
        [SerializeField] private FishingSceneRenderer sceneRenderer;
        [SerializeField] private ProceduralAudio audioSynth;

        private const float SplashDurationSeconds = 2.2f; // matches GameController's splash wait

        private GameState _state;
        private SaveService _save;
        private GameController _controller;
        private float _splashStartTime = -1f;

        /// <summary>Exposed for future UI (Phase 5) to drive shop/quest/etc. actions.</summary>
        public GameController Controller => _controller;
        public GameState State => _state;

        private void Awake()
        {
            if (sceneRenderer == null) sceneRenderer = GetComponent<FishingSceneRenderer>();
            if (audioSynth == null) audioSynth = GetComponent<ProceduralAudio>();

            _state = new GameState();
            _save = new SaveService(_state);
            _save.Load();

            _controller = new GameController(_state, _save, audioSynth);
            if (audioSynth != null) _controller.SetSynthesizer(audioSynth); // initial audio sync

            WireRenderer();

            _controller.ResetDailyQuestsIfNewDay();
            _controller.StartEnvironmentCycles();
        }

        private void WireRenderer()
        {
            if (sceneRenderer == null) return;

            // Initial push.
            sceneRenderer.SetTimeOfDay(_state.TimeOfDay.Value);
            sceneRenderer.SetWeather(_state.Weather.Value);
            sceneRenderer.SetFishingState(_state.FishingState.Value);
            sceneRenderer.SetBobber(_state.BobberPositionX.Value, _state.BobberPositionY.Value);
            sceneRenderer.SetRhythm(_state.RhythmRingScale.Value, _state.RhythmBeatActive.Value);

            // Live updates from the observable game state.
            _state.TimeOfDay.Changed += sceneRenderer.SetTimeOfDay;
            _state.Weather.Changed += sceneRenderer.SetWeather;
            _state.FishingState.Changed += OnFishingStateChanged;
            _state.BobberPositionX.Changed += OnBobberMoved;
            _state.BobberPositionY.Changed += OnBobberMoved;
            _state.RhythmRingScale.Changed += OnRhythmScaleChanged;
            _state.RhythmBeatActive.Changed += OnRhythmBeatChanged;
        }

        private void OnRhythmScaleChanged(float scale) =>
            sceneRenderer.SetRhythm(scale, _state.RhythmBeatActive.Value);

        private void OnRhythmBeatChanged(bool active) =>
            sceneRenderer.SetRhythm(_state.RhythmRingScale.Value, active);

        private void OnBobberMoved(float _) =>
            sceneRenderer.SetBobber(_state.BobberPositionX.Value, _state.BobberPositionY.Value);

        private void OnFishingStateChanged(FishingState state)
        {
            sceneRenderer.SetFishingState(state);
            _splashStartTime = state == FishingState.Splashing ? Time.time : -1f;
        }

        private void Update()
        {
            HandleTapInput();
            DriveSplashLeap();
        }

        // Mouse (PC) or first touch (mobile) → a single tap dispatched to the controller.
        private void HandleTapInput()
        {
            bool tapped = Input.GetMouseButtonDown(0);
            Vector2 pos = Input.mousePosition;

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                tapped = true;
                pos = Input.GetTouch(0).position;
            }
            if (!tapped) return;

            // Input is bottom-left origin (y up); the scene/water coords are top-left (y down).
            float relX = Screen.width > 0 ? pos.x / Screen.width : 0.5f;
            float relY = Screen.height > 0 ? 1f - pos.y / Screen.height : 0.65f;
            _controller.HandleScreenTap(relX, relY);
        }

        // While SPLASHING, feed the renderer the caught species + 0..1 leap progress.
        private void DriveSplashLeap()
        {
            if (_splashStartTime < 0f || sceneRenderer == null) return;

            CaughtFish last = _state.LastCaughtFish.Value;
            FishSpecies fish = last != null ? FishSpecies.Find(last.SpeciesId) : null;
            if (fish == null) return;

            float progress = Mathf.Clamp01((Time.time - _splashStartTime) / SplashDurationSeconds);
            sceneRenderer.SetSplash(fish, progress);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _save?.Save();
        }

        private void OnDestroy()
        {
            _save?.Save();
            _controller?.Dispose();
        }
    }
}
