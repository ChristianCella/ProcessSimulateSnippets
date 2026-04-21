"""
Test script: no RL, just sends a hardcoded sequence of actions.
Use this first to verify the connection works before training.

Usage:
  1. Click "Start RL Server" in Process Simulate
  2. Run: python test_connection.py
"""

from simple_robot_env import SimpleRobotEnv

env = SimpleRobotEnv()

# Reset the environment
obs, info = env.reset()
print(f"Initial observation: {obs}")

# Send 10 actions: all move +X (action 0)
for i in range(10):
    obs, reward, terminated, truncated, info = env.step(0)
    print(f"Step {i+1}: obs={obs[0]:.3f}, reward={reward:.3f}, done={terminated}, truncated={truncated}")

    if terminated or truncated:
        print("Episode ended!")
        obs, info = env.reset()
        print(f"Reset -> obs: {obs}")

env.close()
print("Done.")
