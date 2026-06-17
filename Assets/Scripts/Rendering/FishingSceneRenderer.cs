using UnityEngine;
using UnityEngine.UIElements;
using YoonseulFishing.Data;

namespace YoonseulFishing.Rendering
{
    /// <summary>
    /// Hosts the procedural fishing scene on a UI Toolkit panel and animates it.
    /// Builds a full-panel <see cref="VisualElement"/>, draws it each frame through
    /// <see cref="ScenePainter"/> in <c>generateVisualContent</c>, and ticks the
    /// animation via <see cref="MarkDirtyRepaint"/>.
    ///
    /// Until the Phase 6 bootstrap drives time/weather from <c>GameState</c>, the
    /// inspector <b>Preview</b> fields choose what's shown — change them in Play mode
    /// to preview Day/Sunset/Night × Clear/Mist/Rain. Later, the bootstrap calls
    /// <see cref="SetTimeOfDay"/> / <see cref="SetWeather"/> from the controller's
    /// observables.
    ///
    /// SETUP (one-time editor step — UI Toolkit needs a panel asset):
    ///   1. Project: Create ▸ UI Toolkit ▸ Panel Settings Asset.
    ///   2. Add a GameObject with a <c>UIDocument</c> component; assign the Panel
    ///      Settings to it. (Leave its Source Asset / UXML empty.)
    ///   3. Add this component to the same GameObject and press Play.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class FishingSceneRenderer : MonoBehaviour
    {
        [Header("Preview (until Phase 6 bootstrap drives these)")]
        [SerializeField] private TimeOfDay previewTime = TimeOfDay.Day;
        [SerializeField] private Weather previewWeather = Weather.Clear;
        [SerializeField] private FishingState previewFishingState = FishingState.Waiting;
        [Range(0f, 1f)] [SerializeField] private float bobberX = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float bobberY = 0.7f;

        [Header("Decor density")]
        [SerializeField] private int starCount = 70;
        [SerializeField] private int sparkleCount = 42;

        private VisualElement _scene;
        private ScenePainter.Star[] _stars;
        private ScenePainter.Sparkle[] _sparkles;
        private float _startTime;

        /// <summary>Set by the bootstrap (Phase 6) from the time-of-day observable.</summary>
        public void SetTimeOfDay(TimeOfDay time) => previewTime = time;

        /// <summary>Set by the bootstrap (Phase 6) from the weather observable.</summary>
        public void SetWeather(Weather weather) => previewWeather = weather;

        /// <summary>Set by the bootstrap (Phase 6) from the fishing-state observable.</summary>
        public void SetFishingState(FishingState state) => previewFishingState = state;

        /// <summary>Set by the bootstrap (Phase 6) from the bobber-position observables (0..1).</summary>
        public void SetBobber(float x, float y) { bobberX = x; bobberY = y; }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;

            GenerateDecor();

            _scene = new VisualElement { name = "fishing-scene" };
            _scene.style.flexGrow = 1; // fill the panel
            _scene.generateVisualContent += OnGenerateVisualContent;
            root.Add(_scene);

            _startTime = Time.time;
        }

        private void OnDisable()
        {
            if (_scene == null) return;
            _scene.generateVisualContent -= OnGenerateVisualContent;
            _scene.RemoveFromHierarchy();
            _scene = null;
        }

        private void Update()
        {
            _scene?.MarkDirtyRepaint(); // re-issue draw each frame to animate
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect rect = _scene.contentRect;
            long ticks = (long)((Time.time - _startTime) * 1000f);
            ScenePainter.DrawScene(ctx.painter2D, rect, ticks, previewTime, previewWeather,
                previewFishingState, bobberX, bobberY, _stars, _sparkles);
        }

        private void GenerateDecor()
        {
            int sc = Mathf.Max(0, starCount);
            _stars = new ScenePainter.Star[sc];
            for (int i = 0; i < sc; i++)
            {
                _stars[i] = new ScenePainter.Star
                {
                    RelX = Random.value,
                    RelY = Random.value * 0.5f,                 // upper sky
                    TwinkleSpeed = 0.0016f + (i % 4) * 0.0004f,
                    Big = (i % 5 == 0),
                };
            }

            int pc = Mathf.Max(0, sparkleCount);
            _sparkles = new ScenePainter.Sparkle[pc];
            for (int i = 0; i < pc; i++)
            {
                _sparkles[i] = new ScenePainter.Sparkle
                {
                    RelX = Random.value,
                    RelY = 0.55f + Random.value * 0.4f,         // on the water band
                    ScaleFactor = 0.6f + Random.value * 0.8f,
                    Phase = Random.value * 6.28318f,
                };
            }
        }
    }
}
