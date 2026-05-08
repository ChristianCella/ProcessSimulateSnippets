using System;
using System.Collections.Generic;
using Tecnomatix.Engineering;

namespace ProcessSimulateSnippets
{
    
    public class RLEnvironment : IDisposable
    {
        // === CONFIGURATION ===
        private const int NUM_ACTIONS = 6;
        private const int NUM_CRATES = 3;
        private const int MAX_STEPS = 10;
        private const double OFFSET = 200.0;
        private const double tool_change_duration = 15.0; // CHANGE TO 75!

        // Robot poses
        private const string pp_station = "PP_station"; // position on the line to put pieces in boxes
        private const string pp_box_in_crate_station = "PP_station_box_in_crate"; // position on the line to put boxes in crates
        private const string tool_change_station = "Tool_change_station";
        private const string load_crates_station = "Load_crates_station";

        // Home poses
        private const string pp_home = "PP_home";
        private const string crate_home = "Crate_home";
        private const string tool_change_home = "Tool_change_home";

        // === SCENE OBJECTS ===
        private readonly TxRobot _robot;
        private readonly TxDevice _line;
        private readonly TxResources _robotResource;
        //private readonly TxResources _lineResource;

        // === SIMULATION PLAYER ===
        private TxSimulationPlayer _player;

        // === SNAPSHOT ===
        private readonly TxSnapshot _snapshot;
        private readonly TxApplySnapshotParams _snapParams;

        // === TRACK CREATED OPERATIONS ===
        private List<TxContinuousRoboticOperation> _createdOps = new List<TxContinuousRoboticOperation>();
        private List<TxDeviceOperation> _created_deviceOps = new List<TxDeviceOperation>();

        // === COMMUNICATION (Lorenzo's approach) ===
        private readonly CommunicationManager _communicator;
        private readonly RequestHandler _handler;

        // === EPISODE STATE ===
        private int _stepCount;
        private bool _actionZeroDone;
        private bool _actionOneDone;
        private bool _actionFiveDone;
        private double _totalRobotTime;
        private int _episodeId = 0;
        private string _currentGripper = "Crate_gripper";
        private List<int> _available_places_on_slider = new List<int> { 1, 1, 1 }; // All places on the slider available

        // === FRAME NAMES (change these to match your scene) ===
        private readonly string _pickA1 = "Pick_A_1";
        private readonly string _pickA2 = "Pick_A_2";
        private readonly string _placeA1 = "Place_A_1";
        private readonly string _placeA2 = "Place_A_2";
        private readonly string _pickB1 = "Pick_B_1";
        private readonly string _pickB2 = "Pick_B_2";
        private readonly string _placeB1 = "Place_B_1";
        private readonly string _placeB2 = "Place_B_2";
        private readonly string _pickBoxA1 = "pick_box_A_1";
        private readonly string _pickBoxA2 = "pick_box_A_2";
        private readonly string _placeBox1Crate3 = "crate_3_place1";
        private readonly string _placeBox2Crate3 = "crate_3_place2";
        private readonly string _pickCrate3 = "pick_top_crate_frame";
        private readonly string _pickCrate2 = "pick_middle_crate_frame";
        private readonly string _pickCrate1 = "pick_low_crate_frame";
        private readonly string _placeCrates = "place_crate";
        private readonly List<string> slider_frames = new List<string> { "crate_low_on_slider_station", "crate_middle_on_slider_station", "crate_top_on_slider_station" };
        private readonly string _gripperName = "Smart_gripper";

        public RLEnvironment(string robotName, string lineName)
        {
            // 1. Find the robot
            var objects = TxApplication.ActiveDocument.GetObjectsByName(robotName);
            if (objects.Count == 0)
                throw new Exception($"Robot '{robotName}' not found in the scene.");
            _robot = objects[0] as TxRobot;
            if (_robot == null)
                throw new Exception($"'{robotName}' exists but is not a TxRobot.");

            // 2. Find the line
            var objects_line = TxApplication.ActiveDocument.GetObjectsByName(lineName);
            if (objects_line.Count == 0)
                throw new Exception($"Line '{lineName}' not found in the scene.");
            _line = objects_line[0] as TxDevice;
            if (_line == null)
                throw new Exception($"'{lineName}' exists but is not a TxDevice.");

            // 2. Create resource helper
            _robotResource = new TxResources();

            // 3. Take a snapshot of the initial scene
            _snapshot = _robotResource.CreateSnap("RL_Initial_Conditions");
            _snapParams = _robotResource.CreateSnapPar();

            // 4. Setup simulation player
            _player = TxApplication.ActiveDocument.SimulationPlayer;
            _player.ResetToDefaultSetting();
            _player.AskUserForReset(false);
            _player.DoOnlyUnscheduledReset(true);

            // 5. Start communication (Lorenzo's approach)
            _communicator = new CommunicationManager("GymPort");
            _handler = new RequestHandler(this, _communicator);
            _communicator.StartListening(_handler);

            System.Diagnostics.Trace.WriteLine("[RL] Environment created. Waiting for Python...");
        }

        // =====================================================
        //  RESET
        // =====================================================

        public ObservationPacket Reset()
        {
            System.Diagnostics.Trace.WriteLine("[RL] === RESET ===");

            // Remove previously created robotic opeartions
            foreach (var op in _createdOps)
            {
                try
                {
                    if (op != null)
                        op.Delete();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[RL] Failed to delete op: {ex.Message}");
                }
            }
            _createdOps.Clear();

            // Remove previously created base opeartions
            foreach (var op in _created_deviceOps)
            {
                try
                {
                    if (op != null)
                        op.Delete();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[RL] Failed to delete op: {ex.Message}");
                }
            }
            _created_deviceOps.Clear();

            // Restore scene to initial state
            _snapshot.Apply(_snapParams);
            TxApplication.RefreshDisplay();

            // Reset episode variables
            _stepCount = 0;
            _actionZeroDone = false;
            _actionOneDone = false;
            _actionFiveDone = false;
            _totalRobotTime = 0.0;
            _episodeId++; // Increment the episode id
            _currentGripper = "Crate_gripper"; // Always start with this
            _available_places_on_slider[0] = 1;
            _available_places_on_slider[1] = 1;
            _available_places_on_slider[2] = 1;

            // Reset player settings
            _player = TxApplication.ActiveDocument.SimulationPlayer;
            _player.ResetToDefaultSetting();
            _player.AskUserForReset(false);
            _player.DoOnlyUnscheduledReset(true);

            return BuildObservation();
        }

        // =====================================================
        //  STEP
        // =====================================================

        public StepResult Step(int actionId)
        {
            System.Diagnostics.Trace.WriteLine($"[RL] Step {_stepCount + 1}, Action: {actionId}");

            _stepCount++;
            bool terminated = false;
            bool truncated = false;
            double reward = 0.0;

            // --- FEASIBILITY CHECKS ---
            if (actionId == 0 && (_actionZeroDone || _currentGripper != "Smart_gripper"))
                return new StepResult(BuildObservation(), -5.0, true, false);

            if (actionId == 1 && (_actionOneDone || _currentGripper != "Smart_gripper"))
                return new StepResult(BuildObservation(), -5.0, true, false);

            if (actionId == 2 && (!_actionZeroDone || !_actionOneDone || !_actionFiveDone || _currentGripper != "Smart_gripper"))
            {
                System.Diagnostics.Trace.WriteLine(
                    "[RL] Action 2 attempted without preconditions. Episode terminated.");
                return new StepResult(BuildObservation(), -10.0, true, false);
            }

            if (actionId == 3 && _currentGripper == "Smart_gripper")
                return new StepResult(BuildObservation(), -5.0, true, false);

            if (actionId == 4 && _currentGripper == "Crate_gripper")
                return new StepResult(BuildObservation(), -5.0, true, false);

            if (actionId == 5 && (_actionFiveDone || _currentGripper != "Crate_gripper"))
                return new StepResult(BuildObservation(), -5.0, true, false);

            // --- EXECUTE ACTIONS ---
            try
            {
                List<string> pickFrames = new List<string>();
                List<string> placeFrames = new List<string>();
                int n_sequential_op = 0;
                string opName = "rl_op_" + _stepCount + "_" + _episodeId;
                string base_opName = "base_op_" + _stepCount + "_" + _episodeId;
                string home_opName = "home_op_" + _stepCount + "_" + _episodeId;

                // Get the poses to implement the base motion
                TxPose pp_station_pose = _line.GetPoseByName(pp_station); // Pick & place pieces in boxes
                var pp_pose_rob = (double)pp_station_pose.PoseData.JointValues[0];

                TxPose pp_station_pose_bic = _line.GetPoseByName(pp_box_in_crate_station); // Pick & place boxes in crates
                var pp_pose_rob_bic = (double)pp_station_pose_bic.PoseData.JointValues[0];

                TxPose tool_chan_station = _line.GetPoseByName(tool_change_station); // Tool change
                var tool_chan_stat = (double)tool_chan_station.PoseData.JointValues[0];

                TxPose load_crate_station = _line.GetPoseByName(load_crates_station); // Load crates
                var load_crat_stat = (double)load_crate_station.PoseData.JointValues[0];

                // Checks on the robot base motion
                bool check_pos = false;
                string op_type = "";
                double currentY = _robot.AbsoluteLocation.Translation.Y; // Get thr current robot pose on the line
                string rob_pos = "";

                // Checks on the grippers
                string gripper_to_mount = "";
                string gripper_to_unmount = "";

                // Select the home pose
                string home_pose = "";
                bool optimize_config = false;

                // Create the robot operations
                switch (actionId)
                {
                    case 0: // Piece A1 in the box A1 left
                        pickFrames.Add(_pickA1);
                        pickFrames.Add(_pickA2);
                        placeFrames.Add(_placeA1);
                        placeFrames.Add(_placeA2);
                        n_sequential_op = 2;
                        op_type = "pp";
                        home_pose = pp_home;
                        optimize_config = false;
                        
                        if (currentY != pp_pose_rob)
                        {
                            check_pos = true;
                            rob_pos = pp_station;
                        }
                        
                        _actionZeroDone = true;
                        break;

                    case 1: // Piece B1 in the box B1 left
                        pickFrames.Add(_pickB1);
                        pickFrames.Add(_pickB2);
                        placeFrames.Add(_placeB1);
                        placeFrames.Add(_placeB2);
                        n_sequential_op = 2;
                        op_type = "pp";
                        home_pose = pp_home;
                        optimize_config = false;

                        if (currentY != pp_pose_rob)
                        {
                            check_pos = true;
                            rob_pos = pp_station;
                        }
                        
                        _actionOneDone = true;
                        break;

                    case 2: // Box A1 'left' in crate
                        pickFrames.Add(_pickBoxA1);
                        pickFrames.Add(_pickBoxA2);
                        placeFrames.Add(_placeBox1Crate3);
                        placeFrames.Add(_placeBox2Crate3);
                        n_sequential_op = 2;
                        op_type = "pp";
                        home_pose = pp_home;
                        optimize_config = true;

                        if (currentY != pp_pose_rob_bic)
                        {
                            check_pos = true;
                            rob_pos = pp_box_in_crate_station;
                        }
                        
                        break;

                    case 3: // Tool change to Smart_gripper
                        op_type = "tc";
                        gripper_to_mount = "Smart_gripper";
                        gripper_to_unmount = "Crate_gripper";
                        home_pose = tool_change_home;
                        optimize_config = false;
                        if (currentY != tool_chan_stat)
                        {
                            check_pos = true;
                            rob_pos = tool_change_station;
                        }
                        _currentGripper = "Smart_gripper";
                        break;

                    case 4: // Tool change to Crate_gripper
                        op_type = "tc";
                        gripper_to_unmount = "Smart_gripper";
                        gripper_to_mount = "Crate_gripper";
                        home_pose = tool_change_home;
                        optimize_config = false;
                        if (currentY != tool_chan_stat)
                        {
                            check_pos = true;
                            rob_pos = tool_change_station;
                        }
                        _currentGripper = "Crate_gripper";
                        break;
                    case 5: // Place crates on the slider
                        pickFrames.Add(_pickCrate3);
                        pickFrames.Add(_pickCrate2);
                        pickFrames.Add(_pickCrate1);
                        placeFrames.Add(_placeCrates);
                        placeFrames.Add(_placeCrates);
                        placeFrames.Add(_placeCrates);
                        n_sequential_op = 3;
                        op_type = "pp";
                        home_pose = crate_home;
                        optimize_config = false;

                        if (currentY != load_crat_stat)
                        {
                            check_pos = true;
                            rob_pos = load_crates_station;
                        }
                        _actionFiveDone = true; 
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine($"[RL] Unknown action: {actionId}");
                        return new StepResult(BuildObservation(), -10.0, true, false);
                }

                // Create and execute the operation
                double opTime = 0.0;
                if (op_type == "pp") // If the action is a pick-and-place
                {
                    for (int i = 0; i < pickFrames.Count; i++)
                    {
                        opTime = ExecutePickAndPlace(pickFrames[i], placeFrames[i], opName + "_" + i, check_pos, rob_pos, base_opName + "_" + i, actionId, i, home_pose, home_opName + "_" + i, optimize_config);
                        _totalRobotTime += opTime;
                    }
                }
                else if (op_type == "tc") // If the action is a tool change
                {
                    opTime = ExecuteWait(opName, check_pos, rob_pos, base_opName, gripper_to_mount, gripper_to_unmount, home_pose, home_opName);
                    _totalRobotTime += opTime;
                }
                

                // Compute reward: negative time penalty
                reward = -opTime * 0.01;

                // Success condition: action 2 completed
                if (actionId == 2)
                {
                    reward += 5.0;
                    terminated = true;
                    System.Diagnostics.Trace.WriteLine("[RL] Goal reached! Box inserted into crate.");
                }

                System.Diagnostics.Trace.WriteLine(
                    $"[RL] Operation took {opTime:F2}s. Total: {_totalRobotTime:F2}s. Reward: {reward:F3}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RL] Simulation error: {ex.Message}");
                return new StepResult(BuildObservation(), -10.0, true, false);
            }

            // --- CHECK TRUNCATION ---
            if (_stepCount >= MAX_STEPS)
                truncated = true;

            return new StepResult(BuildObservation(), reward, terminated, truncated);
        }

        // =====================================================
        //  EXECUTE A PICK AND PLACE OPERATION
        // =====================================================

        private double ExecutePickAndPlace(string pickFrame, string placeFrame, string opName, bool check_pos, string rob_pos, string base_opName, int actionId, int it, string home_pose, string home_opName, bool optimize_config)
        {
            // Ensure the robot home pose is correct
            TxDeviceOperation home_op = _robotResource.HomeRobot("GoFa12", home_opName, home_pose, 0.0);
            _created_deviceOps.Add(home_op);
            TxApplication.ActiveDocument.CurrentOperation = home_op;
            _player.Play();
            double home_pos_time = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Homing operation '{home_opName}' completed in {home_pos_time:F2}s");

            double base_pos_time = 0.0;
            //TxDeviceOperation base_op = null;
            if (check_pos)
            {
                TxDeviceOperation base_op = _robotResource.CreateDeviceOp("Line", base_opName, rob_pos, 0.0);
                _created_deviceOps.Add(base_op);
                TxApplication.ActiveDocument.CurrentOperation = base_op;
                _player.Play();
                base_pos_time = _player.CurrentTime;
                System.Diagnostics.Trace.WriteLine($"[RL] Base operation '{base_opName}' completed in {base_pos_time:F2}s");
            }
            
            // Create the pick-and-place operation
            TxContinuousRoboticOperation myop = _robotResource.PP_op("GoFa12", _currentGripper, pickFrame, placeFrame, opName, OFFSET, home_pose, optimize_config);
            _createdOps.Add(myop);

            TxApplication.ActiveDocument.CurrentOperation = myop;
            _player.Play();

            // In case of crates on the slider, refresh their pose (slider management logic)
            if (actionId == 5)
            {
                int num_crate = NUM_CRATES - it; // Crate being put on slider
                string on_slider_frame = slider_frames[it]; // What
                _robotResource.PlaceResourceAccordingToFrame("Crate_" + num_crate, on_slider_frame);
                _available_places_on_slider[it] = 0;
            }

            // Print info about time
            double timeTaken = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Robot operation '{opName}' completed in {timeTaken:F2}s");
            System.Diagnostics.Trace.WriteLine($"[RL] Total operation completed in {timeTaken + base_pos_time + home_pos_time:F2}s");

            return timeTaken + base_pos_time + home_pos_time;
        }

        // =====================================================
        //  EXECUTE A WAIT OPERATION
        // =====================================================

        private double ExecuteWait(string opName, bool check_pos, string rob_pos, string base_opName, string gripper_to_mount, string gripper_to_unmount, string home_pose, string home_opName)
        {
            // Ensure the robot home pose is correct
            TxDeviceOperation home_op = _robotResource.HomeRobot("GoFa12", home_opName, home_pose, 0.0);
            _created_deviceOps.Add(home_op);
            TxApplication.ActiveDocument.CurrentOperation = home_op;
            _player.Play();
            double home_pos_time = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Homing operation '{home_opName}' completed in {home_pos_time:F2}s");

            // Check necessity of moving the base
            double base_pos_time = 0.0;
            if (check_pos)
            {
                TxDeviceOperation base_op = _robotResource.CreateDeviceOp("Line", base_opName, rob_pos, 0.0);
                _created_deviceOps.Add(base_op);
                TxApplication.ActiveDocument.CurrentOperation = base_op;
                _player.Play();
                base_pos_time = _player.CurrentTime;
                System.Diagnostics.Trace.WriteLine($"[RL] Base operation '{base_opName}' completed in {base_pos_time:F2}s");
            }
            
            // Create the 'Wait' operation ==> Tool changes are not simulated as opeartions (the operation becomes a 'wait')
            _robotResource.UnMountToolGripper("GoFa12", gripper_to_unmount, "tool_station_" + gripper_to_unmount);
            _robotResource.MountToolGripper("GoFa12", gripper_to_mount, "tool_holder_offset", "BASEFRAME_" + gripper_to_mount, "TCPF_" + gripper_to_mount);
            TxDeviceOperation myop = _robotResource.CreateDeviceOp("Line", "Wait_" + base_opName, rob_pos, tool_change_duration);
            _created_deviceOps.Add(myop);


            TxApplication.ActiveDocument.CurrentOperation = myop;
            _player.Play();

            double timeTaken = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Robot operation '{opName}' completed in {timeTaken:F2}s");
            System.Diagnostics.Trace.WriteLine($"[RL] Total operation completed in {timeTaken + base_pos_time + home_pos_time:F2}s");

            return timeTaken + base_pos_time + home_pos_time;
        }

        // =====================================================
        //  OBSERVATION + ACTION MASK
        // =====================================================

        private ObservationPacket BuildObservation()
        {
            double stepNorm = (double)_stepCount / MAX_STEPS;
            double actionZero = _actionZeroDone ? 1.0 : 0.0;
            double actionOne = _actionOneDone ? 1.0 : 0.0;
            double actionFive = _actionFiveDone ? 1.0 : 0.0;
            double timeNorm = _totalRobotTime / 100.0;
            double gripperState = _currentGripper == "Smart_gripper" ? 0.0 : 1.0;

            bool hasSmartGripper = _currentGripper == "Smart_gripper";
            bool hasCrateGripper = _currentGripper == "Crate_gripper";

            var state = new List<double> { stepNorm, actionZero, actionOne, timeNorm, gripperState, actionFive };
            var actionMask = new List<int>
            {
                (!_actionZeroDone && hasSmartGripper) ? 1 : 0,                    // Action 0
                (!_actionOneDone && hasSmartGripper) ? 1 : 0,                     // Action 1
                (_actionZeroDone && _actionOneDone && _actionFiveDone && hasSmartGripper) ? 1 : 0,   // Action 2
                !hasSmartGripper ? 1 : 0,                                         // Action 3: mount smart gripper
                !hasCrateGripper ? 1 : 0,                                      // Action 4: mount crate gripper
                (!_actionFiveDone && hasCrateGripper) ? 1 : 0    // Action 5
            };
            return new ObservationPacket
            {
                State = state,
                ActionMask = actionMask
            };
        }

        // =====================================================
        //  CLEANUP
        // =====================================================

        public void Dispose()
        {
            // Cleanup robotic operations if anything remains
            foreach (var op in _createdOps)
            {
                try
                {
                    op?.Delete();
                }
                catch { }
            }
            _createdOps.Clear();

            // Cleanup mobile base operations if anything remains
            foreach (var op in _created_deviceOps)
            {
                try
                {
                    op?.Delete();
                }
                catch { }
            }
            _created_deviceOps.Clear();

            // Restore scene to initial state
            _snapshot.Apply(_snapParams);
            TxApplication.RefreshDisplay();
            _player?.ResetToDefaultSetting();
            _player?.ResetToDefaultSetting();
            _player?.AskUserForReset(false);
            _player?.DoOnlyUnscheduledReset(true);
            _communicator?.Dispose();
        }
    }
}
