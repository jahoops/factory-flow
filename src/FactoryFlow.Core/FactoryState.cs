namespace FactoryFlow.Core;

public enum MaterialKind
{
    Raw,
    Intermediate,
    ComponentA,
    RawB,
    ComponentB,
    Finished,
}

public enum StageOneRoutingPolicy
{
    Alternate,
    PriorityAWithOverflowB,
}

public readonly record struct BeltItemView(MaterialKind Material, double Progress);

public readonly record struct MachineView(
    bool IsBuilt,
    bool IsProcessing,
    double ProcessingProgress,
    double RecentUtilization);

public sealed class FactoryState
{
    public const int StartingOperatingRunwayCredits = 3500;
    public const int MachineFPurchaseCost = 500;
    public const int MachineBPurchaseCost = 750;
    public const int FinisherDPurchaseCost = 750;
    public const int SecondTransportPurchaseCost = 750;
    public const int FrontierRegionRevealCost = 2000;
    public const int StartingCredits = StartingOperatingRunwayCredits + MachineFPurchaseCost;
    public const int IdleMachinePowerPerStep = 2;
    public const int ProducingMachinePowerPerStep = 3;
    public const int GridCostPerPowerUnit = 1;
    public const int FinishedItemShipmentRevenue = 34;
    public const int TransportCapacity = 6;
    public const int TransportTravelSteps = 15;
    public const int DirectFreightLinkTravelSteps = 6;
    public const int TransportEnergyCostPerCargoItem = 1;
    public const int IndirectRouteSurchargePerCargoItem = 2;
    public const int RemoteInputStorageCapacity = 12;
    public const int RemoteBeltCapacity = 5;
    public const int RemoteBeltTravelSteps = 2;
    public const int AssemblerProcessingSteps = 1;
    public const int MachineFProcessingSteps = 1;
    public const int ComponentBBeltCapacity = 5;
    public const int InitialShippingStorageCapacity = 12;
    public const int ExpandedShippingStorageCapacity = 48;
    public const int ShippingStorageExpansionFinishedItemCost = 12;
    public const int DirectFreightLinkAssemblyCost = 12;
    public const int CustomerOrderSize = 12;
    public const int UtilizationWindowSteps = 200;

    private const int BeltTravelSteps = 2;

    private readonly int _beltCapacity;
    private readonly int _branchBeltCapacity;
    private readonly int _bufferCapacity;
    private readonly int _storageCapacity;
    private readonly int[] _stageOneProcessingSteps;
    private readonly int _finisherProcessingSteps;
    private readonly int _sourceIntervalSteps;
    private readonly int _sourceBIntervalSteps;
    private readonly int _sourceBStartDelaySteps;
    private readonly List<BeltItem> _rawInputBelt = [];
    private readonly List<BeltItem> _branchBeltA = [];
    private readonly List<BeltItem> _branchBeltB = [];
    private readonly List<BeltItem> _intermediateBelt = [];
    private readonly List<BeltItem> _componentBelt = [];
    private readonly List<BeltItem> _remoteInputBelt = [];
    private readonly List<BeltItem> _finishedOutputBelt = [];
    private readonly List<BeltItem> _rawBBelt = [];
    private readonly List<BeltItem> _componentBBelt = [];
    private readonly MachineState[] _stageOneMachines = [new(isBuilt: true), new(isBuilt: false)];
    private readonly MachineState[] _finishers = [new(isBuilt: true), new(isBuilt: false)];
    private readonly MachineState _assembler = new(isBuilt: true);
    private readonly MachineState _machineF = new(isBuilt: false);
    private bool _assemblerHasComponentA;
    private bool _assemblerHasComponentB;
    private bool _componentBBeltIsBuilt;
    private bool _inputBeltIsBuilt;
    private bool _expansionAvailable = true;
    private int _bufferCount;
    private int _nextAlternateBranch;
    private int _shippingStorageCount;
    private bool _shippingStorageIsExpanded;
    private bool _directFreightLinkIsBuilt;
    private int _remoteInputStorageCount;
    private int _transportCargoCount;
    private int _transportStepsRemaining;
    private int _transportAssignedTravelSteps;
    private bool _transport2IsBuilt;
    private int _transport2CargoCount;
    private int _transport2StepsRemaining;
    private int _transport2AssignedTravelSteps;
    private bool _transport1WaitingForCredits;
    private bool _transport2WaitingForCredits;

    public FactoryState(
        int machineAProcessingSteps = 1,
        int machineBProcessingSteps = 4,
        int finisherProcessingSteps = 1,
        int beltCapacity = 5,
        int branchBeltCapacity = 2,
        int bufferCapacity = 6,
        int storageCapacity = 12,
        int sourceIntervalSteps = 2,
        int sourceBIntervalSteps = 1,
        int sourceBStartDelaySteps = 0,
        int startingCredits = StartingCredits,
        bool northRegionIsRevealed = false,
        bool westRegionIsRevealed = false,
        bool eastRegionIsRevealed = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(machineAProcessingSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(machineBProcessingSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(finisherProcessingSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(beltCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(branchBeltCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(storageCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceIntervalSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceBIntervalSteps, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceBStartDelaySteps);
        ArgumentOutOfRangeException.ThrowIfNegative(startingCredits);

        _stageOneProcessingSteps = [machineAProcessingSteps, machineBProcessingSteps];
        _finisherProcessingSteps = finisherProcessingSteps;
        _beltCapacity = beltCapacity;
        _branchBeltCapacity = branchBeltCapacity;
        _bufferCapacity = bufferCapacity;
        _storageCapacity = storageCapacity;
        _sourceIntervalSteps = sourceIntervalSteps;
        _sourceBIntervalSteps = sourceBIntervalSteps;
        _sourceBStartDelaySteps = sourceBStartDelaySteps;
        Credits = startingCredits;
        NorthRegionIsRevealed = northRegionIsRevealed;
        WestRegionIsRevealed = westRegionIsRevealed;
        EastRegionIsRevealed = eastRegionIsRevealed;
    }

    public long ElapsedSteps { get; private set; }

    public int MaterialCreated { get; private set; }

    public int Credits { get; private set; }

    public bool HomeRegionIsRevealed => true;

    public bool NorthRegionIsRevealed { get; private set; }

    public bool WestRegionIsRevealed { get; private set; }

    public bool EastRegionIsRevealed { get; private set; }

    public int CurrentPowerDraw => _stageOneMachines.Sum(GetMachinePower) + _finishers.Sum(GetMachinePower)
        + GetMachinePower(_assembler) + GetMachinePower(_machineF);

    public int CurrentGridCostPerStep => CurrentPowerDraw * GridCostPerPowerUnit;

    public bool IsOutOfCredits => Credits == 0;

    public int ExpansionsAvailable => _expansionAvailable ? 1 : 0;

    public StageOneRoutingPolicy StageOneRoutingPolicy { get; private set; } = StageOneRoutingPolicy.Alternate;

    public bool InputBeltIsBuilt => _inputBeltIsBuilt;

    public int RawInputBeltCount => _rawInputBelt.Count;

    public int RawInputBeltCapacity => _beltCapacity;

    public IReadOnlyList<BeltItemView> RawInputBeltItems => CreateBeltView(_rawInputBelt);

    public int BranchBeltACount => _branchBeltA.Count;

    public int BranchBeltBCount => _branchBeltB.Count;

    public int BranchBeltCapacity => _branchBeltCapacity;

    public IReadOnlyList<BeltItemView> BranchBeltAItems => CreateBeltView(_branchBeltA);

    public IReadOnlyList<BeltItemView> BranchBeltBItems => CreateBeltView(_branchBeltB);

    public int IntermediateBeltCount => _intermediateBelt.Count;

    public int IntermediateBeltCapacity => _beltCapacity;

    public IReadOnlyList<BeltItemView> IntermediateBeltItems => CreateBeltView(_intermediateBelt);

    public int BufferCount => _bufferCount;

    public int BufferCapacity => _bufferCapacity;

    public bool IsBufferFull => _bufferCount == _bufferCapacity;

    public int ComponentBeltCount => _componentBelt.Count;

    public int ComponentBeltCapacity => _beltCapacity;

    public IReadOnlyList<BeltItemView> ComponentBeltItems => CreateBeltView(_componentBelt);

    public int RemoteInputBeltCount => _remoteInputBelt.Count;

    public IReadOnlyList<BeltItemView> RemoteInputBeltItems => CreateBeltView(_remoteInputBelt, RemoteBeltTravelSteps);

    public int FinishedOutputBeltCount => _finishedOutputBelt.Count;

    public IReadOnlyList<BeltItemView> FinishedOutputBeltItems => CreateBeltView(_finishedOutputBelt, RemoteBeltTravelSteps);

    public MachineView MachineA => CreateMachineView(_stageOneMachines[0], _stageOneProcessingSteps[0]);

    public MachineView MachineB => CreateMachineView(_stageOneMachines[1], _stageOneProcessingSteps[1]);

    public MachineView FinisherC => CreateMachineView(_finishers[0], _finisherProcessingSteps);

    public MachineView FinisherD => CreateMachineView(_finishers[1], _finisherProcessingSteps);

    public MachineView AssemblerE => CreateMachineView(_assembler, AssemblerProcessingSteps);

    public MachineView MachineF => CreateMachineView(_machineF, MachineFProcessingSteps);

    public bool MachineFIsBuilt => _machineF.IsBuilt;

    public bool AssemblerHasComponentA => _assemblerHasComponentA;

    public bool AssemblerHasComponentB => _assemblerHasComponentB;

    public int RawBBeltCount => _rawBBelt.Count;

    public IReadOnlyList<BeltItemView> RawBBeltItems => CreateBeltView(_rawBBelt);

    public int ComponentBBeltCount => _componentBBelt.Count;

    public IReadOnlyList<BeltItemView> ComponentBBeltItems => CreateBeltView(_componentBBelt);

    public bool SourceBIsBlocked => _rawBBelt.Count >= ComponentBBeltCapacity;

    public bool ComponentBBeltIsBlocked => _componentBBelt.Count >= ComponentBBeltCapacity;

    public bool ComponentBBeltIsBuilt => _componentBBeltIsBuilt;

    public int MaterialBCreated { get; private set; }

    public bool SourceIsBlocked => _rawInputBelt.Count >= _beltCapacity;

    public bool IntermediateBeltIsBlocked => _intermediateBelt.Count >= _beltCapacity;

    public bool ComponentBeltIsBlocked => _componentBelt.Count >= _beltCapacity;

    public bool RemoteInputBeltIsBlocked => _remoteInputBelt.Count >= RemoteBeltCapacity;

    public bool FinishedOutputBeltIsBlocked => _finishedOutputBelt.Count >= RemoteBeltCapacity;

    public int ComponentStorageCount { get; private set; }

    public int StorageCapacity => _storageCapacity;

    public bool IsComponentStorageFull => ComponentStorageCount == _storageCapacity;

    public int ShippedFinishedCount { get; private set; }

    public int TotalFinishedProduced => _finishedOutputBelt.Count + _shippingStorageCount
        + ShippedFinishedCount + FinishedItemsConsumedForInfrastructure;

    public int TransportCargoCount => _transportCargoCount;

    public bool TransportIsDelivering => _transportCargoCount > 0;

    public double TransportProgress => TransportIsDelivering
        ? (_transportAssignedTravelSteps - _transportStepsRemaining) / (double)_transportAssignedTravelSteps
        : 0;

    public bool Transport1Exists => true;

    public int Transport1CargoCount => _transportCargoCount;

    public bool Transport1IsDelivering => TransportIsDelivering;

    public double Transport1Progress => TransportProgress;

    public bool Transport2Exists => _transport2IsBuilt;

    public int Transport2CargoCount => _transport2CargoCount;

    public bool Transport2IsDelivering => _transport2CargoCount > 0;

    public double Transport2Progress => Transport2IsDelivering
        ? (_transport2AssignedTravelSteps - _transport2StepsRemaining) / (double)_transport2AssignedTravelSteps
        : 0;

    public int Transport1AssignedTravelSteps => _transportAssignedTravelSteps;

    public int Transport2AssignedTravelSteps => _transport2AssignedTravelSteps;

    public int CommittedTransportCargoCount => _transportCargoCount + _transport2CargoCount;

    public bool Transport1WaitingForCredits => _transport1WaitingForCredits;

    public bool Transport2WaitingForCredits => _transport2WaitingForCredits;

    public int ShippingStorageCount => _shippingStorageCount;

    public bool ShippingStorageIsExpanded => _shippingStorageIsExpanded;

    public int ShippingStorageCapacity => _shippingStorageIsExpanded
        ? ExpandedShippingStorageCapacity
        : InitialShippingStorageCapacity;

    public int FinishedItemsConsumedForInfrastructure { get; private set; }

    public bool DirectFreightLinkIsBuilt => _directFreightLinkIsBuilt;

    public int ComponentATransportTravelSteps => _directFreightLinkIsBuilt
        ? DirectFreightLinkTravelSteps
        : TransportTravelSteps;

    public int ComponentATransportCostPerCargoItem => TransportEnergyCostPerCargoItem
        + (_directFreightLinkIsBuilt ? 0 : IndirectRouteSurchargePerCargoItem);

    public int RemoteInputStorageCount => _remoteInputStorageCount;

    public bool IsRemoteInputStorageFull => _remoteInputStorageCount == RemoteInputStorageCapacity;

    public bool IsShippingStorageFull => _shippingStorageCount == ShippingStorageCapacity;

    public int StoredRawMaterialCount { get; private set; }

    public int StoredIntermediateMaterialCount { get; private set; }

    public int StoredComponentMaterialCount => ComponentStorageCount - StoredRawMaterialCount - StoredIntermediateMaterialCount;

    public bool RevealNorthRegion()
    {
        if (NorthRegionIsRevealed || Credits < FrontierRegionRevealCost)
        {
            return false;
        }

        Credits -= FrontierRegionRevealCost;
        NorthRegionIsRevealed = true;
        return true;
    }

    public bool RevealWestRegion()
    {
        if (WestRegionIsRevealed || Credits < FrontierRegionRevealCost)
        {
            return false;
        }

        Credits -= FrontierRegionRevealCost;
        WestRegionIsRevealed = true;
        return true;
    }

    public bool RevealEastRegion()
    {
        if (EastRegionIsRevealed || Credits < FrontierRegionRevealCost)
        {
            return false;
        }

        Credits -= FrontierRegionRevealCost;
        EastRegionIsRevealed = true;
        return true;
    }

    public bool BuildInputBelt()
    {
        if (_inputBeltIsBuilt)
        {
            return false;
        }

        _inputBeltIsBuilt = true;
        return true;
    }

    public bool BuildMachineB()
    {
        if (!_expansionAvailable || _stageOneMachines[1].IsBuilt || Credits < MachineBPurchaseCost)
        {
            return false;
        }

        Credits -= MachineBPurchaseCost;
        _stageOneMachines[1].IsBuilt = true;
        _expansionAvailable = false;
        return true;
    }

    public bool BuildFinisherD()
    {
        if (!_expansionAvailable || _finishers[1].IsBuilt || Credits < FinisherDPurchaseCost)
        {
            return false;
        }

        Credits -= FinisherDPurchaseCost;
        _finishers[1].IsBuilt = true;
        _expansionAvailable = false;
        return true;
    }

    public bool BuildSecondTransport()
    {
        if (_transport2IsBuilt || Credits < SecondTransportPurchaseCost)
        {
            return false;
        }

        Credits -= SecondTransportPurchaseCost;
        _transport2IsBuilt = true;
        return true;
    }

    public bool BuildMachineF()
    {
        if (_machineF.IsBuilt || Credits < MachineFPurchaseCost)
        {
            return false;
        }

        Credits -= MachineFPurchaseCost;
        _machineF.IsBuilt = true;
        return true;
    }

    public bool BuildComponentBBelt()
    {
        if (!_machineF.IsBuilt || _componentBBeltIsBuilt)
        {
            return false;
        }

        _componentBBeltIsBuilt = true;
        return true;
    }

    public bool BuildShippingStorageExpansion()
    {
        if (_shippingStorageIsExpanded
            || _shippingStorageCount < ShippingStorageExpansionFinishedItemCost)
        {
            return false;
        }

        _shippingStorageCount -= ShippingStorageExpansionFinishedItemCost;
        FinishedItemsConsumedForInfrastructure += ShippingStorageExpansionFinishedItemCost;
        _shippingStorageIsExpanded = true;
        return true;
    }

    public bool BuildDirectFreightLink()
    {
        if (_directFreightLinkIsBuilt || _shippingStorageCount < DirectFreightLinkAssemblyCost)
        {
            return false;
        }

        _shippingStorageCount -= DirectFreightLinkAssemblyCost;
        FinishedItemsConsumedForInfrastructure += DirectFreightLinkAssemblyCost;
        _directFreightLinkIsBuilt = true;
        return true;
    }

    public bool SetStageOneRoutingPolicy(StageOneRoutingPolicy policy)
    {
        if (!_stageOneMachines[1].IsBuilt || !Enum.IsDefined(policy))
        {
            return false;
        }

        StageOneRoutingPolicy = policy;
        return true;
    }

    public bool DispatchFullShipment()
    {
        if (_shippingStorageCount < CustomerOrderSize)
        {
            return false;
        }

        _shippingStorageCount -= CustomerOrderSize;
        ShippedFinishedCount += CustomerOrderSize;
        Credits += CustomerOrderSize * FinishedItemShipmentRevenue;
        return true;
    }

    public bool AdvanceOneStep()
    {
        if (IsOutOfCredits)
        {
            ElapsedSteps++;
            RecordNonproductiveMachineSteps();
            return true;
        }

        Credits = Math.Max(0, Credits - CurrentGridCostPerStep);
        ElapsedSteps++;

        DeliverFinishedOutputToShippingStorage();
        MoveCompletedItemsToBelt(_assembler, _finishedOutputBelt, MaterialKind.Finished, RemoteBeltCapacity, RemoteBeltTravelSteps);
        ProcessMachine(_assembler);
        AcceptAssemblerInputs();
        StartAssemblerIfReady();
        AdvanceBelt(_finishedOutputBelt);
        AdvanceBelt(_remoteInputBelt);
        AdvanceBelt(_componentBBelt);
        FeedRemoteInputBelt();
        AdvanceTransport(ref _transportCargoCount, ref _transportStepsRemaining, ref _transportAssignedTravelSteps,
            ref _transport1WaitingForCredits, isBuilt: true);
        AdvanceTransport(ref _transport2CargoCount, ref _transport2StepsRemaining, ref _transport2AssignedTravelSteps,
            ref _transport2WaitingForCredits, _transport2IsBuilt);

        if (_componentBBeltIsBuilt)
        {
            MoveCompletedItemsToBelt(_machineF, _componentBBelt, MaterialKind.ComponentB, ComponentBBeltCapacity, BeltTravelSteps);
        }
        ProcessMachine(_machineF);
        MoveRawBItemToMachineF();
        AdvanceBelt(_rawBBelt);
        ProduceRawB();

        DeliverComponentBeltToStorage();
        MoveCompletedItemsToBelt(_finishers, _componentBelt, MaterialKind.ComponentA);
        ProcessMachines(_finishers);
        MoveBufferItemsToFinishers();
        AdvanceBelt(_componentBelt);

        MoveCompletedItemsToBelt(_stageOneMachines, _intermediateBelt, MaterialKind.Intermediate);
        ProcessMachines(_stageOneMachines);
        MoveBranchItemToMachine(_branchBeltA, _stageOneMachines[0], _stageOneProcessingSteps[0]);
        MoveBranchItemToMachine(_branchBeltB, _stageOneMachines[1], _stageOneProcessingSteps[1]);
        MoveIntermediateItemToBuffer();
        AdvanceBelt(_intermediateBelt);
        AdvanceBelt(_branchBeltA);
        AdvanceBelt(_branchBeltB);
        RouteRawMaterial();
        AdvanceBelt(_rawInputBelt);

        ProduceRawMaterial();
        return true;
    }

    private void AdvanceTransport(ref int cargoCount, ref int stepsRemaining, ref int assignedTravelSteps,
        ref bool waitingForCredits, bool isBuilt)
    {
        waitingForCredits = false;
        if (!isBuilt)
        {
            return;
        }

        if (cargoCount == 0)
        {
            var uncommittedDestinationCapacity = RemoteInputStorageCapacity
                - _remoteInputStorageCount - CommittedTransportCargoCount;
            var loadSize = Math.Min(
                TransportCapacity,
                Math.Min(ComponentStorageCount, uncommittedDestinationCapacity));
            if (loadSize == 0)
            {
                return;
            }

            var departureCost = loadSize * ComponentATransportCostPerCargoItem;
            if (Credits < departureCost)
            {
                waitingForCredits = true;
                return;
            }

            Credits -= departureCost;
            ComponentStorageCount -= loadSize;
            cargoCount = loadSize;
            assignedTravelSteps = ComponentATransportTravelSteps;
            stepsRemaining = assignedTravelSteps;
            return;
        }

        if (stepsRemaining > 0)
        {
            stepsRemaining--;
        }

        if (stepsRemaining == 0
            && _remoteInputStorageCount + cargoCount <= RemoteInputStorageCapacity)
        {
            _remoteInputStorageCount += cargoCount;
            cargoCount = 0;
        }
    }

    private static int GetMachinePower(MachineState machine)
    {
        if (!machine.IsBuilt)
        {
            return 0;
        }

        return machine.Item.HasValue && machine.StepsRemaining > 0
            ? ProducingMachinePowerPerStep
            : IdleMachinePowerPerStep;
    }

    private void DeliverComponentBeltToStorage()
    {
        for (var index = _componentBelt.Count - 1; index >= 0; index--)
        {
            var item = _componentBelt[index];
            if (item.StepsRemaining > 0)
            {
                continue;
            }

            if (IsComponentStorageFull)
            {
                return;
            }

            _componentBelt.RemoveAt(index);
            ComponentStorageCount++;

            switch (item.Material)
            {
                case MaterialKind.Raw:
                    StoredRawMaterialCount++;
                    break;
                case MaterialKind.Intermediate:
                    StoredIntermediateMaterialCount++;
                    break;
                case MaterialKind.ComponentA:
                case MaterialKind.RawB:
                case MaterialKind.ComponentB:
                case MaterialKind.Finished:
                    break;
            }
        }
    }

    private void FeedRemoteInputBelt()
    {
        if (_remoteInputStorageCount == 0 || _remoteInputBelt.Count >= RemoteBeltCapacity)
        {
            return;
        }

        _remoteInputStorageCount--;
        _remoteInputBelt.Add(new BeltItem(MaterialKind.ComponentA, RemoteBeltTravelSteps));
    }

    private void AcceptAssemblerInputs()
    {
        if (!_assemblerHasComponentA)
        {
            var itemIndex = _remoteInputBelt.FindIndex(item => item.StepsRemaining == 0 && item.Material == MaterialKind.ComponentA);
            if (itemIndex >= 0)
            {
                _remoteInputBelt.RemoveAt(itemIndex);
                _assemblerHasComponentA = true;
            }
        }

        if (!_assemblerHasComponentB)
        {
            var itemIndex = _componentBBelt.FindIndex(item => item.StepsRemaining == 0 && item.Material == MaterialKind.ComponentB);
            if (itemIndex >= 0)
            {
                _componentBBelt.RemoveAt(itemIndex);
                _assemblerHasComponentB = true;
            }
        }
    }

    private void StartAssemblerIfReady()
    {
        if (_assembler.Item.HasValue || !_assemblerHasComponentA || !_assemblerHasComponentB)
        {
            return;
        }

        _assemblerHasComponentA = false;
        _assemblerHasComponentB = false;
        _assembler.Item = MaterialKind.Finished;
        _assembler.StepsRemaining = AssemblerProcessingSteps;
    }

    private void MoveRawBItemToMachineF()
    {
        if (!_machineF.IsBuilt || _machineF.Item.HasValue)
        {
            return;
        }

        var itemIndex = _rawBBelt.FindIndex(item => item.StepsRemaining == 0 && item.Material == MaterialKind.RawB);
        if (itemIndex < 0)
        {
            return;
        }

        _rawBBelt.RemoveAt(itemIndex);
        _machineF.Item = MaterialKind.RawB;
        _machineF.StepsRemaining = MachineFProcessingSteps;
    }

    private void ProduceRawB()
    {
        if (ElapsedSteps <= _sourceBStartDelaySteps
            || (ElapsedSteps - _sourceBStartDelaySteps - 1) % _sourceBIntervalSteps != 0
            || SourceBIsBlocked)
        {
            return;
        }

        _rawBBelt.Add(new BeltItem(MaterialKind.RawB, BeltTravelSteps));
        MaterialBCreated++;
    }

    private void DeliverFinishedOutputToShippingStorage()
    {
        var itemIndex = _finishedOutputBelt.FindIndex(item => item.StepsRemaining == 0 && item.Material == MaterialKind.Finished);
        if (itemIndex < 0 || IsShippingStorageFull)
        {
            return;
        }

        _finishedOutputBelt.RemoveAt(itemIndex);
        _shippingStorageCount++;
    }

    private void MoveCompletedItemsToBelt(
        IEnumerable<MachineState> machines,
        List<BeltItem> destination,
        MaterialKind outputMaterial)
    {
        foreach (var machine in machines)
        {
            if (machine.Item is null || machine.StepsRemaining > 0 || destination.Count >= _beltCapacity)
            {
                continue;
            }

            destination.Add(new BeltItem(outputMaterial, BeltTravelSteps));
            machine.Item = null;
        }
    }

    private static void MoveCompletedItemsToBelt(
        MachineState machine,
        List<BeltItem> destination,
        MaterialKind outputMaterial,
        int capacity,
        int travelSteps)
    {
        if (machine.Item is null || machine.StepsRemaining > 0 || destination.Count >= capacity)
        {
            return;
        }

        destination.Add(new BeltItem(outputMaterial, travelSteps));
        machine.Item = null;
    }

    private static void ProcessMachines(IEnumerable<MachineState> machines)
    {
        foreach (var machine in machines)
        {
            ProcessMachine(machine);
        }
    }

    private static void ProcessMachine(MachineState machine)
    {
        var productive = machine.Item.HasValue && machine.StepsRemaining > 0;
        if (productive)
        {
            machine.StepsRemaining--;
        }

        if (machine.IsBuilt)
        {
            machine.Utilization.Record(productive);
        }
    }

    private void RecordNonproductiveMachineSteps()
    {
        foreach (var machine in _stageOneMachines)
        {
            if (machine.IsBuilt)
            {
                machine.Utilization.Record(productive: false);
            }
        }
        foreach (var machine in _finishers)
        {
            if (machine.IsBuilt)
            {
                machine.Utilization.Record(productive: false);
            }
        }
        _assembler.Utilization.Record(productive: false);
        if (_machineF.IsBuilt)
        {
            _machineF.Utilization.Record(productive: false);
        }
    }

    private void MoveBufferItemsToFinishers()
    {
        foreach (var finisher in _finishers)
        {
            if (!finisher.IsBuilt || finisher.Item.HasValue || _bufferCount == 0)
            {
                continue;
            }

            _bufferCount--;
            finisher.Item = MaterialKind.Intermediate;
            finisher.StepsRemaining = _finisherProcessingSteps;
        }
    }

    private void MoveIntermediateItemToBuffer()
    {
        if (IsBufferFull)
        {
            return;
        }

        var itemIndex = _intermediateBelt.FindIndex(
            item => item.StepsRemaining == 0 && item.Material == MaterialKind.Intermediate);
        if (itemIndex < 0)
        {
            return;
        }

        _intermediateBelt.RemoveAt(itemIndex);
        _bufferCount++;
    }

    private static void MoveBranchItemToMachine(List<BeltItem> branch, MachineState machine, int processingSteps)
    {
        if (!machine.IsBuilt || machine.Item.HasValue)
        {
            return;
        }

        var itemIndex = branch.FindIndex(item => item.StepsRemaining == 0);
        if (itemIndex < 0)
        {
            return;
        }

        machine.Item = branch[itemIndex].Material;
        machine.StepsRemaining = processingSteps;
        branch.RemoveAt(itemIndex);
    }

    private void RouteRawMaterial()
    {
        var itemIndex = _rawInputBelt.FindIndex(item => item.StepsRemaining == 0);
        if (itemIndex < 0)
        {
            return;
        }

        List<BeltItem>? destination;
        if (!_stageOneMachines[1].IsBuilt)
        {
            destination = CanAccept(_branchBeltA) ? _branchBeltA : null;
        }
        else if (StageOneRoutingPolicy == StageOneRoutingPolicy.Alternate)
        {
            destination = _nextAlternateBranch == 0 ? _branchBeltA : _branchBeltB;
            if (!CanAccept(destination))
            {
                return;
            }
            _nextAlternateBranch = 1 - _nextAlternateBranch;
        }
        else
        {
            destination = CanAccept(_branchBeltA)
                ? _branchBeltA
                : CanAccept(_branchBeltB) ? _branchBeltB : null;
        }

        if (destination is null)
        {
            return;
        }

        destination.Add(new BeltItem(_rawInputBelt[itemIndex].Material, BeltTravelSteps));
        _rawInputBelt.RemoveAt(itemIndex);
    }

    private bool CanAccept(List<BeltItem> branch)
    {
        return branch.Count < _branchBeltCapacity;
    }

    private static void AdvanceBelt(List<BeltItem> belt)
    {
        for (var index = 0; index < belt.Count; index++)
        {
            var item = belt[index];
            if (item.StepsRemaining > 0)
            {
                belt[index] = item with { StepsRemaining = item.StepsRemaining - 1 };
            }
        }
    }

    private static IReadOnlyList<BeltItemView> CreateBeltView(List<BeltItem> belt, int travelSteps = BeltTravelSteps)
    {
        var view = new BeltItemView[belt.Count];

        for (var index = 0; index < belt.Count; index++)
        {
            var item = belt[index];
            var progress = (travelSteps - item.StepsRemaining) / (double)travelSteps;
            view[index] = new BeltItemView(item.Material, progress);
        }

        return view;
    }

    private static MachineView CreateMachineView(MachineState machine, int processingSteps)
    {
        var progress = machine.Item.HasValue
            ? (processingSteps - machine.StepsRemaining) / (double)processingSteps
            : 0;

        return new MachineView(machine.IsBuilt, machine.Item.HasValue, progress, machine.Utilization.Value);
    }

    private void ProduceRawMaterial()
    {
        if (!_inputBeltIsBuilt || (ElapsedSteps - 1) % _sourceIntervalSteps != 0)
        {
            return;
        }

        if (_rawInputBelt.Count >= _beltCapacity)
        {
            return;
        }

        _rawInputBelt.Add(new BeltItem(MaterialKind.Raw, BeltTravelSteps));
        MaterialCreated++;
    }

    private readonly record struct BeltItem(MaterialKind Material, int StepsRemaining);

    private sealed class MachineState(bool isBuilt)
    {
        public bool IsBuilt { get; set; } = isBuilt;

        public MaterialKind? Item { get; set; }

        public int StepsRemaining { get; set; }

        public RecentUtilization Utilization { get; } = new();
    }

    private sealed class RecentUtilization
    {
        private readonly bool[] _samples = new bool[UtilizationWindowSteps];
        private int _nextIndex;
        private int _sampleCount;
        private int _productiveCount;

        public double Value => _sampleCount == 0 ? 0 : _productiveCount / (double)_sampleCount;

        public void Record(bool productive)
        {
            if (_sampleCount == UtilizationWindowSteps)
            {
                if (_samples[_nextIndex])
                {
                    _productiveCount--;
                }
            }
            else
            {
                _sampleCount++;
            }

            _samples[_nextIndex] = productive;
            if (productive)
            {
                _productiveCount++;
            }
            _nextIndex = (_nextIndex + 1) % UtilizationWindowSteps;
        }
    }
}
