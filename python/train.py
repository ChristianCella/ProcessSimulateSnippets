"""
Training script: trains a PPO agent to move the robot to the target.

Usage:
  1. Click "Start RL Server" in Process Simulate
  2. Run: python train.py
  3. Watch TensorBoard: tensorboard --logdir ./logs/

Requirements:
  pip install gymnasium stable-baselines3
"""

from stable_baselines3 import PPO
from stable_baselines3.common.monitor import Monitor
from simple_robot_env import SimpleRobotEnv
import os

# --- CONFIG ---
TOTAL_TIMESTEPS = 10_000
LOG_DIR = "./logs/"
os.makedirs(LOG_DIR, exist_ok=True)

# 1. Create environment
env = SimpleRobotEnv()
env = Monitor(env)

# 2. Create PPO agent
model = PPO(
    "MlpPolicy",
    env,
    verbose=1,
    learning_rate=3e-4,
    n_steps=128,
    batch_size=64,
    n_epochs=10,
    gamma=0.99,
    tensorboard_log=LOG_DIR,
)

# 3. Train
print(f"Starting training for {TOTAL_TIMESTEPS} steps...")
model.learn(total_timesteps=TOTAL_TIMESTEPS)

# 4. Save
model.save("ppo_simple_robot")
print("Training complete. Model saved to ppo_simple_robot.zip")

env.close()
