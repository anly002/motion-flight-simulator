# Raptor Valley

Open this repository's root folder as a project in Unity Hub.

Open **Flight Lab → Open Raptor Valley**, then press Unity's **Play** button. The F-22 starts parked on runway 36 with its landing gear down and throttle at zero.

**ESP32 + MPU-6050 motion control is enabled by default.** Keep the controller still for about two seconds until **MOTION READY** appears, then tilt it to pitch and roll. Hold the **breadboard button on D23** for throttle; release it to idle. Mouse throttle is disabled in motion mode. **C** recalibrates neutral; **M** switches between motion and keyboard controls. Close Arduino Serial Monitor before playing. Setup, mounting direction, and troubleshooting are in the project's `Firmware/README.md`.

The mouse, W/S and A/D controls below apply in keyboard mode:

| Control | Action |
|---|---|
| Hold left mouse button inside the Game view | Full throttle |
| Release left mouse button | Engine idle; the aircraft keeps coasting |
| W / S | Pitch down / up |
| A / D | Roll left / right |
| Q / E | Steer or yaw left / right |
| Space | Wheel brakes |
| G | Raise or lower landing gear while airborne |
| R | Return to the runway, stationary |

For takeoff, hold the breadboard throttle button (or left mouse in keyboard mode) and keep straight. At around **160 knots**, gently tilt the controller's front edge up (or hold **S** in keyboard mode) to raise the nose while holding throttle. Ease off the pitch input after lifting off. Press G to retract the gear. Releasing the throttle button reduces thrust to zero; it does not instantly stop the aircraft. A parking brake holds it still when it has stopped.

The scene contains the downloaded 4k F-22, a 3 km runway, hangars, a control tower, 573 city buildings, 2,279 trees, and a 16 × 16 km terrain with surrounding mountains. Scenery is a procedural prototype. The flight physics are tunable game approximations.

The new scene is `Assets/FlightLab/RaptorValley/RaptorValley.unity`. The original scene is still in `Assets/FlightLab/Generated/FlightSandbox.unity`. Runtime scripts, including the USB serial motion adapter, are in `Assets/FlightLab/Runtime`. The separate external control interface remains available for scripted checks.

**Verified in Unity 6000.6.0f1:** mouse hold/release in keyboard mode, independent W/S pitch, restart-button throttle exclusion, stationary runway start, takeoff, reset, and braking. Developer checks are available under **Flight Lab → Run flight checks** and **Flight Lab → Check motion controller software**; reports are saved to the project's `Library/FlightLabValidation` folder.

The aircraft model is by **bohmerang**, licensed **CC BY-NC-SA 4.0** (noncommercial). Its source, license, and attribution are retained in `Assets/Models/F22/ATTRIBUTION.md`. Review that license before distributing a game using the model.
