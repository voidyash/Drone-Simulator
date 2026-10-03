# AI-Enabled Drone & Counter-Drone Threat Simulation Trainer

## Goal

Build a Unity-based simulation trainer for an SIH prototype.

The simulator must provide an FPV drone experience where users can configure
environmental conditions, sensor degradation, terrain, simulation speed,
drone count, and threat state.

The system must be modular and data-driven.

---

# CORE REQUIREMENTS

## Flight

The drone must support:

- FPV camera
- Forward/backward movement
- Strafe
- Ascend/descend
- Pitch
- Roll
- Yaw
- Adjustable drone velocity
- Acceleration/deceleration
- Wind disturbance
- Turbulence disturbance

Simulation speed is independent from physical drone speed.

Simulation speed:

0.25x
0.5x
1x
2x
4x

The simulation speed can be changed while running.

---

# ENVIRONMENT

Support:

- Time of day
- Day
- Dawn
- Dusk
- Night

Weather:

- Clear
- Cloudy
- Fog
- Rain

Environment parameters:

- Wind strength: 0-100
- Wind direction
- Turbulence: 0-100
- Visibility: 0-100

Environment changes must affect simulation behaviour, not just visuals.

---

# TERRAIN

Support:

- Urban
- Rural

Terrain must be selectable from the scenario configuration.

Terrain affects:

- Visibility
- Obstacles
- Navigation difficulty
- Sensor effectiveness

---

# SENSORS

Create independent sensor models.

Required sensors:

- Camera
- GPS
- IMU
- LiDAR
- Radar

Each sensor has:

- Health: 0-100
- Accuracy
- Noise
- Availability

Sensor degradation must affect sensor output.

Do NOT implement degradation merely as a UI effect.

---

# DRONES

Support:

- Single drone
- Multiple drones
- Swarm

Each drone must have:

- Unique ID
- Position
- Rotation
- Velocity
- Health
- Sensor state
- Threat state
- Telemetry

---

# SWARM

Create a SwarmManager.

Support:

- Drone count
- Formation
- Formation spacing
- Shared target
- Individual drone state

Initial formations:

- Line
- V
- Wedge
- Grid

---

# THREAT STATE

Each drone can be:

- Unknown
- Unarmed
- Armed

For the prototype, armed status is a classification/state variable.

Do not implement weapon mechanics.

---

# FPV

The primary user view is FPV.

Display:

- Crosshair
- Speed
- Altitude
- Heading
- Simulation speed
- GPS status
- Camera status
- IMU status
- Wind
- Turbulence

---

# TELEMETRY

Display:

Altitude
Velocity
Heading
Pitch
Roll
GPS health
Camera health
IMU health
Wind
Turbulence
Drone count
Tracked drones
Lost drones

---

# SCENARIO SYSTEM

Scenarios must be data-driven.

Create a ScenarioConfig using ScriptableObject.

It should contain:

- Terrain
- Time
- Weather
- Wind
- Turbulence
- Visibility
- Drone count
- Swarm enabled
- Formation
- Armed state
- Sensor degradation
- Simulation speed

---

# RUNTIME CONTROL

The following must be changeable during simulation:

- Simulation speed
- Wind
- Turbulence
- Visibility
- Sensor degradation

Changes must immediately affect the simulation.

---

# ARCHITECTURE

Use separate systems:

SimulationManager
ScenarioManager
SimulationClock
DroneController
FlightController
DroneSpawner
SwarmManager
EnvironmentManager
WeatherManager
WindSystem
TimeOfDayController
SensorManager
CameraSensor
GPSSensor
IMUSensor
LidarSensor
RadarSensor
TelemetryManager
ThreatManager
UIManager

Avoid giant MonoBehaviour classes.

Prefer composition.

Use ScriptableObjects for configuration.

Use events/interfaces where appropriate.

---

# DEVELOPMENT PRIORITY

Implement in this order:

1. Project architecture
2. Simulation clock
3. Drone flight
4. FPV camera
5. Environment
6. Wind/turbulence
7. Sensors
8. Sensor degradation
9. Single/multiple drones
10. Swarm
11. Threat states
12. Telemetry
13. Scenario configuration
14. Runtime control panel
15. Demo scenario
16. Polish

---

# IMPORTANT

The simulator must remain playable even if AI components are unavailable.

AI detection/tracking should be implemented behind interfaces so it can later
be replaced with an actual ML model.

Do not hardcode scenario values into gameplay scripts.

Do not create unnecessary dependencies.

Prefer Unity-native systems where possible.

The prototype must prioritize reliability and demonstrability over photorealism.