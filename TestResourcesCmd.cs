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

            double[] translation = new double[] { 500.0, 0.0, 0.0 };
            double[] rotation = new double[] { 0.0, 0.0, 0.0 };
            double[] joint_values = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };

            // This class is defined in TxResources.cs
            TxResources ur5_resource = new TxResources();
            ur5_resource.PlaceResource("UR5", translation, rotation);
            ur5_resource.SetJointValue("UR5", joint_values);

        }
    }
}
