using EngineeringInternalExtension;
using Newtonsoft.Json;
using System;
using Tecnomatix.Engineering;

namespace ProcessSimulateSnippets
{
    /// <summary>
    /// Wraps TxTcpCommunicationManagerEx (Tecnomatix SDK).
    /// This class handles TCP communication AND threading:
    /// when a message arrives, HandleRequestMessage is called 
    /// on the main UI thread automatically.
    /// </summary>
    public class CommunicationManager : IDisposable
    {
        public TxTcpCommunicationManagerEx Communicator { get; set; }
        private readonly string _portName;

        public CommunicationManager(string portName)
        {
            _portName = portName;
        }

        public void StartListening(ITxRequestHandler handler)
        {
            Communicator = new TxTcpCommunicationManagerEx(new TxJsonSerializer(), handler);
            try
            {
                Communicator.Init(_portName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to initialize TCP communication on port '{_portName}': {ex.Message}");
            }
        }

        public void SendMessage(object message)
        {
            if (Communicator == null)
                throw new InvalidOperationException("Cannot send message: Communicator is not initialized.");
            Communicator.SendMessage(message);
        }

        public void Dispose()
        {
            Communicator?.Close();
        }
    }
}
