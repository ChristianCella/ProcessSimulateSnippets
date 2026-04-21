using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Tecnomatix.Engineering;
using System.Collections.Generic;
using Tecnomatix.Engineering.Olp;
using System.Linq;

namespace ProcessSimulateSnippets
{
    public class RLCmd : TxButtonCommand
    {
        public override string Category
        {
            get
            {
                return "Resource";
            }
        }

        public override string Name
        {
            get
            {
                return "Start RL";
            }
        }

        public override void Execute(object cmdParams)
        {

            TxResources robot_resource = new TxResources();

            // Run the simulation with events
            TxSimulationPlayer player = TxApplication.ActiveDocument.SimulationPlayer;
            TxSimulationPlayerSource source = new TxSimulationPlayerSource();
            source = TxSimulationPlayerSource.TaskSimulationPlayer;
            double dt = 0.10;
            double time = 0.0;
            player.Rewind();
            //player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(robot_resource.player_TimeIntervalReached);
            while (time < 8.0)
            {
                player.JumpSimulationToTime(time, true, source); // step method
                time = time + dt;

                if (Math.Abs(time - 1.0) < 1e-6)
                {
                    robot_resource.AddToCompound("complete_op", "Human_task_2", 3.0);
                    TxApplication.RefreshDisplay();
                }

                //TxApplication.RefreshDisplay();

                //TxMessageBox.Show(string.Format(time.ToString()), "Current time instant", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
            //player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(robot_resource.player_TimeIntervalReached);

        }
    }
}
