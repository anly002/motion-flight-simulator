using UnityEngine;

namespace FlightLab
{
    public sealed class FlightHUD : MonoBehaviour
    {
        public AirplanePhysics aircraft;
        public FlightInput controls;
        private GUIStyle text, title, number, small;
        private Texture2D panel;
        private SerialMotionController motion;
        private void OnGUI()
        {
            if (!Application.isPlaying || aircraft == null || controls == null) return;
            if (text == null || panel == null)
            {
                text = Style(16, new Color(0.85f, 0.93f, 0.97f));
                title = Style(19, new Color(0.48f, 0.86f, 0.92f), FontStyle.Bold);
                number = Style(30, Color.white, FontStyle.Bold);
                small = Style(13, new Color(0.7f, 0.81f, 0.86f));
                panel = new Texture2D(1, 1); panel.SetPixel(0, 0, new Color(0.025f, 0.065f, 0.095f, 0.9f)); panel.Apply();
            }
            float uiScale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.5f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * uiScale);
            float width = Screen.width / uiScale, height = Screen.height / uiScale;
            if (motion == null) motion = aircraft.GetComponent<SerialMotionController>();
            bool usingMotion = controls.source == FlightInput.InputSource.Motion;
            GUI.DrawTexture(new Rect(22, 22, 300, motion == null ? 236 : 360), panel);
            GUI.Label(new Rect(38, 35, 270, 28), "RAPTOR  /  FLIGHT LAB", title);
            GUI.Label(new Rect(38, 74, 130, 18), "AIRSPEED", small);
            GUI.Label(new Rect(188, 74, 120, 18), "HEIGHT AGL", small);
            GUI.Label(new Rect(38, 92, 142, 42), $"{aircraft.Airspeed * 1.943844f:0} kt", number);
            GUI.Label(new Rect(188, 92, 130, 42), $"{aircraft.HeightAboveGround:0} m", number);
            GUI.Label(new Rect(38, 147, 260, 22), $"THROTTLE   {controls.Throttle * 100:0}%", text);
            Color saved = GUI.color;
            GUI.color = new Color(0.16f, 0.28f, 0.33f); GUI.DrawTexture(new Rect(38, 176, 266, 6), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.8f, 0.87f); GUI.DrawTexture(new Rect(38, 176, 266 * controls.Throttle, 6), Texture2D.whiteTexture);
            GUI.color = saved;
            string gear = aircraft.landingGear == null || aircraft.landingGear.Deployed ? "DOWN" : "UP";
            GUI.Label(new Rect(38, 199, 275, 28), $"GEAR {gear}     AoA {aircraft.AngleOfAttack:0.0} deg", small);
            if (motion != null)
            {
                GUI.Label(new Rect(38, 230, 275, 23), usingMotion ? "MPU-6050  /  " + motion.portName : "KEYBOARD CONTROL", title);
                var statusStyle = new GUIStyle(small) { wordWrap = true };
                GUI.Label(new Rect(38, 261, 263, 40), motion.Status, statusStyle);
                GUI.Label(new Rect(38, 307, 266, 24), $"Pitch {motion.PitchDegrees:0.0}°   Roll {motion.RollDegrees:0.0}°", small);
                GUI.Label(new Rect(38, 335, 266, 24), "Throttle button  " + (!motion.Connected ? "NO SIGNAL" : motion.ThrottlePressed ? "HELD" : "RELEASED"), small);
            }
            string state = aircraft.Crashed ? "AIRCRAFT DAMAGED  —  R TO RESTART" :
                usingMotion && (motion == null || !motion.Ready) ? "MOTION NOT READY  —  CHECK STATUS / HOLD STILL" :
                controls.ExternalSignalLost ? "CONTROLLER SIGNAL LOST" :
                aircraft.Grounded || aircraft.Airspeed < 0.5f ?
                    aircraft.Airspeed > aircraft.rotationSpeed ? (usingMotion ? "ROTATE  —  GENTLY TILT THE FRONT UP" : "ROTATE  —  GENTLY HOLD S TO LIFT OFF") :
                    controls.Throttle < 0.01f ? (usingMotion ? "RUNWAY READY  —  HOLD BUTTON FOR THROTTLE" : "RUNWAY READY  —  HOLD LEFT MOUSE FOR THROTTLE") :
                    (usingMotion ? "TAKEOFF ROLL  —  160 kt, THEN TILT THE FRONT UP" : "TAKEOFF ROLL  —  REACH APPROX. 160 kt, THEN HOLD S") : "AIRBORNE  —  G TO RETRACT LANDING GEAR";
            GUI.DrawTexture(new Rect((width - 720) / 2, height - 95, 720, 74), panel);
            GUI.Label(new Rect((width - 690) / 2, height - 88, 690, 25), state, title);
            GUI.Label(new Rect((width - 690) / 2, height - 57, 695, 25),
                usingMotion ? "Tilt: pitch/roll   Button throttle   Q/E yaw   Space brake   C center   M keys   G gear   R reset" :
                "W/S pitch   A/D roll   Q/E steer   LMB throttle   Space brake   M motion   G gear   R reset", small);
            if (GUI.Button(new Rect(width - 154, 22, 132, 30), "Restart on runway")) controls.RequestReset();
            GUI.matrix = previous;
        }
        private static GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal)
        { var value = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight }; value.normal.textColor = color; return value; }
        private void OnDestroy() { if (panel != null) Destroy(panel); }
    }
}
