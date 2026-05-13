"""
Gymnasium environment that connects to the C# RL server in Process Simulate.
Uses the same protocol as Lorenzo's TxTcpCommunicationManagerEx.
"""

import gymnasium as gym
import numpy as np
import socket
import json


class SimpleRobotEnv(gym.Env):
    """
    Actions:
        0 = Pick and place type A
        1 = Pick and place type B
        2 = Insert small box into crate (only valid after both 0 and 1)

    Observation: [step_normalized, action0_done, action1_done, total_time_normalized]
    ActionMask: [1/0, 1/0, 1/0] for each action
    """

    def __init__(self, host="127.0.0.1", port=8580):
        super().__init__()

        self.host = host
        self.port = port
        self.sock = None

        self.action_space = gym.spaces.Discrete(13)
        self.observation_space = gym.spaces.Box(
            low=-np.inf, high=np.inf, shape=(29,), dtype=np.float32
        )

        self.valid_action_mask = np.ones(self.action_space.n, dtype=bool)
        self._connect()

    def reset(self, seed=None, options=None):
        super().reset(seed=seed)
        response = self._send_and_receive({"Command": "Reset"})

        observation = np.array(response["State"], dtype=np.float32)
        self.valid_action_mask = np.array(response["ActionMask"]) == 1
        info = {"action_mask": self.valid_action_mask}

        return observation, info

    def step(self, action):
        response = self._send_and_receive({
            "Command": "Step",
            "ActionId": int(action)
        })

        observation_and_mask = response["Observation"]
        observation = np.array(observation_and_mask["State"], dtype=np.float32)
        self.valid_action_mask = np.array(observation_and_mask["ActionMask"]) == 1

        reward = float(response["Reward"])
        terminated = bool(response["Terminated"])
        truncated = bool(response["Truncated"])

        info = {"action_mask": self.valid_action_mask}

        return observation, reward, terminated, truncated, info

    def action_masks(self) -> np.ndarray:
        """Called by MaskablePPO to know which actions are valid."""
        return self.valid_action_mask

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
        response = self.sock.recv(65536)
        return json.loads(response.decode("utf-8"))
