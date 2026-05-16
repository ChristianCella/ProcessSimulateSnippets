from sb3_contrib import MaskablePPO
from stable_baselines3.common.monitor import Monitor
from simple_robot_env import SimpleRobotEnv
import matplotlib.pyplot as plt
import numpy as np

# Load trained model
env = SimpleRobotEnv()
env = Monitor(env)
model = MaskablePPO.load("maskable_ppo_robot", env=env)

# Run evaluation episodes
NUM_EVAL_EPISODES = 10 # Run and evaluate 20 episodes in TPS
episode_rewards = []
episode_lengths = []
action_counts = np.zeros(14)

for ep in range(NUM_EVAL_EPISODES):
    obs, info = env.reset()
    mask = info["action_mask"]
    total_reward = 0.0
    steps = 0
    
    while True:
        action, _ = model.predict(obs, action_masks=mask, deterministic=True)
        obs, reward, terminated, truncated, info = env.step(action)
        mask = info["action_mask"]
        total_reward += reward
        steps += 1
        action_counts[action] += 1
        
        if terminated or truncated:
            break
    
    episode_rewards.append(total_reward)
    episode_lengths.append(steps)
    print(f"Episode {ep+1}: reward={total_reward:.2f}, steps={steps}")

env.close()

# Plot 1: Episode rewards
plt.figure(figsize=(10, 4))
plt.plot(episode_rewards, marker='o')
plt.axhline(np.mean(episode_rewards), color='red', linestyle='--', label=f'Mean={np.mean(episode_rewards):.2f}')
plt.xlabel("Episode")
plt.ylabel("Total Reward")
plt.title("Evaluation Episode Rewards")
plt.legend()
plt.tight_layout()
plt.savefig("eval_rewards.png")
plt.show()

# Plot 2: Episode lengths
plt.figure(figsize=(10, 4))
plt.plot(episode_lengths, marker='o', color='orange')
plt.axhline(np.mean(episode_lengths), color='red', linestyle='--', label=f'Mean={np.mean(episode_lengths):.1f}')
plt.xlabel("Episode")
plt.ylabel("Steps")
plt.title("Evaluation Episode Lengths")
plt.legend()
plt.tight_layout()
plt.savefig("eval_lengths.png")
plt.show()

# Plot 3: Action frequency
ACTION_NAMES = [
    "Fill A1", "Fill B1", "Box A1->Crate3",
    "->Smart", "->Crate", "Load Crate",
    "Remove Crate3", "Box B1->Crate2", "Remove Crate2",
    "Fill A2", "Box A2->Crate3", "Fill B2",
    "Box B2->Crate2", "Wait"
]
plt.figure(figsize=(12, 4))
plt.bar(ACTION_NAMES, action_counts, color='steelblue')
plt.xlabel("Action")
plt.ylabel("Count")
plt.title("Action Frequency During Evaluation")
plt.xticks(rotation=45, ha='right')
plt.tight_layout()
plt.savefig("eval_actions.png")
plt.show()

print(f"\nMean reward: {np.mean(episode_rewards):.2f} +/- {np.std(episode_rewards):.2f}")
print(f"Mean length: {np.mean(episode_lengths):.1f} +/- {np.std(episode_lengths):.1f}")
print(f"Completion rate: {sum(1 for l in episode_lengths if l < 60) / NUM_EVAL_EPISODES:.1%}")