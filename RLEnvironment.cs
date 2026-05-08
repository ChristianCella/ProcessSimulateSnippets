using System;
using System.Collections.Generic;
using Tecnomatix.Engineering;

namespace ProcessSimulateSnippets
{

    public class RLEnvironment : IDisposable
    {
        // === DEBUG GUI ===
        private RLDebugPanel _debugPanel;

        // === CONFIGURATION ===
        private const int NUM_ACTIONS = 7;
        private const int NUM_CRATES = 3;
        private const int MAX_STEPS = 20;
        private const double OFFSET = 200.0;
        private const double tool_change_duration = 15.0; // NOTE: change this to the real values

        // Normalization constants
        private const double MAX_RAIL_LENGTH = 3000.0;   // mm - adjust to your actual rail length
        private const double MAX_EXPECTED_TIME = 300.0;  // seconds - adjust to your expected max episode time

        // Robot poses
        private const string pp_station = "PP_station";
        private const string pp_box_in_crate_station = "PP_station_box_in_crate";
        private const string tool_change_station = "Tool_change_station";
        private const string load_crates_station = "Load_crates_station";
        private const string unload_crates_station = "Unload_crate_station";

        // Home poses
        private const string pp_home = "PP_home";
        private const string crate_home = "Crate_home";
        private const string tool_change_home = "Tool_change_home";

        // === SCENE OBJECTS ===
        private readonly TxRobot _robot;
        private readonly TxDevice _line;
        private readonly TxResources _robotResource;

        // === SIMULATION PLAYER ===
        private TxSimulationPlayer _player;

        // === SNAPSHOT ===
        private readonly TxSnapshot _snapshot;
        private readonly TxApplySnapshotParams _snapParams;

        // === TRACK CREATED OPERATIONS ===
        private List<TxContinuousRoboticOperation> _createdOps = new List<TxContinuousRoboticOperation>();
        private List<TxDeviceOperation> _created_deviceOps = new List<TxDeviceOperation>();

        // === COMMUNICATION ===
        private readonly CommunicationManager _communicator;
        private readonly RequestHandler _handler;

        // === CRATE CONTENTS TRACKING ===
        private int _boxesInCrate3TypeA = 0;  // Type A boxes inserted into crate 3
        private int _boxesInCrate2TypeB = 0;  // Type B boxes inserted into crate 2
        private const int MAX_BOXES_PER_CRATE = 2; // adjust to your actual crate capacity

        // === EPISODE STATE ===
        private int _stepCount;
        private bool _actionZeroDone;   // Type A pieces placed in box
        private bool _actionOneDone;    // Type B pieces placed in box
        private bool _actionTwoDone;    // Box inserted into crate
        private bool _actionFiveDone;   // Crates placed on slider
        private bool _actionSixDone;
        private double _totalRobotTime;
        private int _episodeId = 0;
        private string _currentGripper = "Crate_gripper";
        private List<int> _available_places_on_slider = new List<int> { 1, 1, 1 };

        // === FRAME NAMES ===
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
        private readonly string _crateLowOutfeed = "crate_low_on_table_outfeed";
        private readonly List<string> slider_frames = new List<string>
        {
            "crate_low_on_slider_station",
            "crate_middle_on_slider_station",
            "crate_top_on_slider_station"
        };
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

            // 3. Create resource helper
            _robotResource = new TxResources();

            // 4. Take a snapshot of the initial scene
            _snapshot = _robotResource.CreateSnap("RL_Initial_Conditions");
            _snapParams = _robotResource.CreateSnapPar();

            // 5. Setup simulation player
            _player = TxApplication.ActiveDocument.SimulationPlayer;
            _player.ResetToDefaultSetting();
            _player.AskUserForReset(false);
            _player.DoOnlyUnscheduledReset(true);

            // 6. Start communication
            _communicator = new CommunicationManager("GymPort");
            _handler = new RequestHandler(this, _communicator);
            _communicator.StartListening(_handler);

            // 7. Show debug panel
            _debugPanel = new RLDebugPanel();
            _debugPanel.Show();

            System.Diagnostics.Trace.WriteLine("[RL] Environment created. Waiting for Python...");
        }

        // =====================================================
        //  RESET
        // =====================================================

        public ObservationPacket Reset()
        {
            System.Diagnostics.Trace.WriteLine("[RL] === RESET ===");

            // Remove previously created robotic operations
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

            // Remove previously created device operations
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

            // Reset all episode state variables
            _stepCount = 0;
            _actionZeroDone = false;
            _actionOneDone = false;
            _actionTwoDone = false;   // NEW
            _actionFiveDone = false;
            _totalRobotTime = 0.0;
            _episodeId++;
            _currentGripper = "Crate_gripper";
            _available_places_on_slider[0] = 1;
            _available_places_on_slider[1] = 1;
            _available_places_on_slider[2] = 1;
            _boxesInCrate3TypeA = 0;
            _boxesInCrate2TypeB = 0;

            // Reset player settings
            _player = TxApplication.ActiveDocument.SimulationPlayer;
            _player.ResetToDefaultSetting();
            _player.AskUserForReset(false);
            _player.DoOnlyUnscheduledReset(true);

            // Update the debug panel
            _debugPanel.UpdateState(
                _episodeId, _stepCount, -1, 0.0,
                _currentGripper, _actionZeroDone, _actionOneDone, _actionFiveDone,
                _totalRobotTime,
                _available_places_on_slider[0],
                _available_places_on_slider[1],
                _available_places_on_slider[2]
            );

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

            // --- FEASIBILITY CHECKS (unchanged - these use the internal booleans) ---
            // These are needed only when I do not use the masking mechanism of masked PPO
            if (actionId == 0 && (_actionZeroDone || _currentGripper != "Smart_gripper"))
            {
                UpdateDebug(0, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 1 && (_actionOneDone || _currentGripper != "Smart_gripper"))
            {
                UpdateDebug(1, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 2 && (!_actionZeroDone || !_actionOneDone || !_actionFiveDone || _currentGripper != "Smart_gripper"))
            {
                System.Diagnostics.Trace.WriteLine("[RL] Action 2 attempted without preconditions. Episode terminated.");
                UpdateDebug(2, -10.0);
                return new StepResult(BuildObservation(), -10.0, true, false);
            }

            if (actionId == 3 && _currentGripper == "Smart_gripper")
            {
                UpdateDebug(3, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 4 && _currentGripper == "Crate_gripper")
            {
                UpdateDebug(4, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 5 && (_actionFiveDone || _currentGripper != "Crate_gripper"))
            {
                UpdateDebug(5, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 6 && (_currentGripper != "Crate_gripper" ||
           !_actionTwoDone ||
           _boxesInCrate3TypeA < MAX_BOXES_PER_CRATE))
            {
                System.Diagnostics.Trace.WriteLine("[RL] Action 6 attempted without preconditions.");
                UpdateDebug(6, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            // --- EXECUTE ACTIONS (unchanged) ---
            try
            {
                List<string> pickFrames = new List<string>();
                List<string> placeFrames = new List<string>();
                int n_sequential_op = 0;
                string opName = "rl_op_" + _stepCount + "_" + _episodeId;
                string base_opName = "base_op_" + _stepCount + "_" + _episodeId;
                string home_opName = "home_op_" + _stepCount + "_" + _episodeId;

                TxPose pp_station_pose = _line.GetPoseByName(pp_station);
                var pp_pose_rob = (double)pp_station_pose.PoseData.JointValues[0];

                TxPose pp_station_pose_bic = _line.GetPoseByName(pp_box_in_crate_station);
                var pp_pose_rob_bic = (double)pp_station_pose_bic.PoseData.JointValues[0];

                TxPose tool_chan_station = _line.GetPoseByName(tool_change_station);
                var tool_chan_stat = (double)tool_chan_station.PoseData.JointValues[0];

                TxPose load_crate_station = _line.GetPoseByName(load_crates_station);
                var load_crat_stat = (double)load_crate_station.PoseData.JointValues[0];

                TxPose unload_crate_station = _line.GetPoseByName(unload_crates_station);
                var unload_crat_stat = (double)unload_crate_station.PoseData.JointValues[0];

                bool check_pos = false;
                string op_type = "";
                double currentY = _robot.AbsoluteLocation.Translation.Y;
                string rob_pos = "";

                string gripper_to_mount = "";
                string gripper_to_unmount = "";

                string home_pose = "";
                bool optimize_config = false;

                switch (actionId)
                {
                    case 0:
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

                    case 1:
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

                    case 2:
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
                        _actionTwoDone = true;   // NEW - mark box inserted into crate
                        _boxesInCrate3TypeA = _boxesInCrate3TypeA + n_sequential_op;  // 2 pieces added at a time
                        break;

                    case 3:
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

                    case 4:
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

                    case 5:
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

                    case 6:
                        pickFrames.Add(_pickCrate3);
                        placeFrames.Add(_crateLowOutfeed);
                        n_sequential_op = 1;
                        op_type = "pp";
                        home_pose = crate_home; // adjust if needed
                        optimize_config = false;
                        if (currentY != unload_crat_stat) // adjust position check if needed
                        {
                            check_pos = true;
                            rob_pos = unload_crates_station;
                        }
                        _actionSixDone = true;
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine($"[RL] Unknown action: {actionId}");
                        UpdateDebug(0, -5.0);
                        return new StepResult(BuildObservation(), -10.0, true, false);
                }

                // Execute the operation
                double opTime = 0.0;
                if (op_type == "pp")
                {
                    for (int i = 0; i < pickFrames.Count; i++)
                    {
                        opTime = ExecutePickAndPlace(
                            pickFrames[i], placeFrames[i],
                            opName + "_" + i, check_pos, rob_pos,
                            base_opName + "_" + i, actionId, i,
                            home_pose, home_opName + "_" + i,
                            optimize_config);
                        _totalRobotTime += opTime;
                    }
                }
                else if (op_type == "tc")
                {
                    opTime = ExecuteWait(
                        opName, check_pos, rob_pos, base_opName,
                        gripper_to_mount, gripper_to_unmount,
                        home_pose, home_opName);
                    _totalRobotTime += opTime;
                }

                // Compute reward
                reward = -opTime * 0.01;

                // Success condition
                if (actionId == 6)
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
                UpdateDebug(0, -5.0);
                return new StepResult(BuildObservation(), -10.0, true, false);
            }

            // Check truncation
            if (_stepCount >= MAX_STEPS)
                truncated = true;

            UpdateDebug(actionId, reward);
            return new StepResult(BuildObservation(), reward, terminated, truncated);
        }

        // =====================================================
        //  EXECUTE A PICK AND PLACE OPERATION (unchanged)
        // =====================================================

        private double ExecutePickAndPlace(
            string pickFrame, string placeFrame, string opName,
            bool check_pos, string rob_pos, string base_opName,
            int actionId, int it, string home_pose, string home_opName,
            bool optimize_config)
        {
            TxDeviceOperation home_op = _robotResource.HomeRobot("GoFa12", home_opName, home_pose, 0.0);
            _created_deviceOps.Add(home_op);
            TxApplication.ActiveDocument.CurrentOperation = home_op;
            _player.Play();
            double home_pos_time = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Homing operation '{home_opName}' completed in {home_pos_time:F2}s");

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

            TxContinuousRoboticOperation myop = _robotResource.PP_op(
                "GoFa12", _currentGripper, pickFrame, placeFrame,
                opName, OFFSET, home_pose, optimize_config);
            _createdOps.Add(myop);

            TxApplication.ActiveDocument.CurrentOperation = myop;
            _player.Play();

            if (actionId == 5)
            {
                int num_crate = NUM_CRATES - it;
                string on_slider_frame = slider_frames[it];
                _robotResource.PlaceResourceAccordingToFrame("Crate_" + num_crate, on_slider_frame);
                _available_places_on_slider[it] = 0;
            }

            double timeTaken = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Robot operation '{opName}' completed in {timeTaken:F2}s");
            System.Diagnostics.Trace.WriteLine($"[RL] Total operation completed in {timeTaken + base_pos_time + home_pos_time:F2}s");

            return timeTaken + base_pos_time + home_pos_time;
        }

        // =====================================================
        //  EXECUTE A WAIT OPERATION (unchanged)
        // =====================================================

        private double ExecuteWait(
            string opName, bool check_pos, string rob_pos, string base_opName,
            string gripper_to_mount, string gripper_to_unmount,
            string home_pose, string home_opName)
        {
            TxDeviceOperation home_op = _robotResource.HomeRobot("GoFa12", home_opName, home_pose, 0.0);
            _created_deviceOps.Add(home_op);
            TxApplication.ActiveDocument.CurrentOperation = home_op;
            _player.Play();
            double home_pos_time = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Homing operation '{home_opName}' completed in {home_pos_time:F2}s");

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

            _robotResource.UnMountToolGripper("GoFa12", gripper_to_unmount, "tool_station_" + gripper_to_unmount);
            _robotResource.MountToolGripper("GoFa12", gripper_to_mount, "tool_holder_offset",
                "BASEFRAME_" + gripper_to_mount, "TCPF_" + gripper_to_mount);
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
            // === ROBOT STATE (unchanged) ===
            double railPos = _robot.AbsoluteLocation.Translation.Y / MAX_RAIL_LENGTH;
            railPos = Math.Max(0.0, Math.Min(1.0, railPos));
            double gripperSmart = (_currentGripper == "Smart_gripper") ? 1.0 : 0.0;
            double gripperCrate = (_currentGripper == "Crate_gripper") ? 1.0 : 0.0;
            double gripperNone = 1.0 - gripperSmart - gripperCrate;

            // === LINE STATE (unchanged) ===
            double typeADone = _actionZeroDone ? 1.0 : 0.0;
            double typeBDone = _actionOneDone ? 1.0 : 0.0;
            double boxesReady = (_actionZeroDone && _actionOneDone) ? 1.0 : 0.0;
            int cratesPlaced = _available_places_on_slider.FindAll(x => x == 0).Count;
            double cratesOnSlider = (double)cratesPlaced / NUM_CRATES;
            double boxInCrate = _actionTwoDone ? 1.0 : 0.0;
            double elapsedNorm = Math.Min(_totalRobotTime / MAX_EXPECTED_TIME, 1.0);

            // === CRATE CONTENTS (NEW) ===
            // Normalize by max boxes per crate
            double boxesInCrate3A = (double)_boxesInCrate3TypeA / MAX_BOXES_PER_CRATE;
            double boxesInCrate2B = (double)_boxesInCrate2TypeB / MAX_BOXES_PER_CRATE;

            // === ASSEMBLE STATE VECTOR (12 variables) ===
            var state = new List<double>
            {
                railPos,          // 0: robot rail position normalized
                gripperSmart,     // 1: gripper one-hot - Smart gripper
                gripperCrate,     // 2: gripper one-hot - Crate gripper
                gripperNone,      // 3: gripper one-hot - No gripper
                typeADone,        // 4: Type A pieces placed in box
                typeBDone,        // 5: Type B pieces placed in box
                boxesReady,       // 6: both box types complete and ready
                cratesOnSlider,   // 7: fraction of crates placed on slider
                boxInCrate,       // 8: box successfully inserted into crate
                elapsedNorm,      // 9: total elapsed robot time normalized
                boxesInCrate3A,   // 10: Type A boxes in crate 3 normalized  -- NEW
                boxesInCrate2B    // 11: Type B boxes in crate 2 normalized  -- NEW
            };

            // === ACTION MASK ===
            bool hasSmartGripper = _currentGripper == "Smart_gripper";
            bool hasCrateGripper = _currentGripper == "Crate_gripper";

            // Action 6 feasibility condition
            bool action6Feasible = hasCrateGripper &&
                       _actionTwoDone &&
                       (_boxesInCrate3TypeA >= MAX_BOXES_PER_CRATE);

            var actionMask = new List<int>
            {
                (!_actionZeroDone && hasSmartGripper) ? 1 : 0, // 0
                (!_actionOneDone  && hasSmartGripper) ? 1 : 0, // 1

                (_actionZeroDone &&
                 _actionOneDone &&
                 _actionFiveDone &&
                 hasSmartGripper &&
                 !_actionTwoDone) ? 1 : 0,                     // 2

                !hasSmartGripper ? 1 : 0,                      // 3
                !hasCrateGripper ? 1 : 0,                      // 4
                (!_actionFiveDone && hasCrateGripper) ? 1 : 0,// 5
                action6Feasible ? 1 : 0                        // 6
            };

            return new ObservationPacket
            {
                State = state,
                ActionMask = actionMask
            };
        }

        // =====================================================
        //  HELPER TO UPDATE THE GUI
        // =====================================================

        private void UpdateDebug(int actionId, double reward)
        {
            _debugPanel.UpdateState(
                _episodeId, _stepCount, actionId, reward,
                _currentGripper, _actionZeroDone, _actionOneDone, _actionFiveDone,
                _totalRobotTime,
                _available_places_on_slider[0],
                _available_places_on_slider[1],
                _available_places_on_slider[2]
            );
        }

        // =====================================================
        //  CLEANUP
        // =====================================================

        public void Dispose()
        {
            foreach (var op in _createdOps)
            {
                try { op?.Delete(); }
                catch { }
            }
            _createdOps.Clear();

            foreach (var op in _created_deviceOps)
            {
                try { op?.Delete(); }
                catch { }
            }
            _created_deviceOps.Clear();

            _snapshot.Apply(_snapParams);
            TxApplication.RefreshDisplay();
            _player?.ResetToDefaultSetting();
            _player?.AskUserForReset(false);
            _player?.DoOnlyUnscheduledReset(true);
            _communicator?.Dispose();
        }
    }
}