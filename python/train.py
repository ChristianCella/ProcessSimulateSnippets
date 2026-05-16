"""
Training script: trains a MaskablePPO agent in the collaborative
logistics environment.

Usage:
  1. Click "Start RL Server" in Process Simulate
  2. Run: python train.py
  3. Watch TensorBoard: tensorboard --logdir ./logs/ => After you run this in a terminal,
      open http://localhost:6006/ in your browser to see the training curves.
"""

from sb3_contrib import MaskablePPO
from sb3_contrib.common.maskable.evaluation import evaluate_policy
from stable_baselines3.common.monitor import Monitor
from simple_robot_env import SimpleRobotEnv
import os

# --- CONFIG ---
TOTAL_TIMESTEPS = 10000
LOG_DIR = "./logs/"
MODEL_SAVE_PATH = "maskable_ppo_robot"
os.makedirs(LOG_DIR, exist_ok=True)

# 1. Create environment
env = SimpleRobotEnv()
env = Monitor(env)

# 2. Create MaskablePPO agent
model = MaskablePPO(
    "MlpPolicy",
    env,
    verbose=1,
    learning_rate=2.5e-4,
    n_steps=512,
    batch_size=64,
    n_epochs=10,
    gamma=0.995,
    clip_range=0.2,
    ent_coef=0.02,
    tensorboard_log=LOG_DIR,
)

# 3. Train
print(f"Starting training for {TOTAL_TIMESTEPS} steps...")
model.learn(total_timesteps=TOTAL_TIMESTEPS)

# 4. Save
model.save(MODEL_SAVE_PATH)
print(f"Training complete. Model saved to {MODEL_SAVE_PATH}.zip")

env.close()