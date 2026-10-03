using System.Collections.Generic;
using DroneSimulator.Drones;
using DroneSimulator.Environment;
using DroneSimulator.Sensors;
using DroneSimulator.Simulation;
using UnityEngine;
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
            bool cfg = state == UiState.Config;
            if (configPanel != null) configPanel.SetActive(cfg);
            if (runtimeLeft != null) runtimeLeft.SetActive(!cfg);
            if (runtimeRight != null) runtimeRight.SetActive(!cfg);
            if (runtimeBar != null) runtimeBar.SetActive(!cfg);
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
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();

            configPanel = BuildConfigPanel(canvasGo.transform);
            runtimeLeft = BuildRuntimeLeft(canvasGo.transform);
            runtimeRight = BuildRuntimeRight(canvasGo.transform);
            runtimeBar = BuildRuntimeBar(canvasGo.transform);
            BuildCrosshair(canvasGo.transform);
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

            float totalW = 1260f;
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
