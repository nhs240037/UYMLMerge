namespace UYMerge.Core;

public sealed record UnityYamlDocument
(
		int ClassId,
		long FileId,
		string RawText,
		bool IsStripped = false
);
