using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace FlightLab
{
    public static class MotionControllerChecks
    {
        public static string Folder => Path.Combine(F22CityBuilder.DiagnosticsFolder, "Motion");
        [MenuItem("Flight Lab/Check motion controller software")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var report = new StringBuilder();
            try
            {
                string data = Packet("FL2,15,150000,0,0,16384,131,-262,0,0");
                Check(MotionSample.TryParse(data, out MotionSample packet), "Valid packet rejected");
                Check(packet.Acceleration == Vector3.forward && packet.Gyro == new Vector3(1,-2,0), "Sensor units incorrect");
                Check(!packet.ThrottlePressed, "Released button decoded as held");
                Check(MotionSample.TryParse(Packet("FL2,16,160000,0,0,16384,131,-262,0,1"), out packet) && packet.ThrottlePressed, "Held button not decoded");
                Check(!MotionSample.TryParse(Packet("FL2,16,160000,0,0,16384,131,-262,0,2"), out _), "Invalid button value accepted");
                Check(!MotionSample.TryParse(Packet("FL1,16,160000,0,0,16384,131,-262,0"), out _), "Old firmware accepted without a button field");
                Check(!MotionSample.TryParse(data.Replace("16384", "16385"), out _), "Corrupted packet accepted");
                Check(!MotionSample.TryParse("boot message", out _), "Boot text accepted");
                Check(!MotionSample.TryParse(Packet("FL2,1,1,40000,0,16000,0,0,0,0"), out _), "Out-of-range sample accepted");
                Check(!MotionSample.TryParse(Packet("FL2,1,1,0,0,0,0,0,0,0"), out _), "Dead sensor accepted");
                report.AppendLine("PASS: FL2 framing, button held/released, checksum, ranges, sensor units, old-firmware and boot-noise rejection.");
                var filter = new MotionTiltFilter();
                Vector3 bias = new Vector3(1,-1.6f,0.2f);
                uint sequence = 0, timestamp = uint.MaxValue - 1000000;
                void Feed(Vector3 gravity, int count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        timestamp = unchecked(timestamp + 10000);
                        filter.Add(new MotionSample { Sequence = sequence++, Microseconds = timestamp, Acceleration = gravity * 0.8f, Gyro = bias });
                    }
                }
                Feed(Vector3.forward, 1200);
                Check(filter.Calibrated, "Stationary calibration failed");
                Check(Mathf.Abs(filter.PitchDegrees) < 0.02f && Mathf.Abs(filter.RollDegrees) < 0.02f && (filter.Bias - bias).magnitude < 0.01f, "Neutral drift: " + filter.PitchDegrees + ", " + filter.RollDegrees);
                report.AppendLine("PASS: gyro-bias calibration, stable neutral, measured 0.8g scale and timestamp rollover.");
                Feed(new Vector3(Mathf.Sin(20*Mathf.Deg2Rad),0,Mathf.Cos(20*Mathf.Deg2Rad)), 300);
                Check(Mathf.Abs(filter.PitchDegrees - 20) < 1 && Mathf.Abs(filter.RollDegrees) < 1, "Pitch sign or axis wrong");
                Feed(new Vector3(0,Mathf.Sin(25*Mathf.Deg2Rad),Mathf.Cos(25*Mathf.Deg2Rad)), 300);
                Check(Mathf.Abs(filter.RollDegrees - 25) < 1 && Mathf.Abs(filter.PitchDegrees) < 1, "Roll sign or axis wrong");
                Check(MotionTiltFilter.Axis(1.5f,25,2) == 0 && MotionTiltFilter.Axis(30,25,2)==1 && MotionTiltFilter.Axis(-30,25,2)==-1, "Dead zone or limits wrong");
                filter.Recenter(); Feed(Vector3.forward, 150);
                Check(filter.Calibrated && filter.PitchDegrees==0 && filter.RollDegrees==0, "Recenter failed");
                report.AppendLine("PASS: pitch/roll tilt mapping, dead zone, saturation and recenter.");
                HybridBindings();
                report.AppendLine("PASS: physical button hold/release controls throttle in motion mode; mouse cannot apply power; pitch/roll, yaw and brakes remain; stale input idles throttle and brakes; keyboard mode retains mouse throttle.");
                File.WriteAllText(Path.Combine(Folder,"software-checks.txt"),report.ToString());
                Debug.Log(report.ToString());
            }
            catch (Exception error)
            {
                report.AppendLine("FAIL: " + error); File.WriteAllText(Path.Combine(Folder,"software-checks.txt"),report.ToString()); throw;
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static string Packet(string body)
        { byte checksum=0; foreach(char c in body) checksum^=(byte)c; return body+"*"+checksum.ToString("X2"); }
        private static void HybridBindings()
        {
#if ENABLE_INPUT_SYSTEM
            var go = new GameObject("Motion controller check");
            var input = go.AddComponent<FlightInput>();
            Mouse previousMouse=Mouse.current; Keyboard previousKeyboard=Keyboard.current;
            var mouse=InputSystem.AddDevice<Mouse>("FlightLabTestMouse");
            var keyboard=InputSystem.AddDevice<Keyboard>("FlightLabTestKeyboard");
            MethodInfo read=typeof(FlightInput).GetMethod("ReadKeyboardAndMouse",BindingFlags.NonPublic|BindingFlags.Instance);
            MethodInfo merge=typeof(FlightInput).GetMethod("ApplyMotionInput",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width/2f,Screen.height/2f)}.WithButton(MouseButton.Left));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.A,Key.E));
                InputSystem.Update(); mouse.MakeCurrent(); keyboard.MakeCurrent();
                input.source=FlightInput.InputSource.Motion;
                input.SetMotionControls(0.4f,-0.6f,false); read.Invoke(input,null); merge.Invoke(input,null);
                Check(input.Pitch==0.4f && input.Roll==-0.6f && input.Throttle==0 && input.Yaw==1,"Mouse must not apply throttle in motion mode");
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width/2f,Screen.height/2f)});
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space)); InputSystem.Update();
                input.SetMotionControls(0.4f,-0.6f,true);
                read.Invoke(input,null); merge.Invoke(input,null);
                Check(input.Throttle==1 && input.Brake,"Physical button hold or keyboard brake mismatch");
                input.SetMotionControls(0.4f,-0.6f,false); read.Invoke(input,null); merge.Invoke(input,null);
                Check(input.Throttle==0,"Physical button release must idle throttle");
                input.SetMotionControls(0.4f,-0.6f,true);
                typeof(FlightInput).GetField("lastMotionTime",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(input,Time.realtimeSinceStartup-1f);
                read.Invoke(input,null); merge.Invoke(input,null);
                Check(input.Pitch==0 && input.Roll==0 && input.Throttle==0 && input.Brake && input.ExternalSignalLost,"Stale held button retained throttle");
                input.SetMotionControls(0.4f,-0.6f,true);
                input.ClearMotionInput(); read.Invoke(input,null); merge.Invoke(input,null);
                Check(input.Pitch==0 && input.Roll==0 && input.Throttle==0 && input.Brake && input.ExternalSignalLost,"Disconnect failsafe mismatch");
                input.source=FlightInput.InputSource.Keyboard;
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width/2f,Screen.height/2f)}.WithButton(MouseButton.Left));
                InputSystem.Update(); read.Invoke(input,null); merge.Invoke(input,null);
                Check(input.Throttle==1,"Keyboard fallback lost mouse throttle");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
                previousMouse?.MakeCurrent(); previousKeyboard?.MakeCurrent();
                UnityEngine.Object.DestroyImmediate(go);
            }
#endif
        }
    }
}
