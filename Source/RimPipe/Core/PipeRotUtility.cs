using Verse;

namespace RimPipe;

/// <summary>本地 Rot4（相对建筑默认 North）→ 世界 Rot4 / 外一格辅助。</summary>
public static class PipeRotUtility
{
	public static Rot4 ToWorld(Rot4 localRot, Rot4 buildingRotation)
	{
		return new Rot4(localRot.AsInt + buildingRotation.AsInt);
	}

	public static IntVec3 OuterCell(IntVec3 buildingPos, Rot4 worldRot)
	{
		return buildingPos + worldRot.FacingCell;
	}
}
