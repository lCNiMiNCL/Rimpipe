namespace RimPipe;

public enum MappingType : byte
{
	Flow = 0,
	Heat = 1,
	/// <summary>
	/// 占位枚举。多流体化学反应走反应釜 + RecipeDef，不用「成对 Chemical Mapping」传质，所以一般用不到这个值。
	/// </summary>
	Chemical = 2
}
