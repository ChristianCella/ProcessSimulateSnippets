using EngineeringInternalExtension;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Tecnomatix.Engineering;

namespace ProcessSimulateSnippets
{
    /// <summary>
    /// Receives messages from Python, routes them to the environment,
    /// and sends back the response. This is called on the UI thread
    /// by TxTcpCommunicationManagerEx automatically.
    /// </summary>
    public class RequestHandler : ITxRequestHandler
    {
        private readonly RLEnvironment _environment;
        private readonly CommunicationManager _communicator;

        public RequestHandler(RLEnvironment env, CommunicationManager communicator)
        {
            _environment = env;
            _communicator = communicator;
        }

        public void HandleRequestMessage(TxRequestMessageArgs arg)
        {
            string jsonString = Encoding.UTF8.GetString(arg.Data);
            ClientRequest request = JsonConvert.DeserializeObject<ClientRequest>(jsonString);

            try
            {
                switch (request.Command)
                {
                    case "Reset":
                        ObservationPacket initialObs = _environment.Reset();
                        _communicator.SendMessage(initialObs);
                        break;

                    case "Step":
                        if (request.ActionId.HasValue)
                        {
                            StepResult result = _environment.Step(request.ActionId.Value);
                            _communicator.SendMessage(result);
                        }
                        else
                        {
                            _communicator.SendMessage(new Dictionary<string, string>
                            {
                                { "Error", "Step command received without ActionId." }
                            });
                        }
                        break;

                    case "Close":
                        _communicator.SendMessage(new Dictionary<string, string>
                        {
                            { "Message", "Environment is closing." }
                        });
                        _environment.Dispose();
                        break;

                    default:
                        _communicator.SendMessage(new Dictionary<string, string>
                        {
                            { "Error", $"Unknown command: {request.Command}" }
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _communicator.SendMessage(new { Error = ex.Message });
            }
        }
    }
}
