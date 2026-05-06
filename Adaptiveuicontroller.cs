using System;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

/// <summary>
/// Polls .runtime/current_emotion.json and exposes an AdaptiveState
/// that TuioDemo (or any WinForms form) can query to adjust its UI.
///
/// Drop this file next to TuioDemo.cs and call:
///     _adaptive = new AdaptiveUIController(this);
///     _adaptive.Start();
/// Then in your paint / refresh logic call _adaptive.ApplyTo(control).
/// </summary>
public class AdaptiveUIController : IDisposable
{
    // ── Configuration ────────────────────────────────────────────────────────
    private const int POLL_MS = 500;   // how often to re-read JSON
    private const int FRUSTRATION_SECS = 8;     // sustained frustration → reset hint
    private const float CONFIDENCE_MIN = 0.35f; // ignore low-confidence readings

    // ── State ────────────────────────────────────────────────────────────────
    public AdaptiveState CurrentState { get; private set; } = AdaptiveState.Neutral;
    public string RawEmotion { get; private set; } = "neutral";
    public float Confidence { get; private set; } = 0f;
    public bool FaceDetected { get; private set; } = false;
    public DateTime LastUpdate { get; private set; } = DateTime.UtcNow;

    // Raised on the UI thread whenever the state changes
    public event EventHandler<AdaptiveStateChangedEventArgs> StateChanged;

    // ── Internals ────────────────────────────────────────────────────────────
    private readonly Control _uiControl;          // used for Invoke
    private readonly Timer _timer;
    private readonly string _jsonPath;
    private AdaptiveState _prevState = AdaptiveState.Neutral;
    private DateTime _frustratedSince = DateTime.MinValue;
    private bool _disposed = false;

    public AdaptiveUIController(Control uiControl, string jsonPath = null)
    {
        _uiControl = uiControl;
        _jsonPath = jsonPath ?? ResolveJsonPath();
        _timer = new Timer { Interval = POLL_MS };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    // ── Polling ──────────────────────────────────────────────────────────────
    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            if (!File.Exists(_jsonPath)) return;

            string json = File.ReadAllText(_jsonPath, Encoding.UTF8);
            string hint = ExtractJsonStringValue(json, "adaptive_hint");
            string emotion = ExtractJsonStringValue(json, "emotion");
            string confidenceRaw = ExtractJsonRawValue(json, "confidence");
            string detectedRaw = ExtractJsonRawValue(json, "face_detected");

            float conf = 0f;
            bool detected = false;

            if (!string.IsNullOrWhiteSpace(confidenceRaw))
            {
                float.TryParse(
                    confidenceRaw,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out conf);
            }
            if (!string.IsNullOrWhiteSpace(detectedRaw))
            {
                bool.TryParse(detectedRaw, out detected);
            }

            if (conf < CONFIDENCE_MIN) return;   // too uncertain – keep previous state

            var newState = HintToState(hint, emotion);

            // Sustained-frustration escalation
            if (newState == AdaptiveState.Frustrated)
            {
                if (_frustratedSince == DateTime.MinValue)
                    _frustratedSince = DateTime.UtcNow;
                else if ((DateTime.UtcNow - _frustratedSince).TotalSeconds >= FRUSTRATION_SECS)
                    newState = AdaptiveState.SustainedFrustration;
            }
            else
            {
                _frustratedSince = DateTime.MinValue;
            }

            RawEmotion = emotion;
            Confidence = conf;
            FaceDetected = detected;
            LastUpdate = DateTime.UtcNow;
            CurrentState = newState;

            if (newState != _prevState)
            {
                _prevState = newState;
                RaiseStateChanged(newState);
            }
        }
        catch (Exception ex)
        {
            // Silently ignore – emotion bridge may be writing at same moment
            System.Diagnostics.Debug.WriteLine($"[AdaptiveUI] read error: {ex.Message}");
        }
    }

    private static AdaptiveState HintToState(string hint, string emotion)
    {
        switch (hint)
        {
            case "frustrated":
                return AdaptiveState.Frustrated;
            case "confused":
                return AdaptiveState.Confused;
            case "engaged":
                return AdaptiveState.Engaged;
            case "interested":
                return AdaptiveState.Interested;
            case "disengaged":
                return AdaptiveState.Disengaged;
            default:
                return AdaptiveState.Neutral;
        }
    }

    // ── UI helpers ───────────────────────────────────────────────────────────
    /// <summary>
    /// Apply adaptive visual changes to a target control (typically the main form or panel).
    /// Call this in your Refresh / Paint handler.
    /// </summary>
    public void ApplyTo(Control target)
    {
        if (target == null) return;
        switch (CurrentState)
        {
            case AdaptiveState.Confused:
            case AdaptiveState.Frustrated:
                target.Font = new Font(target.Font.FontFamily, 15f, FontStyle.Regular);
                break;
            case AdaptiveState.Engaged:
            case AdaptiveState.Interested:
                target.Font = new Font(target.Font.FontFamily, 12f, FontStyle.Regular);
                break;
            default:
                target.Font = new Font(target.Font.FontFamily, 12f, FontStyle.Regular);
                break;
        }
    }

    /// <summary>
    /// Returns a help-overlay visibility flag based on current state.
    /// Use this to show/hide a tooltip or help panel in TuioDemo.
    /// </summary>
    public bool ShouldShowHelp() => CurrentState == AdaptiveState.Confused
                                    || CurrentState == AdaptiveState.Frustrated;

    /// <summary>
    /// Returns true when the UI should show upsell / recommendation panels.
    /// </summary>
    public bool ShouldShowUpsell() => CurrentState == AdaptiveState.Engaged
                                    || CurrentState == AdaptiveState.Interested;

    /// <summary>
    /// Returns true when kiosk should play attract animation (user looks away / disengaged).
    /// </summary>
    public bool ShouldAttract() => CurrentState == AdaptiveState.Disengaged;

    /// <summary>
    /// Returns true when TuioDemo should offer "call attendant" or reset to home.
    /// </summary>
    public bool ShouldReset() => CurrentState == AdaptiveState.SustainedFrustration;

    // ── Events ───────────────────────────────────────────────────────────────
    private void RaiseStateChanged(AdaptiveState state)
    {
        if (_uiControl.InvokeRequired)
            _uiControl.Invoke(new Action(() => RaiseStateChanged(state)));
        else
            StateChanged?.Invoke(this, new AdaptiveStateChangedEventArgs(state, RawEmotion, Confidence));
    }

    // ── Path resolution (mirrors TuioDemo BluetoothStatePath pattern) ────────
    private static string ResolveJsonPath()
    {
        // Walk up from exe location to find .runtime/
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 5; i++)
        {
            string candidate = Path.Combine(dir, ".runtime", "current_emotion.json");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        // fallback: next to project root
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".runtime", "current_emotion.json");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
    }

    private static string ExtractJsonStringValue(string content, string key)
    {
        try
        {
            string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?<v>(?:\\\\.|[^\\\"])*)\"";
            Match m = Regex.Match(content, pattern, RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                return "";
            }
            return Regex.Unescape(m.Groups["v"].Value);
        }
        catch
        {
            return "";
        }
    }

    private static string ExtractJsonRawValue(string content, string key)
    {
        try
        {
            string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*(?<v>true|false|-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)";
            Match m = Regex.Match(content, pattern, RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                return "";
            }
            return m.Groups["v"].Value;
        }
        catch
        {
            return "";
        }
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

public enum AdaptiveState
{
    Neutral,
    Confused,
    Frustrated,
    SustainedFrustration,   // frustrated for FRUSTRATION_SECS seconds
    Engaged,
    Interested,
    Disengaged,
}

public class AdaptiveStateChangedEventArgs : EventArgs
{
    public AdaptiveState State { get; }
    public string Emotion { get; }
    public float Confidence { get; }

    public AdaptiveStateChangedEventArgs(AdaptiveState state, string emotion, float confidence)
    {
        State = state;
        Emotion = emotion;
        Confidence = confidence;
    }
}
