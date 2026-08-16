namespace RimPipe.Tests;

public class ChemSolverCoreTests
{
	private static ChemReactionSpec CreateLoxRp1()
	{
		return new ChemReactionSpec
		{
			InputFluids = new[] { "LOX", "RP1" },
			InputStoich = new[] { 2.3f, 1f },
			OutputFluids = new[] { "Exhaust" },
			OutputStoich = new[] { 3.3f },
			MaxRate = 5f,
			BaseMixRatio = 2.3f,
			RatioMin = 1.5f,
			RatioMax = 3f,
			MixRatioOxidizerInputIndex = 0,
			MixRatioFuelInputIndex = 1,
			EfficiencyAtStoich = 1f,
			EfficiencyAtRatioEdge = 0.5f,
			MinTemperature = -273f,
			MinPressure = 0f,
			ConditionContainerIndex = -1
		};
	}

	private static List<ChemContainerState> Inputs(float lox = 100f, float rp1 = 100f, float temp = 25f)
	{
		return new List<ChemContainerState>
		{
			new ChemContainerState("LOX", lox, 100f, temp),
			new ChemContainerState("RP1", rp1, 100f, temp)
		};
	}

	private static List<ChemContainerState> Outputs(float exhaust = 0f, float capacity = 500f)
	{
		return new List<ChemContainerState>
		{
			new ChemContainerState("Exhaust", exhaust, capacity, 25f)
		};
	}

	[Fact]
	public void Efficiency_StoichIsPeak()
	{
		var rx = CreateLoxRp1();
		Assert.Equal(1f, ChemSolverCore.ComputeEfficiency(rx, rx.StoichMixRatio), 4);
	}

	[Fact]
	public void Efficiency_EdgeIsHalf()
	{
		var rx = CreateLoxRp1();
		Assert.Equal(0.5f, ChemSolverCore.ComputeEfficiency(rx, rx.RatioMin), 4);
		Assert.Equal(0.5f, ChemSolverCore.ComputeEfficiency(rx, rx.RatioMax), 4);
	}

	[Fact]
	public void ClampMixRatio_UsesDefaultWhenNonPositive()
	{
		var rx = CreateLoxRp1();
		Assert.Equal(rx.ResolvedBaseMixRatio, ChemSolverCore.ClampMixRatio(rx, 0f), 4);
		Assert.Equal(rx.RatioMin, ChemSolverCore.ClampMixRatio(rx, 0.1f), 4);
		Assert.Equal(rx.RatioMax, ChemSolverCore.ClampMixRatio(rx, 99f), 4);
	}

	[Fact]
	public void PassesConditions_RejectsCold()
	{
		var rx = CreateLoxRp1();
		rx.MinTemperature = 20f;
		Assert.False(ChemSolverCore.PassesConditions(rx, Inputs(temp: 10f), out string? reason));
		Assert.Equal("cold", reason);
	}

	[Fact]
	public void PassesConditions_RejectsLowPressure()
	{
		var rx = CreateLoxRp1();
		rx.MinPressure = 0.5f;
		Assert.False(ChemSolverCore.PassesConditions(rx, Inputs(lox: 10f, rp1: 10f), out string? reason));
		Assert.Equal("lowP", reason);
	}

	[Fact]
	public void ComputeBatchCount_ReturnsMaxRateWhenPlenty()
	{
		var rx = CreateLoxRp1();
		float n = ChemSolverCore.ComputeBatchCount(rx, Inputs(), Outputs(), true, 2.3f, out float eta, out string? reason);
		Assert.Equal(5f, n, 4);
		Assert.Equal(1f, eta, 4);
		Assert.Null(reason);
	}

	[Fact]
	public void ComputeBatchCount_LimitedByInputAmount()
	{
		var rx = CreateLoxRp1();
		// LOX need = 2.3 per batch; with 11.5 LOX -> n=5; with 11.4 -> n≈4.956
		float n = ChemSolverCore.ComputeBatchCount(rx, Inputs(lox: 11.4f, rp1: 100f), Outputs(), true, 2.3f, out _, out _);
		Assert.Equal(4.9565215f, n, 3);
	}

	[Fact]
	public void ComputeBatchCount_DisabledReturnsZero()
	{
		var rx = CreateLoxRp1();
		float n = ChemSolverCore.ComputeBatchCount(rx, Inputs(), Outputs(), false, 2.3f, out _, out string? reason);
		Assert.Equal(0f, n);
		Assert.Equal("disabled", reason);
	}

	[Fact]
	public void BuildAmountDeltas_MatchesStoichiometry()
	{
		var rx = CreateLoxRp1();
		var deltas = new List<(int Index, float Delta)>();
		ChemSolverCore.BuildAmountDeltas(rx, Inputs(), Outputs(), 5f, 2.3f, 1f, deltas);
		Assert.Equal(3, deltas.Count);
		Assert.Equal((0, -5f * 2.3f), deltas[0]);
		Assert.Equal((1, -5f * 1f), deltas[1]);
		Assert.Equal((2, 5f * 3.3f), deltas[2]);
	}
}
