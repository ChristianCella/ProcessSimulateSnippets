using System;
using System.IO;
using System.Windows.Forms;
using Tecnomatix.Engineering;
using Tecnomatix.Engineering.Plc;
using System.Collections;

namespace ProcessSimulateSnippets
{
    public class TxResources
    {
        // ------------------------------- From snippets to methods
        public void PlaceResource(string resource_name, double[] translation, double[] rotation)
        {
            // Get the instance
            var resource = GetLocatableResource(resource_name);

            // Rotate of an angle around the z axis		
            TxTransformation Rotation = new TxTransformation(new TxVector(rotation[0], rotation[1], rotation[2]),
            TxTransformation.TxRotationType.RPY_XYZ);
            resource.AbsoluteLocation = Rotation;

            // Move the base of a certain quantity		
            var position = new TxTransformation(resource.LocationRelativeToWorkingFrame);
            position.Translation = new TxVector(translation[0], translation[1], translation[2]);
            resource.LocationRelativeToWorkingFrame = position;
        }

        public void SetJointValue(string resource_name, double[] joint_values)
        {
            var logic_resource = GetLogicResource(resource_name);
            TxObjectList drivingJoints = (logic_resource as ITxDevice).DrivingJoints;
            TxJoint j1 = drivingJoints[0] as TxJoint;
            TxJoint j2 = drivingJoints[1] as TxJoint;
            TxJoint j3 = drivingJoints[2] as TxJoint;
            TxJoint j4 = drivingJoints[3] as TxJoint;
            TxJoint j5 = drivingJoints[4] as TxJoint;
            TxJoint j6 = drivingJoints[5] as TxJoint;

            // print the name of the selected joint      
            //TxMessageBox.Show(string.Format("{0:F3} radians", j1.CurrentValue), "Name", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Set the joint values
            j1.CurrentValue = joint_values[0];
            j2.CurrentValue = joint_values[1];
            j3.CurrentValue = joint_values[2];
            j4.CurrentValue = joint_values[3];
            j5.CurrentValue = joint_values[4];
            j6.CurrentValue = joint_values[5];
            TxApplication.RefreshDisplay();
        }

        // Change the position of the frame called TCPF to a new one (i.e. you have a tool)
        public void SetTCP(string robot_name, string frame_name)
        {
            // Get the robot
            var robot = GetLocatableResource(robot_name);
            var rob = robot as TxRobot; // Needed to access the method 'TCPF'

            // Create an instance of the frame to which you want to overlap TOOLFRAME
            TxObjectList selectedObjects = TxApplication.ActiveSelection.GetItems();
            selectedObjects = TxApplication.ActiveDocument.GetObjectsByName(frame_name); // "TOOLFRAME" to restore the pose of TCPF
            TxFrame fram = selectedObjects[0] as TxFrame;

            // Impose the new position to TCPF
            rob.TCPF.AbsoluteLocation = fram.AbsoluteLocation;
            TxApplication.RefreshDisplay();
        }

        // Make an object visible or invisible
        public void ChangeVisibility(string resource_name, bool invisible)
        {
            var resource = GetLocatableResource(resource_name);
            TxComponent res_component = resource as TxComponent;

            // Set the visibiliy of the object
            if (invisible == true)
            {
                res_component.Blank();
            }
            else 
            {
                res_component.Display();
            }

            // Refresh the display
            TxApplication.RefreshDisplay();
        }

        // ------------------------------- Auxiliary methods

        public ITxLocatableObject GetLocatableResource(string resource_name)
        {
            TxObjectList selectedObjects = TxApplication.ActiveSelection.GetItems();
            selectedObjects = TxApplication.ActiveDocument.GetObjectsByName(resource_name);
            return selectedObjects[0] as ITxLocatableObject;

        }

        public ITxPlcLogicResource GetLogicResource(string resource_name)
        {
            TxObjectList selectedObjects = TxApplication.ActiveSelection.GetItems();
            selectedObjects = TxApplication.ActiveDocument.GetObjectsByName(resource_name);
            return selectedObjects[0] as ITxPlcLogicResource;
        }

    }
}
