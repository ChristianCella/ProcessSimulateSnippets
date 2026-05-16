using System;
using System.Collections.Generic;
using Tecnomatix.Engineering;
using System.Windows.Forms;

namespace ProcessSimulateSnippets
{
    public class HumanWaypointAction
    {
        public int WaypointIndex { get; set; }
        public List<string> RelocateResources { get; set; }
        public List<string> RelocateFrames { get; set; }
        public List<string> UnblankResources { get; set; }
        public List<string> BlankResources { get; set; }
        public string UnlocksFlag { get; set; }
        public double CustomPause { get; set; }

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

        // Human flags
        private bool _piecesAAvailable = false;
        private bool _piecesBAvailable = false;
        private bool _cratesAvailable = false;

        // Batch 1
        private bool _smallBoxesA1Created = false;
        private bool _smallBoxesB1Created = false;
        private bool _smallBoxesA1Closed = false;
        private bool _smallBoxesB1Closed = false;

        // Batch 2
        private bool _smallBoxesA2Created = false;
        private bool _smallBoxesB2Created = false;
        private bool _smallBoxesA2Closed = false;
        private bool _smallBoxesB2Closed = false;

        // Human task management
        private HumanTask _currentHumanTask;
        private bool _humanWaiting = false;
        private double _humanWaitRemaining = 0.0;
        private Random _humanRng = new Random();

        // Track one-shot human tasks
        private bool _htPiecesADone = false;
        private bool _htPiecesBDone = false;
        private bool _htCratesDone = false;
        private bool _htCreateBoxesA1Done = false;
        private bool _htCreateBoxesB1Done = false;
        private bool _htCloseBoxesA1Done = false;
        private bool _htCloseBoxesB1Done = false;
        private bool _htCreateBoxesA2Done = false;
        private bool _htCreateBoxesB2Done = false;
        private bool _htCloseBoxesA2Done = false;
        private bool _htCloseBoxesB2Done = false;

        private readonly string _humanHomeFrame = "human_home_frame";
        private const double WAYPOINT_PAUSE = 5.0;

        // Elapsed time tracking
        private double _lastDecisionEventTime = 0.0;
        private double _elapsedSinceLastDecision = 0.0;
        private double _elapsedNorm = 0.0;

        // =====================================================
        //  HUMAN TASK DEFINITIONS
        // =====================================================

        private readonly HumanTask _htInspection1 = new HumanTask
        {
            Name = "Inspection 1",
            Waypoints = new List<string> {
                "human_home_frame",
                "human_inspection_1_a",
                "human_inspection_1_b",
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction> {
                new HumanWaypointAction {
                    WaypointIndex = 1,
                    CustomPause = 15.0
                }
            }
        };

        private readonly HumanTask _htInspection2 = new HumanTask
        {
            Name = "Inspection 2",
            Waypoints = new List<string> {
                "human_home_frame",
                "human_inspection_2_a",
                "human_inspection_2_b",
                "human_inspection_2_c",
                "human_inspection_2_d",
                "human_inspection_2_e",
                "human_inspection_2_f",
                "human_inspection_2_g",
                "human_inspection_2_h",
                "human_home_frame"
            },
            Actions = new List<HumanWaypointAction> {
                new HumanWaypointAction {
                    WaypointIndex = 4,
                    CustomPause = 15.0
                }
            }
        };

        private readonly HumanTask _htPiecesA = new HumanTask
        {
            Name = "Deliver Pieces A",
            Waypoints = new List<string> { "human_home_frame", "human_leave_pallet_A_1", "human_leave_pallet_A_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Pallet_pieces_A" },
                RelocateFrames = new List<string> { "fixtures_pallet_A_frame" },
                UnblankResources = new List<string>(),
                BlankResources = new List<string>(),
                UnlocksFlag = "piecesAAvailable"
            }}
        };

        private readonly HumanTask _htPiecesB = new HumanTask
        {
            Name = "Deliver Pieces B",
            Waypoints = new List<string> { "human_home_frame", "human_leave_pallet_B_1", "human_leave_pallet_B_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Pallet_pieces_B" },
                RelocateFrames = new List<string> { "fixtures_pallet_B_frame" },
                UnblankResources = new List<string>(),
                BlankResources = new List<string>(),
                UnlocksFlag = "piecesBAvailable"
            }}
        };

        private readonly HumanTask _htCrates = new HumanTask
        {
            Name = "Deliver Crates",
            Waypoints = new List<string> {
                "human_home_frame", "human_leave_crates_1", "human_leave_crates_2",
                "human_leave_crates_3", "human_leave_crates_4", "human_leave_crates_5",
                "human_leave_crates_6", "human_leave_crates_7", "human_leave_crates_8",
                "human_leave_crates_9", "human_leave_crates_10", "human_leave_crates_11", "human_home_frame"
            },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 5,
                RelocateResources = new List<string> { "Crate_1", "Crate_2", "Crate_3" },
                RelocateFrames = new List<string> { "crate_low_on_line_station", "crate_middle_on_line_station", "crate_top_on_line_station" },
                UnblankResources = new List<string>(),
                BlankResources = new List<string>(),
                UnlocksFlag = "cratesAvailable"
            }}
        };

        // --- BATCH 1: Create boxes ---
        private readonly HumanTask _htCreateBoxesA1 = new HumanTask
        {
            Name = "Create Small Boxes A (batch 1)",
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes_1", "human_create_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Type_A_box_left_1", "Type_A_box_right_1" },
                RelocateFrames = new List<string> { "pallet_box_A_left", "pallet_box_A_right" },
                UnblankResources = new List<string> { "Type_A_box_left_1", "Type_A_box_right_1" },
                BlankResources = new List<string>(),
                UnlocksFlag = "smallBoxesA1Created"
            }}
        };

        private readonly HumanTask _htCreateBoxesB1 = new HumanTask
        {
            Name = "Create Small Boxes B (batch 1)",
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes_1", "human_create_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Type_B_box_left_1", "Type_B_box_right_1" },
                RelocateFrames = new List<string> { "pallet_box_B_left", "pallet_box_B_right" },
                UnblankResources = new List<string> { "Type_B_box_left_1", "Type_B_box_right_1" },
                BlankResources = new List<string>(),
                UnlocksFlag = "smallBoxesB1Created"
            }}
        };

        // --- BATCH 1: Close boxes ---
        private readonly HumanTask _htCloseBoxesA1 = new HumanTask
        {
            Name = "Close Small Boxes A (batch 1)",
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes_1", "human_close_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(),
                RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_A_1", "Piece_A_2" },
                UnblankResources = new List<string> { "Type_A_box_cover_left_1", "Type_A_box_cover_right_1" },
                UnlocksFlag = "smallBoxesA1Closed"
            }}
        };

        private readonly HumanTask _htCloseBoxesB1 = new HumanTask
        {
            Name = "Close Small Boxes B (batch 1)",
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes_1", "human_close_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(),
                RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_B_1", "Piece_B_2" },
                UnblankResources = new List<string> { "Type_B_box_cover_left_1", "Type_B_box_cover_right_1" },
                UnlocksFlag = "smallBoxesB1Closed"
            }}
        };

        // --- BATCH 2: Create boxes ---
        private readonly HumanTask _htCreateBoxesA2 = new HumanTask
        {
            Name = "Create Small Boxes A (batch 2)",
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes_1", "human_create_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Type_A_box_left_2", "Type_A_box_right_2" },
                RelocateFrames = new List<string> { "pallet_box_A_left", "pallet_box_A_right" },
                UnblankResources = new List<string> { "Type_A_box_left_2", "Type_A_box_right_2" },
                BlankResources = new List<string>(),
                UnlocksFlag = "smallBoxesA2Created"
            }}
        };

        private readonly HumanTask _htCreateBoxesB2 = new HumanTask
        {
            Name = "Create Small Boxes B (batch 2)",
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes_1", "human_create_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Type_B_box_left_2", "Type_B_box_right_2" },
                RelocateFrames = new List<string> { "pallet_box_B_left", "pallet_box_B_right" },
                UnblankResources = new List<string> { "Type_B_box_left_2", "Type_B_box_right_2" },
                BlankResources = new List<string>(),
                UnlocksFlag = "smallBoxesB2Created"
            }}
        };

        // --- BATCH 2: Close boxes ---
        private readonly HumanTask _htCloseBoxesA2 = new HumanTask
        {
            Name = "Close Small Boxes A (batch 2)",
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes_1", "human_close_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(),
                RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_A_3", "Piece_A_4" },
                UnblankResources = new List<string> { "Type_A_box_cover_left_2", "Type_A_box_cover_right_2" },
                UnlocksFlag = "smallBoxesA2Closed"
            }}
        };

        private readonly HumanTask _htCloseBoxesB2 = new HumanTask
        {
            Name = "Close Small Boxes B (batch 2)",
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes_1", "human_close_boxes_2", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(),
                RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_B_3", "Piece_B_4" },
                UnblankResources = new List<string> { "Type_B_box_cover_left_2", "Type_B_box_cover_right_2" },
                UnlocksFlag = "smallBoxesB2Closed"
            }}
        };

        private readonly HumanTask _htWait = new HumanTask
        {
            Name = "Wait",
            IsWaitTask = true,
            WaitDuration = 15.0,
            Waypoints = new List<string>(),
            Actions = new List<HumanWaypointAction>()
        };

        // === CONFIGURATION ===
        private const int NUM_ACTIONS = 14;
        private const int NUM_CRATES = 3;
        private const int MAX_STEPS = 80;
        private const double OFFSET = 200.0;
        private const double tool_change_duration = 15.0;
        private const double MAX_RAIL_LENGTH = 3000.0;
        private const double MAX_EXPECTED_TIME = 800.0;
        private const int MAX_BOXES_PER_CRATE = 4;

        // Workspace normalization constants
        private const double MAX_WORKSPACE_X = 5000.0;
        private const double MAX_WORKSPACE_Y = 5000.0;
        private const double MAX_DIST = 7000.0;

        // Robot poses
        private const string pp_station = "PP_station";
        private const string pp_box_in_crate_station = "PP_station_box_in_crate";
        private const string tool_change_station = "Tool_change_station";
        private const string load_crates_station = "Load_crates_station";
        private const string unload_crates_station = "Unload_crate_station";
        private const string wait_station = "Wait_station";

        private const string pp_home = "PP_home";
        private const string crate_home = "Crate_home";
        private const string tool_change_home = "Tool_change_home";
        private const string wait_home = "Wait_home";

        // Batch 1 boxes/pieces/covers
        private readonly List<string> small_boxes_A_1 = new List<string> { "Type_A_box_left_1", "Type_A_box_right_1" };
        private readonly List<string> cover_boxes_A_1 = new List<string> { "Type_A_box_cover_left_1", "Type_A_box_cover_right_1" };
        private readonly List<string> pieces_A_1 = new List<string> { "Piece_A_1", "Piece_A_2" };
        private readonly List<string> small_boxes_B_1 = new List<string> { "Type_B_box_left_1", "Type_B_box_right_1" };
        private readonly List<string> cover_boxes_B_1 = new List<string> { "Type_B_box_cover_left_1", "Type_B_box_cover_right_1" };
        private readonly List<string> pieces_B_1 = new List<string> { "Piece_B_1", "Piece_B_2" };

        // Batch 2 boxes/pieces/covers
        private readonly List<string> small_boxes_A_2 = new List<string> { "Type_A_box_left_2", "Type_A_box_right_2" };
        private readonly List<string> cover_boxes_A_2 = new List<string> { "Type_A_box_cover_left_2", "Type_A_box_cover_right_2" };
        private readonly List<string> pieces_A_2 = new List<string> { "Piece_A_3", "Piece_A_4" };
        private readonly List<string> small_boxes_B_2 = new List<string> { "Type_B_box_left_2", "Type_B_box_right_2" };
        private readonly List<string> cover_boxes_B_2 = new List<string> { "Type_B_box_cover_left_2", "Type_B_box_cover_right_2" };
        private readonly List<string> pieces_B_2 = new List<string> { "Piece_B_3", "Piece_B_4" };

        // Scene objects
        private readonly TxRobot _robot;
        private readonly TxDevice _line;
        private readonly TxResources _robotResource;
        private TxSimulationPlayer _player;
        private readonly TxSnapshot _snapshot;
        private readonly TxApplySnapshotParams _snapParams;
        private List<TxContinuousRoboticOperation> _createdOps = new List<TxContinuousRoboticOperation>();
        private List<TxDeviceOperation> _created_deviceOps = new List<TxDeviceOperation>();
        private readonly CommunicationManager _communicator;
        private readonly RequestHandler _handler;

        private int _boxesInCrate3TypeA = 0;
        private int _boxesInCrate2TypeB = 0;

        // Episode state — robot actions
        private int _stepCount;
        private bool _action0Done;
        private bool _action1Done;
        private bool _action2Done;
        private bool _action5Done;
        private bool _action6Done;
        private bool _action7Done;
        private bool _action8Done;
        private bool _action9Done;
        private bool _action10Done;
        private bool _action11Done;
        private bool _action12Done;
        private double _totalRobotTime;
        private int _episodeId = 0;
        private string _currentGripper = "Crate_gripper";

        private List<int> _available_places_on_slider = new List<int> { 1, 1, 1 };

        private int _cratesWaitingOnLine = 0;
        private int _nextCrateLoadSequenceIndex = 0;
        private int _pendingCrateLoadSequenceIndex = -1;
        private bool _crate3OnSlider = false;
        private bool _crate2OnSlider = false;
        private bool _crate1OnSlider = false;

        // Frame names — batch 1
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

        // Frame names — batch 2
        private readonly string _pickA3 = "Pick_A_3";
        private readonly string _pickA4 = "Pick_A_4";
        private readonly string _placeA3 = "Place_A_3";
        private readonly string _placeA4 = "Place_A_4";
        private readonly string _pickB3 = "Pick_B_3";
        private readonly string _pickB4 = "Pick_B_4";
        private readonly string _placeB3 = "Place_B_3";
        private readonly string _placeB4 = "Place_B_4";
        private readonly string _pickBoxA3 = "pick_box_A_3";
        private readonly string _pickBoxA4 = "pick_box_A_4";
        private readonly string _pickBoxB3 = "pick_box_B_3";
        private readonly string _pickBoxB4 = "pick_box_B_4";
        private readonly string _placeBox3Crate3 = "crate_3_place3";
        private readonly string _placeBox4Crate3 = "crate_3_place4";
        private readonly string _placeBox3Crate2 = "crate_2_place3";
        private readonly string _placeBox4Crate2 = "crate_2_place4";

        // Crate frames
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

        // =====================================================
        //  SAFETY SPEED CONTROL
        // =====================================================

        private const double DangerZoneDistance = 100.0;
        private const double CollaborativeZoneDistance = 2000.0;
        private const double DangerZoneSpeedFactor = 0.05;
        private const double CollaborativeSpeedFactor = 0.35;
        private const double NominalSpeedFactor = 1.0;

        // =====================================================
        //  CONSTRUCTOR
        // =====================================================

        public RLEnvironment(string robotName, string lineName, string humanName)
        {
            var objects = TxApplication.ActiveDocument.GetObjectsByName(robotName);
            _robot = objects[0] as TxRobot;
            var objects_line = TxApplication.ActiveDocument.GetObjectsByName(lineName);
            _line = objects_line[0] as TxDevice;
            var humanObjects = TxApplication.ActiveDocument.GetObjectsByName(humanName);
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
        }

        // =====================================================
        //  RESET
        // =====================================================

        public ObservationPacket Reset()
        {
            foreach (var op in _createdOps) { try { op?.Delete(); } catch { } }
            _createdOps.Clear();
            foreach (var op in _created_deviceOps) { try { op?.Delete(); } catch { } }
            _created_deviceOps.Clear();

            _snapshot.Apply(_snapParams);
            TxApplication.RefreshDisplay();

            _stepCount = 0;
            _action0Done = false;
            _action1Done = false;
            _action2Done = false;
            _action5Done = false;
            _action6Done = false;
            _action7Done = false;
            _action8Done = false;
            _action9Done = false;
            _action10Done = false;
            _action11Done = false;
            _action12Done = false;
            _totalRobotTime = 0.0;
            _lastDecisionEventTime = 0.0;
            _elapsedSinceLastDecision = 0.0;
            _elapsedNorm = 0.0;
            _episodeId++;
            _currentGripper = "Crate_gripper";
            _available_places_on_slider[0] = 1;
            _available_places_on_slider[1] = 1;
            _available_places_on_slider[2] = 1;
            _boxesInCrate3TypeA = 0;
            _boxesInCrate2TypeB = 0;

            _cratesWaitingOnLine = 0;
            _nextCrateLoadSequenceIndex = 0;
            _pendingCrateLoadSequenceIndex = -1;
            _crate3OnSlider = false;
            _crate2OnSlider = false;
            _crate1OnSlider = false;

            _piecesAAvailable = false;
            _piecesBAvailable = false;
            _cratesAvailable = false;
            _smallBoxesA1Created = false;
            _smallBoxesB1Created = false;
            _smallBoxesA1Closed = false;
            _smallBoxesB1Closed = false;
            _smallBoxesA2Created = false;
            _smallBoxesB2Created = false;
            _smallBoxesA2Closed = false;
            _smallBoxesB2Closed = false;

            _htPiecesADone = false;
            _htPiecesBDone = false;
            _htCratesDone = false;
            _htCreateBoxesA1Done = false;
            _htCreateBoxesB1Done = false;
            _htCloseBoxesA1Done = false;
            _htCloseBoxesB1Done = false;
            _htCreateBoxesA2Done = false;
            _htCreateBoxesB2Done = false;
            _htCloseBoxesA2Done = false;
            _htCloseBoxesB2Done = false;

            _humanBusy = false;
            _humanWaiting = false;
            _humanWaitRemaining = 0.0;
            _humanWaypointIndex = 0;
            _humanProgress = 0.0;
            _currentHumanTask = null;

            var homeObj = TxApplication.ActiveDocument.GetObjectsByName(_humanHomeFrame);
            TxFrame homeFrame = homeObj[0] as TxFrame;
            _humanProxy.AbsoluteLocation = new TxTransformation(homeFrame.AbsoluteLocation);

            PickNextHumanTask();

            _player = TxApplication.ActiveDocument.SimulationPlayer;
            _player.ResetToDefaultSetting();
            _player.AskUserForReset(false);
            _player.DoOnlyUnscheduledReset(true);

            _debugPanel.UpdateState(_episodeId, _stepCount, -1, 0.0, _currentGripper,
                _action0Done, _action1Done, _action5Done, _totalRobotTime,
                _available_places_on_slider[0], _available_places_on_slider[1], _available_places_on_slider[2]);

            return BuildObservation();
        }

        // =====================================================
        //  HUMAN TASK SELECTION
        // =====================================================

        private void PickNextHumanTask()
        {
            List<HumanTask> feasible = new List<HumanTask>();

            if (!_htPiecesADone) feasible.Add(_htPiecesA);
            if (!_htPiecesBDone) feasible.Add(_htPiecesB);
            if (!_htCratesDone) feasible.Add(_htCrates);

            if (!_htCreateBoxesA1Done) feasible.Add(_htCreateBoxesA1);
            if (!_htCreateBoxesB1Done) feasible.Add(_htCreateBoxesB1);

            if (!_htCloseBoxesA1Done && _action0Done) feasible.Add(_htCloseBoxesA1);
            if (!_htCloseBoxesB1Done && _action1Done) feasible.Add(_htCloseBoxesB1);

            if (!_htCreateBoxesA2Done && _action2Done) feasible.Add(_htCreateBoxesA2);
            if (!_htCreateBoxesB2Done && _action7Done) feasible.Add(_htCreateBoxesB2);

            if (!_htCloseBoxesA2Done && _action9Done) feasible.Add(_htCloseBoxesA2);
            if (!_htCloseBoxesB2Done && _action11Done) feasible.Add(_htCloseBoxesB2);

            // Inspection 1 only after both piece types have been filled into boxes
            if (_action0Done && _action1Done)
                feasible.Add(_htInspection1);

            // Inspection 2 only after all 3 crates are on the slider
            if (CountCratesOnSlider() >= NUM_CRATES)
                feasible.Add(_htInspection2);

            feasible.Add(_htWait); // Wait is always available


            _currentHumanTask = feasible[_humanRng.Next(feasible.Count)];
            System.Diagnostics.Trace.WriteLine($"[RL] Human selected: {_currentHumanTask.Name}");

            if (_currentHumanTask.IsWaitTask)
            {
                _humanBusy = true;
                _humanWaiting = true;
                _humanWaitRemaining = _currentHumanTask.WaitDuration;
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
            SetNextHumanSegment();
        }

        private void SetNextHumanSegment()
        {
            _humanWaypointIndex++;
            if (_humanWaypointIndex >= _currentHumanTask.Waypoints.Count)
            {
                _humanBusy = false;
                _humanWaiting = false;

                if (_currentHumanTask == _htPiecesA) _htPiecesADone = true;
                if (_currentHumanTask == _htPiecesB) _htPiecesBDone = true;
                if (_currentHumanTask == _htCrates) _htCratesDone = true;
                if (_currentHumanTask == _htCreateBoxesA1) _htCreateBoxesA1Done = true;
                if (_currentHumanTask == _htCreateBoxesB1) _htCreateBoxesB1Done = true;
                if (_currentHumanTask == _htCloseBoxesA1) _htCloseBoxesA1Done = true;
                if (_currentHumanTask == _htCloseBoxesB1) _htCloseBoxesB1Done = true;
                if (_currentHumanTask == _htCreateBoxesA2) _htCreateBoxesA2Done = true;
                if (_currentHumanTask == _htCreateBoxesB2) _htCreateBoxesB2Done = true;
                if (_currentHumanTask == _htCloseBoxesA2) _htCloseBoxesA2Done = true;
                if (_currentHumanTask == _htCloseBoxesB2) _htCloseBoxesB2Done = true;

                PickNextHumanTask();
                return;
            }

            _humanSegmentStart = new TxTransformation(_humanProxy.AbsoluteLocation);
            string target = _currentHumanTask.Waypoints[_humanWaypointIndex];
            TxFrame tf = TxApplication.ActiveDocument.GetObjectsByName(target)[0] as TxFrame;
            _humanSegmentGoal = new TxTransformation(tf.AbsoluteLocation);
            TxVector s = _humanSegmentStart.Translation;
            TxVector e = _humanSegmentGoal.Translation;
            _humanPathLength = Math.Sqrt(
                (e.X - s.X) * (e.X - s.X) +
                (e.Y - s.Y) * (e.Y - s.Y));
            _humanProgress = 0.0;
        }

        private void UpdateHumanMovement(double dt)
        {
            if (!_humanBusy) return;

            if (_humanWaiting)
            {
                _humanWaitRemaining -= dt;
                if (_humanWaitRemaining <= 0)
                {
                    _humanWaiting = false;
                    if (_currentHumanTask != null && _currentHumanTask.IsWaitTask)
                    {
                        _humanBusy = false;
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
                if (!_humanWaiting) SetNextHumanSegment();
                return;
            }

            _humanProgress += _humanSpeed * dt / _humanPathLength;
            if (_humanProgress >= 1.0)
            {
                _humanProxy.AbsoluteLocation = new TxTransformation(_humanSegmentGoal);
                PerformHumanActionAtWaypoint();
                if (!_humanWaiting) SetNextHumanSegment();
            }
            else
            {
                TxVector s = _humanSegmentStart.Translation;
                TxVector g = _humanSegmentGoal.Translation;
                TxTransformation np = new TxTransformation(_humanSegmentGoal);
                np.Translation = new TxVector(
                    s.X + (g.X - s.X) * _humanProgress,
                    s.Y + (g.Y - s.Y) * _humanProgress,
                    _humanProxy.AbsoluteLocation.Translation.Z);
                _humanProxy.AbsoluteLocation = np;
            }
        }

        private void PerformHumanActionAtWaypoint()
        {
            if (_currentHumanTask == null) return;

            bool acted = false;
            double pauseDuration = 0;

            foreach (var a in _currentHumanTask.Actions)
            {
                if (a.WaypointIndex == _humanWaypointIndex)
                {
                    for (int i = 0; i < a.RelocateResources.Count; i++)
                        _robotResource.PlaceResourceAccordingToFrame(
                            a.RelocateResources[i], a.RelocateFrames[i]);

                    foreach (string r in a.UnblankResources)
                        _robotResource.ChangeVisibility(r, false);
                    foreach (string r in a.BlankResources)
                        _robotResource.ChangeVisibility(r, true);

                    if (!string.IsNullOrEmpty(a.UnlocksFlag))
                        SetHumanFlag(a.UnlocksFlag, true);

                    acted = true;
                    pauseDuration = a.CustomPause > 0 ? a.CustomPause : WAYPOINT_PAUSE;
                }
            }

            if (acted)
            {
                _humanWaiting = true;
                _humanWaitRemaining = pauseDuration;
            }
        }

        private void SetHumanFlag(string f, bool v)
        {
            switch (f)
            {
                case "piecesAAvailable":
                    _piecesAAvailable = v;
                    break;
                case "piecesBAvailable":
                    _piecesBAvailable = v;
                    break;
                case "cratesAvailable":
                    _cratesAvailable = v;
                    if (v)
                    {
                        _cratesWaitingOnLine = NUM_CRATES;
                        _nextCrateLoadSequenceIndex = 0;
                        _pendingCrateLoadSequenceIndex = -1;
                    }
                    else
                    {
                        _cratesWaitingOnLine = 0;
                        _nextCrateLoadSequenceIndex = 0;
                        _pendingCrateLoadSequenceIndex = -1;
                    }
                    break;
                case "smallBoxesA1Created": _smallBoxesA1Created = v; break;
                case "smallBoxesB1Created": _smallBoxesB1Created = v; break;
                case "smallBoxesA1Closed": _smallBoxesA1Closed = v; break;
                case "smallBoxesB1Closed": _smallBoxesB1Closed = v; break;
                case "smallBoxesA2Created": _smallBoxesA2Created = v; break;
                case "smallBoxesB2Created": _smallBoxesB2Created = v; break;
                case "smallBoxesA2Closed": _smallBoxesA2Closed = v; break;
                case "smallBoxesB2Closed": _smallBoxesB2Closed = v; break;
            }
        }

        private void OnTimeIntervalReached(object sender, TxSimulationPlayer_TimeIntervalReachedEventArgs args)
        {
            double t = args.CurrentTime;
            double dt = (t < _lastSimTime) ? t : t - _lastSimTime;
            _lastSimTime = t;

            UpdateHumanMovement(dt);

            double speedFactor = ComputeSpeedFactor();
            TxRoboticIntParam speedParam = new TxRoboticIntParam("REDUCE_SPEED", (int)(speedFactor * 100));
            _robot.SetParameter(speedParam);
        }

        // =====================================================
        //  CRATE / SLIDER HELPERS
        // =====================================================

        private int CountCratesOnSlider()
        {
            int count = 0;
            if (_crate3OnSlider) count++;
            if (_crate2OnSlider) count++;
            if (_crate1OnSlider) count++;
            return count;
        }

        private bool HasAnyCrateOnSlider()
        {
            return CountCratesOnSlider() > 0;
        }

        private int FirstAvailableSliderIndex()
        {
            for (int i = 0; i < _available_places_on_slider.Count; i++)
                if (_available_places_on_slider[i] == 1) return i;
            return -1;
        }

        private string GetCrateNameForLoadSequenceIndex(int sequenceIndex)
        {
            switch (sequenceIndex)
            {
                case 0: return "Crate_3";
                case 1: return "Crate_2";
                case 2: return "Crate_1";
                default: return null;
            }
        }

        private string GetPickFrameForLoadSequenceIndex(int sequenceIndex)
        {
            switch (sequenceIndex)
            {
                case 0: return _pickCrate3;
                case 1: return _pickCrate2;
                case 2: return _pickCrate1;
                default: return null;
            }
        }

        private void SetCrateOnSlider(string crateName, bool value)
        {
            if (crateName == "Crate_3") _crate3OnSlider = value;
            else if (crateName == "Crate_2") _crate2OnSlider = value;
            else if (crateName == "Crate_1") _crate1OnSlider = value;
        }

        private void RefreshLoadedCratePoseOnSlider(int loadSequenceIndex)
        {
            string crateName = GetCrateNameForLoadSequenceIndex(loadSequenceIndex);
            if (string.IsNullOrEmpty(crateName)) return;

            int slot = FirstAvailableSliderIndex();
            if (slot < 0) return;

            _robotResource.PlaceResourceAccordingToFrame(crateName, slider_frames[slot]);
            _available_places_on_slider[slot] = 0;
            SetCrateOnSlider(crateName, true);

            _cratesWaitingOnLine = Math.Max(0, _cratesWaitingOnLine - 1);
            _nextCrateLoadSequenceIndex++;
            _action5Done = HasAnyCrateOnSlider();
        }

        private void RefreshAllCratePosesOnSliderAfterRemoval()
        {
            for (int i = 0; i < _available_places_on_slider.Count; i++)
                _available_places_on_slider[i] = 1;

            int slot = 0;

            if (_crate3OnSlider && slot < slider_frames.Count)
            {
                _robotResource.PlaceResourceAccordingToFrame("Crate_3", slider_frames[slot]);
                _available_places_on_slider[slot] = 0;
                slot++;
            }

            if (_crate2OnSlider && slot < slider_frames.Count)
            {
                _robotResource.PlaceResourceAccordingToFrame("Crate_2", slider_frames[slot]);
                _available_places_on_slider[slot] = 0;
                slot++;
            }

            if (_crate1OnSlider && slot < slider_frames.Count)
            {
                _robotResource.PlaceResourceAccordingToFrame("Crate_1", slider_frames[slot]);
                _available_places_on_slider[slot] = 0;
                slot++;
            }

            _action5Done = HasAnyCrateOnSlider();
        }

        // =====================================================
        //  STEP
        // =====================================================

        public StepResult Step(int actionId)
        {
            _stepCount++;

            // Compute elapsed time since last decision event
            _elapsedSinceLastDecision = _totalRobotTime - _lastDecisionEventTime;
            _lastDecisionEventTime = _totalRobotTime;
            _elapsedNorm = Math.Min(_elapsedSinceLastDecision / MAX_EXPECTED_TIME, 1.0);

            bool hasS = _currentGripper == "Smart_gripper";
            bool hasC = _currentGripper == "Crate_gripper";

            // ---- FEASIBILITY CHECKS ----
            if (actionId == 0 && (_action0Done || !hasS || !_piecesAAvailable || !_smallBoxesA1Created))
            { UpdateDebug(0, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 1 && (_action1Done || !hasS || !_piecesBAvailable || !_smallBoxesB1Created))
            { UpdateDebug(1, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 2 && (!_action0Done || !_crate3OnSlider || !hasS || _action2Done || !_smallBoxesA1Closed))
            { UpdateDebug(2, -10); return new StepResult(BuildObservation(), -10, true, false); }

            if (actionId == 3 && hasS)
            { UpdateDebug(3, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 4 && hasC)
            { UpdateDebug(4, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 5 && (!hasC || !_cratesAvailable || _cratesWaitingOnLine <= 0 || CountCratesOnSlider() >= NUM_CRATES))
            { UpdateDebug(5, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 6 && (!hasC || !_crate3OnSlider || !_action2Done || !_action10Done || _action6Done || _boxesInCrate3TypeA < MAX_BOXES_PER_CRATE))
            { UpdateDebug(6, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 7 && (!_action1Done || !_crate2OnSlider || !hasS || _action7Done || !_smallBoxesB1Closed))
            { UpdateDebug(7, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 8 && (!hasC || !_crate2OnSlider || !_action6Done || !_action7Done || !_action12Done || _action8Done || _boxesInCrate2TypeB < MAX_BOXES_PER_CRATE))
            { UpdateDebug(8, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 9 && (_action9Done || !hasS || !_piecesAAvailable || !_smallBoxesA2Created))
            { UpdateDebug(9, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 10 && (!_action9Done || !_crate3OnSlider || !_action2Done || !hasS || _action10Done || !_smallBoxesA2Closed))
            { UpdateDebug(10, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 11 && (_action11Done || !hasS || !_piecesBAvailable || !_smallBoxesB2Created))
            { UpdateDebug(11, -5); return new StepResult(BuildObservation(), -5, true, false); }

            if (actionId == 12 && (!_action11Done || !_crate2OnSlider || !_action7Done || !hasS || _action12Done || !_smallBoxesB2Closed))
            { UpdateDebug(12, -5); return new StepResult(BuildObservation(), -5, true, false); }

            try
            {
                List<string> pick = new List<string>();
                List<string> place = new List<string>();
                int nOps = 0;
                string opN = "rl_op_" + _stepCount + "_" + _episodeId;
                string baseN = "base_op_" + _stepCount + "_" + _episodeId;
                string homeN = "home_op_" + _stepCount + "_" + _episodeId;

                var ppP = (double)_line.GetPoseByName(pp_station).PoseData.JointValues[0];
                var bicP = (double)_line.GetPoseByName(pp_box_in_crate_station).PoseData.JointValues[0];
                var tcP = (double)_line.GetPoseByName(tool_change_station).PoseData.JointValues[0];
                var lcP = (double)_line.GetPoseByName(load_crates_station).PoseData.JointValues[0];
                var ulP = (double)_line.GetPoseByName(unload_crates_station).PoseData.JointValues[0];

                bool chk = false;
                string opT = "";
                string rPos = "";
                string gMount = "";
                string gUnmount = "";
                string hPose = "";
                bool optCfg = false;
                double curY = _robot.AbsoluteLocation.Translation.Y;

                _pendingCrateLoadSequenceIndex = -1;

                switch (actionId)
                {
                    case 0:
                        pick.Add(_pickA1); pick.Add(_pickA2);
                        place.Add(_placeA1); place.Add(_placeA2);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;

                    case 1:
                        pick.Add(_pickB1); pick.Add(_pickB2);
                        place.Add(_placeB1); place.Add(_placeB2);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;

                    case 2:
                        pick.Add(_pickBoxA1); pick.Add(_pickBoxA2);
                        place.Add(_placeBox1Crate3); place.Add(_placeBox2Crate3);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        _boxesInCrate3TypeA += 2;
                        break;

                    case 3:
                        opT = "tc"; gMount = "Smart_gripper"; gUnmount = "Crate_gripper";
                        hPose = tool_change_home;
                        if (curY != tcP) { chk = true; rPos = tool_change_station; }
                        _currentGripper = "Smart_gripper";
                        break;

                    case 4:
                        opT = "tc"; gUnmount = "Smart_gripper"; gMount = "Crate_gripper";
                        hPose = tool_change_home;
                        if (curY != tcP) { chk = true; rPos = tool_change_station; }
                        _currentGripper = "Crate_gripper";
                        break;

                    case 5:
                        _pendingCrateLoadSequenceIndex = _nextCrateLoadSequenceIndex;
                        pick.Add(GetPickFrameForLoadSequenceIndex(_pendingCrateLoadSequenceIndex));
                        place.Add(_placeCrates);
                        nOps = 1; opT = "pp"; hPose = crate_home;
                        if (curY != lcP) { chk = true; rPos = load_crates_station; }
                        break;

                    case 6:
                        pick.Add(_pickCrate3);
                        place.Add(_crateLowOutfeed);
                        nOps = 1; opT = "pp"; hPose = crate_home;
                        if (curY != ulP) { chk = true; rPos = unload_crates_station; }
                        // NOTE: _action6Done set after execution below
                        break;

                    case 7:
                        pick.Add(_pickBoxB1); pick.Add(_pickBoxB2);
                        place.Add(_placeBox1Crate2); place.Add(_placeBox2Crate2);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        _boxesInCrate2TypeB += 2;
                        break;

                    case 8:
                        pick.Add(_pickCrate2);
                        place.Add(_crate2Outfeed);
                        nOps = 1; opT = "pp"; hPose = crate_home;
                        if (curY != ulP) { chk = true; rPos = unload_crates_station; }
                        // NOTE: _action8Done set after execution below
                        break;

                    case 9:
                        pick.Add(_pickA3); pick.Add(_pickA4);
                        place.Add(_placeA3); place.Add(_placeA4);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;

                    case 10:
                        pick.Add(_pickBoxA3); pick.Add(_pickBoxA4);
                        place.Add(_placeBox3Crate3); place.Add(_placeBox4Crate3);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        _boxesInCrate3TypeA += 2;
                        break;

                    case 11:
                        pick.Add(_pickB3); pick.Add(_pickB4);
                        place.Add(_placeB3); place.Add(_placeB4);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;

                    case 12:
                        pick.Add(_pickBoxB3); pick.Add(_pickBoxB4);
                        place.Add(_placeBox3Crate2); place.Add(_placeBox4Crate2);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        _boxesInCrate2TypeB += 2;
                        break;
                    case 13: // Wait - always valid, robot holds position
                        opT = "wait";
                        hPose = wait_home; // or whatever home pose makes sense
                        break;

                    default:
                        return new StepResult(BuildObservation(), -10, true, false);
                }

                double opTime = 0;

                if (opT == "pp")
                {
                    for (int i = 0; i < pick.Count; i++)
                    {
                        opTime = ExecutePickAndPlace(
                            pick[i], place[i], opN + "_" + i,
                            chk, rPos, baseN + "_" + i,
                            actionId, i, hPose, homeN + "_" + i, optCfg);
                        _totalRobotTime += opTime;

                        
                    }
                }
                else if (opT == "tc")
                {
                    opTime = ExecuteWait(opN, chk, rPos, baseN, gMount, gUnmount, hPose, homeN);
                    _totalRobotTime += opTime;

                }

                else if (opT == "wait")
                {
                    // Create a short device wait operation
                    var waitOp = _robotResource.CreateDeviceOp("Line",
                        "Wait_" + opN, wait_station, 5.0); // 5 second wait
                    _created_deviceOps.Add(waitOp);
                    _lastSimTime = 0;
                    _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                    TxApplication.ActiveDocument.CurrentOperation = waitOp;
                    _player.Play();
                    _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                    opTime = _player.CurrentTime;
                    _totalRobotTime += opTime;
                }

                // ---- SET DONE FLAGS AFTER SUCCESSFUL EXECUTION ----
                if (actionId == 0) _action0Done = true;
                if (actionId == 1) _action1Done = true;
                if (actionId == 2) _action2Done = true;
                if (actionId == 6) _action6Done = true;  // moved here from switch
                if (actionId == 7) _action7Done = true;
                if (actionId == 8) _action8Done = true;  // moved here from switch
                if (actionId == 9) _action9Done = true;
                if (actionId == 10) _action10Done = true;
                if (actionId == 11) _action11Done = true;
                if (actionId == 12) _action12Done = true;

                // ---- COMPUTE REWARD ----
                double reward = -opTime * 0.01;

                // Tool change penalty
                if (actionId == 3 || actionId == 4)
                    reward -= 1.5; // Discourage tool changes if unnecessary

                // Progress reward for successful pick and place operations
                if (actionId == 0 || actionId == 1 || actionId == 2 || actionId == 7 ||
                    actionId == 9 || actionId == 10 || actionId == 11 || actionId == 12)
                    reward += 0.75;

                // Crate removal intermediate reward
                if (actionId == 6)
                    reward += 2.0;

                /*
                if (actionId == 13)
                    reward -= 0.1; // small extra penalty on top of time penalty
                */

                if (actionId == 13)
                {
                    double distance = GetHumanRobotDistance();
                    if (distance < CollaborativeZoneDistance)
                    {
                        // Reward waiting when human is nearby — good spatial behavior
                        double proximityBonus = 0.3 * (1.0 - distance / CollaborativeZoneDistance);
                        reward += proximityBonus;
                    }
                }

                // Final termination reward
                bool terminated = false;
                if (actionId == 8)
                {
                    reward += 15.0;
                    terminated = true;
                }

                if (_stepCount >= MAX_STEPS)
                    return new StepResult(BuildObservation(), reward, terminated, true);

                UpdateDebug(actionId, reward);
                return new StepResult(BuildObservation(), reward, terminated, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RL] Error in Step({actionId}): {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"[RL] StackTrace: {ex.StackTrace}");
                return new StepResult(BuildObservation(), -10, true, false);
            }
        }

        // =====================================================
        //  EXECUTE PICK AND PLACE
        // =====================================================

        private double ExecutePickAndPlace(
            string pickF, string placeF, string opN,
            bool chk, string rPos, string baseN,
            int aId, int it, string hPose, string homeN, bool optCfg)
        {
            // Home operation
            TxDeviceOperation homeOp = _robotResource.HomeRobot("GoFa12", homeN, hPose, 0);
            _created_deviceOps.Add(homeOp);
            _lastSimTime = 0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = homeOp;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            double hTime = _player.CurrentTime;


            // Base positioning operation
            double bTime = 0;
            if (chk)
            {
                var bOp = _robotResource.CreateDeviceOp("Line", baseN, rPos, 0);
                _created_deviceOps.Add(bOp);
                _lastSimTime = 0;
                _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                TxApplication.ActiveDocument.CurrentOperation = bOp;
                _player.Play();
                _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                bTime = _player.CurrentTime;
            }

            // Pick and place operation
            var myop = _robotResource.PP_op(
                "GoFa12", _currentGripper, pickF, placeF, opN, OFFSET, hPose, optCfg);
            _createdOps.Add(myop);
            _lastSimTime = 0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = myop;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);


            // Post-execution scene updates (only reached if no safety violation)
            if (aId == 2)
            {
                _robotResource.AttachItem(small_boxes_A_1[it], "Crate_3");
                _robotResource.AttachItem(cover_boxes_A_1[it], "Crate_3");
                _robotResource.AttachItem(pieces_A_1[it], "Crate_3");
            }

            if (aId == 10)
            {
                _robotResource.AttachItem(small_boxes_A_2[it], "Crate_3");
                _robotResource.AttachItem(cover_boxes_A_2[it], "Crate_3");
                _robotResource.AttachItem(pieces_A_2[it], "Crate_3");
            }

            if (aId == 7)
            {
                _robotResource.AttachItem(small_boxes_B_1[it], "Crate_2");
                _robotResource.AttachItem(cover_boxes_B_1[it], "Crate_2");
                _robotResource.AttachItem(pieces_B_1[it], "Crate_2");
            }

            if (aId == 12)
            {
                _robotResource.AttachItem(small_boxes_B_2[it], "Crate_2");
                _robotResource.AttachItem(cover_boxes_B_2[it], "Crate_2");
                _robotResource.AttachItem(pieces_B_2[it], "Crate_2");
            }

            if (aId == 5)
                RefreshLoadedCratePoseOnSlider(_pendingCrateLoadSequenceIndex);

            if (aId == 6)
            {
                _crate3OnSlider = false;
                RefreshAllCratePosesOnSliderAfterRemoval();
            }

            if (aId == 8)
            {
                _crate2OnSlider = false;
                RefreshAllCratePosesOnSliderAfterRemoval();
            }

            return _player.CurrentTime + bTime + hTime;
        }

        // =====================================================
        //  EXECUTE WAIT (TOOL CHANGE)
        // =====================================================

        private double ExecuteWait(
            string opN, bool chk, string rPos, string baseN,
            string gM, string gU, string hPose, string homeN)
        {
            // Home operation
            TxDeviceOperation homeOp = _robotResource.HomeRobot("GoFa12", homeN, hPose, 0);
            _created_deviceOps.Add(homeOp);
            _lastSimTime = 0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = homeOp;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            double hTime = _player.CurrentTime;

            // Base positioning operation
            double bTime = 0;
            if (chk)
            {
                var bOp = _robotResource.CreateDeviceOp("Line", baseN, rPos, 0);
                _created_deviceOps.Add(bOp);
                _lastSimTime = 0;
                _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                TxApplication.ActiveDocument.CurrentOperation = bOp;
                _player.Play();
                _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                bTime = _player.CurrentTime;

            }

            // Tool change and wait operation
            _robotResource.UnMountToolGripper("GoFa12", gU, "tool_station_" + gU);
            _robotResource.MountToolGripper(
                "GoFa12", gM, "tool_holder_offset",
                "BASEFRAME_" + gM, "TCPF_" + gM);

            var wOp = _robotResource.CreateDeviceOp(
                "Line", "Wait_" + baseN, rPos, tool_change_duration);
            _created_deviceOps.Add(wOp);
            _lastSimTime = 0;
            _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = wOp;
            _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

            return _player.CurrentTime + bTime + hTime;
        }

        // =====================================================
        //  OBSERVATION + ACTION MASK
        // =====================================================

        private ObservationPacket BuildObservation()
        {
            // Robot state
            double rp = Math.Max(0, Math.Min(1,
                _robot.AbsoluteLocation.Translation.Y / MAX_RAIL_LENGTH));
            double gS = (_currentGripper == "Smart_gripper") ? 1 : 0;
            double gC = (_currentGripper == "Crate_gripper") ? 1 : 0;
            double gN = 1 - gS - gC;

            // Line state
            int cp = _available_places_on_slider.FindAll(x => x == 0).Count;

            // Total elapsed time normalized (global episode progress)
            double et = Math.Min(_totalRobotTime / MAX_EXPECTED_TIME, 1);

            // Human state
            TxVector hPos = _humanProxy.AbsoluteLocation.Translation;
            TxVector rPosTcp = _robot.TCPF.AbsoluteLocation.Translation;

            double humanX = hPos.X / MAX_WORKSPACE_X;
            double humanY = hPos.Y / MAX_WORKSPACE_Y;
            double relX = (rPosTcp.X - hPos.X) / MAX_DIST;
            double relY = (rPosTcp.Y - hPos.Y) / MAX_DIST;
            double dist = GetHumanRobotDistance() / MAX_DIST;

            // Human intent one-hot
            double[] intentOneHot = GetHumanIntentOneHot();

            var state = new List<double>
            {
                // Robot state (4)
                rp, gS, gC, gN,

                // Action completion flags batch 1 (4)
                _action0Done  ? 1 : 0,   // pieces A batch 1 filled
                _action1Done  ? 1 : 0,   // pieces B batch 1 filled
                _action9Done  ? 1 : 0,   // pieces A batch 2 filled
                _action11Done ? 1 : 0,   // pieces B batch 2 filled

                // Box-in-crate flags (4)
                _action2Done  ? 1 : 0,   // A boxes batch 1 in crate 3
                _action7Done  ? 1 : 0,   // B boxes batch 1 in crate 2
                _action10Done ? 1 : 0,   // A boxes batch 2 in crate 3
                _action12Done ? 1 : 0,   // B boxes batch 2 in crate 2

                // Crate state (3)
                (double)cp / NUM_CRATES,
                _action6Done ? 1 : 0,
                _action8Done ? 1 : 0,

                // Fill levels (2)
                (double)_boxesInCrate3TypeA / MAX_BOXES_PER_CRATE,
                (double)_boxesInCrate2TypeB / MAX_BOXES_PER_CRATE,

                // Total elapsed time normalized (1)
                et,

                // Human delivery flags (3)
                _piecesAAvailable ? 1 : 0,
                _piecesBAvailable ? 1 : 0,
                _cratesAvailable  ? 1 : 0,

                // Small box status flags (8)
                _smallBoxesA1Created ? 1 : 0,
                _smallBoxesB1Created ? 1 : 0,
                _smallBoxesA1Closed  ? 1 : 0,
                _smallBoxesB1Closed  ? 1 : 0,
                _smallBoxesA2Created ? 1 : 0,
                _smallBoxesB2Created ? 1 : 0,
                _smallBoxesA2Closed  ? 1 : 0,
                _smallBoxesB2Closed  ? 1 : 0,

                // Crate loading detail (5)
                (double)_cratesWaitingOnLine / NUM_CRATES,
                _crate3OnSlider ? 1 : 0,
                _crate2OnSlider ? 1 : 0,
                _crate1OnSlider ? 1 : 0,
                (double)CountCratesOnSlider() / NUM_CRATES,

                // Elapsed time since last decision event (1)
                _elapsedNorm,

                // Human position and distance (5)
                humanX,
                humanY,
                relX,
                relY,
                dist
            };

            // Append 8-dimensional intent one-hot
            state.AddRange(intentOneHot);

            // Total state dimensions:
            // 4 + 4 + 4 + 3 + 2 + 1 + 3 + 8 + 5 + 1 + 5 + 8 = 48

            // Action mask
            bool S = _currentGripper == "Smart_gripper";
            bool C = _currentGripper == "Crate_gripper";

            var mask = new List<int>
            {
                (!_action0Done  && S && _piecesAAvailable && _smallBoxesA1Created) ? 1 : 0,
                (!_action1Done  && S && _piecesBAvailable && _smallBoxesB1Created) ? 1 : 0,
                (_action0Done   && _crate3OnSlider && S && !_action2Done && _smallBoxesA1Closed) ? 1 : 0,
                (!S) ? 1 : 0,
                (!C) ? 1 : 0,
                (C && _cratesAvailable && _cratesWaitingOnLine > 0 && CountCratesOnSlider() < NUM_CRATES) ? 1 : 0,
                (C && _crate3OnSlider && _action2Done && _action10Done && !_action6Done && _boxesInCrate3TypeA >= MAX_BOXES_PER_CRATE) ? 1 : 0,
                (_action1Done   && _crate2OnSlider && S && !_action7Done && _smallBoxesB1Closed) ? 1 : 0,
                (C && _crate2OnSlider && _action6Done && _action7Done && _action12Done && !_action8Done && _boxesInCrate2TypeB >= MAX_BOXES_PER_CRATE) ? 1 : 0,
                (!_action9Done  && S && _piecesAAvailable && _smallBoxesA2Created) ? 1 : 0,
                (_action9Done   && _crate3OnSlider && _action2Done && S && !_action10Done && _smallBoxesA2Closed) ? 1 : 0,
                (!_action11Done && S && _piecesBAvailable && _smallBoxesB2Created) ? 1 : 0,
                (_action11Done  && _crate2OnSlider && _action7Done && S && !_action12Done && _smallBoxesB2Closed) ? 1 : 0,
                1 // Action 13 => Wait => Always valid
            };

            return new ObservationPacket { State = state, ActionMask = mask };
        }

        // =====================================================
        //  HELPERS
        // =====================================================

        private void UpdateDebug(int a, double r)
        {
            _debugPanel.UpdateState(
                _episodeId, _stepCount, a, r, _currentGripper,
                _action0Done, _action1Done, _action5Done,
                _totalRobotTime,
                _available_places_on_slider[0],
                _available_places_on_slider[1],
                _available_places_on_slider[2]);
        }

        private double GetHumanRobotDistance()
        {
            TxVector r = _robot.TCPF.AbsoluteLocation.Translation;
            TxVector h = _humanProxy.AbsoluteLocation.Translation;
            double dx = r.X - h.X;
            double dy = r.Y - h.Y;
            double dz = r.Z - h.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private double ComputeSpeedFactor()
        {
            double distance = GetHumanRobotDistance();
            if (distance < DangerZoneDistance) return DangerZoneSpeedFactor;
            if (distance < CollaborativeZoneDistance) return CollaborativeSpeedFactor;
            return NominalSpeedFactor;
        }

        private double[] GetHumanIntentOneHot()
        {
            double[] intent = new double[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            // 0: ZonePieceA     — htPiecesA
            // 1: ZonePieceB     — htPiecesB
            // 2: ZoneSmallBoxes — create/close box tasks
            // 3: ZoneCrates     — htCrates
            // 4: PickZone1      — htInspection1
            // 5: PickZone2      — htInspection2
            // 6: StandingStill  — htWait or null
            // 7: SafeZone       — fallback

            if (_currentHumanTask == null)
            { intent[6] = 1; return intent; }

            if (_currentHumanTask == _htPiecesA) intent[0] = 1;
            else if (_currentHumanTask == _htPiecesB) intent[1] = 1;
            else if (_currentHumanTask == _htCreateBoxesA1 ||
                     _currentHumanTask == _htCreateBoxesA2 ||
                     _currentHumanTask == _htCreateBoxesB1 ||
                     _currentHumanTask == _htCreateBoxesB2 ||
                     _currentHumanTask == _htCloseBoxesA1 ||
                     _currentHumanTask == _htCloseBoxesA2 ||
                     _currentHumanTask == _htCloseBoxesB1 ||
                     _currentHumanTask == _htCloseBoxesB2) intent[2] = 1;
            else if (_currentHumanTask == _htCrates) intent[3] = 1;
            else if (_currentHumanTask == _htInspection1) intent[4] = 1;
            else if (_currentHumanTask == _htInspection2) intent[5] = 1;
            else if (_currentHumanTask == _htWait) intent[6] = 1;
            else intent[7] = 1;

            return intent;
        }

        // =====================================================
        //  DISPOSE
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
            _communicator?.Dispose();
        }
    }
}