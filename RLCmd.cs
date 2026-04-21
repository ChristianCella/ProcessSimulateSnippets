using System;
using System.Windows.Forms;
using Tecnomatix.Engineering;

namespace DemoRL
{
    /// <summary>
    /// Button that appears in Process Simulate.
    /// Click it to start the RL server, then run the Python script.
    /// </summary>
    public class RLCmd : TxButtonCommand
    {
        private RLEnvironment _environment;

        public override string Category => "RL Demo";
        public override string Name => "Start RL Server";

        public override void Execute(object cmdParams)
        {
            // Close any previous server still running
            _environment?.Dispose();

            try
            {
                // ===================================================
                // CHANGE THIS to match your robot's name in the scene
                // ===================================================
                string robotName = "GoFa12";

                _environment = new RLEnvironment(robotName);
                TxMessageBox.Show(
                    "RL server started. Now run the Python script.",
                    "Server Status",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                TxMessageBox.Show(
                    $"Failed to start RL server:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
