namespace RimPipe.Tests;

public class HeatSolverCoreTests
{
	private static void AssertClose(float expected, float actual, float tolerance = 1e-4f)
	{
		Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected}, got {actual}");
	}

	[Fact]
	public void HeatWant_BasicExchange()
	{
		// 50 mass both sides, c=1, dT=60, maxHeatRate=50 -> q capped at 50
		float q = HeatSolverCore.ComputeHeatWant(50f, 50f, 80f, 20f, 1f, 1f, 50f);
		AssertClose(50f, q);
	}

	[Fact]
	public void HeatWant_NotCappedWhenRateHigh()
	{
		// dT=20, m=10 each, c=1 -> mEff=10, q=20/2*10=100
		float q = HeatSolverCore.ComputeHeatWant(10f, 10f, 40f, 20f, 1f, 1f, 1_000f);
		AssertClose(100f, q);
	}

	[Fact]
	public void HeatWant_SpecificHeatUsesSmallerThermalMass()
	{
		// hot 50*c1=50, cold 50*c2=100 -> mEff=50; 给足够大的 rate 避免封顶
		float q = HeatSolverCore.ComputeHeatWant(50f, 50f, 80f, 20f, 1f, 2f, 10_000f);
		AssertClose(1500f, q);
	}

	[Fact]
	public void HeatWant_EmptySideNoExchange()
	{
		float q = HeatSolverCore.ComputeHeatWant(0f, 50f, 80f, 20f, 1f, 1f, 1_000f);
		AssertClose(0f, q);
	}

	[Fact]
	public void HeatWant_SmallDeltaNoExchange()
	{
		float q = HeatSolverCore.ComputeHeatWant(50f, 50f, 20.005f, 20f, 1f, 1f, 1_000f);
		AssertClose(0f, q);
	}

	[Fact]
	public void HeatWant_MaxHeatRateCaps()
	{
		float q = HeatSolverCore.ComputeHeatWant(50f, 50f, 80f, 20f, 1f, 1f, 10f);
		AssertClose(10f, q);
	}

	[Fact]
	public void HeatWant_NegativeOrNanAmountReturnsZero()
	{
		AssertClose(0f, HeatSolverCore.ComputeHeatWant(-5f, 50f, 80f, 20f, 1f, 1f, 1_000f));
		AssertClose(0f, HeatSolverCore.ComputeHeatWant(50f, -5f, 80f, 20f, 1f, 1f, 1_000f));
		AssertClose(0f, HeatSolverCore.ComputeHeatWant(float.NaN, 50f, 80f, 20f, 1f, 1f, 1_000f));
		AssertClose(0f, HeatSolverCore.ComputeHeatWant(50f, float.NaN, 80f, 20f, 1f, 1f, 1_000f));
	}

	[Fact]
	public void HeatWant_NanParametersReturnZero()
	{
		AssertClose(0f, HeatSolverCore.ComputeHeatWant(50f, 50f, float.NaN, 20f, 1f, 1f, 1_000f));
		AssertClose(0f, HeatSolverCore.ComputeHeatWant(50f, 50f, 80f, 20f, float.NaN, 1f, 1_000f));
		AssertClose(0f, HeatSolverCore.ComputeHeatWant(50f, 50f, 80f, 20f, 1f, 1f, float.NaN));
	}

}
