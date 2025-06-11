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

        public void PlaceResource(string resource_name, double[] translation, double[] rotation)
        {
            // Get the instance
            var robot = GetLocatableResource(resource_name);

            // Rotate of an angle around the z axis		
            TxTransformation Rotation = new TxTransformation(new TxVector(rotation[0], rotation[1], rotation[2]),
            TxTransformation.TxRotationType.RPY_XYZ);
            robot.AbsoluteLocation = Rotation;

            // Move the base of a certain quantity		
            var position = new TxTransformation(robot.LocationRelativeToWorkingFrame);
            position.Translation = new TxVector(translation[0], translation[1], translation[2]);
            robot.LocationRelativeToWorkingFrame = position;
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
            TxMessageBox.Show(string.Format("{0:F3} radians", j1.CurrentValue), "Name", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Set the joint values
            j1.CurrentValue = joint_values[0];
            j2.CurrentValue = joint_values[1];
            j3.CurrentValue = joint_values[2];
            j4.CurrentValue = joint_values[3];
            j5.CurrentValue = joint_values[4];
            j6.CurrentValue = joint_values[5];
            TxApplication.RefreshDisplay();
        }

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
