using DroneSimulator.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DroneSimulator.UI
{
    /// <summary>
    /// Code-built menu flow (same flat aerospace styling as OperatorUI).
    /// One component drives two screens via <see cref="ScreenMode"/>:
    /// StartMenu (DRONE SIMULATOR + PLAY + SIH DEMO) and Placeholder
    /// (SIH DEMONSTRATION MODE / COMING SOON + BACK). No gameplay, no
    /// drones, no swarm logic — navigation shell only.
    /// </summary>
    public sealed class StartMenuController : MonoBehaviour
    {
        [SerializeField] private string simulatorScene = "Simulator";

        private static readonly Color Bg = new Color(0.04f, 0.07f, 0.10f, 0.97f);
        private static readonly Color HeaderCyan = new Color(0.45f, 0.83f, 1.00f);
        private static readonly Color BodyText = new Color(0.79f, 0.83f, 0.87f);
        private static readonly Color DimText = new Color(0.45f, 0.51f, 0.57f);
        private static readonly Color ButtonBg = new Color(0.10f, 0.16f, 0.22f);
        private static readonly Color AccentBg = new Color(0.08f, 0.35f, 0.48f);

        private Font font;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                var any = Resources.FindObjectsOfTypeAll<Font>();
                if (any.Length > 0) font = any[0];
            }

            BuildCanvas();
        }

        private void Start()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM();
            }
        }

        public void OpenSimulator()
        {
            SceneManager.LoadScene(simulatorScene);
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("MenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bg = bgGo.AddComponent<Image>();
            bg.color = Bg;
            bg.raycastTarget = false;

            var boxGo = new GameObject("MenuBox");
            boxGo.transform.SetParent(canvasGo.transform, false);
            var boxRect = boxGo.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(380f, 340f);
            boxRect.anchoredPosition = Vector2.zero;

            float y = -150f;
            y = AddCenteredText(boxGo, y, "DRONE SIMULATOR", 34, HeaderCyan, true);
            y = AddCenteredText(boxGo, y, "FPV TRAINING SYSTEM", 13, DimText, false);
            y += 18f;
            y = AddMenuButton(boxGo, y, "PLAY", OpenSimulator, AccentBg);
            AddCenteredText(boxGo, y, "Single-drone FPV Training", 11, DimText, false);
        }

        private float AddCenteredText(GameObject parent, float yFromCenter, string text, int size, Color color, bool bold)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, yFromCenter);
            rect.sizeDelta = new Vector2(360f, size + 10f);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            if (bold) label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            label.raycastTarget = false;
            return yFromCenter - (size + 16f);
        }

        private float AddMenuButton(GameObject parent, float yFromCenter, string text, UnityEngine.Events.UnityAction onClick, Color? bg)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, yFromCenter - 22f);
            rect.sizeDelta = new Vector2(320f, 44f);
            var image = go.AddComponent<Image>();
            image.color = bg ?? ButtonBg;
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.16f, 0.24f, 0.32f);
            colors.pressedColor = new Color(0.06f, 0.28f, 0.40f);
            button.colors = colors;
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 16;
            label.fontStyle = FontStyle.Bold;
            label.color = BodyText;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            label.raycastTarget = false;
            button.onClick.AddListener(onClick);
            return yFromCenter - 52f;
        }
    }
}
