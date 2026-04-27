using System;
using System.IO;
using System.Windows.Forms;
using Tecnomatix.Engineering;
using Tecnomatix.Engineering.Plc;
using Tecnomatix.Engineering.Olp;
using System.Collections;
using EngineeringInternalExtension;
using Tecnomatix.Engineering.ModelObjects;

namespace ProcessSimulateSnippets
{
    public class TxResources
    {
        // ------------------------------- From 'snippets' to methods
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

            // Put TCPF back in the robot flange (called "TOOLFRAME")
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

        // Impose a specific joint configuration to the robot
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

        // Create compound operation 
        public void CreateCompOp(string comp_op_name)
        {
            // Create the compound operation and save it in a variable
            TxCompoundOperationCreationData dat = new TxCompoundOperationCreationData(comp_op_name);
            TxApplication.ActiveDocument.OperationRoot.CreateCompoundOperation(dat);

            // Refresh the display
            TxApplication.RefreshDisplay();
        }

        // Add an operation to a compound operation
        public void AddToCompound(string comp_op_name, string op_to_add, double starting_time)
        {
            // Find the compound operation by name
            TxObjectList operations = TxApplication.ActiveDocument.GetObjectsByName(comp_op_name);
            var comp_op = operations[0] as TxCompoundOperation;

            // Find the operation to be added by name
            TxObjectList OpToAdd = TxApplication.ActiveDocument.GetObjectsByName(op_to_add);
            var op_to_add_obj = OpToAdd[0] as ITxObject;
            var op_to_add_op = OpToAdd[0] as ITxOperation;

            // Add to the compound operation
            comp_op.AddObject(op_to_add_obj);
            comp_op.SetChildOperationRelativeStartTime(op_to_add_op, starting_time);

            // Refresh the display
            TxApplication.RefreshDisplay();
        }

        // Create Human task => This must be fixed
        public void CreateHumanOp(string op_name)
        {
            // Initialization variables for the pick and place 	
            TxHumanTsbSimulationOperation op = null;
            TxHumanTSBTaskCreationDataEx taskCreationData = new TxHumanTSBTaskCreationDataEx();

            // Get the human
            TxObjectList humans = TxApplication.ActiveDocument.GetObjectsByName("Jack");
            TxHuman human = humans[0] as TxHuman;

            // Important: these informations are FUNDAMENTAL to not make the script crash
            TxObjectList cube_pick = TxApplication.ActiveSelection.GetItems();
            cube_pick = TxApplication.ActiveDocument.GetObjectsByName("Crate_1");
            var component = cube_pick[0] as ITxLocatableObject;

            TxObjectList ref_frame_cube_place = TxApplication.ActiveSelection.GetItems();
            ref_frame_cube_place = TxApplication.ActiveDocument.GetObjectsByName("crate_top_on_line_station");
            var frame_cube_place = ref_frame_cube_place[0] as ITxLocatableObject;
            var target_place = new TxTransformation(frame_cube_place.AbsoluteLocation);
            var position_place = new TxTransformation(component.AbsoluteLocation);
            position_place.Translation = new TxVector(target_place[0, 3], target_place[1, 3], target_place[2, 3]);
            position_place.RotationRPY_ZYX = target_place.RotationRPY_ZYX;

            // Create the simulation  		
            op = TxHumanTSBSimulationUtilsEx.CreateSimulation(op_name);
            op.SetInitialContext();

            // Fill all the fields: without some of these, the script crashes
            taskCreationData.PrimaryObject = component;
            taskCreationData.TargetLocation = position_place;
            taskCreationData.Human = human;
            taskCreationData.TaskType = TsbTaskType.HUMAN_Wait;
            taskCreationData.TaskDuration = 3.0; // seconds
            TxHumanTsbTaskOperation tsbPoseTaskInt = op.CreateTask(taskCreationData);
            op.ApplyTask(tsbPoseTaskInt, 1);
            TxApplication.RefreshDisplay();

            // Make modifications effective
            //op.ForceResimulation();
        }

        public void player_TimeIntervalReached(object sender, TxSimulationPlayer_TimeIntervalReachedEventArgs args)
        {

            //TxMessageBox.Show(string.Format(args.CurrentTime.ToString()), "Current time instant", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (Math.Abs(args.CurrentTime - 1.0) < 1e-6)
            {
                AddToCompound("complete_op", "Human_task_2", 3.0);
                TxApplication.RefreshDisplay();
            }
        }

        // Pick&Place
        public TxContinuousRoboticOperation PP_op(string robot_name, string gripper_name, string pick_frame_name, string place_frame_name, string op_name, double offset)
        {
            // Get the robot
            var robot = GetLocatableResource(robot_name);
            var rob = robot as TxRobot;

            // Get the pick and place frames
            var pick_fr = GetLocatableResource(pick_frame_name);
            var place_fr = GetLocatableResource(place_frame_name);

            // Create the new operation    	
            TxContinuousRoboticOperationCreationData data = new TxContinuousRoboticOperationCreationData(op_name);
            TxApplication.ActiveDocument.OperationRoot.CreateContinuousRoboticOperation(data);
            TxObjectList allOps = TxApplication.ActiveDocument.GetObjectsByName(op_name);
            TxContinuousRoboticOperation MyOp = allOps[0] as TxContinuousRoboticOperation;

            // Create all the necessary points       
            TxRoboticViaLocationOperationCreationData Point1 = new TxRoboticViaLocationOperationCreationData();
            Point1.Name = "point1";
            TxRoboticViaLocationOperationCreationData Point2 = new TxRoboticViaLocationOperationCreationData();
            Point2.Name = "point2";
            TxRoboticViaLocationOperationCreationData Point3 = new TxRoboticViaLocationOperationCreationData();
            Point3.Name = "point3";
            TxRoboticViaLocationOperationCreationData Point4 = new TxRoboticViaLocationOperationCreationData();
            Point4.Name = "point4";
            TxRoboticViaLocationOperationCreationData Point5 = new TxRoboticViaLocationOperationCreationData();
            Point5.Name = "point5";
            TxRoboticViaLocationOperationCreationData Point6 = new TxRoboticViaLocationOperationCreationData();
            Point6.Name = "point6";
            TxRoboticViaLocationOperationCreationData Point7 = new TxRoboticViaLocationOperationCreationData();
            Point7.Name = "point7";
            TxRoboticViaLocationOperationCreationData Point8 = new TxRoboticViaLocationOperationCreationData();
            Point8.Name = "point8";

            TxRoboticViaLocationOperation FirstPoint = MyOp.CreateRoboticViaLocationOperation(Point1);
            TxRoboticViaLocationOperation SecondPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point2, FirstPoint);
            TxRoboticViaLocationOperation ThirdPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point3, SecondPoint);
            TxRoboticViaLocationOperation FourthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point4, ThirdPoint);
            TxRoboticViaLocationOperation FifthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point5, FourthPoint);
            TxRoboticViaLocationOperation SixthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point6, FifthPoint);
            TxRoboticViaLocationOperation SeventhPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point7, SixthPoint);
            TxRoboticViaLocationOperation EigthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point8, SeventhPoint);

            // Start and end points
            TxTransformation tcp_pos = rob.TCPF.AbsoluteLocation;
            FirstPoint.AbsoluteLocation = new TxTransformation(tcp_pos);
            EigthPoint.AbsoluteLocation = new TxTransformation(tcp_pos);

            // Pick and Place points
            TxTransformation pick_pos = pick_fr.AbsoluteLocation;
            TxTransformation place_pos = place_fr.AbsoluteLocation;
            ThirdPoint.AbsoluteLocation = new TxTransformation(pick_pos);
            SixthPoint.AbsoluteLocation = new TxTransformation(place_pos);

            // Pre/post pick
            SecondPoint.AbsoluteLocation = ThirdPoint.AbsoluteLocation;
            var pose_point2 = new TxTransformation(SecondPoint.AbsoluteLocation);
            pose_point2.Translation = new TxVector(pose_point2.Translation.X, pose_point2.Translation.Y, pose_point2.Translation.Z + offset);
            SecondPoint.AbsoluteLocation = pose_point2;
            FourthPoint.AbsoluteLocation = SecondPoint.AbsoluteLocation;

            // Pre/post place
            FifthPoint.AbsoluteLocation = SixthPoint.AbsoluteLocation;
            var pose_point5 = new TxTransformation(FifthPoint.AbsoluteLocation);
            pose_point5.Translation = new TxVector(pose_point5.Translation.X, pose_point5.Translation.Y, pose_point5.Translation.Z + offset);
            FifthPoint.AbsoluteLocation = pose_point5;
            SeventhPoint.AbsoluteLocation = FifthPoint.AbsoluteLocation;

            // Setup the robot controller
            MyOp.Robot = rob;
            TxTypeFilter filter = new TxTypeFilter(typeof(TxRoboticViaLocationOperation));
            TxObjectList points = MyOp.GetAllDescendants(filter);
            TxOlpControllerUtilities ControllerUtils = new TxOlpControllerUtilities();

            ITxOlpRobotControllerParametersHandler paramHandler = (ITxOlpRobotControllerParametersHandler)
            ControllerUtils.GetInterfaceImplementationFromController(rob.Controller.Name,
            typeof(ITxOlpRobotControllerParametersHandler), typeof(TxRobotSimulationControllerAttribute),
            "ControllerName");

            for (int ii = 0; ii < points.Count; ii++)
            {
                SetWaypointValues(points[ii].Name.ToString(), paramHandler, gripper_name);
            }

            // OLP command for attaching/detaching the obejct
            CloseGripper(points[2].Name.ToString(), gripper_name);
            OpenGripper(points[5].Name.ToString(), gripper_name);

            // Return back the operation to be simulated
            return MyOp;

        }

        public void CloseGripper(string point_name, string gripper_name)
        {
            // Save the second point to close the gripper		
            TxRoboticViaLocationOperation Waypoint = TxApplication.ActiveDocument.
            GetObjectsByName(point_name)[0] as TxRoboticViaLocationOperation;

            // Save the gripper "Camozzi gripper" 	
            ITxObject Gripper = TxApplication.ActiveDocument.
            GetObjectsByName(gripper_name)[0] as TxGripper;

            // Save the pose "Gripper Closed"  		
            ITxObject Pose = TxApplication.ActiveDocument.
            GetObjectsByName("CLOSE_" + gripper_name)[0] as TxPose;

            // Save the reference frame of the gripper 		
            ITxObject tGripper = TxApplication.ActiveDocument.
            GetObjectsByName("TCPF_" + gripper_name)[0] as TxFrame;

            // Create an array called "elements" and the command to be written in it
            ArrayList elements1 = new ArrayList();
            ArrayList elements2 = new ArrayList();
            ArrayList elements3 = new ArrayList();
            ArrayList elements4 = new ArrayList();
            ArrayList elements5 = new ArrayList();

            var myCmd1 = new TxRoboticCompositeCommandStringElement("# Destination");
            var myCmd11 = new TxRoboticCompositeCommandTxObjectElement(Gripper);

            var myCmd2 = new TxRoboticCompositeCommandStringElement("# Drive");
            var myCmd21 = new TxRoboticCompositeCommandTxObjectElement(Pose);

            var myCmd3 = new TxRoboticCompositeCommandStringElement("# Destination");
            var myCmd31 = new TxRoboticCompositeCommandTxObjectElement(Gripper);

            var myCmd4 = new TxRoboticCompositeCommandStringElement("# WaitDevice");
            var myCmd41 = new TxRoboticCompositeCommandTxObjectElement(Pose);

            var myCmd5 = new TxRoboticCompositeCommandStringElement("# Grip");
            var myCmd51 = new TxRoboticCompositeCommandTxObjectElement(tGripper);

            // First line of command	
            elements1.Add(myCmd1);
            elements1.Add(myCmd11);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData1 =
            new TxRoboticCompositeCommandCreationData(elements1);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData1);

            // Second line of command
            elements2.Add(myCmd2);
            elements2.Add(myCmd21);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData2 =
            new TxRoboticCompositeCommandCreationData(elements2);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData2);

            // Third line of command
            elements3.Add(myCmd3);
            elements3.Add(myCmd31);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData3 =
            new TxRoboticCompositeCommandCreationData(elements3);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData3);

            // Fourth line of command
            elements4.Add(myCmd4);
            elements4.Add(myCmd41);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData4 =
            new TxRoboticCompositeCommandCreationData(elements4);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData4);

            // Fifth line of command	
            elements5.Add(myCmd5);
            elements5.Add(myCmd51);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData5 =
            new TxRoboticCompositeCommandCreationData(elements5);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData5);

        }

        public void OpenGripper(string point_name, string gripper_name)
        {
            // Save the second point to close the gripper		
            TxRoboticViaLocationOperation Waypoint = TxApplication.ActiveDocument.
            GetObjectsByName(point_name)[0] as TxRoboticViaLocationOperation;

            // Save the gripper "Camozzi gripper" 	
            ITxObject Gripper = TxApplication.ActiveDocument.
            GetObjectsByName(gripper_name)[0] as TxGripper;

            // Save the pose "Gripper Closed"  		
            ITxObject Pose = TxApplication.ActiveDocument.
            GetObjectsByName("OPEN_" + gripper_name)[0] as TxPose;

            // Save the reference frame of the gripper 		
            ITxObject tGripper = TxApplication.ActiveDocument.
            GetObjectsByName("TCPF_" + gripper_name)[0] as TxFrame;

            // Create an array called "elements" and the command to be written in it
            ArrayList elements1 = new ArrayList();
            ArrayList elements2 = new ArrayList();
            ArrayList elements3 = new ArrayList();
            ArrayList elements4 = new ArrayList();
            ArrayList elements5 = new ArrayList();

            var myCmd1 = new TxRoboticCompositeCommandStringElement("# Destination");
            var myCmd11 = new TxRoboticCompositeCommandTxObjectElement(Gripper);

            var myCmd2 = new TxRoboticCompositeCommandStringElement("# Drive");
            var myCmd21 = new TxRoboticCompositeCommandTxObjectElement(Pose);

            var myCmd3 = new TxRoboticCompositeCommandStringElement("# Destination");
            var myCmd31 = new TxRoboticCompositeCommandTxObjectElement(Gripper);

            var myCmd4 = new TxRoboticCompositeCommandStringElement("# WaitDevice");
            var myCmd41 = new TxRoboticCompositeCommandTxObjectElement(Pose);

            var myCmd5 = new TxRoboticCompositeCommandStringElement("# Release");
            var myCmd51 = new TxRoboticCompositeCommandTxObjectElement(tGripper);

            // First line of command	
            elements1.Add(myCmd1);
            elements1.Add(myCmd11);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData1 =
            new TxRoboticCompositeCommandCreationData(elements1);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData1);

            // Second line of command
            elements2.Add(myCmd2);
            elements2.Add(myCmd21);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData2 =
            new TxRoboticCompositeCommandCreationData(elements2);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData2);

            // Third line of command
            elements3.Add(myCmd3);
            elements3.Add(myCmd31);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData3 =
            new TxRoboticCompositeCommandCreationData(elements3);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData3);

            // Fourth line of command
            elements4.Add(myCmd4);
            elements4.Add(myCmd41);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData4 =
            new TxRoboticCompositeCommandCreationData(elements4);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData4);

            // Fifth line of command	
            elements5.Add(myCmd5);
            elements5.Add(myCmd51);

            TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData5 =
            new TxRoboticCompositeCommandCreationData(elements5);

            Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData5);

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

        // Specify the parameters of the waypoint
        private static void SetWaypointValues(
            string point_name,
            ITxOlpRobotControllerParametersHandler paramHandler,
            string gripper_name
            )
        {
            // Get the waypoint by name
            TxRoboticViaLocationOperation Point = TxApplication.ActiveDocument.
            GetObjectsByName(point_name)[0] as TxRoboticViaLocationOperation;

            // Set the robot characteristics for that point
            paramHandler.OnComplexValueChanged("Tool Frame", "TCPF_" + gripper_name, Point);
            paramHandler.OnComplexValueChanged("Motion Type", "PTP", Point);
            paramHandler.OnComplexValueChanged("Speed", "100%", Point);
            paramHandler.OnComplexValueChanged("Acc", "100%", Point);
            paramHandler.OnComplexValueChanged("Zone", "fine", Point);

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
