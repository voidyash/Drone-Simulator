using System.Collections.Generic;
using DroneSimulator.Configuration;
using DroneSimulator.Drones;
using DroneSimulator.Environment;
using DroneSimulator.Sensors;
using DroneSimulator.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace DroneSimulator.UI
{
    /// <summary>
    /// Simulator operator UI: SCENARIO CONFIG vs RUNTIME SIMULATION states.
    /// Single-drone FPV build: no swarm, formation, threat-board or hostile
    /// controls remain. Code-built uGUI with explicit manual positioning
    /// (no auto-layout: deterministic on every resolution). Flat aerospace
    /// styling, every control writes straight through to the manager APIs.
    /// </summary>
    public sealed class OperatorUI : MonoBehaviour
    {
        private enum UiState { Config, Runtime }

        private static readonly Color Bg = new Color(0.04f, 0.07f, 0.10f, 0.92f);
        private static readonly Color HeaderCyan = new Color(0.45f, 0.83f, 1.00f);
        private static readonly Color BodyText = new Color(0.79f, 0.83f, 0.87f);
        private static readonly Color DimText = new Color(0.45f, 0.51f, 0.57f);
        private static readonly Color Amber = new Color(1.00f, 0.70f, 0.33f);
        private static readonly Color ButtonBg = new Color(0.10f, 0.16f, 0.22f);
        private static readonly Color AccentBg = new Color(0.08f, 0.35f, 0.48f);
        private static readonly Color TrackBg = new Color(0.10f, 0.15f, 0.20f);
        private static readonly Color FillBlue = new Color(0.30f, 0.62f, 0.80f);

        private const float Pad = 12f;
        private const float Gap = 5f;

        private UiState state = UiState.Config;
        private Font font;

        private SimulationManager simulationManager;
        private EnvironmentManager environmentManager;
        private SensorManager sensorManager;
        private DroneTelemetry telemetry;
        private DroneHealth playerHealth;

        private float refreshTimer;

        private GameObject configPanel;
        private GameObject runtimeLeft;
        private GameObject runtimeRight;
        private GameObject runtimeBar;
        private GameObject crosshairGo;
        private GameObject controlsTab;
        private GameObject controlsPanel;
        private GameObject remapPanel;
        private GameObject hudTab;
        private bool controlsOpen;
        private bool remapOpen;
        private bool hudHidden;
        private readonly Dictionary<string, Text> values = new Dictionary<string, Text>();
        private readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();

        private void Awake()
        {
            ResolveReferences();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                var any = Resources.FindObjectsOfTypeAll<Font>();
                if (any.Length > 0) font = any[0];
            }

            BuildCanvas();
            EnterConfig();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            // Guard the first half-second: the InputSystem can deliver a
            // stale pressed frame on play-mode entry, which would flip a
            // panel open before the user touches anything.
            if (keyboard != null && Time.timeSinceLevelLoad > 0.5f)
            {
                if (Pressed(keyboard, "panel")) ToggleControls();
                if (Pressed(keyboard, "hud")) ToggleHud();
            }

            refreshTimer += Time.deltaTime;
            if (refreshTimer < 0.25f)
            {
                return;
            }

            refreshTimer = 0f;
            RefreshValues();
        }

        // ---------- state ----------

        public void EnterRuntime()
        {
            state = UiState.Runtime;
            ApplyVisibility();
            RefreshValues();
        }

        public void EnterConfig()
        {
            state = UiState.Config;
            ApplyVisibility();
            RefreshValues();
        }

        private void ApplyVisibility()
        {
            ApplyHudVisibility();
        }

        private static bool Pressed(Keyboard keyboard, string bindingId)
        {
            Key key = KeyBindings.Get(bindingId);
            if (key == Key.None)
            {
                return false;
            }

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        // ---------- help panels (reference + remap, both states) ----------

        private struct ActionRef
        {
            public string LabelKey;
            public string Action;
            public string PathA;
            public string PathB;
        }

        private struct KeyRef
        {
            public string LabelKey;
            public string IdA;
            public string IdB;
        }

        private readonly List<ActionRef> actionRefs = new List<ActionRef>();
        private readonly List<KeyRef> keyRefs = new List<KeyRef>();
        private InputActionRebindingExtensions.RebindingOperation pendingOp;
        private InputAction captureAction;

        private InputActionMap DroneMap()
        {
            return InputSystem.actions != null ? InputSystem.actions.FindActionMap("Drone", false) : null;
        }

        private void StartActionRebind(string actionName, string defaultPath, string labelKey)
        {
            var map = DroneMap();
            var action = map != null ? map.FindAction(actionName, false) : null;
            int index = InputRebindStore.FindBinding(action, defaultPath);
            if (action == null || index < 0)
            {
                return;
            }

            CancelRebinds();
            SetText(labelKey, "press key…");
            pendingOp = action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    InputRebindStore.Save(InputSystem.actions);
                    RefreshControlLabels();
                    CancelRebinds();
                })
                .OnCancel(op =>
                {
                    RefreshControlLabels();
                    CancelRebinds();
                })
                .Start();
        }

        private void StartKeyRebind(string bindingId, string labelKey)
        {
            CancelRebinds();
            SetText(labelKey, "press key…");
            // NOTE: do NOT enable the capture action first — interactive
            // rebinding throws if its action is already enabled.
            captureAction = new InputAction("capture", InputActionType.Button, "<Keyboard>");
            InputAction target = captureAction;
            string id = bindingId;
            pendingOp = target.PerformInteractiveRebinding()
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    if (op.selectedControl is KeyControl keyControl)
                    {
                        KeyBindings.Set(id, keyControl.keyCode);
                    }

                    RefreshControlLabels();
                    CancelRebinds();
                })
                .OnCancel(op =>
                {
                    RefreshControlLabels();
                    CancelRebinds();
                })
                .Start();
        }

        private void CancelRebinds()
        {
            if (pendingOp != null)
            {
                pendingOp.Dispose();
                pendingOp = null;
            }

            if (captureAction != null)
            {
                captureAction.Disable();
                captureAction.Dispose();
                captureAction = null;
            }
        }

        private void ResetAllBindings()
        {
            CancelRebinds();
            if (InputSystem.actions != null)
            {
                InputRebindStore.Reset(InputSystem.actions);
            }

            KeyBindings.ResetAll();
            RefreshControlLabels();
        }

        private static GameObject AnchorTopRight(Transform canvas, string name, float width, float height, float top)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -top);
            rect.sizeDelta = new Vector2(width, height);
            return go;
        }

        private static GameObject AnchorTopLeft(Transform canvas, string name, float width, float height, float top)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -top);
            rect.sizeDelta = new Vector2(width, height);
            return go;
        }

        public void ToggleControls()
        {
            controlsOpen = !controlsOpen;
            if (controlsOpen) remapOpen = false;
            ApplyHelpVisibility();
        }

        public void ToggleRemap()
        {
            remapOpen = !remapOpen;
            if (remapOpen) controlsOpen = false;
            else CancelRebinds();
            ApplyHelpVisibility();
        }

        public void ToggleHud()
        {
            hudHidden = !hudHidden;
            ApplyHudVisibility();
        }

        private void ApplyHelpVisibility()
        {
            if (controlsTab != null) controlsTab.SetActive(!controlsOpen && !remapOpen && !hudHidden);
            if (controlsPanel != null) controlsPanel.SetActive(controlsOpen && !hudHidden);
            if (remapPanel != null) remapPanel.SetActive(remapOpen && !hudHidden);
        }

        private void ApplyHudVisibility()
        {
            bool show = !hudHidden;
            if (configPanel != null) configPanel.SetActive(show && state == UiState.Config);
            if (runtimeLeft != null) runtimeLeft.SetActive(show && state == UiState.Runtime);
            if (runtimeRight != null) runtimeRight.SetActive(show && state == UiState.Runtime);
            if (runtimeBar != null) runtimeBar.SetActive(show && state == UiState.Runtime);
            if (crosshairGo != null) crosshairGo.SetActive(show);
            if (hudTab != null) hudTab.SetActive(!show);
            ApplyHelpVisibility();
        }

        // ---------- config actions (all live) ----------

        private void CycleTerrain()
        {
            var cur = environmentManager != null && environmentManager.Terrain != null
                ? environmentManager.Terrain.Terrain : TerrainPreset.Rural;
            environmentManager?.SetTerrain(cur == TerrainPreset.Rural ? TerrainPreset.Urban : TerrainPreset.Rural);
            RefreshValues();
        }

        private void CycleTimeOfDay()
        {
            var cur = environmentManager?.TimeOfDay?.CurrentTimeOfDay ?? Configuration.TimeOfDayPreset.Day;
            environmentManager?.SetTimeOfDay((Configuration.TimeOfDayPreset)(((int)cur + 1) % 4));
            RefreshValues();
        }

        private void CycleWeather()
        {
            var cur = environmentManager?.Weather?.CurrentWeather ?? Configuration.WeatherPreset.Clear;
            environmentManager?.SetWeather((Configuration.WeatherPreset)(((int)cur + 1) % 4));
            RefreshValues();
        }

        // ---------- refresh ----------

        private void SetText(string key, string text)
        {
            if (values.TryGetValue(key, out var label) && label != null)
            {
                label.text = text;
            }
        }

        private void SetSlider(string key, float value)
        {
            if (sliders.TryGetValue(key, out var slider) && slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }
        }

        private void RefreshValues()
        {
            ResolveReferences();
            if (environmentManager == null)
            {
                return;
            }

            var wind = environmentManager.Wind;
            var weather = environmentManager.Weather;
            var tod = environmentManager.TimeOfDay;
            var terrain = environmentManager.Terrain;

            SetText("cfgTerrain", $"TERRAIN  <b>{terrain?.Terrain.ToString().ToUpperInvariant()}</b>");
            SetText("cfgTime", $"TIME  <b>{tod?.CurrentTimeOfDay.ToString().ToUpperInvariant()}</b>  >");
            SetText("cfgWeather", $"WEATHER  <b>{weather?.CurrentWeather.ToString().ToUpperInvariant()}</b>  >");
            if (wind != null)
            {
                SetText("windVal", $"{wind.WindStrength:0}");
                SetText("windDirVal", $"{wind.WindDirectionDegrees:0}");
                SetText("turbVal", $"{wind.Turbulence:0}");
                SetSlider("wind", wind.WindStrength);
                SetSlider("windDir", wind.WindDirectionDegrees);
                SetSlider("turb", wind.Turbulence);
            }

            if (weather != null)
            {
                SetText("visVal", $"{weather.Visibility:0}");
                SetSlider("vis", weather.Visibility);
            }

            float deg = sensorManager != null ? sensorManager.OverallDegradation : 0f;
            SetText("sensVal", $"{deg:0}");
            SetSlider("sens", deg);

            if (state == UiState.Config)
            {
                return;
            }

            if (telemetry != null)
            {
                SetText("rAlt", $"ALT  {telemetry.Altitude:0.0} m");
                SetText("rVel", $"VEL  {telemetry.Velocity.x:0.0} / {telemetry.Velocity.y:0.0} / {telemetry.Velocity.z:0.0}  ({telemetry.Speed:0.0} m/s)");
                SetText("rHdg", $"HDG  {telemetry.Heading:000}°  IMU {telemetry.ReportedHeading:000}°");
                SetText("rGps", $"GPS  {telemetry.ReportedPosition.x:0.0} {telemetry.ReportedPosition.y:0.0} {telemetry.ReportedPosition.z:0.0}  ERR {telemetry.PositionError:0.0} m");
            }

            if (playerHealth != null)
            {
                SetText("rHealth", $"HULL  {playerHealth.Health:0}/100  <b>{playerHealth.StateLabel}</b>");
            }

            float mult = simulationManager != null && simulationManager.Clock != null ? simulationManager.Clock.TimeMultiplier : 1f;
            SetText("rSim", $"SIM  {mult:0.##}x");
            if (wind != null)
            {
                SetText("rWind", $"WIND  {wind.WindStrength:0}/100 @ {wind.WindDirectionDegrees:000}°");
                SetText("rTurb", $"TURB  {wind.Turbulence:0}/100");
            }

            if (sensorManager != null)
            {
                var gps = sensorManager.Gps;
                var imu = sensorManager.Imu;
                var cam = sensorManager.Camera;
                var lidar = sensorManager.Lidar;
                var radar = sensorManager.Radar;
                SetText("sGps", $"GPS  {(gps != null ? gps.Health : 0):0}  ERR {(gps != null ? gps.PositionError : 0):0.0}m");
                SetText("sImu", $"IMU  {(imu != null ? imu.Health : 0):0}  YAW {(imu != null ? imu.YawError : 0):0.0}°");
                SetText("sCam", $"CAM  {(cam != null ? cam.Status : "-")}  VIS {(cam != null ? cam.EffectiveVisibility * 100f : 0):0}%");
                SetText("sLidar", $"LIDAR  {(lidar != null ? lidar.ReportedRange : 0):0}m  REL {(lidar != null ? lidar.PointReliability * 100f : 0):0}%");
                SetText("sRadar", $"RADAR  {(radar != null ? radar.ReportedContacts : 0)} CT  REL {(radar != null ? radar.DetectionProbability * 100f : 0):0}%");
            }

            SetText("bWindVal", $"{wind?.WindStrength:0}");
            SetText("bTurbVal", $"{wind?.Turbulence:0}");
            SetText("bVisVal", $"{weather?.Visibility:0}");
            SetText("bSensVal", $"{deg:0}");
            SetText("bSimVal", $"{mult:0.##}x");
            SetSlider("bWind", wind?.WindStrength ?? 0f);
            SetSlider("bTurb", wind?.Turbulence ?? 0f);
            SetSlider("bVis", weather?.Visibility ?? 100f);
            SetSlider("bSens", deg);
        }

        private void ResolveReferences()
        {
            if (simulationManager == null) simulationManager = FindAnyObjectByType<SimulationManager>();
            if (environmentManager == null) environmentManager = FindAnyObjectByType<EnvironmentManager>();
            if (sensorManager == null)
            {
                var primaryForSensors = GameObject.Find("PlayerDrone");
                if (primaryForSensors != null) sensorManager = primaryForSensors.GetComponent<SensorManager>();
                if (sensorManager == null)
                {
                    primaryForSensors = GameObject.Find("Drone");
                    if (primaryForSensors != null) sensorManager = primaryForSensors.GetComponent<SensorManager>();
                }

                if (sensorManager == null) sensorManager = FindAnyObjectByType<SensorManager>();
            }

            if (telemetry == null)
            {
                var primary = GameObject.Find("PlayerDrone") ?? GameObject.Find("Drone");
                if (primary != null) telemetry = primary.GetComponent<DroneTelemetry>();
                if (telemetry == null) telemetry = FindAnyObjectByType<DroneTelemetry>();
            }

            if (playerHealth == null)
            {
                var primary = GameObject.Find("PlayerDrone") ?? GameObject.Find("Drone");
                if (primary != null) playerHealth = primary.GetComponent<DroneHealth>();
            }
        }

        // ---------- manual-layout builders ----------

        private class Column
        {
            public readonly GameObject Panel;
            public float Y;

            public Column(GameObject panel, float top)
            {
                Panel = panel;
                Y = top;
            }
        }

        private GameObject MakePanel(Transform parent, string name, float left, float top, float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            var image = go.AddComponent<Image>();
            image.color = Bg;
            return go;
        }

        private static RectTransform RectOf(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = go.AddComponent<RectTransform>();
            }

            return rect;
        }

        private static RectTransform RowRect(GameObject go, float y, float height, float padLeft = Pad, float padRight = Pad)
        {
            var rect = RectOf(go);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2((padLeft - padRight) / 2f, -y);
            rect.sizeDelta = new Vector2(-(padLeft + padRight), height);
            return rect;
        }

        private Text AddText(GameObject parent, float y, float height, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);
            RowRect(go, y, height);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.text = text;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        private Button AddButton(GameObject parent, float y, float height, string text, UnityEngine.Events.UnityAction onClick, Color? bg = null, float? widthFraction = null, float xOffset = 0f)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent.transform, false);
            var rect = RowRect(go, y, height);
            if (widthFraction.HasValue)
            {
                float panelW = parent.GetComponent<RectTransform>().rect.width;
                float w = (panelW - Pad * 2f - Gap) * widthFraction.Value;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(Pad + xOffset, -y);
                rect.sizeDelta = new Vector2(w, height);
            }

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
            label.fontSize = 13;
            label.color = BodyText;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            label.raycastTarget = false;
            button.onClick.AddListener(onClick);
            return button;
        }

        private void AddCycleRow(Column col, string key, UnityEngine.Events.UnityAction onNext)
        {
            var go = new GameObject("CycleRow");
            go.transform.SetParent(col.Panel.transform, false);
            RowRect(go, col.Y, 28f);
            var image = go.AddComponent<Image>();
            image.color = ButtonBg;
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onNext);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 13;
            label.color = BodyText;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            label.raycastTarget = false;
            values[key] = label;
            col.Y += 28f + Gap;
        }

        private void AddSliderRow(Column col, string sliderKey, string valueKey, float min, float max, UnityEngine.Events.UnityAction<float> onSet)
        {
            float y = col.Y;
            var panel = col.Panel;
            float panelW = panel.GetComponent<RectTransform>().rect.width;

            var valGo = new GameObject("Val");
            valGo.transform.SetParent(panel.transform, false);
            var valRect = valGo.AddComponent<RectTransform>();
            valRect.anchorMin = new Vector2(1f, 1f);
            valRect.anchorMax = new Vector2(1f, 1f);
            valRect.pivot = new Vector2(1f, 1f);
            valRect.anchoredPosition = new Vector2(-Pad, -y);
            valRect.sizeDelta = new Vector2(40f, 26f);
            var valText = valGo.AddComponent<Text>();
            valText.font = font;
            valText.fontSize = 13;
            valText.color = Amber;
            valText.alignment = TextAnchor.MiddleRight;
            valText.raycastTarget = false;
            values[valueKey] = valText;

            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(panel.transform, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 1f);
            sliderRect.anchorMax = new Vector2(1f, 1f);
            sliderRect.pivot = new Vector2(0.5f, 1f);
            sliderRect.anchoredPosition = new Vector2((Pad - (Pad + 48f)) / 2f, -y);
            sliderRect.sizeDelta = new Vector2(-(Pad + Pad + 48f), 26f);
            var sliderBg = sliderGo.AddComponent<Image>();
            sliderBg.color = TrackBg;
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(onSet);

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.2f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.8f);
            fillAreaRect.offsetMin = new Vector2(3f, 0f);
            fillAreaRect.offsetMax = new Vector2(-3f, 0f);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = FillBlue;
            fillImage.raycastTarget = false;
            var fillRect = RectOf(fill);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;
            slider.handleRect = null;
            sliders[sliderKey] = slider;
            col.Y += 26f + Gap;
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("OperatorCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            configPanel = BuildConfigPanel(canvasGo.transform);
            runtimeLeft = BuildRuntimeLeft(canvasGo.transform);
            runtimeRight = BuildRuntimeRight(canvasGo.transform);
            runtimeBar = BuildRuntimeBar(canvasGo.transform);
            BuildCrosshair(canvasGo.transform);
            crosshairGo = canvasGo.transform.Find("Crosshair")?.gameObject;
            BuildHelpWidgets(canvasGo.transform);
        }

        private GameObject BuildConfigPanel(Transform canvas)
        {
            var panel = MakePanel(canvas, "ConfigPanel", 10f, 10f, 320f, 440f);
            var col = new Column(panel, 10f);
            AddText(panel, col.Y, 22f, "SCENARIO CONFIGURATION", 14, HeaderCyan).fontStyle = FontStyle.Bold;
            col.Y += 22f + Gap;
            AddCycleRow(col, "cfgTerrain", CycleTerrain);
            AddCycleRow(col, "cfgTime", CycleTimeOfDay);
            AddCycleRow(col, "cfgWeather", CycleWeather);
            AddText(panel, col.Y, 15f, "WIND / DIRECTION", 11, DimText);
            col.Y += 15f + Gap;
            AddSliderRow(col, "wind", "windVal", 0f, 100f, v => environmentManager?.SetWindStrength(v));
            AddSliderRow(col, "windDir", "windDirVal", 0f, 360f, v => environmentManager?.SetWindDirection(v));
            AddText(panel, col.Y, 15f, "TURBULENCE / VISIBILITY", 11, DimText);
            col.Y += 15f + Gap;
            AddSliderRow(col, "turb", "turbVal", 0f, 100f, v => environmentManager?.SetTurbulence(v));
            AddSliderRow(col, "vis", "visVal", 0f, 100f, v => environmentManager?.SetVisibility(v));
            AddText(panel, col.Y, 15f, "SENSOR DEGRADATION", 11, DimText);
            col.Y += 15f + Gap;
            AddSliderRow(col, "sens", "sensVal", 0f, 100f, v => sensorManager?.SetOverallDegradation(v));
            AddButton(panel, col.Y, 34f, "ENTER SIMULATION  ▸", EnterRuntime, AccentBg);
            return panel;
        }

        private GameObject BuildRuntimeLeft(Transform canvas)
        {
            var panel = MakePanel(canvas, "RuntimeLeft", 10f, 10f, 330f, 217f);
            var col = new Column(panel, 10f);
            AddText(panel, col.Y, 20f, "FPV TELEMETRY", 14, HeaderCyan).fontStyle = FontStyle.Bold;
            col.Y += 20f + Gap;
            foreach (var key in new[] { "rAlt", "rVel", "rHdg", "rGps", "rHealth", "rSim", "rWind", "rTurb" })
            {
                var label = AddText(panel, col.Y, 18f, "--", 13, BodyText);
                values[key] = label;
                col.Y += 18f + 3f;
            }

            return panel;
        }

        private GameObject BuildRuntimeRight(Transform canvas)
        {
            var panel = MakePanel(canvas, "RuntimeRight", 0f, 10f, 320f, 165f);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            var col = new Column(panel, 10f);
            AddText(panel, col.Y, 20f, "SENSORS", 14, HeaderCyan).fontStyle = FontStyle.Bold;
            col.Y += 20f + Gap;
            foreach (var key in new[] { "sGps", "sImu", "sCam", "sLidar", "sRadar" })
            {
                var label = AddText(panel, col.Y, 18f, "--", 13, BodyText);
                values[key] = label;
                col.Y += 18f + 3f;
            }

            return panel;
        }

        private GameObject BuildRuntimeBar(Transform canvas)
        {
            var bar = new GameObject("RuntimeBar");
            bar.transform.SetParent(canvas, false);
            var rect = bar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.sizeDelta = new Vector2(-20f, 104f);
            var image = bar.AddComponent<Image>();
            image.color = Bg;

            float totalW = 1900f;
            float x = 12f;
            AddBarBlock(bar, "SIM", "bSimVal", x, totalW, 0.10f);
            x += totalW * 0.10f;
            var minus = AddBarButton(bar, "−", x, 34f, () => simulationManager?.Clock?.DecreaseMultiplier());
            x += 38f;
            var plus = AddBarButton(bar, "+", x, 34f, () => simulationManager?.Clock?.IncreaseMultiplier());
            x += 38f;
            x = AddBarSlider(bar, "WIND", "bWind", "bWindVal", 0f, 100f, v => environmentManager?.SetWindStrength(v), x, totalW, 0.16f);
            x = AddBarSlider(bar, "TURB", "bTurb", "bTurbVal", 0f, 100f, v => environmentManager?.SetTurbulence(v), x, totalW, 0.16f);
            x = AddBarSlider(bar, "VIS", "bVis", "bVisVal", 0f, 100f, v => environmentManager?.SetVisibility(v), x, totalW, 0.16f);
            x = AddBarSlider(bar, "SENS", "bSens", "bSensVal", 0f, 100f, v => sensorManager?.SetOverallDegradation(v), x, totalW, 0.16f);
            AddBarButton(bar, "◂ CONFIGURE", totalW - 12f - 130f, 130f, EnterConfig);
            return bar;
        }

        private void AddBarBlock(GameObject bar, string title, string valueKey, float x, float totalW, float fraction)
        {
            float w = totalW * fraction;
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(bar.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(x, -10f);
            titleRect.sizeDelta = new Vector2(w, 14f);
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 11;
            titleText.color = DimText;
            titleText.text = title;
            titleText.raycastTarget = false;

            var valGo = new GameObject("Val");
            valGo.transform.SetParent(bar.transform, false);
            var valRect = valGo.AddComponent<RectTransform>();
            valRect.anchorMin = new Vector2(0f, 1f);
            valRect.anchorMax = new Vector2(0f, 1f);
            valRect.pivot = new Vector2(0f, 1f);
            valRect.anchoredPosition = new Vector2(x, -26f);
            valRect.sizeDelta = new Vector2(w, 22f);
            var valText = valGo.AddComponent<Text>();
            valText.font = font;
            valText.fontSize = 15;
            valText.color = BodyText;
            valText.raycastTarget = false;
            values[valueKey] = valText;
        }

        private Button AddBarButton(GameObject bar, string text, float x, float w, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("BarButton");
            go.transform.SetParent(bar.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -8f);
            rect.sizeDelta = new Vector2(w, 34f);
            var img = go.AddComponent<Image>();
            img.color = ButtonBg;
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 14;
            label.color = BodyText;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            label.raycastTarget = false;
            return button;
        }

        private float AddBarSlider(GameObject bar, string title, string sliderKey, string valueKey, float min, float max, UnityEngine.Events.UnityAction<float> onSet, float x, float totalW, float fraction)
        {
            float w = totalW * fraction - 12f;
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(bar.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(x, -10f);
            titleRect.sizeDelta = new Vector2(w, 14f);
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 11;
            titleText.color = DimText;
            titleText.text = title;
            titleText.raycastTarget = false;

            var valGo = new GameObject("Val");
            valGo.transform.SetParent(bar.transform, false);
            var valRect = valGo.AddComponent<RectTransform>();
            valRect.anchorMin = new Vector2(0f, 1f);
            valRect.anchorMax = new Vector2(0f, 1f);
            valRect.pivot = new Vector2(1f, 1f);
            valRect.anchoredPosition = new Vector2(x + w, -10f);
            valRect.sizeDelta = new Vector2(36f, 14f);
            var valText = valGo.AddComponent<Text>();
            valText.font = font;
            valText.fontSize = 11;
            valText.color = Amber;
            valText.alignment = TextAnchor.MiddleRight;
            valText.raycastTarget = false;
            values[valueKey] = valText;

            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(bar.transform, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 1f);
            sliderRect.anchorMax = new Vector2(0f, 1f);
            sliderRect.pivot = new Vector2(0f, 1f);
            sliderRect.anchoredPosition = new Vector2(x, -30f);
            sliderRect.sizeDelta = new Vector2(w, 44f);
            var sliderBg = sliderGo.AddComponent<Image>();
            sliderBg.color = TrackBg;
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(onSet);

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(4f, 0f);
            fillAreaRect.offsetMax = new Vector2(-4f, 0f);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = FillBlue;
            fillImage.raycastTarget = false;
            var fillRect = RectOf(fill);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;
            slider.handleRect = null;
            sliders[sliderKey] = slider;
            return x + totalW * fraction;
        }

        private void AddHeaderButton(GameObject parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("HeaderButton");
            go.transform.SetParent(parent.transform, false);
            var rect = RectOf(go);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(Pad, 4f);
            rect.offsetMax = new Vector2(-Pad, -4f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.color = HeaderCyan;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.text = text;
        }

        private float AddSheetHeader(GameObject panel, float y, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("SheetHeader");
            go.transform.SetParent(panel.transform, false);
            var rect = RectOf(go);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(-(Pad * 2f), 26f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.color = HeaderCyan;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.text = text;
            return y + 26f + Gap;
        }

        private void AddSheetButton(GameObject panel, float y, string text, UnityEngine.Events.UnityAction onClick, Color? bg)
        {
            AddButton(panel, y, 30f, text, onClick, bg);
        }

        private float AddRefSection(GameObject panel, float y, string title)
        {
            AddText(panel, y, 13f, title, 10, DimText);
            return y + 13f + 4f;
        }

        private float AddRefActionRow(GameObject panel, float y, string labelKey, string action, string actionName, string pathA, string pathB)
        {
            actionRefs.Add(new ActionRef { LabelKey = labelKey, Action = actionName, PathA = pathA, PathB = pathB });
            var label = AddText(panel, y, 16f, action, 11, BodyText);
            ShiftLabelRight(label, panel, y, 16f);
            values[labelKey] = label;
            var chip = AddKeyChip(panel, y, "--");
            values[labelKey + "_k"] = chip;
            return y + 16f + 2f;
        }

        private float AddRefKeyRow(GameObject panel, float y, string labelKey, string action, string id)
        {
            return AddRefKeyRow(panel, y, labelKey, action, id, null);
        }

        private float AddRefKeyRow(GameObject panel, float y, string labelKey, string action, string idA, string idB)
        {
            keyRefs.Add(new KeyRef { LabelKey = labelKey, IdA = idA, IdB = idB });
            var label = AddText(panel, y, 16f, action, 11, BodyText);
            ShiftLabelRight(label, panel, y, 16f);
            values[labelKey] = label;
            var chip = AddKeyChip(panel, y, "--");
            values[labelKey + "_k"] = chip;
            return y + 16f + 2f;
        }

        private float AddRefStaticRow(GameObject panel, float y, string key, string action)
        {
            var label = AddText(panel, y, 16f, action, 11, BodyText);
            ShiftLabelRight(label, panel, y, 16f);
            AddKeyChip(panel, y, key);
            return y + 16f + 2f;
        }

        private Text AddKeyChip(GameObject panel, float y, string text)
        {
            var keyGo = new GameObject("Key");
            keyGo.transform.SetParent(panel.transform, false);
            var keyRect = RectOf(keyGo);
            keyRect.anchorMin = new Vector2(0f, 1f);
            keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot = new Vector2(0f, 1f);
            keyRect.anchoredPosition = new Vector2(Pad, -y);
            keyRect.sizeDelta = new Vector2(64f, 16f);
            var keyBg = keyGo.AddComponent<Image>();
            keyBg.color = ButtonBg;
            var keyTextGo = new GameObject("Text");
            keyTextGo.transform.SetParent(keyGo.transform, false);
            var keyTextRect = keyTextGo.AddComponent<RectTransform>();
            keyTextRect.anchorMin = Vector2.zero;
            keyTextRect.anchorMax = Vector2.one;
            keyTextRect.offsetMin = Vector2.zero;
            keyTextRect.offsetMax = Vector2.zero;
            var keyText = keyTextGo.AddComponent<Text>();
            keyText.font = font;
            keyText.fontSize = 10;
            keyText.fontStyle = FontStyle.Bold;
            keyText.color = Amber;
            keyText.alignment = TextAnchor.MiddleCenter;
            keyText.text = text;
            keyText.raycastTarget = false;
            return keyText;
        }

        private void ShiftLabelRight(Text label, GameObject panel, float y, float height)
        {
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Pad + 64f + 8f, -y);
            rect.sizeDelta = new Vector2(-(Pad + 64f + 8f + Pad), height);
        }

        private void AddRemapColumnTitle(GameObject panel, float x, float y, string title)
        {
            var go = new GameObject("ColTitle");
            go.transform.SetParent(panel.transform, false);
            var rect = RectOf(go);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(280f, 15f);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = 11;
            label.color = DimText;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = title;
            label.raycastTarget = false;
        }

        private float AddRemapActionRow(GameObject panel, float x, float y, string actionLabel, string actionName, string path, string keyLabelKey)
        {
            remapActionRows.Add(new RemapActionRef { LabelKey = keyLabelKey, Action = actionName, PathA = path });
            AddRemapRowLabel(panel, x, y, actionLabel);
            var button = AddRemapKeyButton(panel, x, y, "--");
            values[keyLabelKey] = button;
            string a = actionName, p = path, k = keyLabelKey;
            button.GetComponentInParent<Button>().onClick.AddListener(() => StartActionRebind(a, p, k));
            return y + 16f + 2f;
        }

        private float AddRemapKeyRow(GameObject panel, float x, float y, string actionLabel, string bindingId, string keyLabelKey)
        {
            remapKeyRows.Add(new RemapKeyRef { LabelKey = keyLabelKey, IdA = bindingId });
            AddRemapRowLabel(panel, x, y, actionLabel);
            var button = AddRemapKeyButton(panel, x, y, "--");
            values[keyLabelKey] = button;
            string b = bindingId, k = keyLabelKey;
            button.GetComponentInParent<Button>().onClick.AddListener(() => StartKeyRebind(b, k));
            return y + 16f + 2f;
        }

        private void AddRemapRowLabel(GameObject panel, float x, float y, string actionLabel)
        {
            var go = new GameObject("Action");
            go.transform.SetParent(panel.transform, false);
            var rect = RectOf(go);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(150f, 16f);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = 11;
            label.color = BodyText;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = actionLabel;
            label.raycastTarget = false;
        }

        private Text AddRemapKeyButton(GameObject panel, float x, float y, string text)
        {
            var go = new GameObject("KeyButton");
            go.transform.SetParent(panel.transform, false);
            var rect = RectOf(go);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x + 158f, -y);
            rect.sizeDelta = new Vector2(80f, 16f);
            var image = go.AddComponent<Image>();
            image.color = ButtonBg;
            var button = go.AddComponent<Button>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 10;
            label.fontStyle = FontStyle.Bold;
            label.color = Amber;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private void RefreshControlLabels()
        {
            var map = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Drone", false) : null;
            foreach (var row in actionRefs)
            {
                string text = "?";
                if (map != null)
                {
                    var action = map.FindAction(row.Action, false);
                    if (action != null)
                    {
                        text = InputRebindStore.DisplayKey(action, row.PathA);
                        if (!string.IsNullOrEmpty(row.PathB))
                        {
                            text += " / " + InputRebindStore.DisplayKey(action, row.PathB);
                        }
                    }
                }

                SetText(row.LabelKey + "_k", text);
            }

            foreach (var row in keyRefs)
            {
                string text = KeyBindings.Get(row.IdA).ToString().ToUpperInvariant();
                if (!string.IsNullOrEmpty(row.IdB))
                {
                    text += " / " + KeyBindings.Get(row.IdB).ToString().ToUpperInvariant();
                }

                SetText(row.LabelKey + "_k", text);
            }

            // Remap buttons mirror the same live values.
            foreach (var row in remapActionRows)
            {
                string text = "?";
                if (map != null)
                {
                    var action = map.FindAction(row.Action, false);
                    if (action != null)
                    {
                        text = InputRebindStore.DisplayKey(action, row.PathA);
                    }
                }

                SetText(row.LabelKey, text);
            }

            foreach (var row in remapKeyRows)
            {
                SetText(row.LabelKey, KeyBindings.Get(row.IdA).ToString().ToUpperInvariant());
            }
        }

        private struct RemapActionRef
        {
            public string LabelKey;
            public string Action;
            public string PathA;
        }

        private struct RemapKeyRef
        {
            public string LabelKey;
            public string IdA;
        }

        private readonly List<RemapActionRef> remapActionRows = new List<RemapActionRef>();
        private readonly List<RemapKeyRef> remapKeyRows = new List<RemapKeyRef>();

        private void BuildHelpWidgets(Transform canvas)
        {
            controlsTab = AnchorTopRight(canvas, "ControlsTab", 260f, 32f, 185f);
            controlsTab.AddComponent<Image>().color = Bg;
            AddHeaderButton(controlsTab, "▸ CONTROLS  [F1]", ToggleControls);

            controlsPanel = AnchorTopRight(canvas, "ControlsPanel", 260f, 620f, 10f);
            controlsPanel.AddComponent<Image>().color = Bg;
            float y = 10f;
            y = AddSheetHeader(controlsPanel, y, "▾ CONTROLS  [F1]", ToggleControls);
            y = AddRefSection(controlsPanel, y, "FLIGHT");
            y = AddRefActionRow(controlsPanel, y, "ck_fw", "Forward", "Move", "<Keyboard>/w", null);
            y = AddRefActionRow(controlsPanel, y, "ck_bw", "Backward", "Move", "<Keyboard>/s", null);
            y = AddRefActionRow(controlsPanel, y, "ck_lf", "Strafe Left", "Move", "<Keyboard>/a", null);
            y = AddRefActionRow(controlsPanel, y, "ck_rt", "Strafe Right", "Move", "<Keyboard>/d", null);
            y = AddRefSection(controlsPanel, y, "VERTICAL");
            y = AddRefActionRow(controlsPanel, y, "ck_up", "Ascend", "Altitude", "<Keyboard>/e", null);
            y = AddRefActionRow(controlsPanel, y, "ck_dn", "Descend", "Altitude", "<Keyboard>/q", null);
            y = AddRefSection(controlsPanel, y, "ROTATION");
            y = AddRefStaticRow(controlsPanel, y, "MOUSE", "Look / Yaw");
            y = AddRefActionRow(controlsPanel, y, "ck_z", "Yaw Left", "Turn", "<Keyboard>/z", null);
            y = AddRefActionRow(controlsPanel, y, "ck_c", "Yaw Right", "Turn", "<Keyboard>/c", null);
            y = AddRefSection(controlsPanel, y, "ACROBATICS");
            y = AddRefKeyRow(controlsPanel, y, "ck_x", "Pitch Fwd (Hold)", "flipFwd");
            y = AddRefKeyRow(controlsPanel, y, "ck_n", "Roll (Hold)", "flipRoll");
            y = AddRefSection(controlsPanel, y, "SIMULATION");
            y = AddRefActionRow(controlsPanel, y, "ck_spd", "Sim Speed − / +", "SimulationSpeedDown", "<Keyboard>/leftBracket", "<Keyboard>/rightBracket");
            y = AddRefSection(controlsPanel, y, "ENVIRONMENT");
            y = AddRefKeyRow(controlsPanel, y, "ck_wnd", "Wind − / +", "windUp", "windDown");
            y = AddRefKeyRow(controlsPanel, y, "ck_wd", "Wind Dir − / +", "windDirUp", "windDirDown");
            y = AddRefKeyRow(controlsPanel, y, "ck_tb", "Turbulence − / +", "turbUp", "turbDown");
            y = AddRefKeyRow(controlsPanel, y, "ck_vs", "Visibility − / +", "visUp", "visDown");
            y = AddRefKeyRow(controlsPanel, y, "ck_wx", "Cycle Weather", "weather");
            y = AddRefKeyRow(controlsPanel, y, "ck_tm", "Cycle Time", "time");
            y = AddRefSection(controlsPanel, y, "SENSORS");
            y = AddRefKeyRow(controlsPanel, y, "ck_sn", "Degradation − / +", "sensUp", "sensDown");
            y = AddRefKeyRow(controlsPanel, y, "ck_so", "Reset Degradation", "sensReset");
            y = AddRefSection(controlsPanel, y, "INTERFACE");
            y = AddRefKeyRow(controlsPanel, y, "ck_f1", "Controls Panel", "panel");
            y = AddRefKeyRow(controlsPanel, y, "ck_f2", "Hide / Show HUD", "hud");
            AddSheetButton(controlsPanel, y, "REMAP KEYS  ▸", ToggleRemap, AccentBg);

            remapPanel = AnchorTopRight(canvas, "RemapPanel", 560f, 410f, 10f);
            remapPanel.AddComponent<Image>().color = Bg;
            float ry = 10f;
            ry = AddSheetHeader(remapPanel, ry, "▾ REMAP KEYS  (ESC cancels capture)", ToggleRemap);
            AddRemapColumnTitle(remapPanel, Pad, ry, "INPUT ACTIONS");
            AddRemapColumnTitle(remapPanel, 290f, ry, "SHORTCUT KEYS");
            ry += 20f;
            float ly = ry, ryy = ry;
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Forward", "Move", "<Keyboard>/w", "rb_w");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Backward", "Move", "<Keyboard>/s", "rb_s");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Strafe Left", "Move", "<Keyboard>/a", "rb_a");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Strafe Right", "Move", "<Keyboard>/d", "rb_d");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Ascend", "Altitude", "<Keyboard>/e", "rb_e");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Descend", "Altitude", "<Keyboard>/q", "rb_q");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Yaw Left", "Turn", "<Keyboard>/z", "rb_z");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Yaw Right", "Turn", "<Keyboard>/c", "rb_c");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Sim Slower", "SimulationSpeedDown", "<Keyboard>/leftBracket", "rb_lb");
            ly = AddRemapActionRow(remapPanel, Pad, ly, "Sim Faster", "SimulationSpeedUp", "<Keyboard>/rightBracket", "rb_rb2");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Wind +", "windUp", "rk_wu");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Wind −", "windDown", "rk_wd");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Wind Dir +", "windDirUp", "rk_du");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Wind Dir −", "windDirDown", "rk_dd");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Turbulence +", "turbUp", "rk_tu");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Turbulence −", "turbDown", "rk_td");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Visibility +", "visUp", "rk_vu");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Visibility −", "visDown", "rk_vd");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Cycle Weather", "weather", "rk_wx");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Cycle Time", "time", "rk_tm");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Degradation +", "sensUp", "rk_su");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Degradation −", "sensDown", "rk_sd");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Reset Degradation", "sensReset", "rk_sr");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Pitch Fwd (Hold)", "flipFwd", "rk_x");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Roll (Hold)", "flipRoll", "rk_n");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Controls Panel", "panel", "rk_f1");
            ryy = AddRemapKeyRow(remapPanel, 290f, ryy, "Hide / Show HUD", "hud", "rk_f2");
            AddSheetButton(remapPanel, Mathf.Max(ly, ryy) + Gap, "RESET ALL DEFAULTS", ResetAllBindings, null);

            hudTab = AnchorTopLeft(canvas, "HudTab", 170f, 32f, 10f);
            hudTab.AddComponent<Image>().color = Bg;
            AddHeaderButton(hudTab, "SHOW UI  [F2]", ToggleHud);

            controlsTab.SetActive(true);
            controlsPanel.SetActive(false);
            remapPanel.SetActive(false);
            hudTab.SetActive(false);
            controlsOpen = false;
            remapOpen = false;
            hudHidden = false;
            RefreshControlLabels();
        }

        private void BuildCrosshair(Transform canvas)
        {
            var go = new GameObject("Crosshair");
            go.transform.SetParent(canvas, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(60f, 60f);
            rect.anchoredPosition = Vector2.zero;
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = 30;
            label.color = Amber;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "+";
            label.raycastTarget = false;
        }
    }
}
