Autonomous Racing Framework

This Unity project implements a reinforcement learning (RL) pipeline for procedural track generation and autonomous car racing using Unity ML-Agents and Arcade Car Physics.

Overview

Track Generation: Procedurally create roads, walls, and checkpoints from splines via SplineTrackGenerator.

Vehicle Physics: Powered by the Arcade Car Physics asset (WheelVehicle.cs), wrapped by CarController for RL integration.

RL Agent: CarAgent uses Proximal Policy Optimization (PPO) to learn high-speed driving strategies.

Lap Tracking: LapTrackingSystem and LapTracker record lap times and checkpoint progress, with optional CSV output.

Prerequisites

Unity: 2023.3 LTS (or compatible Unity version)

ML-Agents: Unity Machine Learning Agents package (v2.x+)

Arcade Car Physics: Available from the Unity Asset Store

Alpine Terrain: Download the Alpine Terrain asset from the Asset Store for terrain and scenery meshes



Open in Unity

Launch Unity Hub and add the project folder.

Open the project with Unity.

Import Required Packages

ML-Agents: In Package Manager, install com.unity.ml-agents.

Arcade Car Physics: From Asset Store, import "Arcade Car Physics".

Alpine Terrain: From Asset Store, import "Alpine Terrain" (provides ground and environment models).

Scene Setup

Open Assets/Scenes/AI-Racing-Game.unity.

In the Hierarchy, configure the SplineTrackGenerator:

Assign a SplineContainer with your spline path.

Adjust resolution, road width, wall settings, and checkpoint count.

Ensure tags Road, Wall, Checkpoint, and StartFinish exist (auto-created on track generation).

Assign your car prefab to TrainingManager.carPrefab.

Agent Configuration

Select the CarAgent prefab:

Chose a mode manual, heuristic or ML-Agent
MLAgent:

Open Anaconda command prompt and write
 conda activate mlagents
then navigate to the parent folder of asset/
run mlagents-learn CarConfigs\car_config.yaml --run-id=[Test1]

to resume testing:
mlagents-learn CarConfigs\car_config.yaml --run-id=[Test1] --resume

to override and force a new training :
mlagents-learn CarConfigs\car_config.yaml --run-id=[Test1] --force

once you run the command in anaconda
you will have a few seconds to wait, when it says listening on port 5004 then press run in unity and your Ml agnet should start to train
