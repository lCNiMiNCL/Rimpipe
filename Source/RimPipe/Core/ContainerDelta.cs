using Verse;

namespace RimPipe;

/// <summary>
/// 一批里某个 Container 准备加减多少量。只能先记在这里，Commit 时再真正改 amount，禁止中途直接改桶。
/// </summary>
public class ContainerDelta
{
	public Container container;
	public float delta;

	public ContainerDelta(Container container, float delta)
	{
		this.container = container;
		this.delta = delta;
	}
}
