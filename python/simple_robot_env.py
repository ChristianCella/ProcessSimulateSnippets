"""
Gymnasium environment that connects to the C# RL server in Process Simulate.
Start the C# server first (click the button), then run this or the training script.
"""

import gymnasium as gym
import numpy as np
import socket
import json


class SimpleRobotEnv(gym.Env):
    """
    Actions:  0 = move +X,  1 = move -X
    Observation: [robot_x_normalized]  (1 float)
    Reward: negative distance to target (normalized), +10 bonus when reached
    """

    def __init__(self, host="127.0.0.1", port=8580):
        super().__init__()

        self.host = host
        self.port = port
        self.sock = None

        self.action_space = gym.spaces.Discrete(2)
        self.observation_space = gym.spaces.Box(
            low=-np.inf, high=np.inf, shape=(1,), dtype=np.float32
        )

        self._connect()

    def reset(self, seed=None, options=None):
        super().reset(seed=seed)
        response = self._send_and_receive({"Command": "Reset"})
        obs = np.array(response["State"], dtype=np.float32)
        return obs, {}

    def step(self, action):
        response = self._send_and_receive({
            "Command": "Step",
            "ActionId": int(action)
        })

        obs = np.array(response["Observation"]["State"], dtype=np.float32)
        reward = float(response["Reward"])
        terminated = bool(response["Terminated"])
        truncated = bool(response["Truncated"])

        return obs, reward, terminated, truncated, {}

    def close(self):
        if self.sock:
            try:
                self._send_and_receive({"Command": "Close"})
            except Exception:
                pass
            finally:
                self.sock.close()
                self.sock = None

    # ---- Internal helpers ----

    def _connect(self):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.connect((self.host, self.port))
        print(f"Connected to C# server at {self.host}:{self.port}")

    def _send_and_receive(self, data):
        msg = json.dumps(data).encode("utf-8")
        self.sock.sendall(msg)
        response = self.sock.recv(8192)
        return json.loads(response.decode("utf-8"))
