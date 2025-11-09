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
            double[] joint_values = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
            bool invisible = false;
            double device_pos = 1400; 

            // This class is defined in TxResources.cs
            TxResources item_resource = new TxResources();
            TxResources robot_resource = new TxResources();
            TxResources line_resource = new TxResources();
            //robot_resource.PlaceResource("GoFa12", translation, rotation);
            //robot_resource.SetTCP("GoFa12", "TCPF_crate"); //TCPF_crate, TOOLFRAME
            //robot_resource.UnMountToolGripper("GoFa12", "Collaborative_gripper", "collaborative_tool_station");
            //robot_resource.MountToolGripper("GoFa12", "Collaborative_gripper", "tool_holder_offset", "BASEFRAME_collaborative");
            //robot_resource.DisplayMountedTools("GoFa12");
            //robot_resource.ImposeRobotConfig("GoFa12", "fr6");
            //robot_resource.SetJointValue("GoFa12", joint_values);
            //item_resource.ChangeVisibility("YAOSC_cube", invisible);
            // line_resource.CreateDevicePose("Line", device_pos, "TestPose");
            //robot_resource.ComputeJacobian();
            robot_resource.PlaceResourceAccordingToFrame("Crate_3", "crate_low_on_slider_station");

        }
    }
}
