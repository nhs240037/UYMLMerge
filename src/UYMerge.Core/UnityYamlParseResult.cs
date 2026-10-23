namespace UYMerge.Core;

public sealed record UnityYamlParseResult(
		string Preamble,
		IReadOnlyList<UnityYamlDocument> Documents);
