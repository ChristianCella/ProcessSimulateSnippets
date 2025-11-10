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

            // Linear translations	
            var position = new TxTransformation(resource.LocationRelativeToWorkingFrame);
            position.Translation = new TxVector(translation[0], translation[1], translation[2]);
            resource.LocationRelativeToWorkingFrame = position;

            TxApplication.RefreshDisplay();
        }

        public void PlaceResourceAccordingToFrame(string resource_name, string frame_name)
        {
            // Move the tool to the station 
            var frame_var = GetLocatableResource(frame_name);
            TxTransformation frame_pose = frame_var.AbsoluteLocation;
            double[] translation = new double[] { frame_pose.Translation.X, frame_pose.Translation.Y, frame_pose.Translation.Z };
            double[] rotation = new double[] { frame_pose.RotationRPY_XYZ.X, frame_pose.RotationRPY_XYZ.Y, frame_pose.RotationRPY_XYZ.Z };

            PlaceResource(resource_name, translation, rotation);
        }

        public void MountToolGripper(string robot_name, string tool_name, string flange_frame, string tool_frame, string tcp_frame)
        {
            // Get the robot
            var robot = GetLocatableResource(robot_name);
            var rob = robot as TxRobot; // Needed to access the method 'TCPF'

            // Get the tool
            var tool = GetLocatableResource(tool_name);

            // Get the frame on the robot flange
            var flange = GetLocatableResource(flange_frame);
            TxTransformation flange_pose = flange.AbsoluteLocation;

            // Get the frame on the tool
            var tool_target = GetLocatableResource(tool_frame);
            TxTransformation tool_pose = tool_target.AbsoluteLocation;

            // Mount the tool
            rob.MountTool(tool, flange_pose, tool_pose);
            TxApplication.RefreshDisplay();

            // Shift the frame
            SetTCP(robot_name, tcp_frame);
            TxApplication.RefreshDisplay();

        }

        public void UnMountToolGripper(string robot_name, string tool_name, string station_name)
        {
            // Get the robot
            var robot = GetLocatableResource(robot_name);
            var rob = robot as TxRobot; // Needed to access the method 'TCPF'

            // Get the tool
            var tool = GetLocatableResource(tool_name);

            rob.UnmountTool(tool);
            TxApplication.RefreshDisplay();

            // Move the tool to the station 
            PlaceResourceAccordingToFrame(tool_name, station_name);

            // Put TCPF back in the robot flange
            SetTCP(robot_name, "TOOLFRAME");
            TxApplication.RefreshDisplay();
        }

        public void DisplayMountedTools(string robot_name)
        {
            // Get the robot
            var robot = GetLocatableResource(robot_name);
            var rob = robot as TxRobot; // Needed to access the method 'TCPF'

            TxObjectList mounted_tools = rob.MountedTools;

            // Build message with all mounted tool names
            string message = "Mounted Tools:\n";
            for (int i = 0; i < mounted_tools.Count; i++)
            {
                var tool = mounted_tools[i];
                message += string.Format("{0}. {1}\n", i + 1, tool.Name);
            }

            TxMessageBox.Show(message, "Mounted Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        // Make TCPF coincident with a specific frame (and move the robot accordingly)
        public void ImposeRobotConfig(string robot_name, string frame_name)
        {
            // Get the target frame (i.e. where the screw is)
            TxFrame target_frame = TxApplication.ActiveDocument.GetObjectsByName(frame_name)[0] as TxFrame;

            // Define a variable for the inverse kinematics
            TxRobotInverseData inv = new TxRobotInverseData(target_frame.AbsoluteLocation);

            // Get the robot
            var robot = GetLocatableResource(robot_name);
            var rob = robot as TxRobot; // Needed to access the method 'TCPF'

            // Compute the inverse kinematics
            bool proceed = rob.DoesInverseExist(inv);
            if (proceed == true)
            {
                var poses = rob.CalcInverseSolutions(inv);

                // Set a certain pose (i.e. the first one)
                var poseData = poses[0] as TxPoseData;

                // Impose a specific configuration to the robot		
                rob.CurrentPose = poseData;

                // Refresh the display		
                TxApplication.RefreshDisplay();
            }
            else
            {
                TxMessageBox.Show("Impossible to compute IK", "Watch out!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
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

        // Create a pose for a device (i.e. a line, NOT A ROBOT)
        public void CreateDevicePose(string device_name, double device_pos, string pose_name)
        {
            var dev = GetLocatableResource(device_name);
            TxDevice device = dev as TxDevice;

            // Create a new pose
            TxPoseData openposeData = new TxPoseData();
            ArrayList openarraylist = new ArrayList();
            openarraylist.Add(device_pos);
            openposeData.JointValues = openarraylist;
            TxPoseCreationData NewPose = new TxPoseCreationData(pose_name, openposeData);
            TxPose new_base_pose = device.CreatePose(NewPose);
        }

        // Compute the Jacobian of the robot
        public void ComputeJacobian()
        {
            // Get transformation frames
            TxFrame DH0 = TxApplication.ActiveDocument.GetObjectsByName("BASEFRAME")[0] as TxFrame;
            var Frame0 = new TxTransformation(DH0.LocationRelativeToWorkingFrame);

            TxFrame DH1 = TxApplication.ActiveDocument.GetObjectsByName("fr1")[0] as TxFrame;
            var Frame1 = new TxTransformation(DH1.LocationRelativeToWorkingFrame);

            TxFrame DH2 = TxApplication.ActiveDocument.GetObjectsByName("fr2")[0] as TxFrame;
            var Frame2 = new TxTransformation(DH2.LocationRelativeToWorkingFrame);

            TxFrame DH3 = TxApplication.ActiveDocument.GetObjectsByName("fr3")[0] as TxFrame;
            var Frame3 = new TxTransformation(DH3.LocationRelativeToWorkingFrame);

            TxFrame DH4 = TxApplication.ActiveDocument.GetObjectsByName("fr4")[0] as TxFrame;
            var Frame4 = new TxTransformation(DH4.LocationRelativeToWorkingFrame);

            TxFrame DH5 = TxApplication.ActiveDocument.GetObjectsByName("fr5")[0] as TxFrame;
            var Frame5 = new TxTransformation(DH5.LocationRelativeToWorkingFrame);

            TxFrame DH6 = TxApplication.ActiveDocument.GetObjectsByName("TOOLFRAME")[0] as TxFrame;
            var Frame6 = new TxTransformation(DH6.LocationRelativeToWorkingFrame);

            // Get joint positions in meters
            var x1 = Frame1[0, 3] / 1000; var y1 = Frame1[1, 3] / 1000; var z1 = Frame1[2, 3] / 1000;
            var x2 = Frame2[0, 3] / 1000; var y2 = Frame2[1, 3] / 1000; var z2 = Frame2[2, 3] / 1000;
            var x3 = Frame3[0, 3] / 1000; var y3 = Frame3[1, 3] / 1000; var z3 = Frame3[2, 3] / 1000;
            var x4 = Frame4[0, 3] / 1000; var y4 = Frame4[1, 3] / 1000; var z4 = Frame4[2, 3] / 1000;
            var x5 = Frame5[0, 3] / 1000; var y5 = Frame5[1, 3] / 1000; var z5 = Frame5[2, 3] / 1000;
            var x6 = Frame6[0, 3] / 1000; var y6 = Frame6[1, 3] / 1000; var z6 = Frame6[2, 3] / 1000;

            // Define Z axes (rotation vectors)
            double[] Z0 = { Frame0[0, 2], Frame0[1, 2], Frame0[2, 2] };
            double[] Z1 = { Frame1[0, 2], Frame1[1, 2], Frame1[2, 2] };
            double[] Z2 = { Frame2[0, 2], Frame2[1, 2], Frame2[2, 2] };
            double[] Z3 = { Frame3[0, 2], Frame3[1, 2], Frame3[2, 2] };
            double[] Z4 = { Frame4[0, 2], Frame4[1, 2], Frame4[2, 2] };
            double[] Z5 = { Frame5[0, 2], Frame5[1, 2], Frame5[2, 2] };

            // Position vectors
            double[] p0 = { 0.0, 0.0, 0.0 };
            double[] p1 = { x1, y1, z1 };
            double[] p2 = { x2, y2, z2 };
            double[] p3 = { x3, y3, z3 };
            double[] p4 = { x4, y4, z4 };
            double[] p5 = { x5, y5, z5 };
            double[] p6 = { x6, y6, z6 }; // End-effector position
            double[] p = p6;

            // Compute linear part of Jacobian
            double[] result0 = CrossProduct(Z0, Subtract(p, p0));
            double[] result1 = CrossProduct(Z1, Subtract(p, p1));
            double[] result2 = CrossProduct(Z2, Subtract(p, p2));
            double[] result3 = CrossProduct(Z3, Subtract(p, p3));
            double[] result4 = CrossProduct(Z4, Subtract(p, p4));
            double[] result5 = CrossProduct(Z5, Subtract(p, p5));

            // Build Jacobian matrix J (6x6)
            double[,] J = {
            { result0[0], result1[0], result2[0], result3[0], result4[0], result5[0] },
            { result0[1], result1[1], result2[1], result3[1], result4[1], result5[1] },
            { result0[2], result1[2], result2[2], result3[2], result4[2], result5[2] },
            { Z0[0], Z1[0], Z2[0], Z3[0], Z4[0], Z5[0] },
            { Z0[1], Z1[1], Z2[1], Z3[1], Z4[1], Z5[1] },
            { Z0[2], Z1[2], Z2[2], Z3[2], Z4[2], Z5[2] }
        };

            // Compute standard determinant
            double detJ = CalculateDeterminant(J);
            TxMessageBox.Show("Determinant of Jacobian: det(J) = " + detJ.ToString("G6"), "Name", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Compute Yoshikawa manipulability index: w = sqrt(det(J*Jᵗ))
            double[,] JJt = MultiplyMatrixByTranspose(J);
            double detJJt = CalculateDeterminant(JJt);
            double w = Math.Sqrt(Math.Abs(detJJt));
            TxMessageBox.Show("Yoshikawa Manipulability Index: w = sqrt(det(J*Jt) = " + w.ToString("G6"), "Name", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        // Utility: Cross product of 3D vectors
        static double[] CrossProduct(double[] a, double[] b)
        {
            return new double[] {
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]
        };
        }

        // Utility: Vector subtraction
        static double[] Subtract(double[] a, double[] b)
        {
            return new double[] {
            a[0] - b[0],
            a[1] - b[1],
            a[2] - b[2]
        };
        }

        // Utility: Multiply matrix by its transpose: JJᵗ = J * Jᵗ
        static double[,] MultiplyMatrixByTranspose(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[,] result = new double[rows, rows];

            for (int i = 0; i < rows; i++)
                for (int j = 0; j < rows; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < cols; k++)
                        sum += matrix[i, k] * matrix[j, k];
                    result[i, j] = sum;
                }

            return result;
        }

        // Recursive determinant calculator (Laplace expansion)
        static double CalculateDeterminant(double[,] matrix)
        {
            int size = matrix.GetLength(0);
            if (size != matrix.GetLength(1))
                throw new ArgumentException("Matrix must be square.");

            if (size == 1)
                return matrix[0, 0];

            double result = 0.0;

            for (int j = 0; j < size; j++)
            {
                double[,] minor = new double[size - 1, size - 1];

                for (int k = 1; k < size; k++)
                {
                    for (int l = 0, m = 0; l < size; l++)
                    {
                        if (l != j)
                            minor[k - 1, m++] = matrix[k, l];
                    }
                }

                result += (j % 2 == 0 ? 1 : -1) * matrix[0, j] * CalculateDeterminant(minor);
            }

            return result;
        }

    }
}
