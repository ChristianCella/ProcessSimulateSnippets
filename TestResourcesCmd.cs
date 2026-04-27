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

        // Method that is executed when pressing the button
        public override void Execute(object cmdParams)
        {

            double[] translation = new double[] { 200.0, 0.0, 0.0 };
            double[] rotation = new double[] { 0.0, 0.0, 0.0 };
            // double[] joint_values = new double[] { -0.06409666151040447, 0.26179096555220205, 0.5575963904686277, -0.003833670843588153, 0.813862761497515, -0.022435109845964803 };
            double[] joint_values = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
            bool invisible = false;
            double device_pos = 1300;
            double offset = 200;

            // This class is defined in TxResources.cs
            TxResources item_resource = new TxResources();
            TxResources robot_resource = new TxResources();
            TxResources line_resource = new TxResources();
            //robot_resource.PlaceResource("GoFa12", translation, rotation);
            //robot_resource.SetTCP("GoFa12", "TCPF_smart"); //TCPF_crate, TOOLFRAME
            //robot_resource.UnMountToolGripper("GoFa12", "Crate_gripper", "crate_tool_station");
            //robot_resource.MountToolGripper("GoFa12", "Crate_gripper", "tool_holder_offset", "BASEFRAME_crate", "TCPF_crate");
            //robot_resource.UnMountToolGripper("GoFa12", "Smart_gripper", "smart_tool_station");
            //robot_resource.MountToolGripper("GoFa12", "Smart_gripper", "tool_holder_offset", "BASEFRAME_smart", "TCPF_smart");
            //robot_resource.DisplayMountedTools("GoFa12");
            //robot_resource.ImposeRobotConfig("GoFa12", "fr6");
            //robot_resource.SetJointValue("GoFa12", joint_values);
            //item_resource.ChangeVisibility("Type_A_box_cover_left_1", invisible);
            //line_resource.CreateDevicePose("Line", device_pos, "TestPose");
            //robot_resource.ComputeJacobian();
            //robot_resource.PlaceResourceAccordingToFrame("Type_A_box_right_2", "Box_A_4");
            TxContinuousRoboticOperation myop = robot_resource.PP_op("GoFa12", "Smart_gripper", "Pick_A_1", "Place_A_1", "test_op", offset);

        }
    }
}
