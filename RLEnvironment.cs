using System;
using System.Collections.Generic;
using Tecnomatix.Engineering;
using System.Windows.Forms;

namespace ProcessSimulateSnippets
{
    // =====================================================
    //  DATA STRUCTURES FOR HUMAN TASKS
    // =====================================================

    public class HumanWaypointAction
    {
        public int WaypointIndex { get; set; }
        public List<string> RelocateResources { get; set; }
        public List<string> RelocateFrames { get; set; }
        public List<string> UnblankResources { get; set; }
        public List<string> BlankResources { get; set; }
        public string UnlocksFlag { get; set; }

        public HumanWaypointAction()
        {
            RelocateResources = new List<string>();
            RelocateFrames = new List<string>();
            UnblankResources = new List<string>();
            BlankResources = new List<string>();
        }
    }

    public class HumanTask
    {
        public string Name { get; set; }
        public List<string> Waypoints { get; set; }
        public List<HumanWaypointAction> Actions { get; set; }
        public bool IsWaitTask { get; set; }
        public double WaitDuration { get; set; }

        public HumanTask()
        {
            Waypoints = new List<string>();
            Actions = new List<HumanWaypointAction>();
            IsWaitTask = false;
            WaitDuration = 0.0;
        }
    }

    public class RLEnvironment : IDisposable
    {
        // === DEBUG GUI ===
        private RLDebugPanel _debugPanel;

        // === HUMAN STATE ===
        private TxHuman _humanProxy;
        private bool _humanBusy = false;
        private int _humanWaypointIndex = 0;
        private double _humanProgress = 0.0;
        private double _humanSpeed = 400.0;
        private double _humanPathLength = 0.0;
        private TxTransformation _humanSegmentStart;
        private TxTransformation _humanSegmentGoal;
        private double _lastSimTime = 0.0;

        // Human flags — set by human tasks, read by robot feasibility/mask
        private bool _piecesAAvailable = false;
        private bool _piecesBAvailable = false;
        private bool _cratesAvailable = false;
        private bool _smallBoxesACreated = false;   // NEW: small boxes A placed on line
        private bool _smallBoxesBCreated = false;   // NEW: small boxes B placed on line
        private bool _smallBoxesAClosed = false;    // NEW: small boxes A closed (covers on)
        private bool _smallBoxesBClosed = false;    // NEW: small boxes B closed (covers on)

        // Human task management
        private HumanTask _currentHumanTask;
        private bool _humanWaiting = false;
        private double _humanWaitRemaining = 0.0;
        private Random _humanRng = new Random();

        // Track which one-shot tasks have been done
        private bool _humanTaskPiecesADone = false;
        private bool _humanTaskPiecesBDone = false;
        private bool _humanTaskCratesDone = false;
        private bool _humanTaskCreateBoxesADone = false;  // NEW
        private bool _humanTaskCreateBoxesBDone = false;  // NEW
        private bool _humanTaskCloseBoxesADone = false;   // NEW
        private bool _humanTaskCloseBoxesBDone = false;   // NEW

        // Human home frame
        private readonly string _humanHomeFrame = "human_home_frame";

        // Pause duration after waypoint actions
        private const double WAYPOINT_PAUSE = 5.0;

        // =====================================================
        //  HUMAN TASK DEFINITIONS
        // =====================================================

        // Task: deliver pieces A
        private readonly HumanTask _humanTaskPiecesA = new HumanTask
        {
            Name = "Deliver Pieces A",
            Waypoints = new List<string>
            {
                "human_home_frame",
                "human_leave_pallet_A_1", 
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction>
            {
                new HumanWaypointAction
                {
                    WaypointIndex = 1,
                    RelocateResources = new List<string> { "Pallet_pieces_A" },  
                    RelocateFrames = new List<string> { "fixtures_pallet_A_frame" },  
                    UnblankResources = new List<string>(),
                    BlankResources = new List<string>(),
                    UnlocksFlag = "piecesAAvailable"
                }
            }
        };

        // Task: deliver pieces B
        private readonly HumanTask _humanTaskPiecesB_def = new HumanTask
        {
            Name = "Deliver Pieces B",
            Waypoints = new List<string>
            {
                "human_home_frame",
                "human_leave_pallet_B_1",
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction>
            {
                new HumanWaypointAction
                {
                    WaypointIndex = 1,
                    RelocateResources = new List<string> { "Pallet_pieces_B" },  
                    RelocateFrames = new List<string> { "fixtures_pallet_B_frame" },
                    UnblankResources = new List<string>(),
                    BlankResources = new List<string>(),
                    UnlocksFlag = "piecesBAvailable"
                }
            }
        };

        // Task: deliver crates
        private readonly HumanTask _humanTaskCrates = new HumanTask
        {
            Name = "Deliver Crates",
            Waypoints = new List<string>
            {
                "human_home_frame",
                "human_leave_crates_1",
                "human_leave_crates_2",
                "human_leave_crates_3",
                "human_leave_crates_4",
                "human_leave_crates_5",
                "human_leave_crates_6",
                "human_leave_crates_7",
                "human_leave_crates_8",
                "human_leave_crates_9",
                "human_leave_crates_10",
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction>
            {
                new HumanWaypointAction
                {
                    WaypointIndex = 5,
                    RelocateResources = new List<string> { "Crate_1", "Crate_2", "Crate_3" },
                    RelocateFrames = new List<string> { "crate_low_on_line_station", "crate_middle_on_line_station", "crate_top_on_line_station" },
                    UnblankResources = new List<string>(),
                    BlankResources = new List<string>(),
                    UnlocksFlag = "cratesAvailable"
                }
            }
        };

        // NEW Task: create small boxes A (unblank + relocate 2 boxes)
        private readonly HumanTask _humanTaskCreateBoxesA = new HumanTask
        {
            Name = "Create Small Boxes A",
            Waypoints = new List<string>
            {
                "human_home_frame",
                "human_create_boxes",
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction>
            {
                new HumanWaypointAction
                {
                    WaypointIndex = 1,
                    RelocateResources = new List<string>
                    {
                        "Type_A_box_left_1", 
                        "Type_A_box_right_1"  
                    },
                    RelocateFrames = new List<string>
                    {
                        "pallet_box_A_left", 
                        "pallet_box_A_right" 
                    },
                    UnblankResources = new List<string>
                    {
                        "Type_A_box_left_1",
                        "Type_A_box_right_1" 
                    },
                    BlankResources = new List<string>(),
                    UnlocksFlag = "smallBoxesACreated"
                }
            }
        };

        // NEW Task: create small boxes B (unblank + relocate 2 boxes)
        private readonly HumanTask _humanTaskCreateBoxesB = new HumanTask
        {
            Name = "Create Small Boxes B",
            Waypoints = new List<string>
            {
                "human_home_frame",
                "human_create_boxes", 
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction>
            {
                new HumanWaypointAction
                {
                    WaypointIndex = 1,
                    RelocateResources = new List<string>
                    {
                        "Type_B_box_left_1",
                        "Type_B_box_right_1"
                    },
                    RelocateFrames = new List<string>
                    {
                        "pallet_box_B_left", 
                        "pallet_box_B_right" 
                    },
                    UnblankResources = new List<string>
                    {
                        "Type_B_box_left_1",  
                        "Type_B_box_right_1" 
                    },
                    BlankResources = new List<string>(),
                    UnlocksFlag = "smallBoxesBCreated"
                }
            }
        };

        // NEW Task: close small boxes A (blank pieces inside, unblank covers)
        // Can only happen after robot action 0 is done
        private readonly HumanTask _humanTaskCloseBoxesA = new HumanTask
        {
            Name = "Close Small Boxes A",
            Waypoints = new List<string>
            {
                "human_home_frame",
                "human_close_boxes",
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction>
            {
                new HumanWaypointAction
                {
                    WaypointIndex = 1,
                    RelocateResources = new List<string>(),
                    RelocateFrames = new List<string>(),
                    BlankResources = new List<string>
                    {
                        "Piece_A_1",
                        "Piece_A_2" 
                    },
                    UnblankResources = new List<string>
                    {
                        "Type_A_box_cover_left_1", 
                        "Type_A_box_cover_right_1" 
                    },
                    UnlocksFlag = "smallBoxesAClosed"
                }
            }
        };

        // NEW Task: close small boxes B (blank pieces inside, unblank covers)
        // Can only happen after robot action 1 is done
        private readonly HumanTask _humanTaskCloseBoxesB = new HumanTask
        {
            Name = "Close Small Boxes B",
            Waypoints = new List<string>
            {
                "human_home_frame",
                "human_close_boxes",  
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction>
            {
                new HumanWaypointAction
                {
                    WaypointIndex = 1,
                    RelocateResources = new List<string>(),
                    RelocateFrames = new List<string>(),
                    BlankResources = new List<string>
                    {
                        "Piece_B_1",   
                        "Piece_B_2"   
                    },
                    UnblankResources = new List<string>
                    {
                        "Type_B_box_cover_left_1",  
                        "Type_B_box_cover_right_1" 
                    },
                    UnlocksFlag = "smallBoxesBClosed"
                }
            }
        };

        // Task: wait in place
        private readonly HumanTask _humanTaskWait = new HumanTask
        {
            Name = "Wait",
            IsWaitTask = true,
            WaitDuration = 10.0,
            Waypoints = new List<string>(),
            Actions = new List<HumanWaypointAction>()
        };

        // === CONFIGURATION ===
        private const int NUM_ACTIONS = 9;
        private const int NUM_CRATES = 3;
        private const int MAX_STEPS = 30;
        private const double OFFSET = 200.0;
        private const double tool_change_duration = 15.0;

        // Normalization constants
        private const double MAX_RAIL_LENGTH = 3000.0;
        private const double MAX_EXPECTED_TIME = 500.0;

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

        // Small boxes and covers
        private readonly List<string> small_boxes_A_1 = new List<string> { "Type_A_box_left_1", "Type_A_box_right_1" };
        private readonly List<string> cover_boxes_A_1 = new List<string> { "Type_A_box_cover_left_1", "Type_A_box_cover_right_1" };
        private readonly List<string> small_boxes_B_1 = new List<string> { "Type_B_box_left_1", "Type_B_box_right_1" };
        private readonly List<string> cover_boxes_B_1 = new List<string> { "Type_B_box_cover_left_1", "Type_B_box_cover_right_1" };

        // Pieces names
        private readonly List<string> pieces_A_1 = new List<string> { "Piece_A_1", "Piece_A_2" };
        private readonly List<string> pieces_B_1 = new List<string> { "Piece_B_1", "Piece_B_2" };

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
        private bool _actionZeroDone;
        private bool _actionOneDone;
        private bool _actionTwoDone;
        private bool _actionFiveDone;
        private bool _actionSixDone;
        private bool _actionSevenDone;
        private bool _actionEightDone;
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

        // =====================================================
        //  CONSTRUCTOR
        // =====================================================

        public RLEnvironment(string robotName, string lineName, string humanName)
        {
            var objects = TxApplication.ActiveDocument.GetObjectsByName(robotName);
            if (objects.Count == 0) throw new Exception($"Robot '{robotName}' not found.");
            _robot = objects[0] as TxRobot;
            if (_robot == null) throw new Exception($"'{robotName}' is not a TxRobot.");

            var objects_line = TxApplication.ActiveDocument.GetObjectsByName(lineName);
            if (objects_line.Count == 0) throw new Exception($"Line '{lineName}' not found.");
            _line = objects_line[0] as TxDevice;
            if (_line == null) throw new Exception($"'{lineName}' is not a TxDevice.");

            var humanObjects = TxApplication.ActiveDocument.GetObjectsByName(humanName);
            if (humanObjects.Count == 0) throw new Exception("Human proxy not found.");
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
            { try { if (op != null) op.Delete(); } catch { } }
            _createdOps.Clear();

            foreach (var op in _created_deviceOps)
            { try { if (op != null) op.Delete(); } catch { } }
            _created_deviceOps.Clear();

            _snapshot.Apply(_snapParams);
            TxApplication.RefreshDisplay();

            _stepCount = 0;
            _actionZeroDone = false;
            _actionOneDone = false;
            _actionTwoDone = false;
            _actionFiveDone = false;
            _actionSixDone = false;
            _actionSevenDone = false;
            _actionEightDone = false;
            _totalRobotTime = 0.0;
            _episodeId++;
            _currentGripper = "Crate_gripper";
            _available_places_on_slider[0] = 1;
            _available_places_on_slider[1] = 1;
            _available_places_on_slider[2] = 1;
            _boxesInCrate3TypeA = 0;
            _boxesInCrate2TypeB = 0;

            // Reset all human flags
            _piecesAAvailable = false;
            _piecesBAvailable = false;
            _cratesAvailable = false;
            _smallBoxesACreated = false;
            _smallBoxesBCreated = false;
            _smallBoxesAClosed = false;
            _smallBoxesBClosed = false;

            // Reset human task tracking
            _humanTaskPiecesADone = false;
            _humanTaskPiecesBDone = false;
            _humanTaskCratesDone = false;
            _humanTaskCreateBoxesADone = false;
            _humanTaskCreateBoxesBDone = false;
            _humanTaskCloseBoxesADone = false;
            _humanTaskCloseBoxesBDone = false;

            _humanBusy = false;
            _humanWaiting = false;
            _humanWaitRemaining = 0.0;
            _humanWaypointIndex = 0;
            _humanProgress = 0.0;
            _currentHumanTask = null;

            // Place human at home
            var homeObj = TxApplication.ActiveDocument.GetObjectsByName(_humanHomeFrame);
            TxFrame homeFrame = homeObj[0] as TxFrame;
            _humanProxy.AbsoluteLocation = new TxTransformation(homeFrame.AbsoluteLocation);

            PickNextHumanTask();

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
        //  HUMAN TASK SELECTION
        // =====================================================

        private void PickNextHumanTask()
        {
            List<HumanTask> feasibleTasks = new List<HumanTask>();

            // Delivery tasks (no preconditions beyond one-shot)
            if (!_humanTaskPiecesADone)
                feasibleTasks.Add(_humanTaskPiecesA);
            if (!_humanTaskPiecesBDone)
                feasibleTasks.Add(_humanTaskPiecesB_def);
            if (!_humanTaskCratesDone)
                feasibleTasks.Add(_humanTaskCrates);

            // Create boxes tasks (no preconditions beyond one-shot)
            if (!_humanTaskCreateBoxesADone)
                feasibleTasks.Add(_humanTaskCreateBoxesA);
            if (!_humanTaskCreateBoxesBDone)
                feasibleTasks.Add(_humanTaskCreateBoxesB);

            // Close boxes A: requires robot action 0 done (pieces A placed in boxes)
            if (!_humanTaskCloseBoxesADone && _actionZeroDone)
                feasibleTasks.Add(_humanTaskCloseBoxesA);

            // Close boxes B: requires robot action 1 done (pieces B placed in boxes)
            if (!_humanTaskCloseBoxesBDone && _actionOneDone)
                feasibleTasks.Add(_humanTaskCloseBoxesB);

            // Wait is always feasible
            feasibleTasks.Add(_humanTaskWait);

            int idx = _humanRng.Next(feasibleTasks.Count);
            _currentHumanTask = feasibleTasks[idx];

            System.Diagnostics.Trace.WriteLine($"[RL] Human selected task: {_currentHumanTask.Name}");

            if (_currentHumanTask.IsWaitTask)
            {
                _humanBusy = true;
                _humanWaiting = true;
                _humanWaitRemaining = _currentHumanTask.WaitDuration;
                System.Diagnostics.Trace.WriteLine($"[RL] Human waiting for {_humanWaitRemaining:F1}s");
            }
            else
            {
                StartHumanTask();
            }
        }

        private void StartHumanTask()
        {
            _humanBusy = true;
            _humanWaiting = false;
            _humanWaypointIndex = 0;
            _humanProgress = 0.0;
            System.Diagnostics.Trace.WriteLine($"[RL] Human starting task: {_currentHumanTask.Name}");
            SetNextHumanSegment();
        }

        // =====================================================
        //  HUMAN MOVEMENT
        // =====================================================

        private void SetNextHumanSegment()
        {
            _humanWaypointIndex++;
            if (_humanWaypointIndex >= _currentHumanTask.Waypoints.Count)
            {
                _humanBusy = false;
                _humanWaiting = false;

                // Mark one-shot tasks as done
                if (_currentHumanTask == _humanTaskPiecesA) _humanTaskPiecesADone = true;
                if (_currentHumanTask == _humanTaskPiecesB_def) _humanTaskPiecesBDone = true;
                if (_currentHumanTask == _humanTaskCrates) _humanTaskCratesDone = true;
                if (_currentHumanTask == _humanTaskCreateBoxesA) _humanTaskCreateBoxesADone = true;
                if (_currentHumanTask == _humanTaskCreateBoxesB) _humanTaskCreateBoxesBDone = true;
                if (_currentHumanTask == _humanTaskCloseBoxesA) _humanTaskCloseBoxesADone = true;
                if (_currentHumanTask == _humanTaskCloseBoxesB) _humanTaskCloseBoxesBDone = true;

                System.Diagnostics.Trace.WriteLine($"[RL] Human completed task: {_currentHumanTask.Name}");
                PickNextHumanTask();
                return;
            }

            _humanSegmentStart = new TxTransformation(_humanProxy.AbsoluteLocation);

            string targetFrameName = _currentHumanTask.Waypoints[_humanWaypointIndex];
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

            if (_humanWaiting)
            {
                _humanWaitRemaining -= deltaTime;
                if (_humanWaitRemaining <= 0.0)
                {
                    _humanWaiting = false;
                    if (_currentHumanTask != null && _currentHumanTask.IsWaitTask)
                    {
                        _humanBusy = false;
                        System.Diagnostics.Trace.WriteLine("[RL] Human finished waiting.");
                        PickNextHumanTask();
                    }
                    else
                    {
                        SetNextHumanSegment();
                    }
                }
                return;
            }

            if (_humanPathLength < 1.0)
            {
                PerformHumanActionAtWaypoint();
                if (!_humanWaiting)
                    SetNextHumanSegment();
                return;
            }

            double distanceMoved = _humanSpeed * deltaTime;
            _humanProgress += distanceMoved / _humanPathLength;

            if (_humanProgress >= 1.0)
            {
                _humanProxy.AbsoluteLocation = new TxTransformation(_humanSegmentGoal);
                PerformHumanActionAtWaypoint();
                if (!_humanWaiting)
                    SetNextHumanSegment();
            }
            else
            {
                TxVector start = _humanSegmentStart.Translation;
                TxVector goal = _humanSegmentGoal.Translation;
                double x = start.X + (goal.X - start.X) * _humanProgress;
                double y = start.Y + (goal.Y - start.Y) * _humanProgress;
                double z = _humanProxy.AbsoluteLocation.Translation.Z;

                TxTransformation newPos = new TxTransformation(_humanSegmentGoal);
                newPos.Translation = new TxVector(x, y, z);
                _humanProxy.AbsoluteLocation = newPos;
            }
        }

        private void PerformHumanActionAtWaypoint()
        {
            if (_currentHumanTask == null) return;

            bool actionPerformed = false;

            foreach (var action in _currentHumanTask.Actions)
            {
                if (action.WaypointIndex == _humanWaypointIndex)
                {
                    for (int i = 0; i < action.RelocateResources.Count; i++)
                    {
                        _robotResource.PlaceResourceAccordingToFrame(
                            action.RelocateResources[i], action.RelocateFrames[i]);
                        System.Diagnostics.Trace.WriteLine(
                            $"[RL] Human relocated '{action.RelocateResources[i]}' to '{action.RelocateFrames[i]}'");
                    }

                    foreach (string res in action.UnblankResources)
                    {
                        _robotResource.ChangeVisibility(res, false);
                        System.Diagnostics.Trace.WriteLine($"[RL] Human unblanked '{res}'");
                    }

                    foreach (string res in action.BlankResources)
                    {
                        _robotResource.ChangeVisibility(res, true);
                        System.Diagnostics.Trace.WriteLine($"[RL] Human blanked '{res}'");
                    }

                    if (!string.IsNullOrEmpty(action.UnlocksFlag))
                        SetHumanFlag(action.UnlocksFlag, true);

                    actionPerformed = true;
                }
            }

            if (actionPerformed)
            {
                _humanWaiting = true;
                _humanWaitRemaining = WAYPOINT_PAUSE;
                System.Diagnostics.Trace.WriteLine($"[RL] Human pausing for {WAYPOINT_PAUSE:F1}s after waypoint action.");
            }
        }

        private void SetHumanFlag(string flagName, bool value)
        {
            switch (flagName)
            {
                case "piecesAAvailable": _piecesAAvailable = value; break;
                case "piecesBAvailable": _piecesBAvailable = value; break;
                case "cratesAvailable": _cratesAvailable = value; break;
                case "smallBoxesACreated": _smallBoxesACreated = value; break;
                case "smallBoxesBCreated": _smallBoxesBCreated = value; break;
                case "smallBoxesAClosed": _smallBoxesAClosed = value; break;
                case "smallBoxesBClosed": _smallBoxesBClosed = value; break;
            }
            System.Diagnostics.Trace.WriteLine($"[RL] Flag '{flagName}' set to {value}");
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

            bool hasSmartGripper = _currentGripper == "Smart_gripper";
            bool hasCrateGripper = _currentGripper == "Crate_gripper";

            // --- FEASIBILITY CHECKS ---
            // Action 0: needs smart gripper + pieces A available + small boxes A created + not already done
            if (actionId == 0 && (_actionZeroDone || !hasSmartGripper || !_piecesAAvailable || !_smallBoxesACreated))
            {
                UpdateDebug(0, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            // Action 1: needs smart gripper + pieces B available + small boxes B created + not already done
            if (actionId == 1 && (_actionOneDone || !hasSmartGripper || !_piecesBAvailable || !_smallBoxesBCreated))
            {
                UpdateDebug(1, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            // Action 2: needs actions 0,1,5 done + smart gripper + boxes A closed + not already done
            if (actionId == 2 && (!_actionZeroDone || !_actionOneDone || !_actionFiveDone ||
                                   !hasSmartGripper || _actionTwoDone || !_smallBoxesAClosed))
            {
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

            if (actionId == 5 && (_actionFiveDone || !hasCrateGripper || !_cratesAvailable))
            {
                UpdateDebug(5, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 6 && (!hasCrateGripper || !_actionTwoDone ||
                       _boxesInCrate3TypeA < MAX_BOXES_PER_CRATE || _actionSixDone))
            {
                UpdateDebug(6, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            // Action 7: needs action 5 done + smart gripper + action 1 done + boxes B closed + not already done
            if (actionId == 7 && (!_actionFiveDone || !hasSmartGripper ||
                                   !_actionOneDone || _actionSevenDone || !_smallBoxesBClosed))
            {
                UpdateDebug(7, -5.0);
                return new StepResult(BuildObservation(), -5.0, true, false);
            }

            if (actionId == 8 && (!_actionSixDone || !hasCrateGripper ||
                                   !_actionSevenDone || _actionEightDone))
            {
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
                TxPose unload_crate2_station_pose = _line.GetPoseByName(unload_crate2_station);
                var unload_crat2_stat = (double)unload_crate2_station_pose.PoseData.JointValues[0];

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
                        pickFrames.Add(_pickA1); pickFrames.Add(_pickA2);
                        placeFrames.Add(_placeA1); placeFrames.Add(_placeA2);
                        n_sequential_op = 2; op_type = "pp"; home_pose = pp_home;
                        optimize_config = false;
                        if (currentY != pp_pose_rob) { check_pos = true; rob_pos = pp_station; }
                        //_actionZeroDone = true;
                        break;

                    case 1:
                        pickFrames.Add(_pickB1); pickFrames.Add(_pickB2);
                        placeFrames.Add(_placeB1); placeFrames.Add(_placeB2);
                        n_sequential_op = 2; op_type = "pp"; home_pose = pp_home;
                        optimize_config = false;
                        if (currentY != pp_pose_rob) { check_pos = true; rob_pos = pp_station; }
                        //_actionOneDone = true;
                        break;

                    case 2:
                        pickFrames.Add(_pickBoxA1); pickFrames.Add(_pickBoxA2);
                        placeFrames.Add(_placeBox1Crate3); placeFrames.Add(_placeBox2Crate3);
                        n_sequential_op = 2; op_type = "pp"; home_pose = pp_home;
                        optimize_config = true;
                        if (currentY != pp_pose_rob_bic) { check_pos = true; rob_pos = pp_box_in_crate_station; }
                        _actionTwoDone = true;
                        _boxesInCrate3TypeA += n_sequential_op;
                        break;

                    case 3:
                        op_type = "tc"; gripper_to_mount = "Smart_gripper"; gripper_to_unmount = "Crate_gripper";
                        home_pose = tool_change_home; optimize_config = false;
                        if (currentY != tool_chan_stat) { check_pos = true; rob_pos = tool_change_station; }
                        _currentGripper = "Smart_gripper";
                        break;

                    case 4:
                        op_type = "tc"; gripper_to_unmount = "Smart_gripper"; gripper_to_mount = "Crate_gripper";
                        home_pose = tool_change_home; optimize_config = false;
                        if (currentY != tool_chan_stat) { check_pos = true; rob_pos = tool_change_station; }
                        _currentGripper = "Crate_gripper";
                        break;

                    case 5:
                        pickFrames.Add(_pickCrate3); pickFrames.Add(_pickCrate2); pickFrames.Add(_pickCrate1);
                        placeFrames.Add(_placeCrates); placeFrames.Add(_placeCrates); placeFrames.Add(_placeCrates);
                        n_sequential_op = 3; op_type = "pp"; home_pose = crate_home;
                        optimize_config = false;
                        if (currentY != load_crat_stat) { check_pos = true; rob_pos = load_crates_station; }
                        _actionFiveDone = true;
                        break;

                    case 6:
                        pickFrames.Add(_pickCrate3); placeFrames.Add(_crateLowOutfeed);
                        n_sequential_op = 1; op_type = "pp"; home_pose = crate_home;
                        optimize_config = false;
                        if (currentY != unload_crat_stat) { check_pos = true; rob_pos = unload_crates_station; }
                        _actionSixDone = true;
                        break;

                    case 7:
                        pickFrames.Add(_pickBoxB1); pickFrames.Add(_pickBoxB2);
                        placeFrames.Add(_placeBox1Crate2); placeFrames.Add(_placeBox2Crate2);
                        n_sequential_op = 2; op_type = "pp"; home_pose = pp_home;
                        optimize_config = true;
                        if (currentY != pp_pose_rob_bic) { check_pos = true; rob_pos = pp_box_in_crate_station; }
                        _actionSevenDone = true;
                        _boxesInCrate2TypeB += n_sequential_op;
                        break;

                    case 8:
                        pickFrames.Add(_pickCrate2); placeFrames.Add(_crate2Outfeed);
                        n_sequential_op = 1; op_type = "pp"; home_pose = crate_home;
                        optimize_config = false;
                        if (currentY != unload_crat2_stat) { check_pos = true; rob_pos = unload_crate2_station; }
                        _actionEightDone = true;
                        break;

                    default:
                        UpdateDebug(0, -5.0);
                        return new StepResult(BuildObservation(), -10.0, true, false);
                }

                double opTime = 0.0;
                if (op_type == "pp")
                {
                    for (int i = 0; i < pickFrames.Count; i++)
                    {
                        opTime = ExecutePickAndPlace(
                            pickFrames[i], placeFrames[i], opName + "_" + i,
                            check_pos, rob_pos, base_opName + "_" + i,
                            actionId, i, home_pose, home_opName + "_" + i, optimize_config);
                        _totalRobotTime += opTime;
                    }
                }
                else if (op_type == "tc")
                {
                    opTime = ExecuteWait(opName, check_pos, rob_pos, base_opName,
                        gripper_to_mount, gripper_to_unmount, home_pose, home_opName);
                    _totalRobotTime += opTime;
                }

                // After the execution loop
                if (actionId == 0) _actionZeroDone = true;
                if (actionId == 1) _actionOneDone = true;

                // Reward computation
                reward = -opTime * 0.01;

                if (actionId == 8)
                {
                    reward += 10.0;
                    terminated = true;
                    System.Diagnostics.Trace.WriteLine("[RL] Goal reached! Crate 2 removed.");
                    TxMessageBox.Show("Episode correctly terminated", "Termination",
                                      MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }

                if (actionId == 6)
                {
                    reward += 5.0;
                    System.Diagnostics.Trace.WriteLine("[RL] Crate 3 removed. Halfway done.");
                }

                System.Diagnostics.Trace.WriteLine(
                    $"[RL] Op took {opTime:F2}s. Total: {_totalRobotTime:F2}s. Reward: {reward:F3}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RL] Simulation error: {ex.Message}");
                UpdateDebug(0, -5.0);
                return new StepResult(BuildObservation(), -10.0, true, false);
            }

            if (_stepCount >= MAX_STEPS) truncated = true;

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

            if (actionId == 2)
            {
                _robotResource.AttachItem(small_boxes_A_1[it], "Crate_3");
                _robotResource.AttachItem(cover_boxes_A_1[it], "Crate_3");
                _robotResource.AttachItem(pieces_A_1[it], "Crate_3");
            }
            if (actionId == 7)
            {
                _robotResource.AttachItem(small_boxes_B_1[it], "Crate_2");
                _robotResource.AttachItem(cover_boxes_B_1[it], "Crate_2");
                _robotResource.AttachItem(pieces_B_1[it], "Crate_2");
            }
            if (actionId == 5)
            {
                int num_crate = NUM_CRATES - it;
                _robotResource.PlaceResourceAccordingToFrame("Crate_" + num_crate, slider_frames[it]);
                _available_places_on_slider[it] = 0;
            }
            if (actionId == 6)
            {
                _robotResource.PlaceResourceAccordingToFrame("Crate_2", slider_frames[0]);
                _robotResource.PlaceResourceAccordingToFrame("Crate_1", slider_frames[1]);
            }
            if (actionId == 8)
            {
                _robotResource.PlaceResourceAccordingToFrame("Crate_1", slider_frames[0]);
            }

            double timeTaken = _player.CurrentTime;
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
            }

            _robotResource.UnMountToolGripper("GoFa12", gripper_to_unmount, "tool_station_" + gripper_to_unmount);
            _robotResource.MountToolGripper("GoFa12", gripper_to_mount, "tool_holder_offset",
                "BASEFRAME_" + gripper_to_mount, "TCPF_" + gripper_to_mount);
            TxDeviceOperation myop = _robotResource.CreateDeviceOp("Line", "Wait_" + base_opName, rob_pos, tool_change_duration);
            _created_deviceOps.Add(myop);

            _lastSimTime = 0.0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = myop;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

            double timeTaken = _player.CurrentTime;
            return timeTaken + base_pos_time + home_pos_time;
        }

        // =====================================================
        //  OBSERVATION + ACTION MASK
        // =====================================================

        private ObservationPacket BuildObservation()
        {
            double railPos = _robot.AbsoluteLocation.Translation.Y / MAX_RAIL_LENGTH;
            railPos = Math.Max(0.0, Math.Min(1.0, railPos));
            double gripperSmart = (_currentGripper == "Smart_gripper") ? 1.0 : 0.0;
            double gripperCrate = (_currentGripper == "Crate_gripper") ? 1.0 : 0.0;
            double gripperNone = 1.0 - gripperSmart - gripperCrate;

            double typeADone = _actionZeroDone ? 1.0 : 0.0;
            double typeBDone = _actionOneDone ? 1.0 : 0.0;
            double boxesReady = (_actionZeroDone && _actionOneDone) ? 1.0 : 0.0;
            int cratesPlaced = _available_places_on_slider.FindAll(x => x == 0).Count;
            double cratesOnSlider = (double)cratesPlaced / NUM_CRATES;
            double boxInCrate = _actionTwoDone ? 1.0 : 0.0;
            double elapsedNorm = Math.Min(_totalRobotTime / MAX_EXPECTED_TIME, 1.0);

            double boxesInCrate3A = (double)_boxesInCrate3TypeA / MAX_BOXES_PER_CRATE;
            double boxesInCrate2B = (double)_boxesInCrate2TypeB / MAX_BOXES_PER_CRATE;
            double crate3Removed = _actionSixDone ? 1.0 : 0.0;
            double boxBInCrate2 = _actionSevenDone ? 1.0 : 0.0;

            // Human flags
            double piecesAReady = _piecesAAvailable ? 1.0 : 0.0;
            double piecesBReady = _piecesBAvailable ? 1.0 : 0.0;
            double cratesReady = _cratesAvailable ? 1.0 : 0.0;
            double boxesACreated = _smallBoxesACreated ? 1.0 : 0.0;
            double boxesBCreated = _smallBoxesBCreated ? 1.0 : 0.0;
            double boxesAClosed = _smallBoxesAClosed ? 1.0 : 0.0;
            double boxesBClosed = _smallBoxesBClosed ? 1.0 : 0.0;

            // State vector: 21 elements
            var state = new List<double>
            {
                railPos,          // 0
                gripperSmart,     // 1
                gripperCrate,     // 2
                gripperNone,      // 3
                typeADone,        // 4
                typeBDone,        // 5
                boxesReady,       // 6
                cratesOnSlider,   // 7
                boxInCrate,       // 8
                elapsedNorm,      // 9
                boxesInCrate3A,   // 10
                boxesInCrate2B,   // 11
                crate3Removed,    // 12
                boxBInCrate2,     // 13
                piecesAReady,     // 14
                piecesBReady,     // 15
                cratesReady,      // 16
                boxesACreated,    // 17  NEW
                boxesBCreated,    // 18  NEW
                boxesAClosed,     // 19  NEW
                boxesBClosed      // 20  NEW
            };

            bool hasSmartGripper = _currentGripper == "Smart_gripper";
            bool hasCrateGripper = _currentGripper == "Crate_gripper";

            // Action 2: needs boxes A closed
            bool action2Feasible = _actionZeroDone && _actionOneDone && _actionFiveDone &&
                                   hasSmartGripper && !_actionTwoDone && _smallBoxesAClosed;

            bool action6Feasible = hasCrateGripper && _actionTwoDone &&
                       (_boxesInCrate3TypeA >= MAX_BOXES_PER_CRATE) && !_actionSixDone;

            // Action 7: needs boxes B closed
            bool action7Feasible = hasSmartGripper && _actionFiveDone &&
                                   _actionOneDone && !_actionSevenDone && _smallBoxesBClosed;

            bool action8Feasible = hasCrateGripper && _actionSixDone &&
                                   _actionSevenDone && !_actionEightDone;

            var actionMask = new List<int>
            {
                (!_actionZeroDone && hasSmartGripper && _piecesAAvailable && _smallBoxesACreated) ? 1 : 0,  // 0
                (!_actionOneDone && hasSmartGripper && _piecesBAvailable && _smallBoxesBCreated) ? 1 : 0,   // 1
                action2Feasible ? 1 : 0,                                                                     // 2
                !hasSmartGripper ? 1 : 0,                                                                    // 3
                !hasCrateGripper ? 1 : 0,                                                                    // 4
                (!_actionFiveDone && hasCrateGripper && _cratesAvailable) ? 1 : 0,                           // 5
                action6Feasible ? 1 : 0,                                                                     // 6
                action7Feasible ? 1 : 0,                                                                     // 7
                action8Feasible ? 1 : 0                                                                      // 8
            };

            return new ObservationPacket
            {
                State = state,
                ActionMask = actionMask
            };
        }

        // =====================================================
        //  DEBUG
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
            { try { op?.Delete(); } catch { } }
            _createdOps.Clear();

            foreach (var op in _created_deviceOps)
            { try { op?.Delete(); } catch { } }
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