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

        // =====================================================
        //  HUMAN TASK DEFINITIONS
        // =====================================================

        private readonly HumanTask _htPiecesA = new HumanTask
        {
            Name = "Deliver Pieces A",
            Waypoints = new List<string> { "human_home_frame", "human_leave_pallet_A_1", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Pallet_pieces_A" },
                RelocateFrames = new List<string> { "fixtures_pallet_A_frame" },
                UnblankResources = new List<string>(), BlankResources = new List<string>(),
                UnlocksFlag = "piecesAAvailable"
            }}
        };

        private readonly HumanTask _htPiecesB = new HumanTask
        {
            Name = "Deliver Pieces B",
            Waypoints = new List<string> { "human_home_frame", "human_leave_pallet_B_1", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string> { "Pallet_pieces_B" },
                RelocateFrames = new List<string> { "fixtures_pallet_B_frame" },
                UnblankResources = new List<string>(), BlankResources = new List<string>(),
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
                "human_leave_crates_9", "human_leave_crates_10", "human_home_frame"
            },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 5,
                RelocateResources = new List<string> { "Crate_1", "Crate_2", "Crate_3" },
                RelocateFrames = new List<string> { "crate_low_on_line_station", "crate_middle_on_line_station", "crate_top_on_line_station" },
                UnblankResources = new List<string>(), BlankResources = new List<string>(),
                UnlocksFlag = "cratesAvailable"
            }}
        };

        // --- BATCH 1: Create boxes ---
        private readonly HumanTask _htCreateBoxesA1 = new HumanTask
        {
            Name = "Create Small Boxes A (batch 1)",
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes", "human_home_frame" },
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
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes", "human_home_frame" },
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
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(), RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_A_1", "Piece_A_2" },                               
                UnblankResources = new List<string> { "Type_A_box_cover_left_1", "Type_A_box_cover_right_1" },
                UnlocksFlag = "smallBoxesA1Closed"
            }}
        };

        private readonly HumanTask _htCloseBoxesB1 = new HumanTask
        {
            Name = "Close Small Boxes B (batch 1)",
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes", "human_home_frame" },
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(), RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_B_1", "Piece_B_2" },                                
                UnblankResources = new List<string> { "Type_B_box_cover_left_1", "Type_B_box_cover_right_1" }, 
                UnlocksFlag = "smallBoxesB1Closed"
            }}
        };

        // --- BATCH 2: Create boxes ---
        private readonly HumanTask _htCreateBoxesA2 = new HumanTask
        {
            Name = "Create Small Boxes A (batch 2)",
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes", "human_home_frame" },     
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
            Waypoints = new List<string> { "human_home_frame", "human_create_boxes", "human_home_frame" },     
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
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes", "human_home_frame" },      
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(), RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_A_3", "Piece_A_4" },                               
                UnblankResources = new List<string> { "Type_A_box_cover_left_2", "Type_A_box_cover_right_2" },
                UnlocksFlag = "smallBoxesA2Closed"
            }}
        };

        private readonly HumanTask _htCloseBoxesB2 = new HumanTask
        {
            Name = "Close Small Boxes B (batch 2)",
            Waypoints = new List<string> { "human_home_frame", "human_close_boxes", "human_home_frame" },     
            Actions = new List<HumanWaypointAction> { new HumanWaypointAction {
                WaypointIndex = 1,
                RelocateResources = new List<string>(), RelocateFrames = new List<string>(),
                BlankResources = new List<string> { "Piece_B_3", "Piece_B_4" },                                
                UnblankResources = new List<string> { "Type_B_box_cover_left_2", "Type_B_box_cover_right_2" },
                UnlocksFlag = "smallBoxesB2Closed"
            }}
        };

        private readonly HumanTask _htWait = new HumanTask
        {
            Name = "Wait",
            IsWaitTask = true,
            WaitDuration = 10.0,
            Waypoints = new List<string>(),
            Actions = new List<HumanWaypointAction>()
        };

        // === CONFIGURATION ===
        private const int NUM_ACTIONS = 13;
        private const int NUM_CRATES = 3;
        private const int MAX_STEPS = 50;
        private const double OFFSET = 200.0;
        private const double tool_change_duration = 15.0;
        private const double MAX_RAIL_LENGTH = 3000.0;
        private const double MAX_EXPECTED_TIME = 800.0;
        private const int MAX_BOXES_PER_CRATE = 4;

        // Robot poses
        private const string pp_station = "PP_station";
        private const string pp_box_in_crate_station = "PP_station_box_in_crate";
        private const string tool_change_station = "Tool_change_station";
        private const string load_crates_station = "Load_crates_station";
        private const string unload_crates_station = "Unload_crate_station";
        private const string unload_crate2_station = "Unload_crate_station";

        private const string pp_home = "PP_home";
        private const string crate_home = "Crate_home";
        private const string tool_change_home = "Tool_change_home";

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
        private bool _action0Done;   // fill boxes A batch 1
        private bool _action1Done;   // fill boxes B batch 1
        private bool _action2Done;   // put boxes A batch 1 in crate 3
        private bool _action5Done;   // crates on slider
        private bool _action6Done;   // remove crate 3
        private bool _action7Done;   // put boxes B batch 1 in crate 2
        private bool _action8Done;   // remove crate 2
        private bool _action9Done;   // fill boxes A batch 2
        private bool _action10Done;  // put boxes A batch 2 in crate 3
        private bool _action11Done;  // fill boxes B batch 2
        private bool _action12Done;  // put boxes B batch 2 in crate 2
        private double _totalRobotTime;
        private int _episodeId = 0;
        private string _currentGripper = "Crate_gripper";
        private List<int> _available_places_on_slider = new List<int> { 1, 1, 1 };

        // Frame names — batch 1
        private readonly string _pickA1 = "Pick_A_1"; private readonly string _pickA2 = "Pick_A_2";
        private readonly string _placeA1 = "Place_A_1"; private readonly string _placeA2 = "Place_A_2";
        private readonly string _pickB1 = "Pick_B_1"; private readonly string _pickB2 = "Pick_B_2";
        private readonly string _placeB1 = "Place_B_1"; private readonly string _placeB2 = "Place_B_2";
        private readonly string _pickBoxA1 = "pick_box_A_1"; private readonly string _pickBoxA2 = "pick_box_A_2";
        private readonly string _pickBoxB1 = "pick_box_B_1"; private readonly string _pickBoxB2 = "pick_box_B_2";
        private readonly string _placeBox1Crate3 = "crate_3_place1"; private readonly string _placeBox2Crate3 = "crate_3_place2";
        private readonly string _placeBox1Crate2 = "crate_2_place1"; private readonly string _placeBox2Crate2 = "crate_2_place2";

        // Frame names — batch 2
        private readonly string _pickA3 = "Pick_A_3"; private readonly string _pickA4 = "Pick_A_4";                 
        private readonly string _placeA3 = "Place_A_3"; private readonly string _placeA4 = "Place_A_4";            
        private readonly string _pickB3 = "Pick_B_3"; private readonly string _pickB4 = "Pick_B_4";              
        private readonly string _placeB3 = "Place_B_3"; private readonly string _placeB4 = "Place_B_4";            
        private readonly string _pickBoxA3 = "pick_box_A_3"; private readonly string _pickBoxA4 = "pick_box_A_4";  
        private readonly string _pickBoxB3 = "pick_box_B_3"; private readonly string _pickBoxB4 = "pick_box_B_4";   
        private readonly string _placeBox3Crate3 = "crate_3_place3"; private readonly string _placeBox4Crate3 = "crate_3_place4"; 
        private readonly string _placeBox3Crate2 = "crate_2_place3"; private readonly string _placeBox4Crate2 = "crate_2_place4";

        // Crate frames
        private readonly string _pickCrate3 = "pick_top_crate_frame";
        private readonly string _pickCrate2 = "pick_middle_crate_frame";
        private readonly string _pickCrate1 = "pick_low_crate_frame";
        private readonly string _placeCrates = "place_crate";
        private readonly string _crateLowOutfeed = "crate_low_on_table_outfeed";
        private readonly string _crate2Outfeed = "crate_middle_on_table_outfeed";
        private readonly List<string> slider_frames = new List<string> { "crate_low_on_slider_station", "crate_middle_on_slider_station", "crate_top_on_slider_station" };

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
            _action0Done = false; _action1Done = false; _action2Done = false;
            _action5Done = false; _action6Done = false; _action7Done = false; _action8Done = false;
            _action9Done = false; _action10Done = false; _action11Done = false; _action12Done = false;
            _totalRobotTime = 0.0; _episodeId++;
            _currentGripper = "Crate_gripper";
            _available_places_on_slider[0] = 1; _available_places_on_slider[1] = 1; _available_places_on_slider[2] = 1;
            _boxesInCrate3TypeA = 0; _boxesInCrate2TypeB = 0;

            _piecesAAvailable = false; _piecesBAvailable = false; _cratesAvailable = false;
            _smallBoxesA1Created = false; _smallBoxesB1Created = false;
            _smallBoxesA1Closed = false; _smallBoxesB1Closed = false;
            _smallBoxesA2Created = false; _smallBoxesB2Created = false;
            _smallBoxesA2Closed = false; _smallBoxesB2Closed = false;

            _htPiecesADone = false; _htPiecesBDone = false; _htCratesDone = false;
            _htCreateBoxesA1Done = false; _htCreateBoxesB1Done = false;
            _htCloseBoxesA1Done = false; _htCloseBoxesB1Done = false;
            _htCreateBoxesA2Done = false; _htCreateBoxesB2Done = false;
            _htCloseBoxesA2Done = false; _htCloseBoxesB2Done = false;

            _humanBusy = false; _humanWaiting = false; _humanWaitRemaining = 0.0;
            _humanWaypointIndex = 0; _humanProgress = 0.0; _currentHumanTask = null;

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

            // Batch 1 create: no precondition
            if (!_htCreateBoxesA1Done) feasible.Add(_htCreateBoxesA1);
            if (!_htCreateBoxesB1Done) feasible.Add(_htCreateBoxesB1);

            // Batch 1 close: requires robot filled them
            if (!_htCloseBoxesA1Done && _action0Done) feasible.Add(_htCloseBoxesA1);
            if (!_htCloseBoxesB1Done && _action1Done) feasible.Add(_htCloseBoxesB1);

            // Batch 2 create: requires batch 1 placed in crate
            if (!_htCreateBoxesA2Done && _action2Done) feasible.Add(_htCreateBoxesA2);
            if (!_htCreateBoxesB2Done && _action7Done) feasible.Add(_htCreateBoxesB2);

            // Batch 2 close: requires robot filled them
            if (!_htCloseBoxesA2Done && _action9Done) feasible.Add(_htCloseBoxesA2);
            if (!_htCloseBoxesB2Done && _action11Done) feasible.Add(_htCloseBoxesB2);

            feasible.Add(_htWait);

            _currentHumanTask = feasible[_humanRng.Next(feasible.Count)];
            System.Diagnostics.Trace.WriteLine($"[RL] Human selected: {_currentHumanTask.Name}");

            if (_currentHumanTask.IsWaitTask)
            { _humanBusy = true; _humanWaiting = true; _humanWaitRemaining = _currentHumanTask.WaitDuration; }
            else
            { StartHumanTask(); }
        }

        private void StartHumanTask()
        { _humanBusy = true; _humanWaiting = false; _humanWaypointIndex = 0; _humanProgress = 0.0; SetNextHumanSegment(); }

        private void SetNextHumanSegment()
        {
            _humanWaypointIndex++;
            if (_humanWaypointIndex >= _currentHumanTask.Waypoints.Count)
            {
                _humanBusy = false; _humanWaiting = false;
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
            TxVector s = _humanSegmentStart.Translation, e = _humanSegmentGoal.Translation;
            _humanPathLength = Math.Sqrt((e.X - s.X) * (e.X - s.X) + (e.Y - s.Y) * (e.Y - s.Y));
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
                    if (_currentHumanTask != null && _currentHumanTask.IsWaitTask) { _humanBusy = false; PickNextHumanTask(); }
                    else SetNextHumanSegment();
                }
                return;
            }
            if (_humanPathLength < 1.0) { PerformHumanActionAtWaypoint(); if (!_humanWaiting) SetNextHumanSegment(); return; }
            _humanProgress += _humanSpeed * dt / _humanPathLength;
            if (_humanProgress >= 1.0)
            {
                _humanProxy.AbsoluteLocation = new TxTransformation(_humanSegmentGoal);
                PerformHumanActionAtWaypoint();
                if (!_humanWaiting) SetNextHumanSegment();
            }
            else
            {
                TxVector s = _humanSegmentStart.Translation, g = _humanSegmentGoal.Translation;
                TxTransformation np = new TxTransformation(_humanSegmentGoal);
                np.Translation = new TxVector(s.X + (g.X - s.X) * _humanProgress, s.Y + (g.Y - s.Y) * _humanProgress, _humanProxy.AbsoluteLocation.Translation.Z);
                _humanProxy.AbsoluteLocation = np;
            }
        }

        private void PerformHumanActionAtWaypoint()
        {
            if (_currentHumanTask == null) return;
            bool acted = false;
            foreach (var a in _currentHumanTask.Actions)
            {
                if (a.WaypointIndex == _humanWaypointIndex)
                {
                    for (int i = 0; i < a.RelocateResources.Count; i++)
                        _robotResource.PlaceResourceAccordingToFrame(a.RelocateResources[i], a.RelocateFrames[i]);
                    foreach (string r in a.UnblankResources) _robotResource.ChangeVisibility(r, false);
                    foreach (string r in a.BlankResources) _robotResource.ChangeVisibility(r, true);
                    if (!string.IsNullOrEmpty(a.UnlocksFlag)) SetHumanFlag(a.UnlocksFlag, true);
                    acted = true;
                }
            }
            if (acted) { _humanWaiting = true; _humanWaitRemaining = WAYPOINT_PAUSE; }
        }

        private void SetHumanFlag(string f, bool v)
        {
            switch (f)
            {
                case "piecesAAvailable": _piecesAAvailable = v; break;
                case "piecesBAvailable": _piecesBAvailable = v; break;
                case "cratesAvailable": _cratesAvailable = v; break;
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
        }

        // =====================================================
        //  STEP
        // =====================================================
        public StepResult Step(int actionId)
        {
            _stepCount++;
            bool hasS = _currentGripper == "Smart_gripper";
            bool hasC = _currentGripper == "Crate_gripper";

            // FEASIBILITY CHECKS
            // 0: fill boxes A batch 1
            if (actionId == 0 && (_action0Done || !hasS || !_piecesAAvailable || !_smallBoxesA1Created))
            { UpdateDebug(0, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 1: fill boxes B batch 1
            if (actionId == 1 && (_action1Done || !hasS || !_piecesBAvailable || !_smallBoxesB1Created))
            { UpdateDebug(1, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 2: put boxes A batch 1 in crate 3 (needs closed)
            if (actionId == 2 && (!_action0Done || !_action5Done || !hasS || _action2Done || !_smallBoxesA1Closed))
            { UpdateDebug(2, -10); return new StepResult(BuildObservation(), -10, true, false); }
            // 3: mount smart gripper
            if (actionId == 3 && hasS) { UpdateDebug(3, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 4: mount crate gripper
            if (actionId == 4 && hasC) { UpdateDebug(4, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 5: crates on slider
            if (actionId == 5 && (_action5Done || !hasC || !_cratesAvailable))
            { UpdateDebug(5, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 6: remove crate 3 (needs 4 boxes: batch 1 + batch 2)
            if (actionId == 6 && (!hasC || !_action2Done || !_action10Done || _action6Done || _boxesInCrate3TypeA < MAX_BOXES_PER_CRATE))
            { UpdateDebug(6, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 7: put boxes B batch 1 in crate 2 (needs closed)
            if (actionId == 7 && (!_action1Done || !_action5Done || !hasS || _action7Done || !_smallBoxesB1Closed))
            { UpdateDebug(7, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 8: remove crate 2 (needs 4 boxes: batch 1 + batch 2)
            if (actionId == 8 && (!hasC || !_action6Done || !_action7Done || !_action12Done || _action8Done || _boxesInCrate2TypeB < MAX_BOXES_PER_CRATE))
            { UpdateDebug(8, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 9: fill boxes A batch 2
            if (actionId == 9 && (_action9Done || !hasS || !_piecesAAvailable || !_smallBoxesA2Created))
            { UpdateDebug(9, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 10: put boxes A batch 2 in crate 3 (needs closed)
            if (actionId == 10 && (!_action9Done || !_action5Done || !hasS || _action10Done || !_smallBoxesA2Closed || !_action2Done))
            { UpdateDebug(10, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 11: fill boxes B batch 2
            if (actionId == 11 && (_action11Done || !hasS || !_piecesBAvailable || !_smallBoxesB2Created))
            { UpdateDebug(11, -5); return new StepResult(BuildObservation(), -5, true, false); }
            // 12: put boxes B batch 2 in crate 2 (needs closed)
            if (actionId == 12 && (!_action11Done || !_action5Done || !hasS || _action12Done || !_smallBoxesB2Closed || !_action7Done))
            { UpdateDebug(12, -5); return new StepResult(BuildObservation(), -5, true, false); }

            try
            {
                List<string> pick = new List<string>(), place = new List<string>();
                int nOps = 0;
                string opN = "rl_op_" + _stepCount + "_" + _episodeId;
                string baseN = "base_op_" + _stepCount + "_" + _episodeId;
                string homeN = "home_op_" + _stepCount + "_" + _episodeId;

                var ppP = (double)_line.GetPoseByName(pp_station).PoseData.JointValues[0];
                var bicP = (double)_line.GetPoseByName(pp_box_in_crate_station).PoseData.JointValues[0];
                var tcP = (double)_line.GetPoseByName(tool_change_station).PoseData.JointValues[0];
                var lcP = (double)_line.GetPoseByName(load_crates_station).PoseData.JointValues[0];
                var ulP = (double)_line.GetPoseByName(unload_crates_station).PoseData.JointValues[0];

                bool chk = false; string opT = "", rPos = "", gMount = "", gUnmount = "", hPose = "";
                bool optCfg = false;
                double curY = _robot.AbsoluteLocation.Translation.Y;

                switch (actionId)
                {
                    case 0: // fill boxes A batch 1
                        pick.Add(_pickA1); pick.Add(_pickA2); place.Add(_placeA1); place.Add(_placeA2);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;
                    case 1: // fill boxes B batch 1
                        pick.Add(_pickB1); pick.Add(_pickB2); place.Add(_placeB1); place.Add(_placeB2);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;
                    case 2: // put boxes A batch 1 in crate 3
                        pick.Add(_pickBoxA1); pick.Add(_pickBoxA2); place.Add(_placeBox1Crate3); place.Add(_placeBox2Crate3);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        //_action2Done = true; 
                        _boxesInCrate3TypeA += 2;
                        break;
                    case 3:
                        opT = "tc"; gMount = "Smart_gripper"; gUnmount = "Crate_gripper"; hPose = tool_change_home;
                        if (curY != tcP) { chk = true; rPos = tool_change_station; }
                        _currentGripper = "Smart_gripper"; break;
                    case 4:
                        opT = "tc"; gUnmount = "Smart_gripper"; gMount = "Crate_gripper"; hPose = tool_change_home;
                        if (curY != tcP) { chk = true; rPos = tool_change_station; }
                        _currentGripper = "Crate_gripper"; break;
                    case 5: // crates on slider
                        pick.Add(_pickCrate3); pick.Add(_pickCrate2); pick.Add(_pickCrate1);
                        place.Add(_placeCrates); place.Add(_placeCrates); place.Add(_placeCrates);
                        nOps = 3; opT = "pp"; hPose = crate_home;
                        if (curY != lcP) { chk = true; rPos = load_crates_station; }
                        _action5Done = true; break;
                    case 6: // remove crate 3
                        pick.Add(_pickCrate3); place.Add(_crateLowOutfeed);
                        nOps = 1; opT = "pp"; hPose = crate_home;
                        if (curY != ulP) { chk = true; rPos = unload_crates_station; }
                        _action6Done = true; break;
                    case 7: // put boxes B batch 1 in crate 2
                        pick.Add(_pickBoxB1); pick.Add(_pickBoxB2); place.Add(_placeBox1Crate2); place.Add(_placeBox2Crate2);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        //_action7Done = true; 
                        _boxesInCrate2TypeB += 2;
                        break;
                    case 8: // remove crate 2
                        pick.Add(_pickCrate2); place.Add(_crate2Outfeed);
                        nOps = 1; opT = "pp"; hPose = crate_home;
                        if (curY != ulP) { chk = true; rPos = unload_crates_station; }
                        _action8Done = true; break;
                    case 9: // fill boxes A batch 2
                        pick.Add(_pickA3); pick.Add(_pickA4); place.Add(_placeA3); place.Add(_placeA4);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;
                    case 10: // put boxes A batch 2 in crate 3
                        pick.Add(_pickBoxA3); pick.Add(_pickBoxA4); place.Add(_placeBox3Crate3); place.Add(_placeBox4Crate3);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        _action10Done = true; _boxesInCrate3TypeA += 2;
                        break;
                    case 11: // fill boxes B batch 2
                        pick.Add(_pickB3); pick.Add(_pickB4); place.Add(_placeB3); place.Add(_placeB4);
                        nOps = 2; opT = "pp"; hPose = pp_home;
                        if (curY != ppP) { chk = true; rPos = pp_station; }
                        break;
                    case 12: // put boxes B batch 2 in crate 2
                        pick.Add(_pickBoxB3); pick.Add(_pickBoxB4); place.Add(_placeBox3Crate2); place.Add(_placeBox4Crate2);
                        nOps = 2; opT = "pp"; hPose = pp_home; optCfg = true;
                        if (curY != bicP) { chk = true; rPos = pp_box_in_crate_station; }
                        _action12Done = true; _boxesInCrate2TypeB += 2;
                        break;
                    default:
                        return new StepResult(BuildObservation(), -10, true, false);
                }

                double opTime = 0;
                if (opT == "pp")
                    for (int i = 0; i < pick.Count; i++)
                    {
                        opTime = ExecutePickAndPlace(pick[i], place[i], opN + "_" + i, chk, rPos, baseN + "_" + i, actionId, i, hPose, homeN + "_" + i, optCfg);
                        _totalRobotTime += opTime;
                    }
                else if (opT == "tc")
                {
                    opTime = ExecuteWait(opN, chk, rPos, baseN, gMount, gUnmount, hPose, homeN);
                    _totalRobotTime += opTime;
                }

                // Set done flags AFTER execution
                if (actionId == 0) _action0Done = true;
                if (actionId == 1) _action1Done = true;
                if (actionId == 2) _action2Done = true;
                if (actionId == 7) _action7Done = true;
                if (actionId == 9) _action9Done = true;
                if (actionId == 11) _action11Done = true;

                double reward = -opTime * 0.01;
                bool terminated = false;

                if (actionId == 8)
                {
                    reward += 10; terminated = true;
                    TxMessageBox.Show("Episode complete!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                if (actionId == 6) reward += 5;

                if (_stepCount >= MAX_STEPS) return new StepResult(BuildObservation(), reward, terminated, true);

                UpdateDebug(actionId, reward);
                return new StepResult(BuildObservation(), reward, terminated, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RL] Error: {ex.Message}");
                return new StepResult(BuildObservation(), -10, true, false);
            }
        }

        // =====================================================
        //  EXECUTE PICK AND PLACE
        // =====================================================
        private double ExecutePickAndPlace(string pickF, string placeF, string opN, bool chk, string rPos, string baseN, int aId, int it, string hPose, string homeN, bool optCfg)
        {
            TxDeviceOperation homeOp = _robotResource.HomeRobot("GoFa12", homeN, hPose, 0);
            _created_deviceOps.Add(homeOp);
            _lastSimTime = 0; _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = homeOp; _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            double hTime = _player.CurrentTime;

            double bTime = 0;
            if (chk)
            {
                var bOp = _robotResource.CreateDeviceOp("Line", baseN, rPos, 0); _created_deviceOps.Add(bOp);
                _lastSimTime = 0; _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                TxApplication.ActiveDocument.CurrentOperation = bOp; _player.Play();
                _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                bTime = _player.CurrentTime;
            }

            var myop = _robotResource.PP_op("GoFa12", _currentGripper, pickF, placeF, opN, OFFSET, hPose, optCfg);
            _createdOps.Add(myop);
            _lastSimTime = 0; _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = myop; _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);

            // Attach items to crates
            if (aId == 2) { _robotResource.AttachItem(small_boxes_A_1[it], "Crate_3"); _robotResource.AttachItem(cover_boxes_A_1[it], "Crate_3"); _robotResource.AttachItem(pieces_A_1[it], "Crate_3"); }
            if (aId == 10) { _robotResource.AttachItem(small_boxes_A_2[it], "Crate_3"); _robotResource.AttachItem(cover_boxes_A_2[it], "Crate_3"); _robotResource.AttachItem(pieces_A_2[it], "Crate_3"); }
            if (aId == 7) { _robotResource.AttachItem(small_boxes_B_1[it], "Crate_2"); _robotResource.AttachItem(cover_boxes_B_1[it], "Crate_2"); _robotResource.AttachItem(pieces_B_1[it], "Crate_2"); }
            if (aId == 12) { _robotResource.AttachItem(small_boxes_B_2[it], "Crate_2"); _robotResource.AttachItem(cover_boxes_B_2[it], "Crate_2"); _robotResource.AttachItem(pieces_B_2[it], "Crate_2"); }
            if (aId == 5) { _robotResource.PlaceResourceAccordingToFrame("Crate_" + (NUM_CRATES - it), slider_frames[it]); _available_places_on_slider[it] = 0; }
            if (aId == 6) { _robotResource.PlaceResourceAccordingToFrame("Crate_2", slider_frames[0]); _robotResource.PlaceResourceAccordingToFrame("Crate_1", slider_frames[1]); }
            if (aId == 8) { _robotResource.PlaceResourceAccordingToFrame("Crate_1", slider_frames[0]); }

            return _player.CurrentTime + bTime + hTime;
        }

        // =====================================================
        //  EXECUTE WAIT (TOOL CHANGE)
        // =====================================================
        private double ExecuteWait(string opN, bool chk, string rPos, string baseN, string gM, string gU, string hPose, string homeN)
        {
            TxDeviceOperation homeOp = _robotResource.HomeRobot("GoFa12", homeN, hPose, 0); _created_deviceOps.Add(homeOp);
            _lastSimTime = 0; _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = homeOp; _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            double hTime = _player.CurrentTime;

            double bTime = 0;
            if (chk)
            {
                var bOp = _robotResource.CreateDeviceOp("Line", baseN, rPos, 0); _created_deviceOps.Add(bOp);
                _lastSimTime = 0; _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                TxApplication.ActiveDocument.CurrentOperation = bOp; _player.Play();
                _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
                bTime = _player.CurrentTime;
            }
            _robotResource.UnMountToolGripper("GoFa12", gU, "tool_station_" + gU);
            _robotResource.MountToolGripper("GoFa12", gM, "tool_holder_offset", "BASEFRAME_" + gM, "TCPF_" + gM);
            var wOp = _robotResource.CreateDeviceOp("Line", "Wait_" + baseN, rPos, tool_change_duration); _created_deviceOps.Add(wOp);
            _lastSimTime = 0; _player.TimeIntervalReached += new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            TxApplication.ActiveDocument.CurrentOperation = wOp; _player.Play();
            _player.TimeIntervalReached -= new TxSimulationPlayer_TimeIntervalReachedEventHandler(OnTimeIntervalReached);
            return _player.CurrentTime + bTime + hTime;
        }

        // =====================================================
        //  OBSERVATION + ACTION MASK
        // =====================================================
        private ObservationPacket BuildObservation()
        {
            double rp = Math.Max(0, Math.Min(1, _robot.AbsoluteLocation.Translation.Y / MAX_RAIL_LENGTH));
            double gS = (_currentGripper == "Smart_gripper") ? 1 : 0, gC = (_currentGripper == "Crate_gripper") ? 1 : 0, gN = 1 - gS - gC;
            int cp = _available_places_on_slider.FindAll(x => x == 0).Count;
            double et = Math.Min(_totalRobotTime / MAX_EXPECTED_TIME, 1);

            // 25-element state vector
            var state = new List<double> {
                rp, gS, gC, gN,                                        // 0-3: robot
                _action0Done?1:0, _action1Done?1:0,                     // 4-5: batch 1 filled
                _action9Done?1:0, _action11Done?1:0,                    // 6-7: batch 2 filled
                _action2Done?1:0, _action7Done?1:0,                     // 8-9: batch 1 in crate
                _action10Done?1:0, _action12Done?1:0,                   // 10-11: batch 2 in crate
                (double)cp/NUM_CRATES,                                  // 12: crates on slider
                _action6Done?1:0, _action8Done?1:0,                     // 13-14: crates removed
                (double)_boxesInCrate3TypeA/MAX_BOXES_PER_CRATE,        // 15: boxes in crate 3
                (double)_boxesInCrate2TypeB/MAX_BOXES_PER_CRATE,        // 16: boxes in crate 2
                et,                                                      // 17: elapsed time
                _piecesAAvailable?1:0, _piecesBAvailable?1:0,           // 18-19: pallets
                _cratesAvailable?1:0,                                    // 20: crates on line
                _smallBoxesA1Created?1:0, _smallBoxesB1Created?1:0,     // 21-22: batch 1 boxes created
                _smallBoxesA1Closed?1:0, _smallBoxesB1Closed?1:0,       // 23-24: batch 1 boxes closed
                _smallBoxesA2Created?1:0, _smallBoxesB2Created?1:0,     // 25-26: batch 2 boxes created
                _smallBoxesA2Closed?1:0, _smallBoxesB2Closed?1:0        // 27-28: batch 2 boxes closed
            };

            bool S = _currentGripper == "Smart_gripper", C = _currentGripper == "Crate_gripper";

            var mask = new List<int> {
                (!_action0Done && S && _piecesAAvailable && _smallBoxesA1Created)?1:0,                          // 0
                (!_action1Done && S && _piecesBAvailable && _smallBoxesB1Created)?1:0,                          // 1
                (_action0Done && _action5Done && S && !_action2Done && _smallBoxesA1Closed)?1:0,                // 2
                (!S)?1:0,                                                                                       // 3
                (!C)?1:0,                                                                                       // 4
                (!_action5Done && C && _cratesAvailable)?1:0,                                                   // 5
                (C && _action2Done && _action10Done && !_action6Done && _boxesInCrate3TypeA>=MAX_BOXES_PER_CRATE)?1:0, // 6
                (_action1Done && _action5Done && S && !_action7Done && _smallBoxesB1Closed)?1:0,                // 7
                (C && _action6Done && _action7Done && _action12Done && !_action8Done && _boxesInCrate2TypeB>=MAX_BOXES_PER_CRATE)?1:0, // 8
                (!_action9Done && S && _piecesAAvailable && _smallBoxesA2Created)?1:0,                          // 9
                (_action9Done && _action5Done && _action2Done && S && !_action10Done && _smallBoxesA2Closed)?1:0, // 10
                (!_action11Done && S && _piecesBAvailable && _smallBoxesB2Created)?1:0,                         // 11
                (_action11Done && _action5Done && _action7Done && S && !_action12Done && _smallBoxesB2Closed)?1:0 // 12
            };

            return new ObservationPacket { State = state, ActionMask = mask };
        }

        private void UpdateDebug(int a, double r)
        {
            _debugPanel.UpdateState(_episodeId, _stepCount, a, r, _currentGripper, _action0Done, _action1Done, _action5Done,
                _totalRobotTime, _available_places_on_slider[0], _available_places_on_slider[1], _available_places_on_slider[2]);
        }

        public void Dispose()
        {
            foreach (var op in _createdOps) { try { op?.Delete(); } catch { } }
            _createdOps.Clear();
            foreach (var op in _created_deviceOps) { try { op?.Delete(); } catch { } }
            _created_deviceOps.Clear();
            _snapshot.Apply(_snapParams); TxApplication.RefreshDisplay();
            _player?.ResetToDefaultSetting(); _communicator?.Dispose();
        }
    }
}