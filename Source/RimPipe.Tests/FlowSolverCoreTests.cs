namespace RimPipe.Tests;

public class FlowSolverCoreTests
{
	private static void AssertClose(float expected, float actual, float tolerance = 1e-4f)
	{
		Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected}, got {actual}");
	}

	[Fact]
	public void Pressure_ClampsNegativeAndOverfull()
	{
		AssertClose(0f, FlowSolverCore.PressureFromAmount(-10f, 100f));
		AssertClose(1f, FlowSolverCore.PressureFromAmount(150f, 100f));
		AssertClose(0f, FlowSolverCore.PressureFromAmount(50f, 0f));
	}

	[Fact]
	public void Equalize_EqualCapacities_UsesPressureDifference()
	{
		// P=0.8 vs P=0.2, minCap=100 -> drive = 0.6/2*100 = 30
		float want = FlowSolverCore.ComputeEqualizeWant(80f, 20f, 100f, 100f, 1_000f, 1f);
		AssertClose(30f, want);
	}

	[Fact]
	public void Equalize_RateCapLimits()
	{
		float want = FlowSolverCore.ComputeEqualizeWant(80f, 20f, 100f, 100f, 10f, 1f);
		AssertClose(10f, want);
	}

	[Fact]
	public void Equalize_NoFlowWhenPressureEqual()
	{
		float want = FlowSolverCore.ComputeEqualizeWant(50f, 50f, 100f, 100f, 1_000f, 1f);
		AssertClose(0f, want);
	}

	[Fact]
	public void Equalize_UnequalCapacities_EqualPressureNoFlow()
	{
		// 16.675/100 ≈ 83.325/500
		float want = FlowSolverCore.ComputeEqualizeWant(16.675f, 83.325f, 100f, 500f, 1_000f, 1f);
		AssertClose(0f, want, 1e-3f);
	}

	[Fact]
	public void Equalize_ViscosityScalesRateCap()
	{
		// maxRate 10 / viscosity 2 = 5
		float want = FlowSolverCore.ComputeEqualizeWant(80f, 20f, 100f, 100f, 10f, 2f);
		AssertClose(5f, want);
	}

	[Fact]
	public void Forced_BasicPump()
	{
		float want = FlowSolverCore.ComputeForcedWant(50f, 0f, 100f, 10f, 1f);
		AssertClose(10f, want);
	}

	[Fact]
	public void Forced_SourceAmountLimits()
	{
		float want = FlowSolverCore.ComputeForcedWant(5f, 0f, 100f, 10f, 1f);
		AssertClose(5f, want);
	}

	[Fact]
	public void Forced_FreeCapacityLimits()
	{
		float want = FlowSolverCore.ComputeForcedWant(50f, 95f, 100f, 10f, 1f);
		AssertClose(5f, want);
	}

	[Fact]
	public void Forced_ZeroViscosityReturnsZero()
	{
		float want = FlowSolverCore.ComputeForcedWant(50f, 0f, 100f, 10f, 0f);
		AssertClose(0f, want);
	}

	[Fact]
	public void RateCap_ZeroViscosityReturnsZero()
	{
		AssertClose(0f, FlowSolverCore.ComputeRateCap(10f, 0f));
	}

	[Fact]
	public void Equalize_ZeroViscosityReturnsZero()
	{
		float want = FlowSolverCore.ComputeEqualizeWant(80f, 20f, 100f, 100f, 10f, 0f);
		AssertClose(0f, want);
	}

	[Fact]
	public void Pressure_NanAmountOrCapacityReturnsZero()
	{
		AssertClose(0f, FlowSolverCore.PressureFromAmount(float.NaN, 100f));
		AssertClose(0f, FlowSolverCore.PressureFromAmount(50f, float.NaN));
	}

	[Fact]
	public void Forced_NegativeOrNanSourceReturnsZero()
	{
		AssertClose(0f, FlowSolverCore.ComputeForcedWant(-5f, 0f, 100f, 10f, 1f));
		AssertClose(0f, FlowSolverCore.ComputeForcedWant(float.NaN, 0f, 100f, 10f, 1f));
	}

	[Fact]
	public void Forced_NanTargetOrCapacityReturnsZero()
	{
		AssertClose(0f, FlowSolverCore.ComputeForcedWant(50f, float.NaN, 100f, 10f, 1f));
		AssertClose(0f, FlowSolverCore.ComputeForcedWant(50f, 0f, float.NaN, 10f, 1f));
	}

	[Fact]
	public void Equalize_NanAmountOrCapacityReturnsZero()
	{
		AssertClose(0f, FlowSolverCore.ComputeEqualizeWant(float.NaN, 20f, 100f, 100f, 10f, 1f));
		AssertClose(0f, FlowSolverCore.ComputeEqualizeWant(80f, float.NaN, 100f, 100f, 10f, 1f));
		AssertClose(0f, FlowSolverCore.ComputeEqualizeWant(80f, 20f, float.NaN, 100f, 10f, 1f));
		AssertClose(0f, FlowSolverCore.ComputeEqualizeWant(80f, 20f, 100f, float.NaN, 10f, 1f));
	}

	[Fact]
	public void Equalize_NegativeLowSideIsRecoverableWithoutNaN()
	{
		// 低侧为负时应按空槽处理（pressure 钳 0），可回流补到非负；且结果必须有限。
		float want = FlowSolverCore.ComputeEqualizeWant(80f, -20f, 100f, 100f, 10f, 1f);
		Assert.False(float.IsNaN(want));
		Assert.True(want > 0f);
		AssertClose(10f, want);
	}

}
