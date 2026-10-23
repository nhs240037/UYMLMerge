namespace UYMerge.Core;

public enum MergeConflictKind
{
	BothModified,
	DeleteVsModify,
	AddAdd,
	ObjectTypeMismatch
}

public sealed record MergeConflict(
		long FileId,
		MergeConflictKind Kind,
		UnityYamlDocument? Base,
		UnityYamlDocument? Ours,
		UnityYamlDocument? Theirs,
		string Message);

public sealed record ObjectMergeResult(
		IReadOnlyList<UnityYamlDocument> Documents,
		IReadOnlyList<MergeConflict> Conflicts)
{
	public bool HasConflicts => Conflicts.Count > 0;

	/// <summary>
	/// 競合が存在せず、完成版として保存できるかどうか。
	/// </summary>
	public bool CanSave => !HasConflicts;
}