namespace RimPipe;

/// <summary>配方中一种流体的化学计量（每 1 批单位）。</summary>
public class PipeReactionFluidAmount
{
	public FluidDef? fluid;

	/// <summary>每 1 批单位消耗或产出的量。</summary>
	public float stoichAmount = 1f;
}
