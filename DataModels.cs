using System.Collections.Generic;

namespace ProcessSimulateSnippets
{
    /// <summary>
    /// Incoming message from Python.
    /// </summary>
    public class ClientRequest
    {
        public string Command { get; set; }
        public int? ActionId { get; set; }
    }

    /// <summary>
    /// Observation sent to Python (on Reset and inside StepResult).
    /// </summary>
    public class ObservationPacket
    {
        public List<double> State { get; set; }
        public List<int> ActionMask { get; set; }
    }

    /// <summary>
    /// Full step response sent to Python.
    /// </summary>
    public class StepResult
    {
        public ObservationPacket Observation { get; set; }
        public double Reward { get; set; }
        public bool Terminated { get; set; }
        public bool Truncated { get; set; }

        public StepResult() { }

        public StepResult(ObservationPacket observation, double reward, bool terminated, bool truncated)
        {
            Observation = observation;
            Reward = reward;
            Terminated = terminated;
            Truncated = truncated;
        }
    }
}
