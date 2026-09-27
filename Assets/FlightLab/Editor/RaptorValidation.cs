using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace FlightLab
{
    [InitializeOnLoad]
    public static class RaptorValidation
    {
        private static string Work => F22CityBuilder.DiagnosticsFolder;
        private const string Active = "FlightLab.RaptorValidation";
        private static AirplanePhysics plane;
        private static FlightInput input;
        private static int stage;
        private static float stageStart, sampleAt;
        private static Vector3 startPosition;
        private static bool initialized, tookOff;
        private static readonly StringBuilder report = new StringBuilder();
        static RaptorValidation()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingPlayMode || !SessionState.GetBool(Active, false)) return;
                SessionState.SetBool(Active, false); initialized = false; Time.timeScale = 1;
            };
        }
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before validation.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != F22CityBuilder.ScenePath)
                throw new InvalidOperationException("Open Raptor Valley before running its flight checks.");
            SessionState.SetBool(Active, true); EditorApplication.isPlaying = true;
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (!initialized)
            {
                plane = UnityEngine.Object.FindAnyObjectByType<AirplanePhysics>();
                if (plane == null || plane.Body == null) return;
                input = plane.GetComponent<FlightInput>(); input.enabled = false;
                initialized = true; stage = 0; stageStart = Time.time; sampleAt = -1; tookOff = false;
                startPosition = plane.transform.position; report.Clear(); Time.timeScale = 3;
                try { ValidateMouseBindings(); }
                catch (Exception error) { Finish(false, "Control binding check: " + error.Message); return; }
                File.WriteAllText(Path.Combine(Work, "flight-samples.csv"), "time,stage,speed,y,z,pitch,grounded,vy,throttle,brake,wheelBrake,wheelRpm,thrust,constraints\n");
            }
            float elapsed = Time.time - stageStart;
            if (Time.time > sampleAt)
            {
                sampleAt = Time.time + 1;
                string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0:F2},{1},{2:F2},{3:F2},{4:F2},{5:F2},{6},{7:F2},{8},{9},{10},{11},{12},{13}\n", Time.time, stage, plane.Airspeed,
                    plane.transform.position.y, plane.transform.position.z, Mathf.DeltaAngle(0, plane.transform.eulerAngles.x), plane.Grounded, plane.Body.linearVelocity.y,
                    input.Throttle, input.Brake, plane.landingGear.noseWheel.brakeTorque, plane.landingGear.noseWheel.rpm, plane.maximumThrustN, plane.Body.constraints);
                File.AppendAllText(Path.Combine(Work, "flight-samples.csv"), line);
            }
            if (plane.Crashed) { Finish(false, "Unexpected collision during stage " + stage); return; }
            if (!float.IsFinite(plane.Airspeed) || plane.transform.position.y < -5)
            { Finish(false, "Invalid aircraft state during stage " + stage); return; }
            if (stage == 0)
            {
                input.SetExternalControls(0, 0, 0, 0);
                if (elapsed > 5)
                {
                    float drift = Vector2.Distance(new Vector2(startPosition.x, startPosition.z), new Vector2(plane.transform.position.x, plane.transform.position.z));
                    if (drift > 0.5f || plane.Airspeed > 0.5f || !plane.Grounded) { Finish(false, "Stationary start failed: drift=" + drift + ", speed=" + plane.Airspeed + ", grounded=" + plane.Grounded); return; }
                    report.AppendLine("PASS: zero-throttle start stayed stationary on the runway for 5 simulated seconds.");
                    stage = 1; stageStart = Time.time;
                }
            }
            else if (stage == 1)
            {
                float pitch = plane.Airspeed >= 80 ? 0.85f : 0;
                input.SetExternalControls(pitch, 0, 0, 1);
                if (!plane.Grounded && plane.HeightAboveGround > 20)
                {
                    tookOff = true; report.AppendLine($"PASS: takeoff reached {plane.HeightAboveGround:F1} m AGL at {plane.Airspeed:F1} m/s after {elapsed:F1} seconds.");
                    F22CityBuilder.CaptureCamera(Camera.main, Path.Combine(Work, "takeoff-preview.png"));
                    input.RequestReset(); stage = 2; stageStart = Time.time;
                }
                else if (elapsed > 32) { Finish(false, "Takeoff did not occur within 32 seconds."); return; }
            }
            else if (stage == 2)
            {
                input.SetExternalControls(0, 0, 0, 0);
                if (elapsed > 3)
                {
                    if (Vector3.Distance(plane.transform.position, startPosition) > 1 || plane.Airspeed > 0.5f)
                    { Finish(false, "Runway reset failed."); return; }
                    report.AppendLine("PASS: reset returned to the runway with zero throttle and zero forward speed.");
                    stage = 3; stageStart = Time.time;
                }
            }
            else if (stage == 3)
            {
                input.SetExternalControls(0, 0, 0, 1);
                if (plane.Airspeed > 35) { stage = 4; stageStart = Time.time; }
                else if (elapsed > 15) { Finish(false, "Aircraft did not accelerate for brake test."); return; }
            }
            else if (stage == 4)
            {
                input.SetExternalControls(0, 0, 0, 0, true);
                if (elapsed > 1 && plane.Airspeed < 0.5f)
                {
                    report.AppendLine($"PASS: brakes stopped a 35 m/s ground roll in {elapsed:F1} seconds.");
                    Finish(tookOff, "All requested ground-start and takeoff checks completed.");
                }
                else if (elapsed > 18) Finish(false, "Brake test failed to stop aircraft.");
            }
        }
        private static void Finish(bool passed, string message)
        {
            report.AppendLine((passed ? "PASS: " : "FAIL: ") + message);
            File.WriteAllText(Path.Combine(Work, "validation-result.txt"), report.ToString());
            if (passed) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
            Time.timeScale = 1; if (input != null) { input.enabled = true; input.source = FlightInput.InputSource.Keyboard; }
            SessionState.SetBool(Active, false); initialized = false; EditorApplication.isPlaying = false;
        }

        private static void ValidateMouseBindings()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse previousMouse = Mouse.current;
            Keyboard previousKeyboard = Keyboard.current;
            Mouse mouse = InputSystem.AddDevice<Mouse>("FlightLabValidationMouse");
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("FlightLabValidationKeyboard");
            var read = typeof(FlightInput).GetMethod("ReadKeyboardAndMouse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            try
            {
                void Sample(bool held, Key? key, Vector2 position)
                {
                    var state = new MouseState { position = position };
                    if (held) state = state.WithButton(MouseButton.Left);
                    InputSystem.QueueStateEvent(mouse, state);
                    InputSystem.QueueStateEvent(keyboard, key.HasValue ? new KeyboardState(key.Value) : new KeyboardState());
                    InputSystem.Update();
                    mouse.MakeCurrent(); keyboard.MakeCurrent();
                    read.Invoke(input, null);
                }
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Sample(true, null, center);
                if (input.Throttle != 1 || input.Pitch != 0) throw new Exception("Holding LMB must apply full throttle without pitch.");
                Sample(false, Key.W, center);
                if (input.Throttle != 0 || input.Pitch != -1) throw new Exception("W must pitch down with throttle at idle.");
                Sample(true, Key.S, center);
                if (input.Throttle != 1 || input.Pitch != 1) throw new Exception("LMB and S must apply throttle and pitch up together.");
                Sample(false, null, center);
                if (input.Throttle != 0) throw new Exception("Releasing LMB must return throttle to idle.");
                float scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.5f);
                Sample(true, null, new Vector2(Screen.width - 90 * scale, Screen.height - 37 * scale));
                if (input.Throttle != 0) throw new Exception("Restart button click must not apply throttle.");
                report.AppendLine("PASS: Input System mouse/keyboard events verify LMB hold/release, W pitch down, S + LMB takeoff controls, and restart click exclusion.");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
                previousMouse?.MakeCurrent(); previousKeyboard?.MakeCurrent();
                input.ResetControls();
            }
#endif
        }
    }
}
