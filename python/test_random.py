"""
Test script: fake RL with random actions for 3 episodes.
Respects the action mask sent by C#.

Usage:
  1. Click "Start RL Server" in Process Simulate
  2. Run: python test_random.py
"""

import numpy as np
from simple_robot_env import SimpleRobotEnv

ACTION_NAMES = {
    0: "Pick & Place Type A",
    1: "Pick & Place Type B",
    2: "Insert box into crate",
    3: "Change to Smart gripper",
    4: "Change to Crate gripper",
    5: "Put crates on the slider",
    6: "Action 6 - your description here" 
}

NUM_EPISODES = 3

env = SimpleRobotEnv()

for episode in range(NUM_EPISODES):
    print(f"\n{'='*50}")
    print(f"  EPISODE {episode + 1} / {NUM_EPISODES}")
    print(f"{'='*50}")

    obs, info = env.reset()
    mask = info["action_mask"]
    print(f"Initial obs: {obs}")
    print(f"Action mask: {mask}")

    step = 0
    total_reward = 0.0

    while True:
        # Choose a random VALID action
        valid_actions = np.where(mask)[0]
        if len(valid_actions) == 0:
            print("No valid actions available. Ending episode.")
            break

        action = np.random.choice(valid_actions)
        print(f"\n  Step {step + 1}: choosing action {action} ({ACTION_NAMES[action]})")

        obs, reward, terminated, truncated, info = env.step(action)
        mask = info["action_mask"]
        total_reward += reward
        step += 1

        print(f"    obs={obs}, reward={reward:.3f}, terminated={terminated}, truncated={truncated}")
        print(f"    action mask: {mask}")

        if terminated or truncated:
            reason = "TERMINATED" if terminated else "TRUNCATED"
            print(f"\n  Episode ended ({reason}) after {step} steps. Total reward: {total_reward:.3f}")
            break

env.close()
print(f"\n{'='*50}")
print("All episodes completed.")
print(f"{'='*50}")
