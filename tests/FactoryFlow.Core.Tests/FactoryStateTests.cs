using FactoryFlow.Core;

namespace FactoryFlow.Core.Tests;

public sealed class FactoryStateTests
{
    [Fact]
    public void CompleteChainUsesEveryRequiredMaterialStateAndLocation()
    {
        var factory = CreateFactory();
        var seen = new bool[7];
        for (var step = 0; step < 400 && factory.ShippingStorageCount == 0; step++)
        {
            factory.AdvanceOneStep();
            seen[0] |= factory.ComponentBeltCount > 0;
            seen[1] |= factory.ComponentStorageCount > 0;
            seen[2] |= factory.TransportCargoCount > 0;
            seen[3] |= factory.RemoteInputStorageCount > 0;
            seen[4] |= factory.RemoteInputBeltCount > 0;
            seen[5] |= factory.AssemblerE.IsProcessing;
            seen[6] |= factory.FinishedOutputBeltCount > 0;
            AssertKinds(factory);
            AssertConservation(factory);
        }
        Assert.All(seen, Assert.True);
        Assert.True(factory.ShippingStorageCount > 0);
    }

    [Fact]
    public void RemoteBeltAndAssemblerTakeFiniteTime()
    {
        var factory = CreateFactory(buildBelt: false);
        Advance(factory, 50);
        Assert.False(factory.AssemblerE.IsProcessing);
        Assert.Equal(0, factory.TotalFinishedProduced);
        factory.BuildInputBelt();
        AdvanceUntil(factory, state => state.RemoteInputBeltCount > 0, 300);
        Assert.Contains(factory.RemoteInputBeltItems, item => item.Progress < 1);
        Assert.False(factory.AssemblerE.IsProcessing);
        AdvanceUntil(factory, state => state.AssemblerE.ProcessingProgress == 1, 10);
        var finished = factory.TotalFinishedProduced;
        factory.AdvanceOneStep();
        Assert.Equal(finished + 1, factory.TotalFinishedProduced);
        Assert.Single(factory.FinishedOutputBeltItems);
    }

    [Fact]
    public void TransportCarriesComponentsForFifteenStepsAndBlockedDestinationRetainsComponentsAtSource()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.TransportIsDelivering, 300);
        var cargo = factory.TransportCargoCount;
        Assert.InRange(cargo, 1, FactoryState.TransportCapacity);
        for (var step = 1; step < FactoryState.TransportTravelSteps; step++)
        {
            factory.AdvanceOneStep();
            Assert.True(factory.TransportIsDelivering);
            Assert.Equal(cargo, factory.TransportCargoCount);
        }
        factory.AdvanceOneStep();
        Assert.False(factory.TransportIsDelivering);
        Assert.True(factory.RemoteInputStorageCount + factory.RemoteInputBeltCount + (factory.AssemblerE.IsProcessing ? 1 : 0) > 0);

        AdvanceUntil(factory, state => state.IsRemoteInputStorageFull && !state.TransportIsDelivering, 1500);
        var storedComponents = factory.ComponentStorageCount;
        Advance(factory, 4);
        Assert.False(factory.TransportIsDelivering);
        Assert.True(factory.ComponentStorageCount >= storedComponents);
        AssertConservation(factory);
    }

    [Fact]
    public void DispatchUsesOnlyFullFinishedShippingStorageAndPaysExactly()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ComponentStorageCount > 0, 300);
        Assert.False(factory.DispatchFullShipment());
        AdvanceUntil(factory, state => state.IsShippingStorageFull, 1500);
        var credits = factory.Credits;
        var elapsed = factory.ElapsedSteps;
        var power = factory.CurrentPowerDraw;
        Assert.True(factory.DispatchFullShipment());
        Assert.Equal(FactoryState.CustomerOrderSize, factory.ShippedFinishedCount);
        Assert.Equal(credits + FactoryState.CustomerOrderSize * FactoryState.FinishedItemShipmentRevenue, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);
        Assert.Equal(power, factory.CurrentPowerDraw);
    }

    [Fact]
    public void FullShippingStorageBacksUpThroughRemoteAndFirstFactories()
    {
        var factory = CreateFactory();
        factory.BuildMachineB();
        factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB);
        Advance(factory, 1800);
        Assert.True(factory.IsShippingStorageFull);
        Assert.True(factory.FinishedOutputBeltIsBlocked);
        Assert.True(factory.AssemblerE.IsProcessing);
        Assert.True(factory.TransportIsDelivering || factory.IsRemoteInputStorageFull);
        Assert.True(factory.IsComponentStorageFull);
        Assert.True(factory.SourceIsBlocked);
        Assert.True(factory.SourceBIsBlocked);
        AssertConservation(factory);
    }

    [Fact]
    public void AssemblerUsesExistingIdleAndProducingPowerRules()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        factory.BuildMachineF();
        factory.BuildComponentBBelt();
        Assert.Equal(8, factory.CurrentPowerDraw);
        factory.BuildInputBelt();
        AdvanceUntil(factory, state => state.AssemblerE.IsProcessing && state.AssemblerE.ProcessingProgress == 0, 300);
        Assert.Equal(3, PowerFor(factory.AssemblerE));
        Advance(factory, 20);
        Assert.True(PowerFor(factory.AssemblerE) is 2 or 3);
    }

    [Fact]
    public void ExistingExpansionRoutingAndBufferRulesRemainValid()
    {
        var machineB = CreateFactory();
        Assert.Equal(1, machineB.ExpansionsAvailable);
        Assert.True(machineB.BuildMachineB());
        Assert.True(machineB.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB));
        Assert.False(machineB.BuildMachineB());
        Assert.False(machineB.BuildFinisherD());
        var finisherD = CreateFactory();
        Assert.True(finisherD.BuildFinisherD());
        Assert.False(finisherD.BuildMachineB());
        Advance(finisherD, 200);
        Assert.InRange(finisherD.BufferCount, 0, finisherD.BufferCapacity);
        AssertConservation(machineB);
        AssertConservation(finisherD);
    }

    [Fact]
    public void BuildingMachineBConsumesOnlyExpansionAndRejectsDuplicates()
    {
        var factory = CreateFactory();
        var credits = factory.Credits;
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.True(factory.BuildMachineB());
        Assert.Equal(0, factory.ExpansionsAvailable);
        Assert.True(factory.MachineB.IsBuilt);
        Assert.False(factory.FinisherD.IsBuilt);
        Assert.False(factory.BuildMachineB());
        Assert.False(factory.BuildFinisherD());
        Assert.Equal(credits - FactoryState.MachineBPurchaseCost, factory.Credits);
    }

    [Fact]
    public void BuildingFinisherDConsumesOnlyExpansionAndRejectsDuplicates()
    {
        var factory = CreateFactory();
        var credits = factory.Credits;
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.True(factory.BuildFinisherD());
        Assert.Equal(0, factory.ExpansionsAvailable);
        Assert.True(factory.FinisherD.IsBuilt);
        Assert.False(factory.MachineB.IsBuilt);
        Assert.False(factory.BuildFinisherD());
        Assert.False(factory.BuildMachineB());
        Assert.Equal(credits - FactoryState.FinisherDPurchaseCost, factory.Credits);
    }

    [Theory]
    [InlineData(StageOneRoutingPolicy.Alternate)]
    [InlineData(StageOneRoutingPolicy.PriorityAWithOverflowB)]
    public void RoutingPoliciesRemainDeterministicAndConservative(StageOneRoutingPolicy policy)
    {
        var first = CreateFactory();
        var second = CreateFactory();
        first.BuildMachineB(); second.BuildMachineB();
        first.SetStageOneRoutingPolicy(policy); second.SetStageOneRoutingPolicy(policy);
        Advance(first, 500); Advance(second, 500);
        Assert.Equal(Snapshot(first), Snapshot(second));
        AssertConservation(first);
    }

    [Fact]
    public void ResetByRecreationClearsCompleteNetworkAndPreservesCredits()
    {
        var used = CreateFactory();
        Advance(used, 100);
        var reset = new FactoryState(startingCredits: used.Credits);
        Assert.Equal(used.Credits, reset.Credits);
        Assert.Equal(0, reset.ElapsedSteps);
        Assert.Equal(0, CountMaterial(reset));
        Assert.Equal(0, reset.ComponentStorageCount);
        Assert.Equal(0, reset.RemoteInputStorageCount);
        Assert.Equal(0, reset.RemoteInputBeltCount);
        Assert.False(reset.AssemblerE.IsProcessing);
        Assert.Equal(0, reset.FinishedOutputBeltCount);
        Assert.Equal(0, reset.ShippingStorageCount);
        Assert.Equal(0, reset.ShippedFinishedCount);
        Assert.False(reset.TransportIsDelivering);
        Assert.False(reset.InputBeltIsBuilt);
        Assert.False(reset.MachineB.IsBuilt);
        Assert.False(reset.FinisherD.IsBuilt);
        Assert.Equal(1, reset.ExpansionsAvailable);
    }

    [Fact]
    public void TenThousandStepCompleteNetworkSoakIsProfitableConservativeAndDeterministic()
    {
        var first = CreateFactoryWithDirectFreightLink(goodFactory: true);
        var second = CreateFactoryWithDirectFreightLink(goodFactory: true);
        var startingCredits = first.Credits;
        for (var step = 0; step < 10_000; step++)
        {
            first.AdvanceOneStep(); second.AdvanceOneStep();
            Dispatch(first); Dispatch(second);
            AssertCapacities(first); AssertCapacities(second);
        }
        Assert.True(first.ShippedFinishedCount > 0);
        Assert.True(first.Credits > startingCredits);
        Assert.Equal(Snapshot(first), Snapshot(second));
    }

    [Fact]
    public void GoodExpansionHasBetterThroughputAndEconomicsThanWrongExpansion()
    {
        var good = CreateGoodFactory(10_000);
        var poor = CreateFactory(startingCredits: 10_000 + FactoryState.FinisherDPurchaseCost);
        poor.BuildFinisherD();
        RunWithDispatch(good, 2_000); RunWithDispatch(poor, 2_000);
        Assert.True(good.ShippedFinishedCount > poor.ShippedFinishedCount,
            $"good={good.ShippedFinishedCount}/{good.Credits}, poor={poor.ShippedFinishedCount}/{poor.Credits}");
        Assert.True(good.Credits > poor.Credits);
    }

    [Fact]
    public void ZeroCreditsFreezeAndDispatchCanRecover()
    {
        var factory = CreateFactory(startingCredits: 2_000);
        AdvanceUntil(factory, state => state.IsShippingStorageFull, 1500);
        AdvanceUntil(factory, state => state.IsOutOfCredits, 1500);
        var elapsed = factory.ElapsedSteps;
        var material = CountMaterial(factory);
        Assert.True(factory.AdvanceOneStep());
        Assert.Equal(elapsed + 1, factory.ElapsedSteps);
        Assert.Equal(material, CountMaterial(factory));
        Assert.True(factory.DispatchFullShipment());
        Assert.Equal(FactoryState.CustomerOrderSize * FactoryState.FinishedItemShipmentRevenue, factory.Credits);
        Assert.True(factory.AdvanceOneStep());
        Assert.Equal(elapsed + 2, factory.ElapsedSteps);
    }

    [Fact]
    public void SingleMachineUsesOnlyBranchAAndRejectsRoutingChanges()
    {
        var factory = CreateFactory();
        Assert.False(factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB));
        Advance(factory, 30);
        Assert.True(factory.BranchBeltACount > 0 || factory.MachineA.IsProcessing);
        Assert.Equal(0, factory.BranchBeltBCount);
        Assert.False(factory.MachineB.IsProcessing);
        AssertConservation(factory);
    }

    [Fact]
    public void AlternateRoutesFutureItemsInStrictBranchOrder()
    {
        var factory = CreateRoutingProbeFactory(5);
        var routed = new List<char>();
        var previousA = 0;
        var previousB = 0;
        for (var step = 0; step < 20 && routed.Count < 4; step++)
        {
            factory.AdvanceOneStep();
            if (factory.BranchBeltACount > previousA) routed.Add('A');
            if (factory.BranchBeltBCount > previousB) routed.Add('B');
            previousA = factory.BranchBeltACount;
            previousB = factory.BranchBeltBCount;
        }
        Assert.Equal(['A', 'B', 'A', 'B'], routed);
        AssertConservation(factory);
    }

    [Fact]
    public void AlternateWaitsForItsDueBranchAndPolicyChangeDoesNotMoveAssignedItems()
    {
        var alternate = CreateAsymmetricRoutingProbeFactory();
        var priority = CreateAsymmetricRoutingProbeFactory();
        priority.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB);
        Advance(alternate, 30); Advance(priority, 30);
        Assert.Equal(1, alternate.BranchBeltBCount);
        Assert.True(alternate.RawInputBeltCount > priority.RawInputBeltCount);
        var branchA = alternate.BranchBeltAItems.ToArray();
        var branchB = alternate.BranchBeltBItems.ToArray();
        Assert.True(alternate.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB));
        Assert.Equal(branchA, alternate.BranchBeltAItems);
        Assert.Equal(branchB, alternate.BranchBeltBItems);
        AssertConservation(alternate); AssertConservation(priority);
    }

    [Fact]
    public void PriorityUsesBranchBAsOverflowAndOutperformsAlternate()
    {
        var alternate = CreateFactory();
        var priority = CreateFactory();
        alternate.BuildMachineB(); priority.BuildMachineB();
        priority.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB);
        var sawOverflow = false;
        for (var step = 0; step < 300; step++)
        {
            alternate.AdvanceOneStep(); priority.AdvanceOneStep();
            Dispatch(alternate); Dispatch(priority);
            sawOverflow |= priority.BranchBeltBCount > 0 || priority.MachineB.IsProcessing;
        }
        Assert.True(sawOverflow);
        Assert.True(priority.MaterialCreated > alternate.MaterialCreated
            || priority.RawInputBeltCount < alternate.RawInputBeltCount);
        AssertConservation(alternate); AssertConservation(priority);
    }

    [Fact]
    public void PriorityFillsBranchAThenBranchBAndBlocksWhenBothAreFull()
    {
        var factory = CreateRoutingProbeFactory(1);
        factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB);
        Advance(factory, 4);
        Assert.Equal(1, factory.BranchBeltACount);
        Assert.Equal(0, factory.BranchBeltBCount);
        factory.AdvanceOneStep();
        Assert.Equal(1, factory.BranchBeltACount);
        Assert.Equal(1, factory.BranchBeltBCount);
        var upstream = factory.RawInputBeltCount;
        factory.AdvanceOneStep();
        Assert.Equal(1, factory.BranchBeltACount);
        Assert.Equal(1, factory.BranchBeltBCount);
        Assert.True(factory.RawInputBeltCount >= upstream);
    }

    [Fact]
    public void PolicyChangeControlsOnlyItemsRoutedAfterTheChange()
    {
        var factory = CreateRoutingProbeFactory(5);
        Advance(factory, 5);
        Assert.Equal(1, factory.BranchBeltACount);
        Assert.Equal(1, factory.BranchBeltBCount);
        factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB);
        Advance(factory, 2);
        Assert.Single(factory.BranchBeltBItems);
        Assert.All(factory.BranchBeltBItems, item => Assert.Equal(MaterialKind.Raw, item.Material));
        Assert.Equal(2, factory.BranchBeltACount);
    }

    [Fact]
    public void InputBeltMustBeBuiltBeforeMaterialCanFlow()
    {
        var factory = CreateFactory(buildBelt: false);
        Advance(factory, 20);
        Assert.False(factory.InputBeltIsBuilt);
        Assert.Equal(0, factory.MaterialCreated);
        Assert.Equal(0, CountMaterial(factory));
        Assert.True(factory.BuildInputBelt());
        Assert.False(factory.BuildInputBelt());
        Advance(factory, 40);
        Assert.True(factory.ComponentStorageCount + factory.TransportCargoCount + factory.RemoteInputStorageCount > 0);
        AssertConservation(factory);
    }

    [Fact]
    public void MachineAAloneCreatesTheFirstUpstreamBottleneck()
    {
        var factory = CreateFactory();
        var sawBlocked = false;
        for (var step = 0; step < 40; step++) { factory.AdvanceOneStep(); sawBlocked |= factory.SourceIsBlocked; }
        Assert.True(factory.MachineA.IsBuilt);
        Assert.False(factory.MachineB.IsBuilt);
        Assert.True(sawBlocked);
        Assert.True(factory.RawInputBeltCount >= factory.RawInputBeltCapacity - 1);
    }

    [Fact]
    public void FactoryOneExplicitlyTransformsRawToIntermediateToComponent()
    {
        var factory = CreateFactory();
        var sawRaw = false; var sawIntermediate = false; var sawComponent = false;
        for (var step = 0; step < 60; step++)
        {
            factory.AdvanceOneStep();
            sawRaw |= factory.RawInputBeltItems.Count > 0;
            sawIntermediate |= factory.IntermediateBeltItems.Count > 0;
            sawComponent |= factory.ComponentBeltItems.Count > 0;
            AssertKinds(factory);
        }
        Assert.True(sawRaw);
        Assert.True(sawIntermediate);
        Assert.True(sawComponent);
        Assert.True(factory.ComponentStorageCount + factory.TransportCargoCount + factory.RemoteInputStorageCount > 0);
        AssertConservation(factory);
    }

    [Fact]
    public void ReadingViewsDoesNotAdvanceSimulation()
    {
        var factory = CreateFactory();
        factory.AdvanceOneStep();
        var elapsed = factory.ElapsedSteps;
        _ = factory.RawInputBeltItems; _ = factory.BranchBeltAItems; _ = factory.BranchBeltBItems;
        _ = factory.IntermediateBeltItems; _ = factory.ComponentBeltItems; _ = factory.RemoteInputBeltItems;
        _ = factory.FinishedOutputBeltItems; _ = factory.MachineA; _ = factory.FinisherC; _ = factory.AssemblerE;
        _ = factory.StageOneRoutingPolicy; _ = factory.ComponentStorageCount; _ = factory.RemoteInputStorageCount;
        Assert.Equal(elapsed, factory.ElapsedSteps);
    }

    [Fact]
    public void ElapsedStepsAdvanceOnlyWhenExplicitlyRequested()
    {
        var factory = CreateFactory(buildBelt: false);
        Assert.Equal(0, factory.ElapsedSteps);
        _ = factory.ComponentStorageCount; _ = factory.RawInputBeltItems;
        factory.BuildInputBelt(); factory.BuildMachineB();
        Assert.Equal(0, factory.ElapsedSteps);
        Advance(factory, 7);
        Assert.Equal(7, factory.ElapsedSteps);
    }

    [Fact]
    public void FreshFactoryHasCompleteInitialStateAndRepeatedRunsMatch()
    {
        var fresh = CreateFactory(buildBelt: false);
        Assert.Equal(0, fresh.ElapsedSteps);
        Assert.Equal(0, fresh.MaterialCreated);
        Assert.Equal(0, CountMaterial(fresh));
        Assert.Equal(12, fresh.StorageCapacity);
        Assert.Equal(12, FactoryState.RemoteInputStorageCapacity);
        Assert.Equal(12, fresh.ShippingStorageCapacity);
        Assert.Equal(6, fresh.BufferCapacity);
        Assert.Equal(1, fresh.ExpansionsAvailable);
        Assert.False(fresh.InputBeltIsBuilt);
        Assert.False(fresh.MachineB.IsBuilt);
        Assert.False(fresh.FinisherD.IsBuilt);
        Assert.True(fresh.AssemblerE.IsBuilt);
        Assert.Equal(StageOneRoutingPolicy.Alternate, fresh.StageOneRoutingPolicy);

        var first = CreateFactory(); var second = CreateFactory();
        Advance(first, 90); Advance(second, 90);
        Assert.Equal(Snapshot(first), Snapshot(second));
        AssertConservation(first); AssertConservation(second);
    }

    [Fact]
    public void IntermediateBeltFeedsFiniteBufferAndFinisherConsumesFromIt()
    {
        var factory = CreateFactory();
        Assert.Equal(0, factory.BufferCount);
        Assert.Equal(6, factory.BufferCapacity);
        Assert.False(factory.IsBufferFull);
        var accepted = false;
        var drained = false;
        var previous = factory.BufferCount;
        for (var step = 0; step < 100; step++)
        {
            factory.AdvanceOneStep();
            accepted |= factory.BufferCount > previous;
            drained |= factory.BufferCount < previous && factory.FinisherC.IsProcessing;
            previous = factory.BufferCount;
            Assert.All(factory.IntermediateBeltItems, item => Assert.Equal(MaterialKind.Intermediate, item.Material));
            Assert.InRange(factory.BufferCount, 0, factory.BufferCapacity);
        }
        Assert.True(accepted);
        Assert.True(drained);
        Assert.True(factory.TotalFinishedProduced > 0);
    }

    [Fact]
    public void BufferFillsBeforeBackpressureReachesSource()
    {
        var factory = new FactoryState(machineAProcessingSteps: 1, machineBProcessingSteps: 4,
            finisherProcessingSteps: 1, beltCapacity: 5, bufferCapacity: 6,
            storageCapacity: 4, sourceIntervalSteps: 2, startingCredits: 100_000);
        factory.BuildInputBelt();
        AdvanceUntil(factory, state => state.IsBufferFull, 1500);
        Assert.False(factory.SourceIsBlocked);
        var createdWhenBufferFilled = factory.MaterialCreated;
        AdvanceUntil(factory, state => state.SourceIsBlocked, 1500);
        Assert.True(factory.MaterialCreated >= createdWhenBufferFilled);
        AssertConservation(factory);
    }

    [Fact]
    public void DispatchDoesNotMutateWipAndNormalStepsResumeDrainage()
    {
        var factory = CreateFactory();
        factory.BuildFinisherD();
        AdvanceUntil(factory, state => state.IsShippingStorageFull && state.IsBufferFull, 1500);
        var buffer = factory.BufferCount;
        Assert.True(factory.DispatchFullShipment());
        Assert.Equal(buffer, factory.BufferCount);
        var drained = false;
        for (var step = 0; step < 200; step++)
        {
            factory.AdvanceOneStep(); Dispatch(factory);
            drained |= factory.BufferCount < buffer;
        }
        Assert.True(drained);
        AssertConservation(factory);
    }

    [Theory]
    [InlineData(StageOneRoutingPolicy.Alternate)]
    [InlineData(StageOneRoutingPolicy.PriorityAWithOverflowB)]
    public void FullNetworkBackpressureAndDispatchAllowNaturalRecovery(StageOneRoutingPolicy policy)
    {
        var factory = CreateFactory();
        factory.BuildMachineB(); factory.SetStageOneRoutingPolicy(policy);
        Advance(factory, 1800);
        Assert.True(factory.SourceIsBlocked);
        Assert.True(factory.IsShippingStorageFull);
        Assert.True(factory.IsComponentStorageFull);
        var createdAtBlockage = factory.MaterialCreated;
        Advance(factory, 50);
        Assert.Equal(createdAtBlockage, factory.MaterialCreated);
        var buffer = factory.BufferCount;
        Assert.True(factory.DispatchFullShipment());
        Assert.Equal(buffer, factory.BufferCount);
        Assert.False(factory.DispatchFullShipment());
        AdvanceUntil(factory, state => state.MaterialCreated > createdAtBlockage, 500);
        AssertConservation(factory);
    }

    [Fact]
    public void BuiltMachineStateAndSuccessfulStepsChargeExactPowerCost()
    {
        var factory = new FactoryState();
        Assert.True(factory.BuildMachineF());
        Assert.Equal(8, factory.CurrentPowerDraw);
        var credits = factory.Credits;
        Assert.True(factory.AdvanceOneStep());
        Assert.Equal(credits - 8, factory.Credits);
        Assert.True(factory.BuildMachineB());
        Assert.Equal(10, factory.CurrentPowerDraw);
        var active = CreateFactory();
        AdvanceUntil(active, state => state.MachineA.IsProcessing && state.MachineA.ProcessingProgress < 1, 30);
        var cost = active.CurrentGridCostPerStep;
        credits = active.Credits;
        active.AdvanceOneStep();
        Assert.Equal(credits - cost, active.Credits);
    }

    [Fact]
    public void OutputBlockedMachinesConsumeIdlePower()
    {
        var factory = CreateFactory();
        factory.BuildMachineB();
        factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB);
        Advance(factory, 1800);
        Assert.True(factory.MachineA.IsProcessing);
        Assert.Equal(1, factory.MachineA.ProcessingProgress);
        Assert.Equal(FactoryState.IdleMachinePowerPerStep, PowerFor(factory.MachineA));
        Assert.True(factory.FinisherC.IsProcessing);
        Assert.Equal(1, factory.FinisherC.ProcessingProgress);
        Assert.Equal(FactoryState.IdleMachinePowerPerStep, PowerFor(factory.FinisherC));
        Assert.True(factory.AssemblerE.IsProcessing);
        Assert.Equal(1, factory.AssemblerE.ProcessingProgress);
        Assert.Equal(FactoryState.IdleMachinePowerPerStep, PowerFor(factory.AssemblerE));
        AssertConservation(factory);
    }

    [Fact]
    public void DelayedDispatchPerformsWorseThanPromptDispatch()
    {
        var prompt = CreateFactory(startingCredits: 100_000);
        var delayed = CreateFactory(startingCredits: 100_000);
        prompt.BuildFinisherD(); delayed.BuildFinisherD();
        for (var step = 1; step <= 500; step++)
        {
            prompt.AdvanceOneStep(); delayed.AdvanceOneStep();
            Dispatch(prompt);
            if (step % 500 == 0) Dispatch(delayed);
        }
        Assert.True(prompt.Credits > delayed.Credits);
        Assert.True(prompt.ShippedFinishedCount > delayed.ShippedFinishedCount);
        AssertConservation(prompt); AssertConservation(delayed);
    }

    [Fact]
    public void SourceBStartsEmptyAndProducesOnlyRawBWithLocalBackpressure()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        factory.BuildMachineF();
        factory.BuildComponentBBelt();
        Assert.Equal(0, factory.MaterialBCreated);
        Assert.Empty(factory.RawBBeltItems);
        Assert.False(factory.MachineF.IsProcessing);
        Assert.Empty(factory.ComponentBBeltItems);
        factory.AdvanceOneStep();
        Assert.Equal(1, factory.MaterialBCreated);
        Assert.Single(factory.RawBBeltItems);
        Assert.All(factory.RawBBeltItems, item => Assert.Equal(MaterialKind.RawB, item.Material));
        AdvanceUntil(factory, state => state.SourceBIsBlocked && state.ComponentBBeltIsBlocked
            && state.MachineF.IsProcessing, 500);
        var created = factory.MaterialBCreated;
        Advance(factory, 5);
        Assert.Equal(created, factory.MaterialBCreated);
        AssertConservation(factory);
    }

    [Fact]
    public void MachineFConsumesOneRawBAndProducesOneComponentBOnFiniteBelt()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        factory.BuildMachineF();
        factory.BuildComponentBBelt();
        Assert.False(factory.MachineF.IsProcessing);
        AdvanceUntil(factory, state => state.MachineF.IsProcessing, 20);
        Assert.Equal(factory.MaterialBCreated, CountMaterialB(factory));
        AdvanceUntil(factory, state => state.ComponentBBeltCount > 0, 10);
        Assert.All(factory.ComponentBBeltItems, item => Assert.Equal(MaterialKind.ComponentB, item.Material));
        Assert.Equal(factory.MaterialBCreated, CountMaterialB(factory));
        Assert.InRange(factory.ComponentBBeltCount, 1, FactoryState.ComponentBBeltCapacity);
    }

    [Fact]
    public void MachineFUsesNormalIdleProducingAndBlockedOutputPower()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        factory.BuildMachineF();
        factory.BuildComponentBBelt();
        Assert.Equal(FactoryState.IdleMachinePowerPerStep, PowerFor(factory.MachineF));
        AdvanceUntil(factory, state => state.MachineF.IsProcessing && state.MachineF.ProcessingProgress < 1, 20);
        Assert.Equal(FactoryState.ProducingMachinePowerPerStep, PowerFor(factory.MachineF));
        AdvanceUntil(factory, state => state.ComponentBBeltIsBlocked && state.MachineF.ProcessingProgress == 1, 1000);
        Assert.Equal(FactoryState.IdleMachinePowerPerStep, PowerFor(factory.MachineF));
    }

    [Fact]
    public void ComponentAArrivesFirstAndIsHeldWithoutProcessingOrDuplication()
    {
        var factory = new FactoryState(machineAProcessingSteps: 1, sourceIntervalSteps: 1,
            sourceBStartDelaySteps: 1_000, startingCredits: 100_000);
        factory.BuildInputBelt();
        AdvanceUntil(factory, state => state.AssemblerHasComponentA, 500);
        Assert.False(factory.AssemblerHasComponentB);
        Assert.False(factory.AssemblerE.IsProcessing);
        Advance(factory, 20);
        Assert.True(factory.AssemblerHasComponentA);
        Assert.False(factory.AssemblerHasComponentB);
        Assert.False(factory.AssemblerE.IsProcessing);
        AssertConservation(factory);
        AdvanceUntil(factory, state => state.SourceIsBlocked, 800);
        Assert.True(factory.AssemblerHasComponentA);
        Assert.False(factory.AssemblerHasComponentB);
    }

    [Fact]
    public void ComponentBArrivesFirstAndIsHeldWithoutProcessingOrDuplication()
    {
        var factory = CreateFactory(buildBelt: false);
        AdvanceUntil(factory, state => state.AssemblerHasComponentB, 100);
        Assert.False(factory.AssemblerHasComponentA);
        Assert.False(factory.AssemblerE.IsProcessing);
        var created = factory.MaterialBCreated;
        Advance(factory, 20);
        Assert.True(factory.AssemblerHasComponentB);
        Assert.False(factory.AssemblerHasComponentA);
        Assert.False(factory.AssemblerE.IsProcessing);
        Assert.True(factory.MaterialBCreated >= created);
        AssertConservation(factory);
    }

    [Fact]
    public void AssemblerConsumesExactlyOneOfEachInputAndProducesOneFinished()
    {
        var factory = CreateFactory();
        Assert.False(factory.AssemblerHasComponentA);
        Assert.False(factory.AssemblerHasComponentB);
        AdvanceUntil(factory, state => state.AssemblerE.IsProcessing, 500);
        Assert.False(factory.AssemblerHasComponentA);
        Assert.False(factory.AssemblerHasComponentB);
        var finished = factory.TotalFinishedProduced;
        AdvanceUntil(factory, state => state.TotalFinishedProduced == finished + 1, 3);
        Assert.Equal(finished + 1, factory.TotalFinishedProduced);
        AssertConservation(factory);
    }

    [Fact]
    public void ResetAndZeroCreditFreezeIncludeBothAssemblerInputsAndSupplyB()
    {
        var used = CreateFactory(buildBelt: false);
        AdvanceUntil(used, state => state.AssemblerHasComponentB, 100);
        var frozen = new FactoryState(startingCredits: 1);
        frozen.AdvanceOneStep();
        var bCreated = frozen.MaterialBCreated;
        Assert.True(frozen.AdvanceOneStep());
        Assert.Equal(bCreated, frozen.MaterialBCreated);
        Assert.Equal(1, frozen.RawBBeltCount);

        var reset = new FactoryState(startingCredits: used.Credits);
        Assert.False(reset.AssemblerHasComponentA);
        Assert.False(reset.AssemblerHasComponentB);
        Assert.Equal(0, reset.MaterialBCreated);
        Assert.Equal(0, reset.RawBBeltCount);
        Assert.False(reset.MachineF.IsProcessing);
        Assert.Equal(0, reset.ComponentBBeltCount);
        Assert.Equal(used.Credits, reset.Credits);
    }

    [Fact]
    public void SecondTransportBuildsExactlyOnceWithoutUsingProductionExpansion()
    {
        var factory = CreateFactory();
        var credits = factory.Credits;
        Assert.True(factory.Transport1Exists);
        Assert.False(factory.Transport2Exists);
        Assert.Equal(0, factory.Transport2CargoCount);
        Assert.False(factory.Transport2IsDelivering);
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.True(factory.BuildSecondTransport());
        Assert.True(factory.Transport2Exists);
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.False(factory.BuildSecondTransport());
        Assert.Equal(credits - FactoryState.SecondTransportPurchaseCost, factory.Credits);
        Assert.True(factory.BuildMachineB());
    }

    [Fact]
    public void TwoTransportsReserveFiniteDestinationCapacityAndOwnLoadedCargo()
    {
        var factory = CreateGoodFactory(100_000);
        AdvanceUntil(factory, state => state.ComponentStorageCount >= FactoryState.TransportCapacity, 500);
        Assert.True(factory.BuildSecondTransport());
        var sourceBefore = factory.ComponentStorageCount;
        var committedBefore = factory.CommittedTransportCargoCount;
        factory.AdvanceOneStep();
        var newlyCommitted = factory.CommittedTransportCargoCount - committedBefore;
        Assert.Equal(newlyCommitted, sourceBefore - factory.ComponentStorageCount);
        Assert.InRange(factory.Transport1CargoCount, 0, FactoryState.TransportCapacity);
        Assert.InRange(factory.Transport2CargoCount, 0, FactoryState.TransportCapacity);
        Assert.True(factory.RemoteInputStorageCount + factory.CommittedTransportCargoCount <= FactoryState.RemoteInputStorageCapacity);
        AssertConservation(factory);
    }

    [Fact]
    public void BuiltTransportsCanTravelTogetherAndOperateIndependently()
    {
        var factory = CreateGoodFactory(100_000);
        factory.BuildSecondTransport();
        var sawBothDelivering = false;
        var sawOnlyOneDelivering = false;
        for (var step = 0; step < 1000; step++)
        {
            factory.AdvanceOneStep(); Dispatch(factory);
            sawBothDelivering |= factory.Transport1IsDelivering && factory.Transport2IsDelivering;
            sawOnlyOneDelivering |= factory.Transport1IsDelivering != factory.Transport2IsDelivering;
            Assert.True(factory.RemoteInputStorageCount + factory.CommittedTransportCargoCount <= FactoryState.RemoteInputStorageCapacity);
        }
        Assert.True(sawBothDelivering);
        Assert.True(sawOnlyOneDelivering);
        AssertConservation(factory);
    }

    [Fact]
    public void CommittedFullDestinationKeepsHomeTransportFromLoadingUntilCapacityReturns()
    {
        var factory = CreateGoodFactory(100_000);
        factory.BuildSecondTransport();
        AdvanceUntil(factory, state => state.IsShippingStorageFull && state.IsRemoteInputStorageFull
            && !state.Transport1IsDelivering && !state.Transport2IsDelivering && state.ComponentStorageCount > 0, 2000);
        var source = factory.ComponentStorageCount;
        Advance(factory, 5);
        Assert.False(factory.Transport1IsDelivering);
        Assert.False(factory.Transport2IsDelivering);
        Assert.Equal(source, factory.ComponentStorageCount);
        Assert.True(factory.DispatchFullShipment());
        AdvanceUntil(factory, state => state.Transport1IsDelivering || state.Transport2IsDelivering, 300);
        AssertConservation(factory);
    }

    [Fact]
    public void SecondTransportPreservesTwoInputRulesAndCanReduceAStarvation()
    {
        var one = CreateGoodFactory(100_000);
        var two = CreateGoodFactory(100_000);
        two.BuildSecondTransport();
        var oneAStarved = 0;
        var twoAStarved = 0;
        for (var step = 0; step < 2000; step++)
        {
            one.AdvanceOneStep(); two.AdvanceOneStep(); Dispatch(one); Dispatch(two);
            if (one.AssemblerHasComponentB && !one.AssemblerHasComponentA && !one.AssemblerE.IsProcessing) oneAStarved++;
            if (two.AssemblerHasComponentB && !two.AssemblerHasComponentA && !two.AssemblerE.IsProcessing) twoAStarved++;
        }
        Assert.True(twoAStarved <= oneAStarved);
        Assert.All(one.RawBBeltItems, item => Assert.Equal(MaterialKind.RawB, item.Material));
        Assert.All(two.RawBBeltItems, item => Assert.Equal(MaterialKind.RawB, item.Material));
        Assert.All(one.ComponentBBeltItems, item => Assert.Equal(MaterialKind.ComponentB, item.Material));
        Assert.All(two.ComponentBBeltItems, item => Assert.Equal(MaterialKind.ComponentB, item.Material));
        AssertConservation(one); AssertConservation(two);
    }

    [Fact]
    public void ResetRemovesSecondTransportAndZeroCreditFreezesBothCargoStates()
    {
        var factory = CreateGoodFactory(100_000);
        factory.BuildSecondTransport();
        AdvanceUntil(factory, state => state.Transport1IsDelivering && state.Transport2IsDelivering, 1000);
        var reset = new FactoryState(startingCredits: factory.Credits);
        Assert.True(reset.Transport1Exists);
        Assert.False(reset.Transport2Exists);
        Assert.Equal(0, reset.Transport1CargoCount);
        Assert.Equal(0, reset.Transport2CargoCount);
        Assert.Equal(factory.Credits, reset.Credits);

        var frozen = CreateGoodFactory(1);
        frozen.BuildSecondTransport();
        frozen.AdvanceOneStep();
        var materialA = CountMaterial(frozen);
        var materialB = CountMaterialB(frozen);
        var transport1Cargo = frozen.Transport1CargoCount;
        var transport2Cargo = frozen.Transport2CargoCount;
        Assert.True(frozen.AdvanceOneStep());
        Assert.Equal(materialA, CountMaterial(frozen));
        Assert.Equal(materialB, CountMaterialB(frozen));
        Assert.Equal(transport1Cargo, frozen.Transport1CargoCount);
        Assert.Equal(transport2Cargo, frozen.Transport2CargoCount);
    }

    [Fact]
    public void TwoTransportTenThousandStepRunIsDeterministicAndConservative()
    {
        var first = CreateGoodFactory(1_000 + FactoryState.SecondTransportPurchaseCost);
        var second = CreateGoodFactory(1_000 + FactoryState.SecondTransportPurchaseCost);
        first.BuildSecondTransport(); second.BuildSecondTransport();
        for (var step = 0; step < 10_000; step++)
        {
            first.AdvanceOneStep(); second.AdvanceOneStep(); Dispatch(first); Dispatch(second);
            AssertCapacities(first); AssertCapacities(second);
        }
        Assert.Equal(Snapshot(first), Snapshot(second));
        Assert.True(first.ShippedFinishedCount > 0);
        AssertConservation(first);
    }

    [Fact]
    public void TransportDepartureAtomicallyChargesExactlyForActualCargoOnce()
    {
        var factory = CreateGoodFactory(100_000);
        AdvanceUntil(factory, state => !state.Transport1IsDelivering && state.ComponentStorageCount > 0, 500);
        var credits = factory.Credits;
        var storage = factory.ComponentStorageCount;
        var gridCost = factory.CurrentGridCostPerStep;
        factory.AdvanceOneStep();
        var cargo = factory.Transport1CargoCount;
        Assert.InRange(cargo, 1, FactoryState.TransportCapacity);
        Assert.Equal(storage - cargo, factory.ComponentStorageCount);
        Assert.Equal(credits - gridCost - cargo * factory.ComponentATransportCostPerCargoItem, factory.Credits);

        credits = factory.Credits;
        gridCost = factory.CurrentGridCostPerStep;
        factory.AdvanceOneStep();
        Assert.Equal(credits - gridCost, factory.Credits);
        Assert.Equal(cargo, factory.Transport1CargoCount);
        AssertConservation(factory);
    }

    [Fact]
    public void FullAndPartialLoadsPayOnlyForItemsActuallyCarried()
    {
        var full = CreateGoodFactory(100_000);
        AdvanceUntil(full, state => !state.Transport1IsDelivering
            && state.ComponentStorageCount >= FactoryState.TransportCapacity
            && state.RemoteInputStorageCount + state.CommittedTransportCargoCount <= FactoryState.RemoteInputStorageCapacity - FactoryState.TransportCapacity, 1000);
        var credits = full.Credits;
        var gridCost = full.CurrentGridCostPerStep;
        full.AdvanceOneStep();
        Assert.Equal(FactoryState.TransportCapacity, full.Transport1CargoCount);
        Assert.Equal(credits - gridCost - 6 * full.ComponentATransportCostPerCargoItem, full.Credits);

        var partial = CreateGoodFactory(100_000);
        AdvanceUntil(partial, state => !state.Transport1IsDelivering && state.ComponentStorageCount is > 0 and < FactoryState.TransportCapacity, 500);
        credits = partial.Credits;
        gridCost = partial.CurrentGridCostPerStep;
        var expectedCargo = partial.ComponentStorageCount;
        partial.AdvanceOneStep();
        Assert.Equal(expectedCargo, partial.Transport1CargoCount);
        Assert.Equal(credits - gridCost - expectedCargo * partial.ComponentATransportCostPerCargoItem, partial.Credits);
    }

    [Fact]
    public void UnaffordableDepartureLeavesCargoAtSourceAndCreditsNonnegative()
    {
        FactoryState? blocked = null;
        for (var startingCredits = 1; startingCredits <= 2_000 && blocked is null; startingCredits++)
        {
            var candidate = CreateGoodFactory(startingCredits);
            candidate.BuildSecondTransport();
            for (var step = 0; step < 500 && !candidate.IsOutOfCredits; step++)
            {
                var source = candidate.ComponentStorageCount;
                candidate.AdvanceOneStep();
                if (candidate.Transport1WaitingForCredits || candidate.Transport2WaitingForCredits)
                {
                    Assert.True(candidate.ComponentStorageCount >= source);
                    Assert.True(candidate.Credits >= 0);
                    blocked = candidate;
                    break;
                }
            }
        }
        Assert.NotNull(blocked);
        Assert.True(blocked.Transport1WaitingForCredits || blocked.Transport2WaitingForCredits);
        if (blocked.Transport1WaitingForCredits) Assert.False(blocked.Transport1IsDelivering);
        if (blocked.Transport2WaitingForCredits) Assert.False(blocked.Transport2IsDelivering);
        AssertConservation(blocked);
    }

    [Fact]
    public void TwoTransportsPayIndependentlyInDeterministicOrder()
    {
        var factory = CreateGoodFactory(100_000);
        factory.BuildSecondTransport();
        var sawTransport1Charge = false;
        var sawTransport2Charge = false;
        for (var step = 0; step < 1000 && (!sawTransport1Charge || !sawTransport2Charge); step++)
        {
            var transport1WasHome = !factory.Transport1IsDelivering;
            var transport2WasHome = !factory.Transport2IsDelivering;
            var credits = factory.Credits;
            var gridCost = factory.CurrentGridCostPerStep;
            factory.AdvanceOneStep();
            var chargedCargo = 0;
            if (transport1WasHome && factory.Transport1IsDelivering)
            {
                chargedCargo += factory.Transport1CargoCount;
                sawTransport1Charge = true;
            }
            if (transport2WasHome && factory.Transport2IsDelivering)
            {
                chargedCargo += factory.Transport2CargoCount;
                sawTransport2Charge = true;
            }
            if (chargedCargo > 0)
            {
                Assert.Equal(credits - gridCost - chargedCargo * factory.ComponentATransportCostPerCargoItem, factory.Credits);
            }
        }
        Assert.True(sawTransport1Charge);
        Assert.True(sawTransport2Charge);
        Assert.True(factory.RemoteInputStorageCount + factory.CommittedTransportCargoCount <= FactoryState.RemoteInputStorageCapacity);
    }

    [Fact]
    public void ScarceCreditsGiveTransport1DeterministicDeparturePriority()
    {
        FactoryState? observed = null;
        for (var startingCredits = 100; startingCredits <= 3_000 && observed is null; startingCredits++)
        {
            var candidate = CreateGoodFactory(startingCredits);
            candidate.BuildSecondTransport();
            for (var step = 0; step < 500 && !candidate.IsOutOfCredits; step++)
            {
                candidate.AdvanceOneStep();
                if (candidate.Transport1IsDelivering && candidate.Transport2WaitingForCredits)
                {
                    observed = candidate;
                    break;
                }
            }
        }
        Assert.NotNull(observed);
        Assert.True(observed.Transport1IsDelivering);
        Assert.False(observed.Transport2IsDelivering);
        Assert.True(observed.Transport2WaitingForCredits);
        Assert.True(observed.RemoteInputStorageCount + observed.CommittedTransportCargoCount <= FactoryState.RemoteInputStorageCapacity);
        Assert.True(observed.Credits >= 0);
        AssertConservation(observed);
    }

    [Fact]
    public void ShipmentRevenueAllowsWaitingTransportToDepartLater()
    {
        FactoryState? waiting = null;
        for (var startingCredits = 500; startingCredits <= 5_000 && waiting is null; startingCredits++)
        {
            var candidate = CreateGoodFactory(startingCredits);
            for (var step = 0; step < 1_000 && !candidate.IsOutOfCredits; step++)
            {
                candidate.AdvanceOneStep();
                if (candidate.IsShippingStorageFull && candidate.Transport1WaitingForCredits)
                {
                    waiting = candidate;
                    break;
                }
            }
        }
        Assert.NotNull(waiting);
        Assert.False(waiting.Transport1IsDelivering);
        Assert.True(waiting.DispatchFullShipment());
        AdvanceUntil(waiting, state => state.Transport1IsDelivering, 100);
        Assert.False(waiting.Transport1WaitingForCredits);
        AssertConservation(waiting);
    }

    [Fact]
    public void DispatchRequiresExactlyOneFullShipmentAndAdvancesNoTime()
    {
        var factory = CreateFactory();
        Assert.Equal(12, FactoryState.CustomerOrderSize);
        AdvanceUntil(factory, state => state.ShippingStorageCount == 11, 1_000);
        var credits = factory.Credits;
        Assert.False(factory.DispatchFullShipment());
        Assert.Equal(credits, factory.Credits);
        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 100);
        credits = factory.Credits;
        var elapsed = factory.ElapsedSteps;

        Assert.True(factory.DispatchFullShipment());

        Assert.Equal(0, factory.ShippingStorageCount);
        Assert.Equal(12, factory.ShippedFinishedCount);
        Assert.Equal(credits + 12 * FactoryState.FinishedItemShipmentRevenue, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);
        AssertConservation(factory);
    }

    [Fact]
    public void RetainedShippingOutputStillBacksUpTheFactory()
    {
        var factory = CreateGoodFactory(100_000);
        factory.BuildSecondTransport();
        AdvanceUntil(factory, state => state.IsShippingStorageFull, 1000);
        var shipped = factory.ShippedFinishedCount;
        Advance(factory, 500);
        Assert.Equal(shipped, factory.ShippedFinishedCount);
        Assert.True(factory.FinishedOutputBeltIsBlocked || factory.AssemblerE.ProcessingProgress == 1);
        Assert.True(factory.SourceIsBlocked);
        AssertConservation(factory);
    }

    [Fact]
    public void FactoryResetPreservesCreditsWithoutCustomerCountdownState()
    {
        var used = new FactoryState(startingCredits: 777);
        var reset = new FactoryState(startingCredits: used.Credits);
        Assert.Equal(777, reset.Credits);
        Assert.Equal(0, CountMaterial(reset));
    }

    [Fact]
    public void ZeroCreditStepsFreezeFactoryState()
    {
        var factory = new FactoryState(startingCredits: 0);
        var materialA = CountMaterial(factory);
        var materialB = CountMaterialB(factory);
        var power = factory.CurrentPowerDraw;
        Assert.True(factory.AdvanceOneStep());
        Assert.True(factory.AdvanceOneStep());
        Assert.Equal(0, factory.Credits);
        Assert.Equal(power, factory.CurrentPowerDraw);
        Assert.Equal(materialA, CountMaterial(factory));
        Assert.Equal(materialB, CountMaterialB(factory));
    }

    [Fact]
    public void CapitalCostsAndStartingRunwayAreExplicit()
    {
        Assert.Equal(500, FactoryState.MachineFPurchaseCost);
        Assert.Equal(750, FactoryState.MachineBPurchaseCost);
        Assert.Equal(750, FactoryState.FinisherDPurchaseCost);
        Assert.Equal(750, FactoryState.SecondTransportPurchaseCost);
        Assert.Equal(FactoryState.StartingOperatingRunwayCredits,
            FactoryState.StartingCredits - FactoryState.MachineFPurchaseCost);
    }

    [Fact]
    public void UnaffordableMachineFPurchaseIsAtomic()
    {
        var factory = new FactoryState(startingCredits: FactoryState.MachineFPurchaseCost - 1);
        var elapsed = factory.ElapsedSteps;
        var credits = factory.Credits;

        Assert.False(factory.BuildMachineF());
        Assert.False(factory.MachineFIsBuilt);
        Assert.Equal(credits, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);
        Assert.True(factory.Credits >= 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnaffordableProductionExpansionIsAtomic(bool buildMachineB)
    {
        var cost = buildMachineB ? FactoryState.MachineBPurchaseCost : FactoryState.FinisherDPurchaseCost;
        var factory = new FactoryState(startingCredits: cost - 1);

        Assert.False(buildMachineB ? factory.BuildMachineB() : factory.BuildFinisherD());
        Assert.False(factory.MachineB.IsBuilt);
        Assert.False(factory.FinisherD.IsBuilt);
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.Equal(cost - 1, factory.Credits);
        Assert.Equal(0, factory.ElapsedSteps);
    }

    [Fact]
    public void UnaffordableSecondTransportPurchaseIsAtomic()
    {
        var factory = new FactoryState(startingCredits: FactoryState.SecondTransportPurchaseCost - 1);

        Assert.False(factory.BuildSecondTransport());
        Assert.False(factory.Transport2Exists);
        Assert.Equal(FactoryState.SecondTransportPurchaseCost - 1, factory.Credits);
        Assert.Equal(0, factory.ElapsedSteps);
    }

    [Fact]
    public void PlayerBuiltBeltsRemainFree()
    {
        var factory = new FactoryState(startingCredits: 10_000);
        var credits = factory.Credits;
        Assert.True(factory.BuildInputBelt());
        Assert.Equal(credits, factory.Credits);
        Assert.True(factory.BuildMachineF());
        credits = factory.Credits;
        Assert.True(factory.BuildComponentBBelt());
        Assert.Equal(credits, factory.Credits);
    }

    [Fact]
    public void MachineFStartsUnbuiltAndCannotConsumeOrProcessRawB()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        Assert.False(factory.MachineFIsBuilt);
        Assert.False(factory.MachineF.IsBuilt);
        Assert.Equal(6, factory.CurrentPowerDraw);

        Advance(factory, 20);

        Assert.True(factory.RawBBeltCount > 0);
        Assert.False(factory.MachineF.IsProcessing);
        Assert.Empty(factory.ComponentBBeltItems);
        Assert.False(factory.AssemblerHasComponentB);
        Assert.Equal(6, factory.CurrentPowerDraw);
        AssertConservation(factory);
    }

    [Fact]
    public void BuildingMachineFChargesExactlyOnceAndRemainsIndependent()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        var elapsed = factory.ElapsedSteps;
        var credits = factory.Credits;

        Assert.True(factory.BuildMachineF());
        Assert.True(factory.MachineFIsBuilt);
        Assert.False(factory.BuildMachineF());
        Assert.Equal(elapsed, factory.ElapsedSteps);
        Assert.Equal(credits - FactoryState.MachineFPurchaseCost, factory.Credits);
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.False(factory.Transport2Exists);
        Assert.False(factory.ComponentBBeltIsBuilt);
    }

    [Fact]
    public void BuiltMachineFResumesRawBFeedAndBlocksUntilItsOutputBeltExists()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        Advance(factory, 5);
        Assert.True(factory.BuildMachineF());

        AdvanceUntil(factory, state => state.MachineF.IsProcessing, 5);
        AdvanceUntil(factory, state => state.MachineF.ProcessingProgress == 1 && state.SourceBIsBlocked, 50);

        Assert.Empty(factory.ComponentBBeltItems);
        Assert.False(factory.AssemblerHasComponentB);
        Assert.True(factory.BuildComponentBBelt());
        AdvanceUntil(factory, state => state.AssemblerHasComponentB, 10);
        AssertConservation(factory);
    }

    [Fact]
    public void ComponentBBeltStartsAbsentAndMissingConnectionBlocksMachineFAndSourceB()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        Assert.True(factory.BuildMachineF());
        Assert.False(factory.ComponentBBeltIsBuilt);
        Assert.True(factory.MachineF.IsBuilt);
        Assert.True(factory.AssemblerE.IsBuilt);
        Assert.Empty(factory.ComponentBBeltItems);
        Assert.False(factory.AssemblerHasComponentB);
        AdvanceUntil(factory, state => state.MachineF.IsProcessing
            && state.MachineF.ProcessingProgress == 1 && state.SourceBIsBlocked, 200);
        Assert.Empty(factory.ComponentBBeltItems);
        Assert.False(factory.AssemblerHasComponentB);
        Assert.Equal(FactoryState.IdleMachinePowerPerStep, PowerFor(factory.MachineF));
        AssertConservation(factory);
    }

    [Fact]
    public void BuildingComponentBBeltIsAtomicFreeAndIndependent()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        Assert.False(factory.BuildComponentBBelt());
        Assert.True(factory.BuildMachineF());
        var elapsed = factory.ElapsedSteps;
        var credits = factory.Credits;
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.False(factory.Transport2Exists);
        Assert.True(factory.BuildComponentBBelt());
        Assert.True(factory.ComponentBBeltIsBuilt);
        Assert.False(factory.BuildComponentBBelt());
        Assert.Equal(elapsed, factory.ElapsedSteps);
        Assert.Equal(credits, factory.Credits);
        Assert.Equal(1, factory.ExpansionsAvailable);
        Assert.False(factory.Transport2Exists);
    }

    [Fact]
    public void BuiltComponentBBeltUsesExistingFiniteTravelBeforeAssemblerAcceptsB()
    {
        var factory = new FactoryState(startingCredits: 100_000);
        factory.BuildMachineF();
        factory.BuildComponentBBelt();
        AdvanceUntil(factory, state => state.ComponentBBeltItems.Any(item => item.Progress == 0), 50);
        Assert.False(factory.AssemblerHasComponentB);
        factory.AdvanceOneStep();
        Assert.Contains(factory.ComponentBBeltItems, item => item.Progress is > 0 and < 1);
        Assert.False(factory.AssemblerHasComponentB);
        AdvanceUntil(factory, state => state.AssemblerHasComponentB, 3);
        AssertConservation(factory);
    }

    [Fact]
    public void RecreatedFactoryRemovesComponentBBeltAndItsMaterialWhilePreservingCredits()
    {
        var used = CreateFactory();
        AdvanceUntil(used, state => state.ComponentBBeltCount > 0, 100);
        var reset = new FactoryState(startingCredits: used.Credits);
        Assert.False(reset.ComponentBBeltIsBuilt);
        Assert.False(reset.MachineFIsBuilt);
        Assert.Empty(reset.ComponentBBeltItems);
        Assert.Equal(used.Credits, reset.Credits);
    }

    [Fact]
    public void ShippingStorageExpansionRequiresAndConsumesExactlyTwelveFinishedItems()
    {
        var factory = CreateFactory();
        Assert.Equal(FactoryState.InitialShippingStorageCapacity, factory.ShippingStorageCapacity);
        Assert.False(factory.ShippingStorageIsExpanded);
        Assert.Equal(12, FactoryState.ShippingStorageExpansionFinishedItemCost);

        AdvanceUntil(factory, state => state.ShippingStorageCount == 11, 1_000);
        var credits = factory.Credits;
        var elapsed = factory.ElapsedSteps;
        Assert.False(factory.BuildShippingStorageExpansion());
        Assert.Equal(11, factory.ShippingStorageCount);
        Assert.Equal(credits, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);

        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 100);
        credits = factory.Credits;
        elapsed = factory.ElapsedSteps;
        var shipped = factory.ShippedFinishedCount;
        var totalProduced = factory.TotalFinishedProduced;
        Assert.True(factory.BuildShippingStorageExpansion());
        Assert.True(factory.ShippingStorageIsExpanded);
        Assert.Equal(0, factory.ShippingStorageCount);
        Assert.Equal(FactoryState.ExpandedShippingStorageCapacity, factory.ShippingStorageCapacity);
        Assert.Equal(12, factory.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(credits, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);
        Assert.Equal(shipped, factory.ShippedFinishedCount);
        Assert.Equal(totalProduced, factory.TotalFinishedProduced);
        AssertConservation(factory);
    }

    [Fact]
    public void DuplicateShippingExpansionFailsWithoutConsumingMoreItems()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(factory.BuildShippingStorageExpansion());
        AdvanceUntil(factory, state => state.ShippingStorageCount > 0, 100);
        var stored = factory.ShippingStorageCount;
        var consumed = factory.FinishedItemsConsumedForInfrastructure;

        Assert.False(factory.BuildShippingStorageExpansion());

        Assert.Equal(stored, factory.ShippingStorageCount);
        Assert.Equal(consumed, factory.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(FactoryState.ExpandedShippingStorageCapacity, factory.ShippingStorageCapacity);
        AssertConservation(factory);
    }

    [Fact]
    public void ExpandedShippingStorageCanDispatchTwoConsecutiveLoadsWithoutCooldown()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(factory.BuildShippingStorageExpansion());
        AdvanceUntil(factory, state => state.ShippingStorageCount >= 24, 1_000);
        Assert.False(factory.IsShippingStorageFull);
        Assert.True(factory.ShippingStorageCount > FactoryState.InitialShippingStorageCapacity);

        var stored = factory.ShippingStorageCount;
        var credits = factory.Credits;
        Assert.True(factory.DispatchFullShipment());
        Assert.True(factory.DispatchFullShipment());
        Assert.Equal(stored - 2 * FactoryState.CustomerOrderSize, factory.ShippingStorageCount);
        Assert.Equal(2 * FactoryState.CustomerOrderSize, factory.ShippedFinishedCount);
        Assert.Equal(credits + 2 * FactoryState.CustomerOrderSize * FactoryState.FinishedItemShipmentRevenue,
            factory.Credits);
        AssertConservation(factory);
    }

    [Fact]
    public void FullExpandedShippingStorageCanDispatchFourConsecutiveLoadsWithoutCooldown()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(factory.BuildShippingStorageExpansion());
        AdvanceUntil(factory, state => state.ShippingStorageCount == 48, 2_000);
        var credits = factory.Credits;
        var elapsed = factory.ElapsedSteps;

        for (var shipment = 0; shipment < 4; shipment++)
        {
            Assert.True(factory.DispatchFullShipment());
        }

        Assert.Equal(0, factory.ShippingStorageCount);
        Assert.Equal(48, factory.ShippedFinishedCount);
        Assert.Equal(credits + 48 * FactoryState.FinishedItemShipmentRevenue, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);
        AssertConservation(factory);
    }

    [Fact]
    public void ExpandedShippingStorageDelaysButDoesNotRemoveBackpressure()
    {
        var factory = CreateGoodFactory(100_000);
        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(factory.IsShippingStorageFull);
        Assert.True(factory.BuildShippingStorageExpansion());
        AdvanceUntil(factory, state => state.ShippingStorageCount > 12, 1_000);
        Assert.False(factory.IsShippingStorageFull);

        AdvanceUntil(factory, state => state.IsShippingStorageFull, 2_000);
        Assert.Equal(48, factory.ShippingStorageCount);
        Advance(factory, 500);
        Assert.True(factory.FinishedOutputBeltIsBlocked);
        Assert.True(factory.AssemblerE.IsProcessing);
        Assert.Equal(1, factory.AssemblerE.ProcessingProgress);
        Assert.True(factory.SourceIsBlocked);
        Assert.True(factory.SourceBIsBlocked);
        AssertCapacities(factory);
    }

    [Fact]
    public void FactoryResetRemovesShippingExpansionWithoutRefund()
    {
        var used = CreateFactory();
        AdvanceUntil(used, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(used.BuildShippingStorageExpansion());
        var reset = new FactoryState(startingCredits: used.Credits);

        Assert.False(reset.ShippingStorageIsExpanded);
        Assert.Equal(FactoryState.InitialShippingStorageCapacity, reset.ShippingStorageCapacity);
        Assert.Equal(0, reset.ShippingStorageCount);
        Assert.Equal(0, reset.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(used.Credits, reset.Credits);
    }

    [Fact]
    public void DirectFreightLinkStartsUnbuiltWithExactIndirectRouteEconomics()
    {
        var factory = CreateFactory();

        Assert.False(factory.DirectFreightLinkIsBuilt);
        Assert.Equal(15, FactoryState.TransportTravelSteps);
        Assert.Equal(15, factory.ComponentATransportTravelSteps);
        Assert.Equal(1, FactoryState.TransportEnergyCostPerCargoItem);
        Assert.Equal(2, FactoryState.IndirectRouteSurchargePerCargoItem);
        Assert.Equal(3, factory.ComponentATransportCostPerCargoItem);
        Assert.Equal(12, FactoryState.DirectFreightLinkAssemblyCost);
    }

    [Fact]
    public void DirectFreightLinkBuildRequiresAndAtomicallyConsumesExactlyTwelveAssemblies()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ShippingStorageCount == 11, 1_000);
        var credits = factory.Credits;
        var elapsed = factory.ElapsedSteps;
        var shipped = factory.ShippedFinishedCount;

        Assert.False(factory.BuildDirectFreightLink());
        Assert.Equal(11, factory.ShippingStorageCount);
        Assert.Equal(0, factory.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(credits, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);

        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 100);
        credits = factory.Credits;
        elapsed = factory.ElapsedSteps;
        shipped = factory.ShippedFinishedCount;
        var produced = factory.TotalFinishedProduced;

        Assert.True(factory.BuildDirectFreightLink());
        Assert.True(factory.DirectFreightLinkIsBuilt);
        Assert.Equal(0, factory.ShippingStorageCount);
        Assert.Equal(12, factory.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(credits, factory.Credits);
        Assert.Equal(elapsed, factory.ElapsedSteps);
        Assert.Equal(shipped, factory.ShippedFinishedCount);
        Assert.Equal(produced, factory.TotalFinishedProduced);
        Assert.Equal(6, factory.ComponentATransportTravelSteps);
        Assert.Equal(1, factory.ComponentATransportCostPerCargoItem);
        AssertConservation(factory);
    }

    [Fact]
    public void DuplicateDirectFreightLinkBuildFailsWithoutFurtherConsumption()
    {
        var factory = CreateFactoryWithDirectFreightLink();
        AdvanceUntil(factory, state => state.ShippingStorageCount > 0, 200);
        var stored = factory.ShippingStorageCount;
        var consumed = factory.FinishedItemsConsumedForInfrastructure;
        var credits = factory.Credits;

        Assert.False(factory.BuildDirectFreightLink());

        Assert.Equal(stored, factory.ShippingStorageCount);
        Assert.Equal(consumed, factory.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(credits, factory.Credits);
        AssertConservation(factory);
    }

    [Fact]
    public void Transport1DeparturesUseRouteEconomicsSelectedAtDeparture()
    {
        var indirect = CreateGoodFactory(100_000);
        AssertNextTransport1Departure(indirect, expectedTravelSteps: 15, expectedCostPerItem: 3);

        var direct = CreateFactoryWithDirectFreightLink(goodFactory: true);
        AdvanceUntil(direct, state => !state.Transport1IsDelivering && state.ComponentStorageCount > 0, 1_000);
        AssertNextTransport1Departure(direct, expectedTravelSteps: 6, expectedCostPerItem: 1);
    }

    [Fact]
    public void Transport2IndependentlyUsesTheSharedRouteEconomicsAtDeparture()
    {
        var indirect = CreateGoodFactory(100_000);
        Assert.True(indirect.BuildSecondTransport());
        AssertNextTransport2Departure(indirect, expectedTravelSteps: 15, expectedCostPerItem: 3);

        var direct = CreateFactoryWithDirectFreightLink(goodFactory: true);
        Assert.True(direct.BuildSecondTransport());
        AssertNextTransport2Departure(direct, expectedTravelSteps: 6, expectedCostPerItem: 1);
    }

    [Fact]
    public void BuildingDirectLinkDoesNotRetargetOrRefundAnIndirectTripAlreadyInFlight()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ShippingStorageCount == 12 && state.Transport1IsDelivering, 2_000);
        Assert.Equal(15, factory.Transport1AssignedTravelSteps);
        var progress = factory.Transport1Progress;
        var cargo = factory.Transport1CargoCount;
        var credits = factory.Credits;

        Assert.True(factory.BuildDirectFreightLink());

        Assert.Equal(15, factory.Transport1AssignedTravelSteps);
        Assert.Equal(progress, factory.Transport1Progress);
        Assert.Equal(cargo, factory.Transport1CargoCount);
        Assert.Equal(credits, factory.Credits);
        Assert.Equal(6, factory.ComponentATransportTravelSteps);
        AssertConservation(factory);
    }

    [Fact]
    public void DirectLinkPreservesTransportCapacityAndDestinationReservationInvariant()
    {
        var factory = CreateFactoryWithDirectFreightLink(goodFactory: true);
        Assert.True(factory.BuildSecondTransport());

        for (var step = 0; step < 2_000; step++)
        {
            factory.AdvanceOneStep();
            Dispatch(factory);
            Assert.InRange(factory.Transport1CargoCount, 0, FactoryState.TransportCapacity);
            Assert.InRange(factory.Transport2CargoCount, 0, FactoryState.TransportCapacity);
            Assert.InRange(factory.RemoteInputStorageCount + factory.CommittedTransportCargoCount,
                0, FactoryState.RemoteInputStorageCapacity);
            AssertConservation(factory);
        }
    }

    [Fact]
    public void DirectLinkAndShippingExpansionCanBeBuiltIndependentlyInEitherOrder()
    {
        var linkFirst = CreateFactory();
        AdvanceUntil(linkFirst, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(linkFirst.BuildDirectFreightLink());
        Assert.False(linkFirst.ShippingStorageIsExpanded);
        AdvanceUntil(linkFirst, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(linkFirst.BuildShippingStorageExpansion());
        Assert.True(linkFirst.DirectFreightLinkIsBuilt);

        var storageFirst = CreateFactory();
        AdvanceUntil(storageFirst, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(storageFirst.BuildShippingStorageExpansion());
        Assert.False(storageFirst.DirectFreightLinkIsBuilt);
        AdvanceUntil(storageFirst, state => state.ShippingStorageCount == 12, 1_000);
        Assert.True(storageFirst.BuildDirectFreightLink());
        Assert.True(storageFirst.ShippingStorageIsExpanded);
        Assert.Equal(24, storageFirst.FinishedItemsConsumedForInfrastructure);
        AssertConservation(linkFirst);
        AssertConservation(storageFirst);
    }

    [Fact]
    public void DispatchRemainsAnIndependentAlternativeToDirectLinkConstruction()
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ShippingStorageCount == 12, 1_000);
        var credits = factory.Credits;

        Assert.True(factory.DispatchFullShipment());

        Assert.False(factory.DirectFreightLinkIsBuilt);
        Assert.Equal(12, factory.ShippedFinishedCount);
        Assert.Equal(0, factory.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(credits + 12 * FactoryState.FinishedItemShipmentRevenue, factory.Credits);
        AssertConservation(factory);
    }

    [Fact]
    public void FactoryResetRemovesDirectLinkWithoutRefundAndRestoresIndirectRoute()
    {
        var used = CreateFactoryWithDirectFreightLink();
        var reset = new FactoryState(startingCredits: used.Credits);

        Assert.False(reset.DirectFreightLinkIsBuilt);
        Assert.Equal(15, reset.ComponentATransportTravelSteps);
        Assert.Equal(3, reset.ComponentATransportCostPerCargoItem);
        Assert.Equal(0, reset.ShippingStorageCount);
        Assert.Equal(0, reset.FinishedItemsConsumedForInfrastructure);
        Assert.Equal(used.Credits, reset.Credits);
    }

    [Fact]
    public void DirectLinkSimulationRemainsDeterministicAndConservative()
    {
        var first = CreateFactoryWithDirectFreightLink(goodFactory: true);
        var second = CreateFactoryWithDirectFreightLink(goodFactory: true);
        Assert.True(first.BuildSecondTransport());
        Assert.True(second.BuildSecondTransport());

        RunWithDispatch(first, 10_000);
        RunWithDispatch(second, 10_000);

        Assert.Equal(Snapshot(first), Snapshot(second));
        Assert.True(first.ShippedFinishedCount > 0);
        AssertConservation(first);
    }

    [Fact]
    public void ContinuouslyProcessingMultiStepMachineReachesFullRecentUtilization()
    {
        var factory = new FactoryState(machineAProcessingSteps: 1_000, startingCredits: 100_000);
        factory.BuildInputBelt();
        AdvanceUntil(factory, state => state.MachineA.IsProcessing, 20);

        Advance(factory, FactoryState.UtilizationWindowSteps);

        Assert.Equal(1, factory.MachineA.RecentUtilization);
    }

    [Fact]
    public void RecentUtilizationMatchesOnlyActualLatestTwoHundredProcessingSteps()
    {
        var factory = CreateFactory();
        var expected = new Queue<bool>();
        var sawProductiveStep = false;

        for (var step = 0; step < 450; step++)
        {
            var machine = factory.MachineA;
            var productive = !factory.IsOutOfCredits
                && machine.IsProcessing && machine.ProcessingProgress < 1;
            expected.Enqueue(productive);
            sawProductiveStep |= productive;
            if (expected.Count > FactoryState.UtilizationWindowSteps)
            {
                expected.Dequeue();
            }

            factory.AdvanceOneStep();
            Assert.Equal(expected.Count(value => value) / (double)expected.Count,
                factory.MachineA.RecentUtilization, 10);
        }

        Assert.Equal(FactoryState.UtilizationWindowSteps, expected.Count);
        Assert.True(sawProductiveStep);
        Assert.DoesNotContain(true, expected);
        Assert.Equal(0, factory.MachineA.RecentUtilization);
    }

    [Fact]
    public void OneHundredProductiveThenOneHundredIdleStepsReportHalfUtilization()
    {
        const int startupSteps = 7;
        var startupCost = startupSteps * 6;
        var productiveCost = 100 * 7;
        var factory = new FactoryState(machineAProcessingSteps: 200,
            startingCredits: startupCost + productiveCost);
        factory.BuildInputBelt();

        Advance(factory, startupSteps);
        Assert.True(factory.MachineA.IsProcessing);
        Advance(factory, 100);
        Assert.True(factory.IsOutOfCredits);
        Advance(factory, 100);

        Assert.Equal(0.5, factory.MachineA.RecentUtilization, 10);
    }

    [Fact]
    public void UnbuiltMachineHasNoHistoryAndNewBuildStartsFresh()
    {
        var factory = CreateFactory();
        Advance(factory, 300);
        Assert.False(factory.MachineB.IsBuilt);
        Assert.Equal(0, factory.MachineB.RecentUtilization);

        Assert.True(factory.BuildMachineB());
        var firstStepProductive = factory.MachineB.IsProcessing
            && factory.MachineB.ProcessingProgress < 1;
        factory.AdvanceOneStep();

        Assert.Equal(firstStepProductive ? 1 : 0, factory.MachineB.RecentUtilization);
    }

    [Fact]
    public void InputStarvedAndOutputBlockedMachinesReportZeroRecentUtilization()
    {
        var inputStarved = new FactoryState(startingCredits: 100_000);
        Advance(inputStarved, FactoryState.UtilizationWindowSteps);
        Assert.Equal(0, inputStarved.MachineA.RecentUtilization);

        var outputBlocked = CreateGoodFactory(100_000);
        Advance(outputBlocked, 1_800);
        Assert.True(outputBlocked.MachineA.IsProcessing);
        Assert.Equal(1, outputBlocked.MachineA.ProcessingProgress);
        Advance(outputBlocked, FactoryState.UtilizationWindowSteps);
        Assert.Equal(0, outputBlocked.MachineA.RecentUtilization);
    }

    [Fact]
    public void AssemblerUtilizationCountsOnlyProcessingWithBothInputs()
    {
        var waitingForA = CreateFactory(buildBelt: false);
        AdvanceUntil(waitingForA, state => state.AssemblerHasComponentB, 100);
        Advance(waitingForA, 20);
        Assert.Equal(0, waitingForA.AssemblerE.RecentUtilization);

        var waitingForB = new FactoryState(startingCredits: 100_000, sourceBStartDelaySteps: 1_000);
        waitingForB.BuildInputBelt();
        AdvanceUntil(waitingForB, state => state.AssemblerHasComponentA, 500);
        Assert.Equal(0, waitingForB.AssemblerE.RecentUtilization);

        var processing = CreateFactory();
        AdvanceUntil(processing, state => state.AssemblerE.IsProcessing
            && state.AssemblerE.ProcessingProgress == 0, 500);
        var before = processing.AssemblerE.RecentUtilization;
        processing.AdvanceOneStep();
        Assert.True(processing.AssemblerE.RecentUtilization > before);
    }

    [Fact]
    public void ZeroCreditStepsAndResetProduceFreshZeroUtilization()
    {
        var frozen = new FactoryState(startingCredits: 0);
        Advance(frozen, FactoryState.UtilizationWindowSteps);
        Assert.Equal(0, frozen.MachineA.RecentUtilization);
        Assert.Equal(0, frozen.FinisherC.RecentUtilization);
        Assert.Equal(0, frozen.AssemblerE.RecentUtilization);

        var used = CreateFactory();
        Advance(used, 300);
        Assert.True(used.MachineA.RecentUtilization > 0);
        var reset = new FactoryState(startingCredits: used.Credits);
        Assert.Equal(0, reset.MachineA.RecentUtilization);
        Assert.Equal(0, reset.FinisherC.RecentUtilization);
        Assert.Equal(0, reset.AssemblerE.RecentUtilization);
    }

    [Fact]
    public void FreshWorldHasOnlyHomeRegionRevealedAndExactDevelopmentRevealCost()
    {
        var factory = new FactoryState();

        Assert.True(factory.HomeRegionIsRevealed);
        Assert.False(factory.NorthRegionIsRevealed);
        Assert.False(factory.WestRegionIsRevealed);
        Assert.False(factory.EastRegionIsRevealed);
        Assert.Equal(2_000, FactoryState.FrontierRegionRevealCost);
    }

    [Theory]
    [InlineData("north")]
    [InlineData("west")]
    [InlineData("east")]
    public void EachFrontierRevealAtomicallyDeductsExactlyTwoThousandCredits(string region)
    {
        var factory = new FactoryState(startingCredits: FactoryState.FrontierRegionRevealCost);

        Assert.True(Reveal(factory, region));
        Assert.Equal(0, factory.Credits);
        Assert.Equal(region == "north", factory.NorthRegionIsRevealed);
        Assert.Equal(region == "west", factory.WestRegionIsRevealed);
        Assert.Equal(region == "east", factory.EastRegionIsRevealed);
    }

    [Fact]
    public void SuccessfulRevealChangesNoFactorySimulationState()
    {
        var factory = CreateFactory();
        Advance(factory, 50);
        var elapsed = factory.ElapsedSteps;
        var inventory = CountMaterial(factory);
        var materialB = CountMaterialB(factory);
        var shipped = factory.ShippedFinishedCount;
        var machineAUtilization = factory.MachineA.RecentUtilization;
        var assemblerUtilization = factory.AssemblerE.RecentUtilization;

        Assert.True(factory.RevealNorthRegion());

        Assert.Equal(elapsed, factory.ElapsedSteps);
        Assert.Equal(inventory, CountMaterial(factory));
        Assert.Equal(materialB, CountMaterialB(factory));
        Assert.Equal(shipped, factory.ShippedFinishedCount);
        Assert.Equal(machineAUtilization, factory.MachineA.RecentUtilization);
        Assert.Equal(assemblerUtilization, factory.AssemblerE.RecentUtilization);
        AssertConservation(factory);
    }

    [Theory]
    [InlineData("north")]
    [InlineData("west")]
    [InlineData("east")]
    public void UnaffordableAndDuplicateRevealsChangeNothing(string region)
    {
        var unaffordable = new FactoryState(startingCredits: FactoryState.FrontierRegionRevealCost - 1);
        Assert.False(Reveal(unaffordable, region));
        Assert.Equal(FactoryState.FrontierRegionRevealCost - 1, unaffordable.Credits);
        Assert.False(unaffordable.NorthRegionIsRevealed);
        Assert.False(unaffordable.WestRegionIsRevealed);
        Assert.False(unaffordable.EastRegionIsRevealed);

        var duplicate = new FactoryState(startingCredits: FactoryState.FrontierRegionRevealCost * 2);
        Assert.True(Reveal(duplicate, region));
        var credits = duplicate.Credits;
        Assert.False(Reveal(duplicate, region));
        Assert.Equal(credits, duplicate.Credits);
    }

    [Fact]
    public void AllFrontiersCanBeRevealedConsecutivelyWithoutFactoryProgression()
    {
        var factory = new FactoryState(startingCredits: FactoryState.FrontierRegionRevealCost * 3);

        Assert.True(factory.RevealNorthRegion());
        Assert.True(factory.RevealWestRegion());
        Assert.True(factory.RevealEastRegion());

        Assert.True(factory.NorthRegionIsRevealed);
        Assert.True(factory.WestRegionIsRevealed);
        Assert.True(factory.EastRegionIsRevealed);
        Assert.Equal(0, factory.Credits);
        Assert.Equal(0, factory.ElapsedSteps);
        Assert.False(factory.InputBeltIsBuilt);
        Assert.False(factory.MachineB.IsBuilt);
        Assert.False(factory.FinisherD.IsBuilt);
    }

    [Fact]
    public void TenThousandStepsAndAutomaticStyleDispatchNeverRevealFrontierRegions()
    {
        var first = CreateFactoryWithDirectFreightLink(goodFactory: true);
        var second = CreateFactoryWithDirectFreightLink(goodFactory: true);
        var startingCredits = first.Credits;
        for (var step = 0; step < 10_000; step++)
        {
            first.AdvanceOneStep();
            second.AdvanceOneStep();
            Dispatch(first);
            Dispatch(second);
        }

        Assert.True(first.Credits > startingCredits);
        Assert.False(first.NorthRegionIsRevealed);
        Assert.False(first.WestRegionIsRevealed);
        Assert.False(first.EastRegionIsRevealed);
        Assert.Equal(Snapshot(first), Snapshot(second));
        AssertConservation(first);
    }

    [Fact]
    public void FactoryResetPreservesAllRevealsWithoutRefundAndResetsPhysicalState()
    {
        var used = CreateFactory();
        Assert.True(used.RevealNorthRegion());
        Assert.True(used.RevealWestRegion());
        Assert.True(used.RevealEastRegion());
        Advance(used, 100);
        var credits = used.Credits;

        var reset = new FactoryState(
            startingCredits: used.Credits,
            northRegionIsRevealed: used.NorthRegionIsRevealed,
            westRegionIsRevealed: used.WestRegionIsRevealed,
            eastRegionIsRevealed: used.EastRegionIsRevealed);

        Assert.True(reset.NorthRegionIsRevealed);
        Assert.True(reset.WestRegionIsRevealed);
        Assert.True(reset.EastRegionIsRevealed);
        Assert.Equal(credits, reset.Credits);
        Assert.Equal(0, reset.ElapsedSteps);
        Assert.Equal(0, CountMaterial(reset));
        Assert.False(reset.InputBeltIsBuilt);
        Assert.False(reset.MachineFIsBuilt);
        Assert.False(reset.Transport2Exists);
        Assert.False(reset.ShippingStorageIsExpanded);
        Assert.False(reset.DirectFreightLinkIsBuilt);
        Assert.Equal(0, reset.MachineA.RecentUtilization);
    }


    private static FactoryState CreateFactory(bool buildBelt = true, int startingCredits = 100_000)
    {
        var factory = new FactoryState(machineAProcessingSteps: 2, machineBProcessingSteps: 4,
            finisherProcessingSteps: 1, beltCapacity: 5, sourceIntervalSteps: 1,
            startingCredits: startingCredits + FactoryState.MachineFPurchaseCost);
        factory.BuildMachineF();
        factory.BuildComponentBBelt();
        if (buildBelt) factory.BuildInputBelt();
        return factory;
    }

    private static FactoryState CreateGoodFactory(int credits)
    {
        var factory = CreateFactory(startingCredits: credits + FactoryState.MachineBPurchaseCost);
        factory.BuildMachineB();
        factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB);
        return factory;
    }

    private static FactoryState CreateFactoryWithDirectFreightLink(bool goodFactory = false)
    {
        var factory = CreateFactory();
        AdvanceUntil(factory, state => state.ShippingStorageCount == FactoryState.DirectFreightLinkAssemblyCost, 1_000);
        Assert.True(factory.BuildDirectFreightLink());
        if (goodFactory)
        {
            Assert.True(factory.BuildMachineB());
            Assert.True(factory.SetStageOneRoutingPolicy(StageOneRoutingPolicy.PriorityAWithOverflowB));
        }
        return factory;
    }

    private static void AssertNextTransport1Departure(FactoryState factory, int expectedTravelSteps, int expectedCostPerItem)
    {
        AdvanceUntil(factory, state => !state.Transport1IsDelivering && state.ComponentStorageCount > 0
            && state.RemoteInputStorageCount + state.CommittedTransportCargoCount < FactoryState.RemoteInputStorageCapacity, 1_000);
        var credits = factory.Credits;
        var gridCost = factory.CurrentGridCostPerStep;
        factory.AdvanceOneStep();
        var cargo = factory.Transport1CargoCount;
        Assert.InRange(cargo, 1, FactoryState.TransportCapacity);
        Assert.Equal(expectedTravelSteps, factory.Transport1AssignedTravelSteps);
        Assert.Equal(credits - gridCost - cargo * expectedCostPerItem, factory.Credits);
    }

    private static void AssertNextTransport2Departure(FactoryState factory, int expectedTravelSteps, int expectedCostPerItem)
    {
        AdvanceUntil(factory, state => state.Transport1IsDelivering && !state.Transport2IsDelivering
            && state.ComponentStorageCount > 0
            && state.RemoteInputStorageCount + state.CommittedTransportCargoCount < FactoryState.RemoteInputStorageCapacity, 1_000);
        var credits = factory.Credits;
        var gridCost = factory.CurrentGridCostPerStep;
        factory.AdvanceOneStep();
        var cargo = factory.Transport2CargoCount;
        Assert.InRange(cargo, 1, FactoryState.TransportCapacity);
        Assert.Equal(expectedTravelSteps, factory.Transport2AssignedTravelSteps);
        Assert.Equal(credits - gridCost - cargo * expectedCostPerItem, factory.Credits);
    }

    private static FactoryState CreateRoutingProbeFactory(int branchCapacity)
    {
        var factory = new FactoryState(machineAProcessingSteps: 100, machineBProcessingSteps: 100,
            finisherProcessingSteps: 1, beltCapacity: 10, branchBeltCapacity: branchCapacity,
            sourceIntervalSteps: 1, startingCredits: 100_000);
        factory.BuildInputBelt(); factory.BuildMachineB();
        return factory;
    }

    private static FactoryState CreateAsymmetricRoutingProbeFactory()
    {
        var factory = new FactoryState(machineAProcessingSteps: 1, machineBProcessingSteps: 100,
            finisherProcessingSteps: 1, beltCapacity: 20, branchBeltCapacity: 1,
            sourceIntervalSteps: 1, startingCredits: 100_000);
        factory.BuildInputBelt(); factory.BuildMachineB();
        return factory;
    }

    private static void Advance(FactoryState factory, int steps) { for (var i = 0; i < steps; i++) factory.AdvanceOneStep(); }
    private static void AdvanceUntil(FactoryState factory, Func<FactoryState, bool> condition, int limit)
    { for (var i = 0; i < limit && !condition(factory); i++) factory.AdvanceOneStep(); Assert.True(condition(factory)); }
    private static void Dispatch(FactoryState factory)
    {
        if (factory.ShippingStorageCount >= FactoryState.CustomerOrderSize)
        {
            Assert.True(factory.DispatchFullShipment());
        }
    }
    private static bool Reveal(FactoryState factory, string region) => region switch
    {
        "north" => factory.RevealNorthRegion(),
        "west" => factory.RevealWestRegion(),
        "east" => factory.RevealEastRegion(),
        _ => throw new ArgumentOutOfRangeException(nameof(region)),
    };
    private static void RunWithDispatch(FactoryState factory, int steps)
    { for (var i = 0; i < steps; i++) { factory.AdvanceOneStep(); Dispatch(factory); } }
    private static int PowerFor(MachineView machine) => machine.IsProcessing && machine.ProcessingProgress < 1 ? 3 : 2;

    private static int CountMaterial(FactoryState f) => f.RawInputBeltCount + f.BranchBeltACount + f.BranchBeltBCount
        + (f.MachineA.IsProcessing ? 1 : 0) + (f.MachineB.IsProcessing ? 1 : 0) + f.IntermediateBeltCount + f.BufferCount
        + (f.FinisherC.IsProcessing ? 1 : 0) + (f.FinisherD.IsProcessing ? 1 : 0) + f.ComponentBeltCount
        + f.ComponentStorageCount + f.Transport1CargoCount + f.Transport2CargoCount + f.RemoteInputStorageCount + f.RemoteInputBeltCount
        + (f.AssemblerHasComponentA ? 1 : 0)
        + (f.AssemblerE.IsProcessing ? 1 : 0) + f.FinishedOutputBeltCount + f.ShippingStorageCount
        + f.ShippedFinishedCount + f.FinishedItemsConsumedForInfrastructure;

    private static int CountMaterialB(FactoryState f) => f.RawBBeltCount + (f.MachineF.IsProcessing ? 1 : 0)
        + f.ComponentBBeltCount + (f.AssemblerHasComponentB ? 1 : 0) + (f.AssemblerE.IsProcessing ? 1 : 0)
        + f.FinishedOutputBeltCount + f.ShippingStorageCount + f.ShippedFinishedCount
        + f.FinishedItemsConsumedForInfrastructure;

    private static void AssertConservation(FactoryState factory)
    {
        Assert.Equal(factory.MaterialCreated, CountMaterial(factory));
        Assert.Equal(factory.MaterialBCreated, CountMaterialB(factory));
    }
    private static void AssertCapacities(FactoryState f)
    {
        Assert.InRange(f.ComponentStorageCount, 0, f.StorageCapacity);
        Assert.InRange(f.Transport1CargoCount, 0, FactoryState.TransportCapacity);
        Assert.InRange(f.Transport2CargoCount, 0, FactoryState.TransportCapacity);
        Assert.InRange(f.RemoteInputStorageCount + f.CommittedTransportCargoCount, 0, FactoryState.RemoteInputStorageCapacity);
        Assert.InRange(f.RemoteInputStorageCount, 0, FactoryState.RemoteInputStorageCapacity);
        Assert.InRange(f.RemoteInputBeltCount, 0, FactoryState.RemoteBeltCapacity);
        Assert.InRange(f.FinishedOutputBeltCount, 0, FactoryState.RemoteBeltCapacity);
        Assert.InRange(f.RawBBeltCount, 0, FactoryState.ComponentBBeltCapacity);
        Assert.InRange(f.ComponentBBeltCount, 0, FactoryState.ComponentBBeltCapacity);
        Assert.InRange(f.ShippingStorageCount, 0, f.ShippingStorageCapacity);
        AssertConservation(f);
    }
    private static void AssertKinds(FactoryState f)
    {
        Assert.All(f.RawInputBeltItems, item => Assert.Equal(MaterialKind.Raw, item.Material));
        Assert.All(f.IntermediateBeltItems, item => Assert.Equal(MaterialKind.Intermediate, item.Material));
        Assert.All(f.ComponentBeltItems, item => Assert.Equal(MaterialKind.ComponentA, item.Material));
        Assert.All(f.RemoteInputBeltItems, item => Assert.Equal(MaterialKind.ComponentA, item.Material));
        Assert.All(f.RawBBeltItems, item => Assert.Equal(MaterialKind.RawB, item.Material));
        Assert.All(f.ComponentBBeltItems, item => Assert.Equal(MaterialKind.ComponentB, item.Material));
        Assert.All(f.FinishedOutputBeltItems, item => Assert.Equal(MaterialKind.Finished, item.Material));
    }
    private static string Snapshot(FactoryState f) => string.Join(',', f.ElapsedSteps, f.MaterialCreated, f.Credits,
        f.ShippedFinishedCount, f.ComponentStorageCount, f.Transport1CargoCount, f.Transport2CargoCount,
        f.Transport2Exists, f.Transport2Progress, f.Transport1AssignedTravelSteps, f.Transport2AssignedTravelSteps,
        f.DirectFreightLinkIsBuilt, f.ComponentATransportTravelSteps, f.ComponentATransportCostPerCargoItem, f.RemoteInputStorageCount,
        f.RemoteInputBeltCount, f.FinishedOutputBeltCount, f.ShippingStorageCount,
        f.ShippingStorageCapacity, f.ShippingStorageIsExpanded, f.FinishedItemsConsumedForInfrastructure, f.RawInputBeltCount,
        f.IntermediateBeltCount, f.BufferCount, f.ComponentBeltCount, f.CurrentPowerDraw, f.MaterialBCreated,
        f.RawBBeltCount, f.ComponentBBeltCount, f.AssemblerHasComponentA, f.AssemblerHasComponentB,
        f.MachineA.RecentUtilization, f.MachineB.RecentUtilization,
        f.FinisherC.RecentUtilization, f.FinisherD.RecentUtilization,
        f.MachineFIsBuilt, f.MachineF.IsProcessing, f.MachineF.ProcessingProgress, f.MachineF.RecentUtilization,
        f.AssemblerE.RecentUtilization, f.ComponentBBeltIsBuilt,
        f.NorthRegionIsRevealed, f.WestRegionIsRevealed, f.EastRegionIsRevealed);
}
