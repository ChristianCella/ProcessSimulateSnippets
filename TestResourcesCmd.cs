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

    public class TestResourcesCmd : TxButtonCommand
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
                return "Resource command";
            }
        }

        double robot_time = 0.0; // Global variable

        // Method that is executed when pressing the button
        public override void Execute(object cmdParams)
        {

            double[] translation = new double[] { 200.0, 0.0, 0.0 };
            double[] rotation = new double[] { 0.0, 0.0, 0.0 };
            // double[] joint_values = new double[] { -0.06409666151040447, 0.26179096555220205, 0.5575963904686277, -0.003833670843588153, 0.813862761497515, -0.022435109845964803 };
            double[] joint_values = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
            bool invisible = false;
            double device_pos = 200;
            double offset = 200;

            // This class is defined in TxResources.cs
            TxResources item_resource = new TxResources();
            TxResources robot_resource = new TxResources();
            TxResources line_resource = new TxResources();
            //robot_resource.PlaceResource("GoFa12", translation, rotation);
            //robot_resource.SetTCP("GoFa12", "TCPF_Smart_gripper"); //TCPF_crate, TOOLFRAME
            //robot_resource.UnMountToolGripper("GoFa12", "Crate_gripper", "crate_tool_station");
            robot_resource.MountToolGripper("GoFa12", "Crate_gripper", "tool_holder_offset", "BASEFRAME_Crate_gripper", "TCPF_Crate_gripper");
            //robot_resource.UnMountToolGripper("GoFa12", "Smart_gripper", "tool_station_Smart_gripper");
            //robot_resource.MountToolGripper("GoFa12", "Smart_gripper", "tool_holder_offset", "BASEFRAME_Smart_gripper", "TCPF_Smart_gripper");
            //robot_resource.DisplayMountedTools("GoFa12");
            //robot_resource.ImposeRobotConfig("GoFa12", "fr6");
            //robot_resource.SetJointValue("GoFa12", joint_values);
            //item_resource.ChangeVisibility("Type_A_box_cover_left_1", invisible);
            //line_resource.CreateDevicePose("Line", device_pos, "Crate_outfeed");
            //robot_resource.ComputeJacobian();
            //robot_resource.PlaceResourceAccordingToFrame("Crate_3", "crate_low_on_slider_station");
            //TxDeviceOperation my_op = robot_resource.CreateDeviceOp("Line", "Op", "Crate_station");

            //TxContinuousRoboticOperation myop = robot_resource.PP_op("GoFa12", "Smart_gripper", "pick_box_A_1", "crate_2_place1", "test_op", offset);        
            //TxSnapshot txSnapshot = robot_resource.CreateSnap("Initial_conditions");
            //TxApplySnapshotParams snapParam = robot_resource.CreateSnapPar();
            //txSnapshot.Apply(snapParam);

            // Setup the simulation player to never ask for reset and never reset the operation
            /*
            TxSnapshot txSnapshot = robot_resource.CreateSnap("Initial_conditions");
            TxApplySnapshotParams snapParam = robot_resource.CreateSnapPar();
            TxSimulationPlayer player = TxApplication.ActiveDocument.SimulationPlayer;
            player.ResetToDefaultSetting();
            player.AskUserForReset(false);
            player.DoOnlyUnscheduledReset(true);
            string[] pickFrames = new string[] { "pick_box_A_1", "pick_box_A_2" };
            string[] placeFrames = new string[] { "crate_3_place1", "crate_3_place2" };
            double human_time = 0.0;
            //double robot_time = 0.0;

            for (int i = 0; i < 2; i++)
            {
                // 1. Create the operation
                TxContinuousRoboticOperation myop = robot_resource.PP_op(
                    "GoFa12", "Smart_gripper", pickFrames[i], placeFrames[i], "test_op_" + i, offset);

                // 2. Run the simulation
                TxApplication.ActiveDocument.CurrentOperation = myop;
                player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(robot_resource.player_TimeIntervalReached);
                player.Play();
                player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(robot_resource.player_TimeIntervalReached);           
                robot_time = robot_time + myop.Duration; // Update the robot time
                System.Diagnostics.Trace.WriteLine("Total time taken by the robot: " + robot_time);
            }

            // Create an empty operation and set it
            TxContinuousRoboticOperationCreationData data = new TxContinuousRoboticOperationCreationData("Empty_task");
            TxApplication.ActiveDocument.OperationRoot.CreateContinuousRoboticOperation(data);
            TxObjectList allOps = TxApplication.ActiveDocument.GetObjectsByName("Empty_task");
            TxContinuousRoboticOperation MyOp = allOps[0] as TxContinuousRoboticOperation;
            TxApplication.ActiveDocument.CurrentOperation = MyOp;

            player.ResetToDefaultSetting();
            txSnapshot.Apply(snapParam);
            */
        }
    }
}
