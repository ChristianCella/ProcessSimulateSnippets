using System;
using System.Windows.Forms;
using Tecnomatix.Engineering;

namespace ProcessSimulateSnippets
{
    public class RLCmd : TxButtonCommand
    {
        private RLEnvironment _environment;

        public override string Category => "RL Demo";
        public override string Name => "Start RL Server";

        public override void Execute(object cmdParams)
        {
            _environment?.Dispose();

            try
            {
                string robotName = "GoFa12";
                string lineName = "Line";
                string humanName = "Jack";
                _environment = new RLEnvironment(robotName, lineName, humanName);
                TxMessageBox.Show(
                    "RL server started. Now run the Python script.",
                    "Server Status",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                TxMessageBox.Show(
                    $"Failed to start RL server:\n{ex}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
