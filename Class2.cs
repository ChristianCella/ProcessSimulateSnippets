using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Tecnomatix.Engineering;

namespace DemoRL
{
    /// <summary>
    /// Minimal RL environment that moves a robot along X.
    /// 
    /// Protocol (JSON over TCP):
    ///   Python sends:  {"Command": "Reset"}
    ///                  {"Command": "Step", "ActionId": 0}
    ///                  {"Command": "Close"}
    ///   C# responds:   for Reset  -> observation dict
    ///                  for Step   -> step result dict
    ///                  for Close  -> ack
    ///
    /// Actions:
    ///   0 = move robot +50mm along X
    ///   1 = move robot -50mm along X
    ///
    /// Observation: [robot_x_normalized]
    ///   robot_x_normalized = (current_x - initial_x) / max_range
    ///
    /// Reward: negative distance from a target X offset (e.g. +200mm from start)
    ///
    /// Done: true when the robot is within 10mm of the target
    /// </summary>
    public class SimpleRLEnvironment : IDisposable
    {
        // === CONFIGURATION ===
        private const int PORT = 8580;
        private const double STEP_SIZE = 50.0;       // mm per action
        private const double TARGET_OFFSET = 200.0;  // target is 200mm in +X from start
        private const double DONE_THRESHOLD = 10.0;  // close enough to target
        private const double MAX_RANGE = 500.0;      // for normalization
        private const int MAX_STEPS = 50;             // truncate after this many steps

        // === SCENE OBJECTS ===
        private readonly TxRobot robot;
        private readonly TxTransformation initialLocation;
        private readonly double targetX;

        // === COMMUNICATION ===
        private TcpListener server;
        private TcpClient client;
        private NetworkStream stream;
        private Thread listenThread;
        private volatile bool running = true;

        // === EPISODE STATE ===
        private int stepCount;

        public SimpleRLEnvironment(string robotName)
        {
            // 1. Find the robot in the scene
            var objects = TxApplication.ActiveDocument.GetObjectsByName(robotName);
            if (objects.Count == 0)
                throw new Exception($"Robot '{robotName}' not found in the scene.");
            robot = objects[0] as TxRobot;
            if (robot == null)
                throw new Exception($"'{robotName}' exists but is not a TxRobot.");

            // 2. Store initial location and compute target
            initialLocation = new TxTransformation(robot.AbsoluteLocation);
            targetX = initialLocation.Translation.X + TARGET_OFFSET;

            // 3. Start TCP server on a background thread
            listenThread = new Thread(ListenLoop);
            listenThread.IsBackground = true;
            listenThread.Start();
        }

        // =====================================================
        //  TCP SERVER LOOP
        // =====================================================

        private void ListenLoop()
        {
            try
            {
                server = new TcpListener(IPAddress.Parse("127.0.0.1"), PORT);
                server.Start();

                // Wait for Python to connect
                client = server.AcceptTcpClient();
                stream = client.GetStream();

                // Main message loop
                while (running)
                {
                    string json = ReceiveJson();
                    if (json == null) break; // connection closed

                    var request = JsonConvert.DeserializeObject<ClientRequest>(json);

                    switch (request.Command)
                    {
                        case "Reset":
                            HandleReset();
                            break;

                        case "Step":
                            HandleStep(request.ActionId ?? 0);
                            break;

                        case "Close":
                            SendJson(new { Message = "Closing" });
                            running = false;
                            break;

                        default:
                            SendJson(new { Error = $"Unknown command: {request.Command}" });
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Server stopped or connection lost — this is expected on Close/Dispose
                System.Diagnostics.Debug.WriteLine($"Server loop ended: {ex.Message}");
            }
        }

        // =====================================================
        //  RESET
        // =====================================================

        private void HandleReset()
        {
            // Move robot back to initial position
            robot.AbsoluteLocation = new TxTransformation(initialLocation);
            TxApplication.RefreshDisplay();
            stepCount = 0;

            // Build and send observation
            var obs = BuildObservation();
            SendJson(obs);
        }

        // =====================================================
        //  STEP
        // =====================================================

        private void HandleStep(int actionId)
        {
            // 1. Apply action
            double dx = (actionId == 0) ? STEP_SIZE : -STEP_SIZE;

            TxTransformation current = robot.AbsoluteLocation;
            TxTransformation move = new TxTransformation(
                new TxVector(dx, 0, 0),
                TxTransformation.TxTransformationType.Translate);
            robot.AbsoluteLocation = current * move;
            TxApplication.RefreshDisplay();

            stepCount++;

            // 2. Compute reward and done
            double currentX = robot.AbsoluteLocation.Translation.X;
            double distanceToTarget = Math.Abs(currentX - targetX);
            double reward = -distanceToTarget / MAX_RANGE;  // normalized negative distance

            bool terminated = distanceToTarget < DONE_THRESHOLD;
            bool truncated = stepCount >= MAX_STEPS;

            if (terminated)
                reward = 10.0; // bonus for reaching the target

            // 3. Build and send response
            var obs = BuildObservation();
            var result = new
            {
                Observation = obs,
                Reward = reward,
                Terminated = terminated,
                Truncated = truncated
            };
            SendJson(result);
        }

        // =====================================================
        //  OBSERVATION
        // =====================================================

        private object BuildObservation()
        {
            double currentX = robot.AbsoluteLocation.Translation.X;
            double normalized = (currentX - initialLocation.Translation.X) / MAX_RANGE;

            return new
            {
                State = new double[] { normalized }
            };
        }

        // =====================================================
        //  TCP HELPERS
        // =====================================================

        private string ReceiveJson()
        {
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) return null;
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        private void SendJson(object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
        }

        // =====================================================
        //  CLEANUP
        // =====================================================

        public void Dispose()
        {
            running = false;
            stream?.Close();
            client?.Close();
            server?.Stop();
        }
    }

    // =====================================================
    //  DATA MODEL for incoming messages
    // =====================================================

    public class ClientRequest
    {
        public string Command { get; set; }
        public int? ActionId { get; set; }
    }
}
