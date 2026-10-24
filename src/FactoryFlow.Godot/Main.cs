using FactoryFlow.Core;
using Godot;
using System;
using System.Collections.Generic;

public partial class Main : Node2D
{
    private const double SimulationStepSeconds = 0.4;
    private const int ThroughputSampleSteps = 25;
    private const float ItemRadius = 11.0f;
    private const float MinimumWorldZoom = 0.4f;
    private const float MaximumWorldZoom = 2.0f;
    private const float ZoomFactor = 1.1f;
    private const ulong UnattendedDelayMilliseconds = 5 * 60 * 1000;
    private const int UnattendedCreditLossThreshold = 250;

    private static readonly Vector2 RawBeltStart = new(170, 320);
    private static readonly Vector2 RawJunction = new(300, 320);
    private static readonly Vector2 MachineAInput = new(430, 245);
    private static readonly Vector2 MachineAOutput = new(670, 245);
    private static readonly Vector2 IntermediateBeltStart = new(720, 320);
    private static readonly Vector2 BufferInput = new(830, 320);
    private static readonly Vector2 BufferOutput = new(970, 320);
    private static readonly Vector2 FinisherCInput = new(1000, 245);
    private static readonly Vector2 FinisherCOutput = new(1240, 245);
    private static readonly Vector2 ComponentBeltStart = new(1280, 320);
    private static readonly Vector2 ComponentBeltEnd = new(1420, 320);
    private static readonly Vector2 TransportHomePosition = new(1570, 320);
    private static readonly Vector2 TransportDestinationPosition = new(2050, 320);
    private static readonly Vector2 RemoteInputBeltStart = new(2200, 280);
    private static readonly Vector2 RemoteInputBeltEnd = new(2550, 280);
    private static readonly Vector2 RawBBeltStart = new(2180, 500);
    private static readonly Vector2 ComponentBBeltEnd = new(2550, 340);
    private static readonly Vector2 FinishedOutputBeltStart = new(2790, 320);
    private static readonly Vector2 FinishedOutputBeltEnd = new(2950, 320);

    private static readonly Rect2 HomeRegionRect = new(-100, 100, 1750, 650);
    private static readonly Rect2 WestRegionRect = new(-950, 100, 850, 650);
    private static readonly Rect2 NorthRegionRect = new(-100, -650, 1750, 750);
    private static readonly Rect2 EastRegionRect = new(1650, 100, 1600, 650);
    private static readonly Vector2[] IndirectFreightRoute =
    [
        TransportHomePosition,
        new Vector2(1640, 600),
        new Vector2(1980, 600),
        TransportDestinationPosition,
    ];
    private static readonly Vector2[] DirectFreightRoute =
    [
        TransportHomePosition,
        TransportDestinationPosition,
    ];

    private static readonly Rect2 MachineARect = new(430, 190, 240, 110);
    private static readonly Rect2 MachineBDefaultRect = new(430, 350, 240, 110);
    private static readonly Rect2 BufferRect = new(830, 260, 140, 120);
    private static readonly Rect2 FinisherCRect = new(1000, 190, 240, 110);
    private static readonly Rect2 FinisherDDefaultRect = new(1000, 350, 240, 110);
    private static readonly Rect2 ComponentStorageRect = new(1420, 255, 150, 130);
    private static readonly Rect2 AssemblerERect = new(2550, 265, 240, 110);
    private static readonly Rect2 RemoteInputStorageRect = new(2050, 255, 150, 130);
    private static readonly Rect2 SourceBRect = new(2050, 440, 130, 120);
    private static readonly Rect2 MachineFBuildArea = new(2200, 400, 540, 280);
    private static readonly Rect2 MachineBBuildArea = new(350, 330, 350, 270);
    private static readonly Rect2 FinisherDBuildArea = new(980, 330, 510, 270);
    private static readonly Vector2 ExpansionMachineFootprint = new(240, 110);
    private static readonly Vector2 MachineFFootprint = new(240, 110);

    private static readonly Color BackgroundColor = new(0.055f, 0.07f, 0.09f);
    private static readonly Color StructureColor = new(0.18f, 0.22f, 0.27f);
    private static readonly Color OutlineColor = new(0.55f, 0.62f, 0.68f);
    private static readonly Color TextColor = new(0.92f, 0.94f, 0.96f);
    private static readonly Color RawColor = new(0.20f, 0.65f, 1.0f);
    private static readonly Color IntermediateColor = new(1.0f, 0.58f, 0.16f);
    private static readonly Color ComponentColor = new(0.72f, 0.42f, 0.95f);
    private static readonly Color RawBColor = new(0.95f, 0.82f, 0.22f);
    private static readonly Color ComponentBColor = new(0.95f, 0.35f, 0.72f);
    private static readonly Color FinishedColor = new(0.35f, 0.85f, 0.48f);
    private static readonly Color BlockedColor = new(1.0f, 0.30f, 0.25f);
    private static readonly Color HomeLandColor = new(0.14f, 0.22f, 0.17f);
    private static readonly Color OpenLandColor = new(0.24f, 0.27f, 0.18f);
    private static readonly Color RockyLandColor = new(0.25f, 0.22f, 0.20f);
    private static readonly Color EastLandColor = new(0.16f, 0.23f, 0.20f);
    private static readonly Color WaterColor = new(0.08f, 0.28f, 0.42f);
    private static readonly Color HiddenLandColor = new(0.035f, 0.045f, 0.055f);
    private static readonly Color RegionBorderColor = new(0.38f, 0.47f, 0.43f);

    private FactoryState _factory = CreateFactory();

    private Button _buildBeltButton = null!;
    private Button _buildMachineButton = null!;
    private Button _buildFinisherButton = null!;
    private Button _alternateRoutingButton = null!;
    private Button _priorityRoutingButton = null!;
    private Button _dispatchButton = null!;
    private Button _autoDispatchButton = null!;
    private Button _expandShippingStorageButton = null!;
    private Button _buildDirectFreightLinkButton = null!;
    private Button _buildSecondTransportButton = null!;
    private Button _buildMachineFButton = null!;
    private Button _buildComponentBBeltButton = null!;
    private Button _resetButton = null!;
    private Button _pauseButton = null!;
    private Button _speed1Button = null!;
    private Button _speed2Button = null!;
    private Button _speed4Button = null!;
    private Button _continueButton = null!;
    private Button _revealNorthButton = null!;
    private Button _revealWestButton = null!;
    private Button _revealEastButton = null!;
    private InteractionMode _interactionMode;
    private double _stepAccumulator;
    private long _throughputSampleStartStep;
    private int _throughputSampleStartFinished;
    private double _recentFinishedPerHundredSteps;
    private bool _transportIsReturning;
    private double _transportReturnElapsed;
    private bool _transport2IsReturning;
    private double _transport2ReturnElapsed;
    private Vector2 _worldOffset = new(390, 330);
    private float _worldZoom = MinimumWorldZoom;
    private bool _isPanning;
    private SimulationSpeed _simulationSpeed = SimulationSpeed.Normal;
    private SimulationSpeed _previousRunningSpeed = SimulationSpeed.Normal;
    private bool _safetyPauseActive;
    private bool _autoDispatchEnabled = true;
    private ulong _lastDeliberateInputMilliseconds;
    private int _creditsAtLastInput;
    private int _safetyPauseCreditLoss;
    private Vector2? _machineBWorldPosition;
    private Vector2? _finisherDWorldPosition;
    private Vector2? _machineFWorldPosition;

    private Rect2 MachineBRect => new(_machineBWorldPosition ?? Vector2.Zero, ExpansionMachineFootprint);
    private Vector2 MachineBInput => new(MachineBRect.Position.X, MachineBRect.GetCenter().Y);
    private Vector2 MachineBOutput => new(MachineBRect.End.X, MachineBRect.GetCenter().Y);
    private Rect2 FinisherDRect => new(_finisherDWorldPosition ?? Vector2.Zero, ExpansionMachineFootprint);
    private Vector2 FinisherDInput => new(FinisherDRect.Position.X, FinisherDRect.GetCenter().Y);
    private Vector2 FinisherDOutput => new(FinisherDRect.End.X, FinisherDRect.GetCenter().Y);
    private Rect2 MachineFRect => new(_machineFWorldPosition ?? Vector2.Zero, MachineFFootprint);
    private Vector2 MachineFInput => new(MachineFRect.Position.X, MachineFRect.GetCenter().Y);
    private Vector2 MachineFOutput => new(MachineFRect.End.X, MachineFRect.GetCenter().Y);

    public override void _Ready()
    {
        _buildBeltButton = CreateButton("BUILD RAW BELT", new Vector2(250, 690));
        _buildBeltButton.Pressed += OnBuildBeltPressed;

        _buildMachineButton = CreateButton($"BUILD MACHINE B — {FactoryState.MachineBPurchaseCost} C", new Vector2(645, 690));
        _buildMachineButton.Disabled = true;
        _buildMachineButton.Pressed += OnBuildMachinePressed;

        _buildFinisherButton = CreateButton($"BUILD FINISHER D — {FactoryState.FinisherDPurchaseCost} C", new Vector2(1040, 690));
        _buildFinisherButton.Disabled = true;
        _buildFinisherButton.Pressed += OnBuildFinisherPressed;

        _alternateRoutingButton = CreateButton("ROUTING: ALTERNATE (NEEDS B)", new Vector2(35, 505));
        _alternateRoutingButton.Pressed += OnAlternateRoutingPressed;

        _priorityRoutingButton = CreateButton("PRIORITY A + OVERFLOW B (NEEDS B)", new Vector2(35, 570));
        _priorityRoutingButton.Pressed += OnPriorityRoutingPressed;

        _dispatchButton = CreateButton("DISPATCH FULL LOAD", new Vector2(1240, 620));
        _dispatchButton.Pressed += OnDispatchPressed;

        _autoDispatchButton = CreateButton("AUTO DISPATCH: ON", new Vector2(1240, 410));
        _autoDispatchButton.Pressed += OnAutoDispatchPressed;

        _expandShippingStorageButton = CreateButton(
            $"EXPAND SHIPPING — {FactoryState.ShippingStorageExpansionFinishedItemCost} ASSEMBLIES",
            new Vector2(1240, 550));
        _expandShippingStorageButton.Pressed += OnExpandShippingStoragePressed;

        _buildDirectFreightLinkButton = CreateButton(
            $"BUILD DIRECT FREIGHT LINK — {FactoryState.DirectFreightLinkAssemblyCost} ASSEMBLIES",
            new Vector2(1240, 480));
        _buildDirectFreightLinkButton.Pressed += OnBuildDirectFreightLinkPressed;

        _buildSecondTransportButton = CreateButton($"BUILD SECOND TRANSPORT — {FactoryState.SecondTransportPurchaseCost} C", new Vector2(830, 620));
        _buildSecondTransportButton.Pressed += OnBuildSecondTransportPressed;

        _buildMachineFButton = CreateButton($"BUILD MACHINE F — {FactoryState.MachineFPurchaseCost} C", new Vector2(830, 550));
        _buildMachineFButton.Pressed += OnBuildMachineFPressed;

        _buildComponentBBeltButton = CreateButton("BUILD COMPONENT B BELT", new Vector2(470, 620));
        _buildComponentBBeltButton.Disabled = true;
        _buildComponentBBeltButton.Pressed += OnBuildComponentBBeltPressed;

        _resetButton = new Button
        {
            Text = "RESET FACTORY",
            Position = new Vector2(1370, 690),
            Size = new Vector2(190, 58),
        };
        _resetButton.Pressed += OnResetPressed;
        AddChild(_resetButton);

        _pauseButton = CreateSpeedButton("PAUSE", new Vector2(35, 640), SimulationSpeed.Paused);
        _speed1Button = CreateSpeedButton("1×", new Vector2(140, 640), SimulationSpeed.Normal);
        _speed2Button = CreateSpeedButton("2×", new Vector2(245, 640), SimulationSpeed.Double);
        _speed4Button = CreateSpeedButton("4×", new Vector2(350, 640), SimulationSpeed.Quadruple);

        _continueButton = CreateButton("CONTINUE", new Vector2(645, 360));
        _continueButton.Pressed += OnContinuePressed;
        _continueButton.Hide();

        _revealNorthButton = CreateFrontierButton(new Vector2(1350, 170), OnRevealNorthPressed);
        _revealWestButton = CreateFrontierButton(new Vector2(1130, 265), OnRevealWestPressed);
        _revealEastButton = CreateFrontierButton(new Vector2(1350, 265), OnRevealEastPressed);

        UpdateRoutingControls();
        UpdateDispatchControl();
        UpdateShippingStorageExpansionControl();
        UpdateDirectFreightLinkControl();
        UpdateCapitalBuildControls();
        UpdateFrontierControls();
        UpdateSpeedControls();
        RecordDeliberateActivity();
        QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false }
            || @event is InputEventMouseButton { Pressed: true })
        {
            RecordDeliberateActivity();
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            SetSimulationSpeed(_simulationSpeed == SimulationSpeed.Paused
                ? _previousRunningSpeed
                : SimulationSpeed.Paused);
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            && CancelMachinePlacement())
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F })
        {
            ResetWorldView();
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _isPanning = mouseButton.Pressed;
                return;
            }

            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomWorldAt(mouseButton.Position, _worldZoom * ZoomFactor);
                return;
            }

            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomWorldAt(mouseButton.Position, _worldZoom / ZoomFactor);
                return;
            }
        }

        if (@event is InputEventMouseMotion mouseMotion && _isPanning)
        {
            RecordDeliberateActivity();
            _worldOffset += mouseMotion.Relative;
            QueueRedraw();
            return;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseEvent)
        {
            return;
        }

        var worldClickPosition = ScreenToWorld(mouseEvent.Position);
        switch (_interactionMode)
        {
            case InteractionMode.PlacingRawBeltAwaitingStart:
                if (IsPortHit(worldClickPosition, RawBeltStart))
                {
                    _interactionMode = InteractionMode.PlacingRawBeltAwaitingEnd;
                }
                break;
            case InteractionMode.PlacingRawBeltAwaitingEnd:
                if (IsPortHit(worldClickPosition, RawJunction))
                {
                    TryBuildInputBelt();
                }
                break;
            case InteractionMode.PlacingMachineB:
                TryPlaceMachineB(worldClickPosition);
                break;
            case InteractionMode.PlacingFinisherD:
                TryPlaceFinisherD(worldClickPosition);
                break;
            case InteractionMode.PlacingMachineF:
                TryPlaceMachineF(worldClickPosition);
                break;
            case InteractionMode.PlacingComponentBBeltAwaitingSource:
                if (_machineFWorldPosition.HasValue && IsPortHit(worldClickPosition, MachineFOutput))
                {
                    _interactionMode = InteractionMode.PlacingComponentBBeltAwaitingDestination;
                }
                break;
            case InteractionMode.PlacingComponentBBeltAwaitingDestination:
                if (IsPortHit(worldClickPosition, ComponentBBeltEnd))
                {
                    TryBuildComponentBBelt();
                }
                break;
        }

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        CheckForUnattendedCreditLoss();
        if (_safetyPauseActive)
        {
            UpdateDispatchControl();
            UpdateShippingStorageExpansionControl();
            UpdateDirectFreightLinkControl();
            UpdateCapitalBuildControls();
            UpdateFrontierControls();
            QueueRedraw();
            return;
        }

        var speedMultiplier = (int)_simulationSpeed;
        if (speedMultiplier == 0)
        {
            UpdateDispatchControl();
            UpdateShippingStorageExpansionControl();
            UpdateDirectFreightLinkControl();
            UpdateCapitalBuildControls();
            UpdateFrontierControls();
            QueueRedraw();
            return;
        }

        var scaledDelta = delta * speedMultiplier;
        UpdateTransportReturnAnimation(scaledDelta);

        _stepAccumulator += scaledDelta;
        while (_stepAccumulator >= SimulationStepSeconds)
        {
            var transportWasDelivering = _factory.Transport1IsDelivering;
            var transport2WasDelivering = _factory.Transport2IsDelivering;
            if (!_factory.AdvanceOneStep())
            {
                _stepAccumulator = 0;
                break;
            }
            UpdateTransportTransition(transportWasDelivering, transport2WasDelivering);
            UpdateThroughputSample();
            DispatchAutomatically();
            _stepAccumulator -= SimulationStepSeconds;
        }

        UpdateDispatchControl();
        UpdateShippingStorageExpansionControl();
        UpdateDirectFreightLinkControl();
        UpdateCapitalBuildControls();
        UpdateFrontierControls();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 1600, 800), BackgroundColor);
        DrawSetTransform(_worldOffset, rotation: 0, new Vector2(_worldZoom, _worldZoom));
        DrawWorldTerrain();
        DrawFreightRoute();
        DrawSource();
        DrawFixedBelts();
        DrawFixedConnections();
        DrawBuffer();
        DrawMachine(MachineARect, "MACHINE A", _factory.MachineA, RawColor);
        DrawMachineB();
        DrawMachine(FinisherCRect, "FINISHER C", _factory.FinisherC, IntermediateColor);
        DrawFinisherD();
        DrawComponentStorage();
        DrawTransport();
        DrawRemoteFactory();
        DrawShippingStorage();

        var interpolation = (float)(_stepAccumulator / SimulationStepSeconds);
        if (_factory.InputBeltIsBuilt)
        {
            DrawBeltItems(_factory.RawInputBeltItems, RawBeltStart, RawJunction, interpolation);
            DrawBeltItems(_factory.BranchBeltAItems, RawJunction, MachineAInput, interpolation);
            if (_factory.MachineB.IsBuilt && _machineBWorldPosition.HasValue)
            {
                DrawBeltItems(_factory.BranchBeltBItems, RawJunction, MachineBInput, interpolation);
            }
        }
        DrawBeltItems(_factory.IntermediateBeltItems, IntermediateBeltStart, BufferInput, interpolation);
        DrawBeltItems(_factory.ComponentBeltItems, ComponentBeltStart, ComponentBeltEnd, interpolation);
        DrawBeltItems(_factory.RemoteInputBeltItems, RemoteInputBeltStart, RemoteInputBeltEnd, interpolation);
        if (_machineFWorldPosition.HasValue)
        {
            DrawBeltItems(_factory.RawBBeltItems, RawBBeltStart, MachineFInput, interpolation);
            DrawBeltItems(_factory.ComponentBBeltItems, MachineFOutput, ComponentBBeltEnd, interpolation);
        }
        DrawBeltItems(_factory.FinishedOutputBeltItems, FinishedOutputBeltStart, FinishedOutputBeltEnd, interpolation);
        DrawSetTransform(Vector2.Zero, rotation: 0, Vector2.One);

        DrawTitle();
        DrawEconomy();
        DrawFrontierBoard();
        DrawSafetyPauseNotice();
    }

    private void ResetWorldView()
    {
        _worldOffset = new Vector2(390, 330);
        _worldZoom = MinimumWorldZoom;
        _isPanning = false;
        QueueRedraw();
    }

    private void ZoomWorldAt(Vector2 screenPosition, float requestedZoom)
    {
        var worldPosition = ScreenToWorld(screenPosition);
        _worldZoom = Mathf.Clamp(requestedZoom, MinimumWorldZoom, MaximumWorldZoom);
        _worldOffset = screenPosition - worldPosition * _worldZoom;
        QueueRedraw();
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        return (screenPosition - _worldOffset) / _worldZoom;
    }

    private Button CreateButton(string text, Vector2 position)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = new Vector2(310, 58),
        };
        AddChild(button);
        return button;
    }

    private Button CreateFrontierButton(Vector2 position, Action handler)
    {
        var button = new Button
        {
            Position = position,
            Size = new Vector2(205, 76),
        };
        button.Pressed += handler;
        AddChild(button);
        return button;
    }

    private Button CreateSpeedButton(string text, Vector2 position, SimulationSpeed speed)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = new Vector2(95, 42),
        };
        button.Pressed += () => SetSimulationSpeed(speed);
        AddChild(button);
        return button;
    }

    private void SetSimulationSpeed(SimulationSpeed speed)
    {
        RecordDeliberateActivity();
        if (_safetyPauseActive)
        {
            return;
        }

        if (speed != SimulationSpeed.Paused)
        {
            _previousRunningSpeed = speed;
        }

        _simulationSpeed = speed;
        if (speed == SimulationSpeed.Paused)
        {
            _stepAccumulator = 0;
        }
        UpdateSpeedControls();
        QueueRedraw();
    }

    private void UpdateSpeedControls()
    {
        _pauseButton.Disabled = _safetyPauseActive || _simulationSpeed == SimulationSpeed.Paused;
        _speed1Button.Disabled = _safetyPauseActive || _simulationSpeed == SimulationSpeed.Normal;
        _speed2Button.Disabled = _safetyPauseActive || _simulationSpeed == SimulationSpeed.Double;
        _speed4Button.Disabled = _safetyPauseActive || _simulationSpeed == SimulationSpeed.Quadruple;
    }

    private void RecordDeliberateActivity()
    {
        _lastDeliberateInputMilliseconds = Time.GetTicksMsec();
        _creditsAtLastInput = _factory.Credits;
    }

    private void CheckForUnattendedCreditLoss()
    {
        if (_safetyPauseActive || _simulationSpeed == SimulationSpeed.Paused || _factory.IsOutOfCredits)
        {
            return;
        }

        var unattendedMilliseconds = Time.GetTicksMsec() - _lastDeliberateInputMilliseconds;
        var creditLoss = _creditsAtLastInput - _factory.Credits;
        if (unattendedMilliseconds < UnattendedDelayMilliseconds || creditLoss < UnattendedCreditLossThreshold)
        {
            return;
        }

        _safetyPauseActive = true;
        _safetyPauseCreditLoss = creditLoss;
        _stepAccumulator = 0;
        _continueButton.Show();
        UpdateSpeedControls();
    }

    private void OnContinuePressed()
    {
        _safetyPauseActive = false;
        _safetyPauseCreditLoss = 0;
        _continueButton.Hide();
        RecordDeliberateActivity();
        UpdateSpeedControls();
        QueueRedraw();
    }

    private static FactoryState CreateFactory(
        int startingCredits = FactoryState.StartingCredits,
        bool northRegionIsRevealed = false,
        bool westRegionIsRevealed = false,
        bool eastRegionIsRevealed = false)
    {
        return new FactoryState(
            machineAProcessingSteps: 2,
            machineBProcessingSteps: 4,
            finisherProcessingSteps: 1,
            beltCapacity: 5,
            sourceIntervalSteps: 1,
            startingCredits: startingCredits,
            northRegionIsRevealed: northRegionIsRevealed,
            westRegionIsRevealed: westRegionIsRevealed,
            eastRegionIsRevealed: eastRegionIsRevealed);
    }

    private void OnRevealNorthPressed() => TryRevealRegion(_factory.RevealNorthRegion);

    private void OnRevealWestPressed() => TryRevealRegion(_factory.RevealWestRegion);

    private void OnRevealEastPressed() => TryRevealRegion(_factory.RevealEastRegion);

    private void TryRevealRegion(Func<bool> reveal)
    {
        if (reveal())
        {
            RecordDeliberateActivity();
        }

        UpdateFrontierControls();
        QueueRedraw();
    }

    private void UpdateFrontierControls()
    {
        UpdateFrontierControl(_revealNorthButton, "NORTH", "ROCKY TERRAIN", "ROCKY REGION", _factory.NorthRegionIsRevealed);
        UpdateFrontierControl(_revealWestButton, "WEST", "FLAT LOWLAND", "OPEN INDUSTRIAL REGION", _factory.WestRegionIsRevealed);
        UpdateFrontierControl(_revealEastButton, "EAST", "WATER BOUNDARY", "WATERFRONT REGION", _factory.EastRegionIsRevealed);
    }

    private void UpdateFrontierControl(Button button, string direction, string clue, string identity, bool revealed)
    {
        if (revealed)
        {
            button.Text = $"{direction}\n{identity}\nREVEALED";
            button.Disabled = true;
            return;
        }

        var affordable = _factory.Credits >= FactoryState.FrontierRegionRevealCost;
        button.Text = affordable
            ? $"{direction} ?\n{clue}\nREVEAL — {FactoryState.FrontierRegionRevealCost:N0} C"
            : $"{direction} ?\n{clue}\n{FactoryState.FrontierRegionRevealCost:N0} C (UNAFFORDABLE)";
        button.Disabled = !affordable;
    }

    private void OnBuildBeltPressed()
    {
        _interactionMode = InteractionMode.PlacingRawBeltAwaitingStart;
        _buildBeltButton.Text = "CLICK SOURCE OUTPUT";
        _buildBeltButton.Disabled = true;
    }

    private void OnBuildMachinePressed()
    {
        _interactionMode = InteractionMode.PlacingMachineB;
        _buildMachineButton.Text = "CLICK MACHINE B LOCATION";
        _buildMachineButton.Disabled = true;
        UpdateCapitalBuildControls();
    }

    private void OnBuildFinisherPressed()
    {
        _interactionMode = InteractionMode.PlacingFinisherD;
        _buildFinisherButton.Text = "CLICK FINISHER D LOCATION";
        _buildFinisherButton.Disabled = true;
        UpdateCapitalBuildControls();
    }

    private void OnAlternateRoutingPressed()
    {
        if (_factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.Alternate))
        {
            UpdateRoutingControls();
        }
    }

    private void OnPriorityRoutingPressed()
    {
        if (_factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB))
        {
            UpdateRoutingControls();
        }
    }

    private void OnDispatchPressed()
    {
        if (_factory.DispatchFullShipment())
        {
            UpdateDispatchControl();
            QueueRedraw();
        }
    }

    private void OnAutoDispatchPressed()
    {
        _autoDispatchEnabled = !_autoDispatchEnabled;
        _autoDispatchButton.Text = _autoDispatchEnabled ? "AUTO DISPATCH: ON" : "AUTO DISPATCH: OFF";
        RecordDeliberateActivity();
        if (_autoDispatchEnabled && !_safetyPauseActive)
        {
            DispatchAutomatically();
        }
        UpdateDispatchControl();
        QueueRedraw();
    }

    private void DispatchAutomatically()
    {
        if (!_autoDispatchEnabled || _safetyPauseActive)
        {
            return;
        }

        while (_factory.DispatchFullShipment())
        {
        }
    }

    private void OnExpandShippingStoragePressed()
    {
        if (!_factory.BuildShippingStorageExpansion())
        {
            return;
        }

        UpdateDispatchControl();
        UpdateShippingStorageExpansionControl();
        RecordDeliberateActivity();
        QueueRedraw();
    }

    private void OnBuildDirectFreightLinkPressed()
    {
        if (!_factory.BuildDirectFreightLink())
        {
            return;
        }

        UpdateDispatchControl();
        UpdateShippingStorageExpansionControl();
        UpdateDirectFreightLinkControl();
        RecordDeliberateActivity();
        QueueRedraw();
    }

    private void OnResetPressed()
    {
        _safetyPauseActive = false;
        _safetyPauseCreditLoss = 0;
        _continueButton.Hide();
        _factory = CreateFactory(
            _factory.Credits,
            _factory.NorthRegionIsRevealed,
            _factory.WestRegionIsRevealed,
            _factory.EastRegionIsRevealed);
        _interactionMode = InteractionMode.Normal;
        _stepAccumulator = 0;
        _throughputSampleStartStep = 0;
        _throughputSampleStartFinished = 0;
        _recentFinishedPerHundredSteps = 0;
        _transportIsReturning = false;
        _transportReturnElapsed = 0;
        _transport2IsReturning = false;
        _transport2ReturnElapsed = 0;
        _machineBWorldPosition = null;
        _finisherDWorldPosition = null;
        _machineFWorldPosition = null;
        _buildSecondTransportButton.Text = $"BUILD SECOND TRANSPORT — {FactoryState.SecondTransportPurchaseCost} C";
        _buildSecondTransportButton.Disabled = false;
        _buildMachineFButton.Show();
        _buildMachineFButton.Text = $"BUILD MACHINE F — {FactoryState.MachineFPurchaseCost} C";
        _buildMachineFButton.Disabled = false;
        _buildComponentBBeltButton.Show();
        _buildComponentBBeltButton.Text = "BUILD COMPONENT B BELT";
        _buildComponentBBeltButton.Disabled = true;
        _expandShippingStorageButton.Text = $"EXPAND SHIPPING — {FactoryState.ShippingStorageExpansionFinishedItemCost} ASSEMBLIES";
        _expandShippingStorageButton.Disabled = true;
        _buildDirectFreightLinkButton.Text = $"BUILD DIRECT FREIGHT LINK — {FactoryState.DirectFreightLinkAssemblyCost} ASSEMBLIES";
        _buildDirectFreightLinkButton.Disabled = true;

        _buildBeltButton.Show();
        _buildBeltButton.Text = "BUILD RAW BELT";
        _buildBeltButton.Disabled = false;

        _buildMachineButton.Show();
        _buildMachineButton.Text = $"BUILD MACHINE B — {FactoryState.MachineBPurchaseCost} C";
        _buildMachineButton.Disabled = true;

        _buildFinisherButton.Show();
        _buildFinisherButton.Text = $"BUILD FINISHER D — {FactoryState.FinisherDPurchaseCost} C";
        _buildFinisherButton.Disabled = true;

        UpdateRoutingControls();
        UpdateDispatchControl();
        UpdateShippingStorageExpansionControl();
        UpdateDirectFreightLinkControl();
        UpdateCapitalBuildControls();
        UpdateFrontierControls();
        UpdateSpeedControls();
        RecordDeliberateActivity();

        QueueRedraw();
    }

    private void TryBuildInputBelt()
    {
        if (!_factory.BuildInputBelt())
        {
            return;
        }

        _interactionMode = InteractionMode.Normal;
        _buildBeltButton.Hide();
        _buildMachineButton.Disabled = false;
        _buildFinisherButton.Disabled = false;
    }

    private void TryPlaceMachineB(Vector2 worldPosition)
    {
        var candidate = PlacementCandidate(worldPosition, ExpansionMachineFootprint);
        if (!IsValidMachineBPlacement(candidate) || !_factory.BuildMachineB())
        {
            return;
        }

        _machineBWorldPosition = candidate.Position;
        _interactionMode = InteractionMode.Normal;
        _buildMachineButton.Hide();
        _buildFinisherButton.Text = "NO EXPANSIONS REMAINING";
        _buildFinisherButton.Disabled = true;
        UpdateRoutingControls();
        RecordDeliberateActivity();
    }

    private void TryPlaceFinisherD(Vector2 worldPosition)
    {
        var candidate = PlacementCandidate(worldPosition, ExpansionMachineFootprint);
        if (!IsValidFinisherDPlacement(candidate) || !_factory.BuildFinisherD())
        {
            return;
        }

        _finisherDWorldPosition = candidate.Position;
        _interactionMode = InteractionMode.Normal;
        _buildMachineButton.Text = "NO EXPANSIONS REMAINING";
        _buildMachineButton.Disabled = true;
        _buildFinisherButton.Hide();
        UpdateRoutingControls();
        RecordDeliberateActivity();
    }

    private bool CancelMachinePlacement()
    {
        if (_interactionMode is not (InteractionMode.PlacingMachineB
            or InteractionMode.PlacingFinisherD
            or InteractionMode.PlacingMachineF))
        {
            return false;
        }

        _interactionMode = InteractionMode.Normal;
        UpdateCapitalBuildControls();
        QueueRedraw();
        return true;
    }

    private static Rect2 PlacementCandidate(Vector2 worldPosition, Vector2 footprint)
    {
        return new Rect2(worldPosition - footprint / 2, footprint);
    }

    private static bool IsFootprintInside(Rect2 candidate, Rect2 buildArea)
    {
        return candidate.Position.X >= buildArea.Position.X
            && candidate.Position.Y >= buildArea.Position.Y
            && candidate.End.X <= buildArea.End.X
            && candidate.End.Y <= buildArea.End.Y;
    }

    private static bool IsValidMachineBPlacement(Rect2 candidate)
    {
        return IsFootprintInside(candidate, MachineBBuildArea)
            && !candidate.Intersects(MachineARect)
            && !candidate.Intersects(BufferRect);
    }

    private static bool IsValidFinisherDPlacement(Rect2 candidate)
    {
        return IsFootprintInside(candidate, FinisherDBuildArea)
            && !candidate.Intersects(FinisherCRect)
            && !candidate.Intersects(BufferRect)
            && !candidate.Intersects(ComponentStorageRect);
    }

    private void UpdateRoutingControls()
    {
        var routingAvailable = _factory.MachineB.IsBuilt;
        var alternateSelected = _factory.StageOneRoutingPolicy == StageOneRoutingPolicy.Alternate;

        _alternateRoutingButton.Text = routingAvailable
            ? alternateSelected ? "ROUTING: ALTERNATE" : "ALTERNATE"
            : "ROUTING: ALTERNATE (NEEDS B)";
        _alternateRoutingButton.Disabled = !routingAvailable || alternateSelected;

        _priorityRoutingButton.Text = routingAvailable
            ? !alternateSelected ? "ROUTING: PRIORITY A + OVERFLOW B" : "PRIORITY A + OVERFLOW B"
            : "PRIORITY A + OVERFLOW B (NEEDS B)";
        _priorityRoutingButton.Disabled = !routingAvailable || !alternateSelected;
    }

    private void UpdateDispatchControl()
    {
        _dispatchButton.Disabled = _factory.ShippingStorageCount < FactoryState.CustomerOrderSize;
    }

    private void UpdateShippingStorageExpansionControl()
    {
        if (_factory.ShippingStorageIsExpanded)
        {
            _expandShippingStorageButton.Text = $"SHIPPING STORAGE EXPANDED — {_factory.ShippingStorageCapacity}";
            _expandShippingStorageButton.Disabled = true;
            return;
        }

        _expandShippingStorageButton.Text = $"EXPAND SHIPPING — {FactoryState.ShippingStorageExpansionFinishedItemCost} ASSEMBLIES";
        _expandShippingStorageButton.Disabled = _factory.ShippingStorageCount
            < FactoryState.ShippingStorageExpansionFinishedItemCost;
    }

    private void UpdateDirectFreightLinkControl()
    {
        if (_factory.DirectFreightLinkIsBuilt)
        {
            _buildDirectFreightLinkButton.Text = "DIRECT FREIGHT LINK BUILT";
            _buildDirectFreightLinkButton.Disabled = true;
            return;
        }

        _buildDirectFreightLinkButton.Text =
            $"BUILD DIRECT FREIGHT LINK — {FactoryState.DirectFreightLinkAssemblyCost} ASSEMBLIES";
        _buildDirectFreightLinkButton.Disabled = _factory.ShippingStorageCount
            < FactoryState.DirectFreightLinkAssemblyCost;
    }

    private void UpdateCapitalBuildControls()
    {
        if (!_factory.MachineFIsBuilt && _interactionMode != InteractionMode.PlacingMachineF)
        {
            var affordable = _factory.Credits >= FactoryState.MachineFPurchaseCost;
            _buildMachineFButton.Text = affordable
                ? $"BUILD MACHINE F — {FactoryState.MachineFPurchaseCost} C"
                : $"BUILD MACHINE F — {FactoryState.MachineFPurchaseCost} C (UNAFFORDABLE)";
            _buildMachineFButton.Disabled = !affordable;
        }

        if (!_factory.Transport2Exists)
        {
            var affordable = _factory.Credits >= FactoryState.SecondTransportPurchaseCost;
            _buildSecondTransportButton.Text = affordable
                ? $"BUILD SECOND TRANSPORT — {FactoryState.SecondTransportPurchaseCost} C"
                : $"BUILD SECOND TRANSPORT — {FactoryState.SecondTransportPurchaseCost} C (UNAFFORDABLE)";
            _buildSecondTransportButton.Disabled = !affordable;
        }

        if (_factory.ExpansionsAvailable == 0)
        {
            return;
        }

        var expansionAffordable = _factory.Credits >= FactoryState.MachineBPurchaseCost;
        if (!_factory.MachineB.IsBuilt && _interactionMode != InteractionMode.PlacingMachineB)
        {
            _buildMachineButton.Text = expansionAffordable
                ? $"BUILD MACHINE B — {FactoryState.MachineBPurchaseCost} C"
                : $"BUILD MACHINE B — {FactoryState.MachineBPurchaseCost} C (UNAFFORDABLE)";
            _buildMachineButton.Disabled = !_factory.InputBeltIsBuilt || !expansionAffordable;
        }
        if (!_factory.FinisherD.IsBuilt && _interactionMode != InteractionMode.PlacingFinisherD)
        {
            _buildFinisherButton.Text = expansionAffordable
                ? $"BUILD FINISHER D — {FactoryState.FinisherDPurchaseCost} C"
                : $"BUILD FINISHER D — {FactoryState.FinisherDPurchaseCost} C (UNAFFORDABLE)";
            _buildFinisherButton.Disabled = !_factory.InputBeltIsBuilt || !expansionAffordable;
        }
    }

    private void OnBuildSecondTransportPressed()
    {
        if (!_factory.BuildSecondTransport())
        {
            return;
        }

        _buildSecondTransportButton.Text = "SECOND TRANSPORT BUILT";
        _buildSecondTransportButton.Disabled = true;
        RecordDeliberateActivity();
        QueueRedraw();
    }

    private void OnBuildMachineFPressed()
    {
        _interactionMode = InteractionMode.PlacingMachineF;
        _buildMachineFButton.Text = "PLACE MACHINE F";
        _buildMachineFButton.Disabled = true;
        QueueRedraw();
    }

    private void TryPlaceMachineF(Vector2 worldPosition)
    {
        var candidate = PlacementCandidate(worldPosition, MachineFFootprint);
        if (!IsValidMachineFPlacement(candidate) || !_factory.BuildMachineF())
        {
            return;
        }

        _machineFWorldPosition = candidate.Position;
        _interactionMode = InteractionMode.Normal;
        _buildMachineFButton.Hide();
        _buildComponentBBeltButton.Disabled = false;
        RecordDeliberateActivity();
    }

    private static bool IsValidMachineFPlacement(Rect2 candidate)
    {
        return IsFootprintInside(candidate, MachineFBuildArea)
            && !candidate.Intersects(RemoteInputStorageRect)
            && !candidate.Intersects(SourceBRect)
            && !candidate.Intersects(AssemblerERect);
    }

    private void OnBuildComponentBBeltPressed()
    {
        _interactionMode = InteractionMode.PlacingComponentBBeltAwaitingSource;
        _buildComponentBBeltButton.Text = "SELECT MACHINE F OUTPUT";
        _buildComponentBBeltButton.Disabled = true;
        QueueRedraw();
    }

    private void TryBuildComponentBBelt()
    {
        if (!_factory.BuildComponentBBelt())
        {
            return;
        }

        _interactionMode = InteractionMode.Normal;
        _buildComponentBBeltButton.Hide();
    }

    private void UpdateThroughputSample()
    {
        var elapsedSampleSteps = _factory.ElapsedSteps - _throughputSampleStartStep;
        if (elapsedSampleSteps < ThroughputSampleSteps)
        {
            return;
        }

        var finishedDuringSample = _factory.TotalFinishedProduced - _throughputSampleStartFinished;
        _recentFinishedPerHundredSteps = finishedDuringSample * 100.0 / elapsedSampleSteps;
        _throughputSampleStartStep = _factory.ElapsedSteps;
        _throughputSampleStartFinished = _factory.TotalFinishedProduced;
    }

    private void UpdateTransportTransition(bool wasDelivering, bool transport2WasDelivering)
    {
        if (wasDelivering && !_factory.TransportIsDelivering)
        {
            _transportIsReturning = true;
            _transportReturnElapsed = 0;
        }
        else if (_factory.TransportIsDelivering)
        {
            _transportIsReturning = false;
            _transportReturnElapsed = 0;
        }


        if (transport2WasDelivering && !_factory.Transport2IsDelivering)
        {
            _transport2IsReturning = true;
            _transport2ReturnElapsed = 0;
        }
        else if (_factory.Transport2IsDelivering)
        {
            _transport2IsReturning = false;
            _transport2ReturnElapsed = 0;
        }
    }

    private void UpdateTransportReturnAnimation(double delta)
    {
        if (_transportIsReturning)
        {
            _transportReturnElapsed += delta;
            if (_transportReturnElapsed >= SimulationStepSeconds)
            {
                _transportIsReturning = false;
                _transportReturnElapsed = 0;
            }
        }

        if (_transport2IsReturning)
        {
            _transport2ReturnElapsed += delta;
            if (_transport2ReturnElapsed >= SimulationStepSeconds)
            {
                _transport2IsReturning = false;
                _transport2ReturnElapsed = 0;
            }
        }
    }

    private void DrawTitle()
    {
        DrawString(ThemeDB.FallbackFont, new Vector2(48, 52), "FACTORY FLOW: MOVING BOTTLENECKS", HorizontalAlignment.Left, -1, 28, TextColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(48, 82), CurrentInstruction(), HorizontalAlignment.Left, -1, 17, OutlineColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(48, 115), "RAW", HorizontalAlignment.Left, -1, 15, RawColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(105, 115), "→  INTERMEDIATE", HorizontalAlignment.Left, -1, 15, IntermediateColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(245, 115), "→  COMPONENT A", HorizontalAlignment.Left, -1, 15, ComponentColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(385, 115), "→  INDUSTRIAL ASSEMBLY", HorizontalAlignment.Left, -1, 15, FinishedColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(48, 145), "MIDDLE DRAG: PAN   •   WHEEL: ZOOM   •   F: RESET VIEW", HorizontalAlignment.Left, -1, 13, OutlineColor);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(1250, 82),
            $"EXPANSIONS AVAILABLE: {_factory.ExpansionsAvailable}",
            HorizontalAlignment.Left,
            -1,
            17,
            _factory.ExpansionsAvailable > 0 ? FinishedColor : OutlineColor);
    }

    private void DrawEconomy()
    {
        DrawString(ThemeDB.FallbackFont, new Vector2(600, 48), $"CREDITS: {_factory.Credits:N0}", HorizontalAlignment.Left, -1, 18, FinishedColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(600, 76), $"GRID POWER: {_factory.CurrentPowerDraw}", HorizontalAlignment.Left, -1, 15, TextColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(600, 102), $"GRID COST: {_factory.CurrentGridCostPerStep} / step", HorizontalAlignment.Left, -1, 15, OutlineColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(920, 48), $"ASSEMBLIES SHIPPED: {_factory.ShippedFinishedCount}", HorizontalAlignment.Left, -1, 14, OutlineColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(920, 76), $"RECENT RATE: {_recentFinishedPerHundredSteps:0.0} / 100 steps", HorizontalAlignment.Left, -1, 14, FinishedColor);
        var routeName = _factory.DirectFreightLinkIsBuilt ? "DIRECT" : "INDIRECT";
        DrawString(ThemeDB.FallbackFont, new Vector2(920, 102), $"FREIGHT ROUTE: {routeName}", HorizontalAlignment.Left, -1, 14,
            _factory.DirectFreightLinkIsBuilt ? FinishedColor : OutlineColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(920, 126),
            $"{_factory.ComponentATransportTravelSteps} STEPS · {_factory.ComponentATransportCostPerCargoItem} C/ITEM",
            HorizontalAlignment.Left, -1, 14, OutlineColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(1210, 115),
            $"CUSTOMER: BUYING {FactoryState.CustomerOrderSize}-ASSEMBLY LOADS",
            HorizontalAlignment.Left, -1, 15, FinishedColor);

        if (_factory.IsOutOfCredits)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(600, 137), "GRID POWER DISCONNECTED", HorizontalAlignment.Left, -1, 18, BlockedColor);
            DrawString(ThemeDB.FallbackFont, new Vector2(600, 161), "NO CREDITS - PRODUCTION PAUSED", HorizontalAlignment.Left, -1, 15, BlockedColor);
        }
    }

    private void DrawFrontierBoard()
    {
        var panel = new Rect2(1110, 150, 465, 205);
        DrawRect(panel, new Color(0.08f, 0.10f, 0.13f, 0.94f));
        DrawRect(panel, OutlineColor, filled: false, width: 2);
        DrawString(ThemeDB.FallbackFont, new Vector2(1130, 180), "STRATEGIC FRONTIER", HorizontalAlignment.Left, -1, 16, TextColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(1220, 238), "HOME REGION", HorizontalAlignment.Left, -1, 15, FinishedColor);
        DrawLine(new Vector2(1325, 220), new Vector2(1350, 208), OutlineColor, 2);
        DrawLine(new Vector2(1325, 242), new Vector2(1350, 286), OutlineColor, 2);
    }

    private void DrawSafetyPauseNotice()
    {
        if (!_safetyPauseActive)
        {
            return;
        }

        var panel = new Rect2(560, 205, 480, 225);
        DrawRect(panel, new Color(0.08f, 0.10f, 0.13f, 0.96f));
        DrawRect(panel, FinishedColor, filled: false, width: 4);
        DrawString(ThemeDB.FallbackFont, new Vector2(655, 255), "FACTORY SAFETY PAUSED", HorizontalAlignment.Left, -1, 24, TextColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(640, 295), $"Credits fell by {_safetyPauseCreditLoss:N0} while unattended.", HorizontalAlignment.Left, -1, 18, BlockedColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(660, 327), "Factory state has been preserved.", HorizontalAlignment.Left, -1, 17, OutlineColor);
    }

    private string CurrentInstruction()
    {
        return _interactionMode switch
        {
            InteractionMode.PlacingRawBeltAwaitingStart => "Click the highlighted SOURCE output port.",
            InteractionMode.PlacingRawBeltAwaitingEnd => "Now click the highlighted ROUTER input port.",
            InteractionMode.PlacingMachineB => "Place Machine B in its highlighted area. Escape cancels.",
            InteractionMode.PlacingFinisherD => "Place Finisher D in its highlighted area. Escape cancels.",
            InteractionMode.PlacingMachineF => "Place Machine F in its highlighted area. Escape cancels.",
            InteractionMode.PlacingComponentBBeltAwaitingSource => "Select Machine F's highlighted Component B output.",
            InteractionMode.PlacingComponentBBeltAwaitingDestination => "Connect it to Assembler E's highlighted B input.",
            _ when !_factory.InputBeltIsBuilt => "Build the raw belt to start production.",
            _ when _factory.Transport1WaitingForCredits || _factory.Transport2WaitingForCredits => "Transport waiting for Credits to buy delivery energy.",
            _ when _factory.IsShippingStorageFull => "Shipping Storage is full. Dispatch a full load to earn Credits.",
            _ when _factory.IsComponentStorageFull => "Component Storage is full. Downstream congestion is blocking Factory 1.",
            _ when _factory.IsBufferFull => "WIP buffer is full. Upstream production is backing up.",
            _ when _factory.ExpansionsAvailable > 0 => "Observe the factory, then choose where to add the one available expansion.",
            _ => "Expansion committed. Watch whether final throughput improves.",
        };
    }

    private void DrawWorldTerrain()
    {
        DrawRect(HomeRegionRect, HomeLandColor);
        DrawRegionBorder(HomeRegionRect, "HOME · WORKING FACTORY", new Vector2(20, 135), FinishedColor);
        DrawHomeLandmarks();

        if (_factory.WestRegionIsRevealed)
        {
            DrawRect(WestRegionRect, OpenLandColor);
            DrawOpenIndustrialLand();
            DrawRegionBorder(WestRegionRect, "WEST · OPEN INDUSTRIAL REGION", new Vector2(-905, 145), FinishedColor);
        }
        else
        {
            DrawHiddenRegion(WestRegionRect, "WEST FRONTIER", "CLUE: FLAT LOWLAND");
        }

        if (_factory.NorthRegionIsRevealed)
        {
            DrawRect(NorthRegionRect, RockyLandColor);
            DrawRockyTerrain();
            DrawRegionBorder(NorthRegionRect, "NORTH · ROCKY REGION", new Vector2(-55, -595), FinishedColor);
        }
        else
        {
            DrawHiddenRegion(NorthRegionRect, "NORTH FRONTIER", "CLUE: ROCKY TERRAIN");
        }

        if (_factory.EastRegionIsRevealed)
        {
            DrawRect(EastRegionRect, EastLandColor);
            DrawWaterfrontTerrain();
            DrawRegionBorder(EastRegionRect, "EAST · WATERFRONT REGION", new Vector2(1695, 145), FinishedColor);
        }
        else
        {
            DrawHiddenRegion(EastRegionRect, "EAST FRONTIER", "CLUE: WATER BOUNDARY");
        }
    }

    private void DrawRegionBorder(Rect2 region, string label, Vector2 labelPosition, Color labelColor)
    {
        DrawRect(region, RegionBorderColor, filled: false, width: 7);
        DrawString(ThemeDB.FallbackFont, labelPosition, label, HorizontalAlignment.Left, -1, 25, labelColor);
    }

    private void DrawHiddenRegion(Rect2 region, string name, string clue)
    {
        DrawRect(region, HiddenLandColor);
        for (var x = region.Position.X - region.Size.Y; x < region.End.X; x += 85)
        {
            var start = new Vector2(Mathf.Max(region.Position.X, x), region.End.Y);
            var end = new Vector2(Mathf.Min(region.End.X, x + region.Size.Y), region.Position.Y);
            DrawLine(start, end, new Color(0.13f, 0.16f, 0.18f), 12);
        }
        DrawRect(region, RegionBorderColor, filled: false, width: 7);
        var center = region.GetCenter();
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-150, -18), name, HorizontalAlignment.Center, 300, 28, TextColor);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-170, 24), clue, HorizontalAlignment.Center, 340, 19, OutlineColor);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-35, 82), "?", HorizontalAlignment.Center, 70, 52, OutlineColor);
    }

    private void DrawHomeLandmarks()
    {
        DrawString(ThemeDB.FallbackFont, new Vector2(55, 660), "BASIC ORE SITE", HorizontalAlignment.Left, -1, 18, RawColor);
        DrawCircle(new Vector2(105, 610), 38, new Color(0.12f, 0.18f, 0.22f));
        DrawCircle(new Vector2(75, 620), 20, RawColor);
        DrawCircle(new Vector2(112, 590), 25, new Color(0.30f, 0.56f, 0.72f));
        DrawCircle(new Vector2(140, 625), 17, RawColor);
    }

    private void DrawOpenIndustrialLand()
    {
        for (var x = -850; x <= -250; x += 200)
        {
            var pad = new Rect2(x, 260, 150, 170);
            DrawRect(pad, new Color(0.31f, 0.33f, 0.24f));
            DrawRect(pad, new Color(0.48f, 0.51f, 0.34f), filled: false, width: 4);
        }
        DrawString(ThemeDB.FallbackFont, new Vector2(-790, 500), "OPEN BUILD PADS", HorizontalAlignment.Left, -1, 22, TextColor);
        DrawCircle(new Vector2(-720, 600), 42, new Color(0.72f, 0.42f, 0.18f));
        DrawCircle(new Vector2(-675, 615), 28, IntermediateColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(-625, 620), "IRON-RICH GROUND", HorizontalAlignment.Left, -1, 18, IntermediateColor);
    }

    private void DrawRockyTerrain()
    {
        var mountainColor = new Color(0.38f, 0.34f, 0.32f);
        var ridgeColor = new Color(0.57f, 0.52f, 0.48f);
        for (var x = 20; x < 1550; x += 210)
        {
            var baseY = -80 - (int)(x / 210) % 2 * 100;
            var peak = new Vector2(x + 90, baseY - 360);
            DrawColoredPolygon([new Vector2(x, baseY), peak, new Vector2(x + 190, baseY)], mountainColor);
            DrawPolyline([new Vector2(x, baseY), peak, new Vector2(x + 190, baseY)], ridgeColor, 7);
        }
        DrawCircle(new Vector2(780, -120), 55, new Color(0.63f, 0.40f, 0.26f));
        DrawCircle(new Vector2(835, -105), 36, RawBColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(690, -35), "EXPOSED ORE", HorizontalAlignment.Left, -1, 20, RawBColor);
    }

    private void DrawWaterfrontTerrain()
    {
        var water = new Vector2[]
        {
            new(1665, 165), new(2010, 135), new(1990, 255), new(2030, 365),
            new(1985, 485), new(2020, 550), new(1665, 565),
        };
        DrawColoredPolygon(water, WaterColor);
        for (var y = 205; y < 550; y += 55)
        {
            DrawLine(new Vector2(1700, y), new Vector2(1960, y - 18), new Color(0.22f, 0.55f, 0.68f), 5);
        }
        DrawString(ThemeDB.FallbackFont, new Vector2(1740, 430), "DEEP WATER", HorizontalAlignment.Left, -1, 27, TextColor);
        DrawRect(new Rect2(2050, 570, 250, 95), new Color(0.28f, 0.31f, 0.25f));
        DrawRect(new Rect2(2050, 570, 250, 95), FinishedColor, filled: false, width: 4);
        DrawString(ThemeDB.FallbackFont, new Vector2(2080, 625), "FREIGHT LANDING", HorizontalAlignment.Left, -1, 19, FinishedColor);
    }

    private void DrawSource()
    {
        var rect = new Rect2(35, 260, 135, 120);
        DrawRect(rect, StructureColor);
        DrawRect(rect, _factory.SourceIsBlocked ? BlockedColor : OutlineColor, filled: false, width: 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(68, 245), "SOURCE", HorizontalAlignment.Left, -1, 19, TextColor);
        DrawCircle(rect.GetCenter(), 21, RawColor);
        if (_factory.SourceIsBlocked)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(38, 415), "SOURCE BLOCKED", HorizontalAlignment.Left, -1, 15, BlockedColor);
        }
    }

    private void DrawFixedBelts()
    {
        if (_factory.InputBeltIsBuilt)
        {
            DrawBelt(RawBeltStart, RawJunction, "RAW BELT", _factory.SourceIsBlocked);
        }
        else
        {
            var sourceColor = _interactionMode == InteractionMode.PlacingRawBeltAwaitingStart ? IntermediateColor : OutlineColor;
            var machineColor = _interactionMode == InteractionMode.PlacingRawBeltAwaitingEnd ? IntermediateColor : OutlineColor;
            DrawPort(RawBeltStart, sourceColor);
            DrawPort(RawJunction, machineColor);
            if (_interactionMode == InteractionMode.PlacingRawBeltAwaitingEnd)
            {
                DrawDashedLine(RawBeltStart, ScreenToWorld(GetViewport().GetMousePosition()), OutlineColor, 3, 10);
            }
        }

        DrawBelt(IntermediateBeltStart, BufferInput, "INTERMEDIATE BELT", _factory.IntermediateBeltIsBlocked);
        DrawBelt(ComponentBeltStart, ComponentBeltEnd, "COMPONENT BELT", _factory.ComponentBeltIsBlocked);
    }

    private void DrawFixedConnections()
    {
        if (_factory.InputBeltIsBuilt)
        {
            DrawBelt(RawJunction, MachineAInput, "BRANCH A", _factory.BranchBeltACount >= _factory.BranchBeltCapacity);
            if (_factory.MachineB.IsBuilt && _machineBWorldPosition.HasValue)
            {
                DrawBelt(RawJunction, MachineBInput, "BRANCH B", _factory.BranchBeltBCount >= _factory.BranchBeltCapacity);
            }
            DrawJunction(RawJunction);
            DrawString(ThemeDB.FallbackFont, new Vector2(RawJunction.X - 28, RawJunction.Y + 66), "ROUTER", HorizontalAlignment.Left, -1, 13, TextColor);
        }

        DrawConnector(MachineAOutput, IntermediateBeltStart);
        if (_factory.MachineB.IsBuilt && _machineBWorldPosition.HasValue)
        {
            DrawConnector(MachineBOutput, IntermediateBeltStart);
        }
        DrawJunction(IntermediateBeltStart);

        DrawConnector(BufferOutput, FinisherCInput);
        if (_factory.FinisherD.IsBuilt && _finisherDWorldPosition.HasValue)
        {
            DrawConnector(BufferOutput, FinisherDInput);
        }
        DrawJunction(BufferOutput);

        DrawConnector(FinisherCOutput, ComponentBeltStart);
        if (_factory.FinisherD.IsBuilt && _finisherDWorldPosition.HasValue)
        {
            DrawConnector(FinisherDOutput, ComponentBeltStart);
        }
        DrawJunction(ComponentBeltStart);
    }

    private void DrawBelt(Vector2 start, Vector2 end, string label, bool blocked)
    {
        DrawLine(start, end, StructureColor, 42);
        DrawLine(start, end, blocked ? BlockedColor : OutlineColor, 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(start.X + 25, start.Y - 32), label, HorizontalAlignment.Left, -1, 13, blocked ? BlockedColor : TextColor);
    }

    private void DrawConnector(Vector2 start, Vector2 end)
    {
        DrawLine(start, end, StructureColor, 16);
        DrawLine(start, end, OutlineColor, 3);
    }

    private void DrawJunction(Vector2 position)
    {
        DrawCircle(position, 11, StructureColor);
        DrawCircle(position, 11, OutlineColor, filled: false, width: 3);
    }

    private void DrawPort(Vector2 position, Color color)
    {
        DrawCircle(position, 10, BackgroundColor);
        DrawCircle(position, 10, color, filled: false, width: 4);
    }

    private void DrawMachineB()
    {
        if (_factory.MachineB.IsBuilt && _machineBWorldPosition.HasValue)
        {
            DrawMachine(MachineBRect, "MACHINE B", _factory.MachineB, RawColor);
            DrawInputTriangle(MachineBInput, RawColor);
            DrawOutputTriangle(MachineBOutput, IntermediateColor);
            return;
        }

        if (_interactionMode == InteractionMode.PlacingMachineB)
        {
            var preview = PlacementCandidate(ScreenToWorld(GetViewport().GetMousePosition()), ExpansionMachineFootprint);
            DrawMachinePlacementPreview(MachineBBuildArea, preview, "MACHINE B", IsValidMachineBPlacement(preview));
            return;
        }

        if (_factory.ExpansionsAvailable == 0)
        {
            DrawUnavailableExpansion(MachineBDefaultRect, "MACHINE B");
        }
    }

    private void DrawFinisherD()
    {
        if (_factory.FinisherD.IsBuilt && _finisherDWorldPosition.HasValue)
        {
            DrawMachine(FinisherDRect, "FINISHER D", _factory.FinisherD, IntermediateColor);
            DrawInputTriangle(FinisherDInput, IntermediateColor);
            DrawOutputTriangle(FinisherDOutput, ComponentColor);
            return;
        }

        if (_interactionMode == InteractionMode.PlacingFinisherD)
        {
            var preview = PlacementCandidate(ScreenToWorld(GetViewport().GetMousePosition()), ExpansionMachineFootprint);
            DrawMachinePlacementPreview(FinisherDBuildArea, preview, "FINISHER D", IsValidFinisherDPlacement(preview));
            return;
        }

        if (_factory.ExpansionsAvailable == 0)
        {
            DrawUnavailableExpansion(FinisherDDefaultRect, "FINISHER D");
        }
    }

    private void DrawMachinePlacementPreview(Rect2 buildArea, Rect2 preview, string name, bool valid)
    {
        DrawRect(buildArea, new Color(0.20f, 0.55f, 0.32f, 0.12f));
        DrawRect(buildArea, FinishedColor, filled: false, width: 3);
        var color = valid ? FinishedColor : BlockedColor;
        DrawRect(preview, new Color(color.R, color.G, color.B, 0.28f));
        DrawRect(preview, color, filled: false, width: 4);
        DrawString(ThemeDB.FallbackFont, preview.Position + new Vector2(58, 60), valid ? name : "INVALID", HorizontalAlignment.Left, -1, 17, color);
        DrawInputTriangle(new Vector2(preview.Position.X, preview.GetCenter().Y), color);
        DrawOutputTriangle(new Vector2(preview.End.X, preview.GetCenter().Y), color);
    }

    private void DrawUnavailableExpansion(Rect2 rect, string name)
    {
        DrawRect(rect, new Color(0.22f, 0.12f, 0.12f, 0.18f));
        DrawRect(rect, BlockedColor, filled: false, width: 2);
        DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(65, 48), name, HorizontalAlignment.Left, -1, 17, OutlineColor);
        DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(65, 73), "UNAVAILABLE", HorizontalAlignment.Left, -1, 15, BlockedColor);
    }

    private void DrawBuffer()
    {
        DrawRect(BufferRect, StructureColor);
        DrawRect(BufferRect, _factory.IsBufferFull ? BlockedColor : OutlineColor, filled: false, width: 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(BufferRect.Position.X + 30, BufferRect.Position.Y - 12), "WIP BUFFER", HorizontalAlignment.Left, -1, 16, TextColor);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(BufferRect.Position.X + 15, BufferRect.Position.Y + 54),
            $"{_factory.BufferCount} / {_factory.BufferCapacity}",
            HorizontalAlignment.Center,
            110,
            25,
            IntermediateColor);

        var capacityBar = new Rect2(BufferRect.Position + new Vector2(15, 75), new Vector2(110, 14));
        var fillWidth = capacityBar.Size.X * _factory.BufferCount / _factory.BufferCapacity;
        DrawRect(capacityBar, BackgroundColor);
        DrawRect(new Rect2(capacityBar.Position, new Vector2(fillWidth, capacityBar.Size.Y)), _factory.IsBufferFull ? BlockedColor : IntermediateColor);
        DrawRect(capacityBar, OutlineColor, filled: false, width: 2);

        if (_factory.IsBufferFull)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(BufferRect.Position.X + 11, BufferRect.End.Y + 25), "BUFFER FULL", HorizontalAlignment.Center, 118, 15, BlockedColor);
        }
    }

    private void DrawMachine(Rect2 rect, string name, MachineView machine, Color materialColor)
    {
        DrawRect(rect, machine.IsProcessing ? new Color(0.28f, 0.22f, 0.14f) : StructureColor);
        DrawRect(rect, FinishedColor, filled: false, width: 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X + 55, rect.Position.Y - 12), name, HorizontalAlignment.Left, -1, 17, TextColor);
        DrawMachineUtilization(rect, machine);

        if (!machine.IsProcessing)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X + 102, rect.Position.Y + 59), "idle", HorizontalAlignment.Left, -1, 16, OutlineColor);
            return;
        }

        DrawCircle(rect.GetCenter() - new Vector2(0, 12), 14, materialColor);
        var progressRect = new Rect2(rect.Position.X + 40, rect.End.Y - 23, 160, 9);
        DrawRect(progressRect, BackgroundColor);
        DrawRect(new Rect2(progressRect.Position, new Vector2(progressRect.Size.X * (float)machine.ProcessingProgress, progressRect.Size.Y)), FinishedColor);
    }

    private void DrawMachineUtilization(Rect2 machineRect, MachineView machine)
    {
        if (!machine.IsBuilt)
        {
            return;
        }

        var bar = new Rect2(machineRect.Position + new Vector2(18, 10), new Vector2(140, 9));
        DrawRect(bar, BackgroundColor);
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * (float)machine.RecentUtilization, bar.Size.Y)), FinishedColor);
        DrawRect(bar, OutlineColor, filled: false, width: 1);
        DrawString(
            ThemeDB.FallbackFont,
            machineRect.Position + new Vector2(170, 21),
            $"{machine.RecentUtilization:P0}",
            HorizontalAlignment.Left,
            -1,
            14,
            TextColor);
    }

    private void DrawComponentStorage()
    {
        var rect = ComponentStorageRect;
        DrawRect(rect, StructureColor);
        DrawRect(rect, _factory.IsComponentStorageFull ? BlockedColor : OutlineColor, filled: false, width: 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(1405, 240), "COMPONENT A STORAGE", HorizontalAlignment.Center, 180, 16, TextColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(1440, 325), $"{_factory.ComponentStorageCount} / {_factory.StorageCapacity}", HorizontalAlignment.Center, 110, 30, ComponentColor);

        var capacityBar = new Rect2(1435, 345, 120, 16);
        var fillWidth = capacityBar.Size.X * _factory.ComponentStorageCount / _factory.StorageCapacity;
        DrawRect(capacityBar, BackgroundColor);
        DrawRect(new Rect2(capacityBar.Position, new Vector2(fillWidth, capacityBar.Size.Y)), _factory.IsComponentStorageFull ? BlockedColor : ComponentColor);
        DrawRect(capacityBar, OutlineColor, filled: false, width: 2);

        if (_factory.IsComponentStorageFull)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(1427, 405), "STORAGE FULL", HorizontalAlignment.Center, 136, 15, BlockedColor);
        }
    }

    private void DrawTransport()
    {
        DrawTransportUnit(1, _factory.Transport1IsDelivering, _factory.Transport1Progress,
            _factory.Transport1CargoCount, _transportIsReturning, _transportReturnElapsed, -16);
        if (_factory.Transport2Exists)
        {
            DrawTransportUnit(2, _factory.Transport2IsDelivering, _factory.Transport2Progress,
                _factory.Transport2CargoCount, _transport2IsReturning, _transport2ReturnElapsed, 16);
        }
    }

    private void DrawFreightRoute()
    {
        if (_factory.DirectFreightLinkIsBuilt)
        {
            DrawPolyline(IndirectFreightRoute, new Color(OutlineColor, 0.22f), 5);
            DrawPolyline(DirectFreightRoute, new Color(0.18f, 0.16f, 0.12f), 32);
            DrawPolyline(DirectFreightRoute, FinishedColor, 8);
            DrawLine(new Vector2(1690, 300), new Vector2(1690, 340), OutlineColor, 7);
            DrawLine(new Vector2(1930, 300), new Vector2(1930, 340), OutlineColor, 7);
            DrawString(ThemeDB.FallbackFont, new Vector2(1730, 285), "DIRECT CAUSEWAY", HorizontalAlignment.Center, 210, 18, FinishedColor);
            return;
        }

        DrawPolyline(IndirectFreightRoute, new Color(0.17f, 0.18f, 0.17f), 24);
        DrawPolyline(IndirectFreightRoute, OutlineColor, 5);
        DrawString(ThemeDB.FallbackFont, new Vector2(1690, 655), "LONG FREIGHT DETOUR", HorizontalAlignment.Center, 300, 19, OutlineColor);
    }

    private void DrawTransportUnit(int number, bool isDelivering, double coreProgress, int cargoCount,
        bool isReturning, double returnElapsed, float laneOffset)
    {
        var assignedTravelSteps = number == 1
            ? _factory.Transport1AssignedTravelSteps
            : _factory.Transport2AssignedTravelSteps;
        var interpolation = assignedTravelSteps > 0
            ? (float)(_stepAccumulator / SimulationStepSeconds / assignedTravelSteps)
            : 0;
        Vector2 position;
        if (isDelivering)
        {
            var progress = Mathf.Min(1.0f, (float)coreProgress + interpolation);
            position = PositionAlongRoute(RouteForAssignedTravel(assignedTravelSteps), progress);
        }
        else if (isReturning)
        {
            var returnProgress = Mathf.Min(1.0f, (float)(returnElapsed / SimulationStepSeconds));
            position = PositionAlongRoute(RouteForAssignedTravel(assignedTravelSteps), 1.0f - returnProgress);
        }
        else
        {
            position = TransportHomePosition;
        }
        position.Y += laneOffset;
        var transportRect = new Rect2(position - new Vector2(24, 12), new Vector2(48, 24));
        DrawRect(transportRect, isDelivering ? ComponentColor : StructureColor);
        DrawRect(transportRect, OutlineColor, filled: false, width: 2);
        DrawString(ThemeDB.FallbackFont, position + new Vector2(-5, 5), number.ToString(), HorizontalAlignment.Left, -1, 12, TextColor);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(1580, 390 + number * 24),
            $"TRANSPORT {number}: {cargoCount} / {FactoryState.TransportCapacity}",
            HorizontalAlignment.Left,
            -1,
            14,
            isDelivering ? ComponentColor : OutlineColor);
    }

    private static Vector2[] RouteForAssignedTravel(int assignedTravelSteps)
    {
        return assignedTravelSteps == FactoryState.DirectFreightLinkTravelSteps
            ? DirectFreightRoute
            : IndirectFreightRoute;
    }

    private static Vector2 PositionAlongRoute(IReadOnlyList<Vector2> route, float progress)
    {
        var totalLength = 0.0f;
        for (var index = 1; index < route.Count; index++)
        {
            totalLength += route[index - 1].DistanceTo(route[index]);
        }

        var remaining = Mathf.Clamp(progress, 0, 1) * totalLength;
        for (var index = 1; index < route.Count; index++)
        {
            var segmentLength = route[index - 1].DistanceTo(route[index]);
            if (remaining <= segmentLength)
            {
                return route[index - 1].Lerp(route[index], remaining / segmentLength);
            }
            remaining -= segmentLength;
        }
        return route[^1];
    }

    private void DrawRemoteFactory()
    {
        DrawString(ThemeDB.FallbackFont, new Vector2(2050, 170), "REMOTE FACTORY 2", HorizontalAlignment.Left, -1, 24, TextColor);
        DrawRemoteInputStorage();
        DrawBelt(RemoteInputBeltStart, RemoteInputBeltEnd, "INPUT BELT A", _factory.RemoteInputBeltIsBlocked);
        DrawSourceB();
        if (_machineFWorldPosition.HasValue)
        {
            DrawBelt(RawBBeltStart, MachineFInput, "RAW-B BELT", _factory.SourceBIsBlocked);
            DrawMachine(MachineFRect, "MACHINE F", _factory.MachineF, RawBColor);
            DrawMachineFPorts();
            DrawComponentBBeltConnection();
        }
        else if (_interactionMode == InteractionMode.PlacingMachineF)
        {
            DrawMachineFPlacementPreview();
        }
        DrawMachine(AssemblerERect, "ASSEMBLER E", _factory.AssemblerE, ComponentColor);
        DrawInputTriangle(ComponentBBeltEnd, OutlineColor);
        DrawAssemblerInputs();
        DrawBelt(FinishedOutputBeltStart, FinishedOutputBeltEnd, "ASSEMBLY OUTPUT", _factory.FinishedOutputBeltIsBlocked);
    }

    private void DrawComponentBBeltConnection()
    {
        if (_factory.ComponentBBeltIsBuilt)
        {
            DrawBelt(MachineFOutput, ComponentBBeltEnd, "COMPONENT-B BELT", _factory.ComponentBBeltIsBlocked);
            return;
        }

        var awaitingSource = _interactionMode == InteractionMode.PlacingComponentBBeltAwaitingSource;
        var awaitingDestination = _interactionMode == InteractionMode.PlacingComponentBBeltAwaitingDestination;
        if (awaitingSource)
        {
            DrawCircle(MachineFOutput, 17, IntermediateColor, filled: false, width: 3);
        }
        if (awaitingDestination)
        {
            DrawCircle(ComponentBBeltEnd, 17, IntermediateColor, filled: false, width: 3);
        }
        if (awaitingDestination)
        {
            DrawDashedLine(MachineFOutput, ScreenToWorld(GetViewport().GetMousePosition()), OutlineColor, 3, 10);
        }
    }

    private void DrawMachineFPlacementPreview()
    {
        var preview = PlacementCandidate(ScreenToWorld(GetViewport().GetMousePosition()), MachineFFootprint);
        DrawMachinePlacementPreview(MachineFBuildArea, preview, "MACHINE F", IsValidMachineFPlacement(preview));
    }

    private void DrawMachineFPorts()
    {
        DrawInputTriangle(MachineFInput, RawBColor);
        DrawOutputTriangle(MachineFOutput, ComponentBColor);
    }

    private void DrawInputTriangle(Vector2 position, Color color)
    {
        var points = new Vector2[]
        {
            position + new Vector2(-10, -10),
            position + new Vector2(10, 0),
            position + new Vector2(-10, 10),
            position + new Vector2(-10, -10),
        };
        DrawPolyline(points, color, 3);
    }

    private void DrawOutputTriangle(Vector2 position, Color color)
    {
        DrawColoredPolygon(new Vector2[]
        {
            position + new Vector2(-10, -10),
            position + new Vector2(10, 0),
            position + new Vector2(-10, 10),
        }, color);
    }

    private void DrawSourceB()
    {
        var rect = SourceBRect;
        DrawRect(rect, StructureColor);
        DrawRect(rect, _factory.SourceBIsBlocked ? BlockedColor : OutlineColor, filled: false, width: 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(2070, 425), "SOURCE B", HorizontalAlignment.Left, -1, 18, TextColor);
        DrawCircle(rect.GetCenter(), 20, RawBColor);
    }

    private void DrawAssemblerInputs()
    {
        var aColor = _factory.AssemblerHasComponentA ? ComponentColor : OutlineColor;
        var bColor = _factory.AssemblerHasComponentB ? ComponentBColor : OutlineColor;
        DrawString(ThemeDB.FallbackFont, new Vector2(2580, 305), $"A: {(_factory.AssemblerHasComponentA ? "READY" : "WAITING")}", HorizontalAlignment.Left, -1, 14, aColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(2680, 305), $"B: {(_factory.AssemblerHasComponentB ? "READY" : "WAITING")}", HorizontalAlignment.Left, -1, 14, bColor);
    }

    private void DrawRemoteInputStorage()
    {
        var rect = new Rect2(2050, 255, 150, 130);
        DrawRect(rect, StructureColor);
        DrawRect(rect, _factory.IsRemoteInputStorageFull ? BlockedColor : OutlineColor, filled: false, width: 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(2040, 240), "REMOTE INPUT A", HorizontalAlignment.Center, 170, 16, TextColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(2070, 325), $"{_factory.RemoteInputStorageCount} / {FactoryState.RemoteInputStorageCapacity}", HorizontalAlignment.Center, 110, 30, ComponentColor);
        var bar = new Rect2(2065, 345, 120, 16);
        var fillWidth = bar.Size.X * _factory.RemoteInputStorageCount / FactoryState.RemoteInputStorageCapacity;
        DrawRect(bar, BackgroundColor);
        DrawRect(new Rect2(bar.Position, new Vector2(fillWidth, bar.Size.Y)), _factory.IsRemoteInputStorageFull ? BlockedColor : ComponentColor);
        DrawRect(bar, OutlineColor, filled: false, width: 2);
    }

    private void DrawShippingStorage()
    {
        var rect = new Rect2(2950, 265, _factory.ShippingStorageIsExpanded ? 220 : 150, 110);
        DrawRect(rect, StructureColor);
        DrawRect(rect, _factory.IsShippingStorageFull ? BlockedColor : OutlineColor, filled: false, width: 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(2945, 250), "SHIPPING STORAGE", HorizontalAlignment.Center, 160, 16, TextColor);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(2970, 325),
            $"{_factory.ShippingStorageCount} / {_factory.ShippingStorageCapacity}",
            HorizontalAlignment.Center,
            110,
            25,
            FinishedColor);

        var capacityBar = new Rect2(2965, 345, _factory.ShippingStorageIsExpanded ? 190 : 120, 14);
        var fillWidth = capacityBar.Size.X * _factory.ShippingStorageCount / _factory.ShippingStorageCapacity;
        DrawRect(capacityBar, BackgroundColor);
        DrawRect(
            new Rect2(capacityBar.Position, new Vector2(fillWidth, capacityBar.Size.Y)),
            _factory.IsShippingStorageFull ? BlockedColor : FinishedColor);
        DrawRect(capacityBar, OutlineColor, filled: false, width: 2);

    }

    private void DrawBeltItems(IReadOnlyList<BeltItemView> items, Vector2 start, Vector2 end, float interpolation)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var progress = Mathf.Min(1.0f, (float)item.Progress + interpolation / 2.0f);
            var position = start.Lerp(end, progress);
            if (item.Progress >= 1.0)
            {
                position.X -= index * ItemRadius * 2.2f;
            }
            DrawMaterial(position, item.Material);
        }
    }

    private void DrawMaterial(Vector2 position, MaterialKind material)
    {
        var color = material switch
        {
            MaterialKind.Raw => RawColor,
            MaterialKind.Intermediate => IntermediateColor,
            MaterialKind.ComponentA => ComponentColor,
            MaterialKind.RawB => RawBColor,
            MaterialKind.ComponentB => ComponentBColor,
            MaterialKind.Finished => FinishedColor,
            _ => TextColor,
        };
        DrawCircle(position, ItemRadius, color);

        if (material == MaterialKind.Intermediate)
        {
            DrawCircle(position, ItemRadius * 0.42f, BackgroundColor);
        }
        else if (material == MaterialKind.ComponentA)
        {
            DrawRect(new Rect2(position - new Vector2(5, 5), new Vector2(10, 10)), TextColor);
        }
        else if (material == MaterialKind.RawB)
        {
            DrawLine(position - new Vector2(6, 0), position + new Vector2(6, 0), BackgroundColor, 3);
        }
        else if (material == MaterialKind.ComponentB)
        {
            DrawRect(new Rect2(position - new Vector2(6, 3), new Vector2(12, 6)), TextColor);
        }
        else if (material == MaterialKind.Finished)
        {
            DrawCircle(position, ItemRadius * 0.28f, TextColor);
        }
    }

    private static bool IsPortHit(Vector2 clickPosition, Vector2 portPosition)
    {
        return clickPosition.DistanceTo(portPosition) <= 20;
    }

    private enum InteractionMode
    {
        Normal,
        PlacingRawBeltAwaitingStart,
        PlacingRawBeltAwaitingEnd,
        PlacingMachineB,
        PlacingFinisherD,
        PlacingMachineF,
        PlacingComponentBBeltAwaitingSource,
        PlacingComponentBBeltAwaitingDestination,
    }

    private enum SimulationSpeed
    {
        Paused = 0,
        Normal = 1,
        Double = 2,
        Quadruple = 4,
    }
}
