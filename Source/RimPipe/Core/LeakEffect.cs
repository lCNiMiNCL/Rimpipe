namespace RimPipe;

/// <summary>
/// 泄漏扣量之后还能往环境泼什么效果（写在 FluidDef.leakEffects 里）。
/// 已实现：Filth（脏污）、Temperature（推热/降温）。None = 只扣量，不搞环境效果。
/// </summary>
public enum LeakEffect : byte
{
	None = 0,
	Filth = 1,
	Temperature = 2
}
