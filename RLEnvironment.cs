using System;
using System.Collections.Generic;
using Tecnomatix.Engineering;
using System.Windows.Forms;

namespace ProcessSimulateSnippets
{
    public class RLEnvironment : IDisposable
    {
        // === DEBUG GUI ===
        private RLDebugPanel _debugPanel;

        // === HUMAN STATE ===
        private TxHuman _humanProxy;
        private bool _humanTaskDone = false;    // Has the human completed its current task?
        private bool _humanBusy = false;        // Is the human currently moving?
        private int _humanWaypointIndex = 0;    // Which waypoint the human is heading to
        private double _humanProgress = 0.0;    // 0.0 to 1.0 along current segment
        private double _humanSpeed = 200.0;    // mm per second
        private double _humanPathLength = 0.0;
        private TxTransformation _humanSegmentStart;
        private TxTransformation _humanSegmentGoal;

        // Human task: sequence of frames to visit (starts and ends at home)
        private readonly string _humanHomeFrame = "human_home_frame";
        private readonly List<string> _humanTask0Frames = new List<string>
        {
            "human_home_frame",
            "human_leave_pallet_A_1",
            "human_home_frame"
        };

        private double _lastSimTime = 0.0;

        // What the human does at each waypoint (null = just walk, otherwise an action)
        // At index 1 (after reaching "human_leave_pallet_A_1"), place the pallet
        private readonly string _palletPiecesA = "Pallet_pieces_A";
        private readonly string _palletProxyFrame = "fixtures_pallet_A_frame"; // frame where pallet gets placed

        // === CONFIGURATION ===
        private const int NUM_ACTIONS = 9;
        private const int NUM_CRATES = 3;
        private const int MAX_STEPS = 30;
        private const double OFFSET = 200.0;
        private const double tool_change_duration = 15.0;

        // Normalization constants
        private const double MAX_RAIL_LENGTH = 3000.0;
        private const double MAX_EXPECTED_TIME = 500.0; // increased for longer episodes

        // Robot poses
        private const string pp_station = "PP_station";
        private const string pp_box_in_crate_station = "PP_station_box_in_crate";
        private const string tool_change_station = "Tool_change_station";
        private const string load_crates_station = "Load_crates_station";
        private const string unload_crates_station = "Unload_crate_station";
        private const string unload_crate2_station = "Unload_crate_station"; 

        // Home poses
        private const string pp_home = "PP_home";
        private const string crate_home = "Crate_home";
        private const string tool_change_home = "Tool_change_home";

        // Small boxes
        private readonly List<string> small_boxes_A_1 = new List<string>
        {
            "Type_A_box_left_1",
            "Type_A_box_right_1"
        };

        private readonly List<string> small_boxes_B_1 = new List<string>
        {
            "Type_B_box_left_1",
            "Type_B_box_right_1"
        };

        // Pieces names
        private readonly List<string> pieces_A_1 = new List<string>
        {
            "Piece_A_1",
            "Piece_A_2"
        };

        private readonly List<string> pieces_B_1 = new List<string>
        {
            "Piece_B_1",
            "Piece_B_2"
        };


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
        private int _boxesInCrate3TypeA = 0;
        private int _boxesInCrate2TypeB = 0;
        private const int MAX_BOXES_PER_CRATE = 2;

        // === EPISODE STATE ===
        private int _stepCount;
        private bool _actionZeroDone;   // Type A pieces placed in box
        private bool _actionOneDone;    // Type B pieces placed in box
        private bool _actionTwoDone;    // Type A boxes inserted into crate 3
        private bool _actionFiveDone;   // Crates placed on slider
        private bool _actionSixDone;    // Crate 3 removed from slider
        private bool _actionSevenDone;  // Type B boxes inserted into crate 2
        private bool _actionEightDone;  // Crate 2 removed from slider
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
        private readonly string _pickBoxB1 = "pick_box_B_1"; 
        private readonly string _pickBoxB2 = "pick_box_B_2";
        private readonly string _placeBox1Crate3 = "crate_3_place1";
        private readonly string _placeBox2Crate3 = "crate_3_place2";
        private readonly string _placeBox1Crate2 = "crate_2_place1";
        private readonly string _placeBox2Crate2 = "crate_2_place2"; 
        private readonly string _pickCrate3 = "pick_top_crate_frame";
        private readonly string _pickCrate2 = "pick_middle_crate_frame";
        private readonly string _pickCrate1 = "pick_low_crate_frame";
        private readonly string _placeCrates = "place_crate";
        private readonly string _crateLowOutfeed = "crate_low_on_table_outfeed";
        private readonly string _crate2Outfeed = "crate_middle_on_table_outfeed"; 
        private readonly List<string> slider_frames = new List<string>
        {
            "crate_low_on_slider_station",
            "crate_middle_on_slider_station",
            "crate_top_on_slider_station"
        };
        private readonly string _gripperName = "Smart_gripper";

        public RLEnvironment(string robotName, string lineName, string humanName)
        {
            // Get the robot
            var objects = TxApplication.ActiveDocument.GetObjectsByName(robotName);
            if (objects.Count == 0)
                throw new Exception($"Robot '{robotName}' not found in the scene.");
            _robot = objects[0] as TxRobot;
            if (_robot == null)
                throw new Exception($"'{robotName}' exists but is not a TxRobot.");

            // Get the line device
            var objects_line = TxApplication.ActiveDocument.GetObjectsByName(lineName);
            if (objects_line.Count == 0)
                throw new Exception($"Line '{lineName}' not found in the scene.");
            _line = objects_line[0] as TxDevice;
            if (_line == null)
                throw new Exception($"'{lineName}' exists but is not a TxDevice.");

            // Get the human
            var humanObjects = TxApplication.ActiveDocument.GetObjectsByName(humanName);
            if (humanObjects.Count == 0)
                throw new Exception("Human proxy not found in the scene.");
            _humanProxy = humanObjects[0] as TxHuman;

            _robotResource = new TxResources();
            _snapshot = _robotResource.CreateSnap("RL_Initial_Conditions");
            _snapParams = _robotResource.CreateSnapPar();

            _player = TxApplication.ActiveDocument.SimulationPlayer;
            _player.ResetToDefaultSetting();
            _player.AskUserForReset(false);
            _player.DoOnlyUnscheduledReset(true);

            _communicator = new CommunicationManager("GymPort");
            _handler = new RequestHandler(this, _communicator);
            _communicator.StartListening(_handler);

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

            foreach (var op in _createdOps)
            {
                try { if (op != null) op.Delete(); }
                catch (Exception ex)
                { System.Diagnostics.Trace.WriteLine($"[RL] Failed to delete op: {ex.Message}"); }
            }
            _createdOps.Clear();

            foreach (var op in _created_deviceOps)
            {
                try { if (op != null) op.Delete(); }
                catch (Exception ex)
                { System.Diagnostics.Trace.WriteLine($"[RL] Failed to delete op: {ex.Message}"); }
            }
            _created_deviceOps.Clear();

            _snapshot.Apply(_snapParams);
            TxApplication.RefreshDisplay();

            // Reset all episode state variables
            _stepCount = 0;
            _actionZeroDone = false;
            _actionOneDone = false;
            _actionTwoDone = false;
            _actionFiveDone = false;
            _actionSixDone = false;
            _actionSevenDone = false;  // NEW
            _actionEightDone = false;  // NEW
            _totalRobotTime = 0.0;
            _episodeId++;
            _currentGripper = "Crate_gripper";
            _available_places_on_slider[0] = 1;
            _available_places_on_slider[1] = 1;
            _available_places_on_slider[2] = 1;
            _boxesInCrate3TypeA = 0;
            _boxesInCrate2TypeB = 0;  // reset properly now that it is used

            _humanTaskDone = false;
            _humanBusy = false;
            _humanWaypointIndex = 0;
            _humanProgress = 0.0;

            // Place human at home position
            var homeObj = TxApplication.ActiveDocument.GetObjectsByName(_humanHomeFrame);
            TxFrame homeFrame = homeObj[0] as TxFrame;
            TxTransformation homePos = new TxTransformation(_humanProxy.AbsoluteLocation);
            homePos.Translation = new TxVector(
                homeFrame.AbsoluteLocation.Translation.X,
                homeFrame.AbsoluteLocation.Translation.Y,
                _humanProxy.AbsoluteLocation.Translation.Z);
            _humanProxy.AbsoluteLocation = homePos;

            // Start the human task immediately
            StartHumanTask();

            _player = TxApplication.ActiveDocument.SimulationPlayer;
            _player.ResetToDefaultSetting();
            _player.AskUserForReset(false);
            _player.DoOnlyUnscheduledReset(true);

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

            // Gripper state booleans - declared once at top for use in all checks
            bool hasSmartGripper = _currentGripper == "Smart_gripper";
            bool hasCrateGripper = _currentGripper == "Crate_gripper";

            // --- FEASIBILITY CHECKS ---
            if (actionId == 0 && (_actionZeroDone || !hasSmartGripper || !_humanTaskDone))
            {
                UpdateDebug(0, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 1 && (_actionOneDone || !hasSmartGripper))
            {
                UpdateDebug(1, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 2 && (!_actionZeroDone || !_actionOneDone || !_actionFiveDone ||
                                   !hasSmartGripper || _actionTwoDone))
            {
                System.Diagnostics.Trace.WriteLine("[RL] Action 2 attempted without preconditions.");
                UpdateDebug(2, -10.0);
                return new StepResult(BuildObservation(), -10.0, true, false);
            }

            if (actionId == 3 && hasSmartGripper)
            {
                UpdateDebug(3, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 4 && hasCrateGripper)
            {
                UpdateDebug(4, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 5 && (_actionFiveDone || !hasCrateGripper))
            {
                UpdateDebug(5, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 6 && (!hasCrateGripper || !_actionTwoDone ||
                       _boxesInCrate3TypeA < MAX_BOXES_PER_CRATE ||
                       _actionSixDone)) 
            {
                System.Diagnostics.Trace.WriteLine("[RL] Action 6 attempted without preconditions.");
                UpdateDebug(6, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            // NEW: Action 7 feasibility check
            // Conditions: action 5 done, smart gripper, action 1 done, not already done
            if (actionId == 7 && (!_actionFiveDone || !hasSmartGripper ||
                                   !_actionOneDone || _actionSevenDone))
            {
                System.Diagnostics.Trace.WriteLine("[RL] Action 7 attempted without preconditions.");
                UpdateDebug(7, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            // NEW: Action 8 feasibility check
            // Conditions: action 6 done, crate gripper, action 7 done, not already done
            if (actionId == 8 && (!_actionSixDone || !hasCrateGripper ||
                                   !_actionSevenDone || _actionEightDone))
            {
                System.Diagnostics.Trace.WriteLine("[RL] Action 8 attempted without preconditions.");
                UpdateDebug(8, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            // --- EXECUTE ACTIONS ---
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

                TxPose unload_crate2_station_pose = _line.GetPoseByName(unload_crate2_station); // NEW
                var unload_crat2_stat = (double)unload_crate2_station_pose.PoseData.JointValues[0]; // NEW

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
                    case 0: // Place Type A pieces in small box
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

                    case 1: // Place Type B pieces in small box
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

                    case 2: // Insert Type A small boxes into Crate 3
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
                        _actionTwoDone = true;
                        _boxesInCrate3TypeA += n_sequential_op; // 2 boxes added
                        break;

                    case 3: // Tool change to Smart gripper
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

                    case 4: // Tool change to Crate gripper
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

                    case 5: // Place all crates on slider
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

                    case 6: // Remove Crate 3 from slider
                        pickFrames.Add(_pickCrate3);
                        placeFrames.Add(_crateLowOutfeed);
                        n_sequential_op = 1;
                        op_type = "pp";
                        home_pose = crate_home;
                        optimize_config = false;
                        if (currentY != unload_crat_stat)
                        {
                            check_pos = true;
                            rob_pos = unload_crates_station;
                        }
                        _actionSixDone = true;
                        break;

                    case 7: // Insert Type B small boxes into Crate 2
                        pickFrames.Add(_pickBoxB1);
                        pickFrames.Add(_pickBoxB2);
                        placeFrames.Add(_placeBox1Crate2);
                        placeFrames.Add(_placeBox2Crate2);
                        n_sequential_op = 2;
                        op_type = "pp";
                        home_pose = pp_home;
                        optimize_config = true;
                        if (currentY != pp_pose_rob_bic)
                        {
                            check_pos = true;
                            rob_pos = pp_box_in_crate_station;
                        }
                        _actionSevenDone = true;
                        _boxesInCrate2TypeB += n_sequential_op; // 2 boxes added
                        break;

                    case 8: // Remove Crate 2 from slider
                        pickFrames.Add(_pickCrate2);
                        placeFrames.Add(_crate2Outfeed);
                        n_sequential_op = 1;
                        op_type = "pp";
                        home_pose = crate_home;
                        optimize_config = false;
                        if (currentY != unload_crat2_stat)
                        {
                            check_pos = true;
                            rob_pos = unload_crate2_station;
                        }
                        _actionEightDone = true;
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

                // Success condition: episode ends when Crate 2 is removed
                if (actionId == 8)
                {
                    reward += 10.0; // larger bonus since this is the final goal
                    terminated = true;
                    System.Diagnostics.Trace.WriteLine("[RL] Goal reached! Crate 2 removed from slider.");
                    TxMessageBox.Show("Episode correctly terminated", "Termination",
                                      MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }

                // Intermediate reward for completing action 6
                if (actionId == 6)
                {
                    reward += 5.0;
                    System.Diagnostics.Trace.WriteLine("[RL] Crate 3 removed. Halfway done.");
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

            if (_stepCount >= MAX_STEPS)
                truncated = true;

            UpdateDebug(actionId, reward);
            return new StepResult(BuildObservation(), reward, terminated, truncated);
        }

        // =====================================================
        //  EXECUTE A PICK AND PLACE OPERATION
        // =====================================================

        private double ExecutePickAndPlace(
            string pickFrame, string placeFrame, string opName,
            bool check_pos, string rob_pos, string base_opName,
            int actionId, int it, string home_pose, string home_opName,
            bool optimize_config)
        {
            TxDeviceOperation home_op = _robotResource.HomeRobot("GoFa12", home_opName, home_pose, 0.0);
            _created_deviceOps.Add(home_op);

            _lastSimTime = 0.0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = home_op;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

            double home_pos_time = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Homing operation '{home_opName}' completed in {home_pos_time:F2}s");

            double base_pos_time = 0.0;
            if (check_pos)
            {
                TxDeviceOperation base_op = _robotResource.CreateDeviceOp("Line", base_opName, rob_pos, 0.0);
                _created_deviceOps.Add(base_op);

                _lastSimTime = 0.0;
                _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                TxApplication.ActiveDocument.CurrentOperation = base_op;
                _player.Play();
                _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

                base_pos_time = _player.CurrentTime;
                System.Diagnostics.Trace.WriteLine($"[RL] Base operation '{base_opName}' completed in {base_pos_time:F2}s");
            }

            TxContinuousRoboticOperation myop = _robotResource.PP_op(
                "GoFa12", _currentGripper, pickFrame, placeFrame,
                opName, OFFSET, home_pose, optimize_config);
            _createdOps.Add(myop);

            _lastSimTime = 0.0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = myop;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

            // Attach the Small boxes A to the crate 3
            if (actionId == 2)
            {
                _robotResource.AttachItem(small_boxes_A_1[it], "Crate_3"); // Box to crate
                _robotResource.AttachItem(pieces_A_1[it], "Crate_3"); // Piece to crate
            }

            // Attach the Small boxes B to the crate 2
            if (actionId == 7)
            {
                _robotResource.AttachItem(small_boxes_B_1[it], "Crate_2"); // Box to crate
                _robotResource.AttachItem(pieces_B_1[it], "Crate_2"); // Piece to crate
            }


            // Handle slider resource placement after action 5 (load crates)
            if (actionId == 5)
            {
                int num_crate = NUM_CRATES - it;
                string on_slider_frame = slider_frames[it];
                _robotResource.PlaceResourceAccordingToFrame("Crate_" + num_crate, on_slider_frame);
                _available_places_on_slider[it] = 0;
            }

            // Handle gravity advancement after action 6 (remove Crate 3)
            // Crate 2 falls to low position, Crate 1 falls to middle position
            if (actionId == 6)
            {
                _robotResource.PlaceResourceAccordingToFrame("Crate_2", slider_frames[0]);
                _robotResource.PlaceResourceAccordingToFrame("Crate_1", slider_frames[1]);
            }

            // Handle gravity advancement after action 8 (remove Crate 2)
            // Crate 1 falls to low position
            if (actionId == 8)
            {
                _robotResource.PlaceResourceAccordingToFrame("Crate_1", slider_frames[0]);
            }

            double timeTaken = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Robot operation '{opName}' completed in {timeTaken:F2}s");
            System.Diagnostics.Trace.WriteLine(
                $"[RL] Total operation completed in {timeTaken + base_pos_time + home_pos_time:F2}s");

            return timeTaken + base_pos_time + home_pos_time;
        }

        // =====================================================
        //  EXECUTE A WAIT OPERATION
        // =====================================================

        private double ExecuteWait(
            string opName, bool check_pos, string rob_pos, string base_opName,
            string gripper_to_mount, string gripper_to_unmount,
            string home_pose, string home_opName)
        {
            TxDeviceOperation home_op = _robotResource.HomeRobot("GoFa12", home_opName, home_pose, 0.0);
            _created_deviceOps.Add(home_op);

            _lastSimTime = 0.0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = home_op;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

            double home_pos_time = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Homing operation '{home_opName}' completed in {home_pos_time:F2}s");

            double base_pos_time = 0.0;
            if (check_pos)
            {
                TxDeviceOperation base_op = _robotResource.CreateDeviceOp("Line", base_opName, rob_pos, 0.0);
                _created_deviceOps.Add(base_op);

                _lastSimTime = 0.0;
                _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                TxApplication.ActiveDocument.CurrentOperation = base_op;
                _player.Play();
                _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

                base_pos_time = _player.CurrentTime;
                System.Diagnostics.Trace.WriteLine($"[RL] Base operation '{base_opName}' completed in {base_pos_time:F2}s");
            }

            _robotResource.UnMountToolGripper("GoFa12", gripper_to_unmount,
                "tool_station_" + gripper_to_unmount);
            _robotResource.MountToolGripper("GoFa12", gripper_to_mount, "tool_holder_offset",
                "BASEFRAME_" + gripper_to_mount, "TCPF_" + gripper_to_mount);
            TxDeviceOperation myop = _robotResource.CreateDeviceOp(
                "Line", "Wait_" + base_opName, rob_pos, tool_change_duration);
            _created_deviceOps.Add(myop);

            _lastSimTime = 0.0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = myop;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

            double timeTaken = _player.CurrentTime;
            System.Diagnostics.Trace.WriteLine($"[RL] Robot operation '{opName}' completed in {timeTaken:F2}s");
            System.Diagnostics.Trace.WriteLine(
                $"[RL] Total operation completed in {timeTaken + base_pos_time + home_pos_time:F2}s");

            return timeTaken + base_pos_time + home_pos_time;
        }

        // =====================================================
        //  OBSERVATION + ACTION MASK
        // =====================================================

        private ObservationPacket BuildObservation()
        {
            // === ROBOT STATE ===
            double railPos = _robot.AbsoluteLocation.Translation.Y / MAX_RAIL_LENGTH;
            railPos = Math.Max(0.0, Math.Min(1.0, railPos));
            double gripperSmart = (_currentGripper == "Smart_gripper") ? 1.0 : 0.0;
            double gripperCrate = (_currentGripper == "Crate_gripper") ? 1.0 : 0.0;
            double gripperNone = 1.0 - gripperSmart - gripperCrate;

            // === LINE STATE ===
            double typeADone = _actionZeroDone ? 1.0 : 0.0;
            double typeBDone = _actionOneDone ? 1.0 : 0.0;
            double boxesReady = (_actionZeroDone && _actionOneDone) ? 1.0 : 0.0;
            int cratesPlaced = _available_places_on_slider.FindAll(x => x == 0).Count;
            double cratesOnSlider = (double)cratesPlaced / NUM_CRATES;
            double boxInCrate = _actionTwoDone ? 1.0 : 0.0;
            double elapsedNorm = Math.Min(_totalRobotTime / MAX_EXPECTED_TIME, 1.0);

            // === HUMAN STATE ===
            double humanTaskComplete = _humanTaskDone ? 1.0 : 0.0;

            // === CRATE CONTENTS ===
            double boxesInCrate3A = (double)_boxesInCrate3TypeA / MAX_BOXES_PER_CRATE;
            double boxesInCrate2B = (double)_boxesInCrate2TypeB / MAX_BOXES_PER_CRATE;

            // === NEW ACTION FLAGS ===
            double crate3Removed = _actionSixDone ? 1.0 : 0.0;  // NEW
            double boxBInCrate2 = _actionSevenDone ? 1.0 : 0.0;  // NEW

            // === ASSEMBLE STATE VECTOR (14 variables) ===
            // ORDER MUST NEVER CHANGE - only append new variables at the end
            var state = new List<double>
            {
                railPos,          // 0:  robot rail position normalized
                gripperSmart,     // 1:  gripper one-hot - Smart gripper
                gripperCrate,     // 2:  gripper one-hot - Crate gripper
                gripperNone,      // 3:  gripper one-hot - No gripper
                typeADone,        // 4:  Type A pieces placed in box
                typeBDone,        // 5:  Type B pieces placed in box
                boxesReady,       // 6:  both box types complete and ready
                cratesOnSlider,   // 7:  fraction of crates placed on slider
                boxInCrate,       // 8:  Type A boxes inserted into crate 3
                elapsedNorm,      // 9:  total elapsed robot time normalized
                boxesInCrate3A,   // 10: Type A boxes in crate 3 normalized
                boxesInCrate2B,   // 11: Type B boxes in crate 2 normalized
                crate3Removed,    // 12: crate 3 has been removed from slider (NEW)
                boxBInCrate2,      // 13: Type B boxes have been inserted into crate 2 (NEW)
                humanTaskComplete // 14: human has delivered pieces A
            };

            // === ACTION MASK ===
            bool hasSmartGripper = _currentGripper == "Smart_gripper";
            bool hasCrateGripper = _currentGripper == "Crate_gripper";

            bool action6Feasible = hasCrateGripper &&
                       _actionTwoDone &&
                       (_boxesInCrate3TypeA >= MAX_BOXES_PER_CRATE) &&
                       !_actionSixDone;

            bool action7Feasible = hasSmartGripper &&
                                   _actionFiveDone &&
                                   _actionOneDone &&
                                   !_actionSevenDone;

            bool action8Feasible = hasCrateGripper &&
                                   _actionSixDone &&
                                   _actionSevenDone &&
                                   !_actionEightDone;

            var actionMask = new List<int>
            {
                (!_actionZeroDone && hasSmartGripper && _humanTaskDone) ? 1 : 0,  // Action 0
                (!_actionOneDone  && hasSmartGripper) ? 1 : 0,                 // 1
                (_actionZeroDone && _actionOneDone && _actionFiveDone &&
                 hasSmartGripper && !_actionTwoDone) ? 1 : 0,                  // 2
                !hasSmartGripper ? 1 : 0,                                       // 3
                !hasCrateGripper ? 1 : 0,                                       // 4
                (!_actionFiveDone && hasCrateGripper) ? 1 : 0,                  // 5
                action6Feasible ? 1 : 0,                                        // 6
                action7Feasible ? 1 : 0,                                        // 7 NEW
                action8Feasible ? 1 : 0                                         // 8 NEW
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

        // =====================================================
        //  HUMAN METHODS
        // =====================================================

        private void StartHumanTask()
        {
            _humanBusy = true;
            _humanTaskDone = false;
            _humanWaypointIndex = 0;
            _humanProgress = 0.0;
            SetNextHumanSegment();
        }

        private void SetNextHumanSegment()
        {
            _humanWaypointIndex++;
            if (_humanWaypointIndex >= _humanTask0Frames.Count)
            {
                // Task complete — human is back home
                _humanBusy = false;
                _humanTaskDone = true;
                System.Diagnostics.Trace.WriteLine("[RL] Human task completed.");
                return;
            }

            _humanSegmentStart = new TxTransformation(_humanProxy.AbsoluteLocation);

            string targetFrameName = _humanTask0Frames[_humanWaypointIndex];
            var obj = TxApplication.ActiveDocument.GetObjectsByName(targetFrameName);
            TxFrame targetFrame = obj[0] as TxFrame;
            _humanSegmentGoal = new TxTransformation(targetFrame.AbsoluteLocation);

            TxVector start = _humanSegmentStart.Translation;
            TxVector end = _humanSegmentGoal.Translation;
            _humanPathLength = Math.Sqrt(
                (end.X - start.X) * (end.X - start.X) +
                (end.Y - start.Y) * (end.Y - start.Y));
            _humanProgress = 0.0;

            System.Diagnostics.Trace.WriteLine($"[RL] Human heading to: {targetFrameName}");
        }

        private void UpdateHumanMovement(double deltaTime)
        {
            if (!_humanBusy) return;

            if (_humanPathLength < 1.0)
            {
                PerformHumanActionAtWaypoint();
                SetNextHumanSegment();
                return;
            }

            double distanceMoved = _humanSpeed * deltaTime;
            _humanProgress += distanceMoved / _humanPathLength;

            if (_humanProgress >= 1.0)
            {
                // Arrived at waypoint
                TxTransformation arrived = new TxTransformation(_humanProxy.AbsoluteLocation);
                arrived.Translation = new TxVector(
                    _humanSegmentGoal.Translation.X,
                    _humanSegmentGoal.Translation.Y,
                    _humanProxy.AbsoluteLocation.Translation.Z); // keep Z unchanged
                _humanProxy.AbsoluteLocation = arrived;

                PerformHumanActionAtWaypoint();
                SetNextHumanSegment();
            }
            else
            {
                // Interpolate X and Y only
                TxVector start = _humanSegmentStart.Translation;
                TxVector goal = _humanSegmentGoal.Translation;
                double x = start.X + (goal.X - start.X) * _humanProgress;
                double y = start.Y + (goal.Y - start.Y) * _humanProgress;

                TxTransformation newPos = new TxTransformation(_humanProxy.AbsoluteLocation);
                newPos.Translation = new TxVector(x, y, _humanProxy.AbsoluteLocation.Translation.Z);
                _humanProxy.AbsoluteLocation = newPos;
            }
        }

        private void PerformHumanActionAtWaypoint()
        {
            // At waypoint index 1 (human_target_1): place the pallet on the line
            if (_humanWaypointIndex == 1)
            {
                _robotResource.PlaceResourceAccordingToFrame(_palletPiecesA, _palletProxyFrame);
                System.Diagnostics.Trace.WriteLine("[RL] Human placed pallet pieces A on the line.");
            }
        }

        private void OnTimeIntervalReached(object sender, TxSimulationPlayer_TimeIntervalReachedEventArgs args)
        {
            double currentSimTime = args.CurrentTime;
            double deltaTime;

            if (currentSimTime < _lastSimTime)
                deltaTime = currentSimTime;
            else
                deltaTime = currentSimTime - _lastSimTime;

            _lastSimTime = currentSimTime;

            UpdateHumanMovement(deltaTime);
        }
    }
}