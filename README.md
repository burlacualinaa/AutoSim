# AutoSim - Mixed Reality Driving Simulator

A research-oriented driving simulator for **Microsoft HoloLens 2** and **Windows PC**, built with **Unity 2022.3 LTS**, **OpenXR**, and **Logitech G920 Driving Force Racing Wheel** support.

---

## 🚗 Features

- **Mixed Reality Cockpit (HoloLens 2):**  
  - Real-time pass-through AR anchored to the driver's seat.
  - OpenXR 1.14.3 + Microsoft Mixed Reality OpenXR plugin.
  - Dynamic camera calibration & transparency for optical see-through displays.

- **High-Fidelity Vehicle Physics:**  
  - Realistic 4-wheel physics using Unity `WheelCollider` and anti-roll bars.
  - Traction control and anti-stall brake interlocks.
  - Procedural road and urban environment generation.

- **Hardware & Controls:**  
  - **Logitech G920 Racing Wheel & Pedals:** Auto-detection, ghost device resolution, auto-calibration, and live on-screen diagnostic HUD.
  - **Simultaneous Hybrid Controls:** Keyboard (WASD / Arrows / Space) can be used seamlessly alongside the physical wheel.
  - **Ultra-Low Latency UDP Streaming:** Streams wheel angle and pedal telemetry from Windows PC to HoloLens 2 over Wi-Fi (port 9050 at 60 Hz).

---

## 📂 Project Architecture

```
Assets/
├── Scripts/
│   ├── Vehicle/          # VehicleController, WheelVisualSync, Speedometer
│   ├── Input/            # LogitechG920InputProvider, KeyboardInputProvider, NetworkInputProvider
│   ├── Networking/       # UDPInputReceiver, NetworkManager
│   ├── XR/               # XRRigSetup (HoloLens seat anchor & AR background)
│   ├── Environment/      # ProceduralRoadGenerator, BuildingGenerator, CockpitSetup
│   ├── Scenarios/        # PedestrianCrossing, HazardScenario, ScenarioManager
│   ├── Telemetry/        # CSV telemetry recorder
│   └── Editor/           # Build tools and automation scripts
├── Scenes/
│   └── DrivingSimulator.unity
└── Packages/
    └── MixedReality/     # Microsoft Mixed Reality OpenXR plugin
```

---

## 🛠️ Quick Start

### 1. Requirements
- **Unity 2022.3.10f1** (with Universal Windows Platform Build Support).
- **Windows 10/11 PC** with Logitech G-HUB installed.
- **Logitech G920** connected via USB to PC.
- **Microsoft HoloLens 2** connected to the same Wi-Fi network.

### 2. Running in Unity Editor (PC)
1. Open the project in Unity.
2. Open `Assets/Scenes/DrivingSimulator.unity`.
3. Press **Play**.
4. The on-screen HUD (top-right) will confirm the connected G920 wheel and pedal state.
5. Control the vehicle with the steering wheel & pedals, or using **WASD**.
6. Set the HoloLens IP in the HUD field (default: `192.168.0.153`) to enable live streaming.

### 3. Deploying to HoloLens 2
1. In Unity: `AutoSim > Generate HoloLens 2 Build (UWP ARM64)`.
2. Open the generated solution in `Builds/HoloLens2/AutoSim.sln` using Visual Studio.
3. Set configuration to `Release`, `ARM64`, target: `Remote Machine` (IP: `192.168.0.153`) or `Device` (USB).
4. Deploy and launch.
