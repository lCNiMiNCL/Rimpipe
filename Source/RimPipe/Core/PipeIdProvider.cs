using Verse;

namespace RimPipe;

/// <summary>地图级自增 int ID（Container / Mapping）。</summary>
public class PipeIdProvider : IExposable
{
	private int nextId = 1;

	public int Next()
	{
		int id = nextId;
		nextId++;
		return id;
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref nextId, "nextId", 1);
		if (nextId < 1)
		{
			nextId = 1;
		}
	}
}
