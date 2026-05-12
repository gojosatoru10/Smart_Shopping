using System;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

/// <summary>
/// Manages face recognition login flow:
/// 1. GUI calls EnableLoginMode() when user enters login page
/// 2. Polls .runtime/face_detection.json for successful recognition
/// 3. Raises LoginSuccess event with user name when recognized
/// 4. GUI calls DisableLoginMode() when leaving login page
/// </summary>
public class FaceRecognitionController : IDisposable
{
    // ── Configuration ────────────────────────────────────────────────────────
    private const int POLL_MS = 300;   // how often to check for login success
    private const float MIN_CONFIDENCE = 0.6f; // minimum confidence for login

    // ── State ────────────────────────────────────────────────────────────────
    public bool LoginModeActive { get; private set; } = false;
    public string RecognizedPerson { get; private set; } = "unknown";
    public string RecognizedGender { get; private set; } = "unknown";
    public float RecognitionConfidence { get; private set; } = 0f;
    public bool FaceDetected { get; private set; } = false;

    // Raised on the UI thread when a user is successfully recognized during login
    public event EventHandler<FaceLoginSuccessEventArgs> LoginSuccess;

    // ── Internals ────────────────────────────────────────────────────────────
    private readonly Control _uiControl;          // used for Invoke
    private readonly Timer _timer;
    private readonly string _faceJsonPath;
    private readonly string _loginModePath;
    private bool _disposed = false;
    private bool _loginSuccessTriggered = false;  // prevent multiple triggers

    public FaceRecognitionController(Control uiControl, string faceJsonPath = null, string loginModePath = null)
    {
        _uiControl = uiControl;
        _faceJsonPath = faceJsonPath ?? ResolveFaceJsonPath();
        _loginModePath = loginModePath ?? ResolveLoginModePath();
        _timer = new Timer { Interval = POLL_MS };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    /// <summary>
    /// Call this when user enters the login page.
    /// Writes login_mode.json to signal Python bridge to start face recognition.
    /// </summary>
    public void EnableLoginMode()
    {
        LoginModeActive = true;
        _loginSuccessTriggered = false;
        WriteLoginModeFile(true);
        System.Diagnostics.Debug.WriteLine("[FaceRecognition] Login mode ENABLED - Python should start detecting faces");
        System.Diagnostics.Debug.WriteLine($"[FaceRecognition] Login mode file written to: {_loginModePath}");
    }

    /// <summary>
    /// Call this when user leaves the login page.
    /// Writes login_mode.json to signal Python bridge to stop face recognition.
    /// </summary>
    public void DisableLoginMode()
    {
        LoginModeActive = false;
        _loginSuccessTriggered = false;
        WriteLoginModeFile(false);
        System.Diagnostics.Debug.WriteLine("[FaceRecognition] Login mode DISABLED");
    }

    // ── Polling ──────────────────────────────────────────────────────────────
    private void OnTick(object sender, EventArgs e)
    {
        if (!LoginModeActive) return;
        if (_loginSuccessTriggered) return;  // already triggered, waiting for GUI to disable

        try
        {
            if (!File.Exists(_faceJsonPath))
            {
                System.Diagnostics.Debug.WriteLine($"[FaceRecognition] Waiting for face_detection.json at: {_faceJsonPath}");
                return;
            }

            string json = File.ReadAllText(_faceJsonPath, Encoding.UTF8);
            
            string person = ExtractJsonStringValue(json, "person_identity");
            string gender = ExtractJsonStringValue(json, "gender");
            string confRaw = ExtractJsonRawValue(json, "recognition_confidence");
            string detectedRaw = ExtractJsonRawValue(json, "face_detected");
            string loginSuccessRaw = ExtractJsonRawValue(json, "login_success");

            float conf = 0f;
            bool detected = false;
            bool loginSuccess = false;

            if (!string.IsNullOrWhiteSpace(confRaw))
            {
                float.TryParse(confRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out conf);
            }
            if (!string.IsNullOrWhiteSpace(detectedRaw))
            {
                bool.TryParse(detectedRaw, out detected);
            }
            if (!string.IsNullOrWhiteSpace(loginSuccessRaw))
            {
                bool.TryParse(loginSuccessRaw, out loginSuccess);
            }

            RecognizedPerson = person;
            RecognizedGender = gender;
            RecognitionConfidence = conf;
            FaceDetected = detected;

            // Log current state for debugging
            System.Diagnostics.Debug.WriteLine($"[FaceRecognition] Poll: face_detected={detected}, person={person}, conf={conf:F2}, login_success={loginSuccess}");

            // Trigger login success if:
            // 1. Face detected
            // 2. Person is not "unknown"
            // 3. Confidence meets threshold
            // 4. login_success flag is true
            if (loginSuccess && detected && person != "unknown" && conf >= MIN_CONFIDENCE)
            {
                _loginSuccessTriggered = true;
                RaiseLoginSuccess(person, gender, conf);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FaceRecognition] read error: {ex.Message}");
        }
    }

    // ── File I/O ─────────────────────────────────────────────────────────────
    private void WriteLoginModeFile(bool active)
    {
        try
        {
            string dir = Path.GetDirectoryName(_loginModePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = string.Format(
                "{{\n  \"active\": {0},\n  \"timestamp\": \"{1}\"\n}}",
                active ? "true" : "false",
                DateTime.UtcNow.ToString("o")
            );

            // Write atomically using temp file to prevent corruption
            string tempPath = _loginModePath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            
            // Ensure write is complete before moving
            File.Delete(_loginModePath);
            File.Move(tempPath, _loginModePath);
            
            System.Diagnostics.Debug.WriteLine($"[FaceRecognition] Wrote login_mode.json: active={active}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FaceRecognition] Failed to write login_mode.json: {ex.Message}");
        }
    }

    // ── Events ───────────────────────────────────────────────────────────────
    private void RaiseLoginSuccess(string person, string gender, float confidence)
    {
        if (_uiControl.InvokeRequired)
            _uiControl.Invoke(new Action(() => RaiseLoginSuccess(person, gender, confidence)));
        else
        {
            System.Diagnostics.Debug.WriteLine($"[FaceRecognition] LOGIN SUCCESS: {person} ({gender}, conf={confidence:F2})");
            LoginSuccess?.Invoke(this, new FaceLoginSuccessEventArgs(person, gender, confidence));
        }
    }

    // ── Path resolution ──────────────────────────────────────────────────────
    private static string ResolveFaceJsonPath()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 5; i++)
        {
            string candidate = Path.Combine(dir, ".runtime", "face_detection.json");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".runtime", "face_detection.json");
    }

    private static string ResolveLoginModePath()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 5; i++)
        {
            string runtimeDir = Path.Combine(dir, ".runtime");
            if (Directory.Exists(runtimeDir))
                return Path.Combine(runtimeDir, "login_mode.json");
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".runtime", "login_mode.json");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisableLoginMode();  // Clean up on dispose
        _timer.Stop();
        _timer.Dispose();
    }

    // ── JSON parsing helpers ─────────────────────────────────────────────────
    private static string ExtractJsonStringValue(string content, string key)
    {
        try
        {
            string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?<v>(?:\\\\.|[^\\\"])*)\"";
            Match m = Regex.Match(content, pattern, RegexOptions.CultureInvariant);
            if (!m.Success) return "";
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
            if (!m.Success) return "";
            return m.Groups["v"].Value;
        }
        catch
        {
            return "";
        }
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

public class FaceLoginSuccessEventArgs : EventArgs
{
    public string PersonName { get; }
    public string Gender { get; }
    public float Confidence { get; }

    public FaceLoginSuccessEventArgs(string personName, string gender, float confidence)
    {
        PersonName = personName;
        Gender = gender;
        Confidence = confidence;
    }
}
