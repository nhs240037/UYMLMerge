using System.Globalization;
using System.Text.RegularExpressions;

namespace UYMerge.Core;

/// <summary>
/// Splits Unity text serialization into object documents while preserving
/// the original text. This is a structural splitter, not a full YAML parser.
/// </summary>
public sealed class UnityYamlParser
{
	// Unity's serialized object header, e.g.:
	// --- !u!1 &123
	// --- !u!114 &-123 stripped
	private static readonly Regex HeaderRegex = new(
			@"^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)(?<stripped>\s+stripped)?[ \t]*$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public IReadOnlyList<UnityYamlDocument> Parse(string text) =>
			ParseDetailed(text).Documents;

	public UnityYamlParseResult ParseDetailed(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		var headers = FindHeaders(text);
		if (headers.Count == 0)
		{
			throw new FormatException(
					"Unity YAML のオブジェクトヘッダーが見つかりません。");
		}

		var preamble = text[..headers[0].Start];
		var documents = new List<UnityYamlDocument>(headers.Count);

		for (var i = 0; i < headers.Count; i++)
		{
			var header = headers[i];
			var end = i + 1 < headers.Count
					? headers[i + 1].Start
					: text.Length;

			var rawText = text[header.Start..end];
			documents.Add(new UnityYamlDocument(
					header.ClassId,
					header.FileId,
					rawText,
					header.IsStripped));
		}

		var duplicate = documents
				.GroupBy(document => document.FileId)
				.FirstOrDefault(group => group.Count() > 1);

		if (duplicate is not null)
		{
			throw new FormatException(
					$"fileID {duplicate.Key} が重複しています。");
		}

		return new UnityYamlParseResult(preamble, documents);
	}

	private static List<HeaderMatch> FindHeaders(string text)
	{
		var result = new List<HeaderMatch>();
		var lineStart = 0;

		while (lineStart < text.Length)
		{
			var lineEnd = text.IndexOf('\n', lineStart);
			if (lineEnd < 0)
			{
				lineEnd = text.Length;
			}

			var contentEnd = lineEnd;
			if (contentEnd > lineStart && text[contentEnd - 1] == '\r')
			{
				contentEnd--;
			}

			var line = text.AsSpan(lineStart, contentEnd - lineStart);
			var match = HeaderRegex.Match(line.ToString());

			if (match.Success)
			{
				var classId = int.Parse(
						match.Groups["classId"].Value,
						CultureInfo.InvariantCulture);
				var fileId = long.Parse(
						match.Groups["fileId"].Value,
						CultureInfo.InvariantCulture);

				result.Add(new HeaderMatch(
						lineStart,
						classId,
						fileId,
						match.Groups["stripped"].Success));
			}

			if (lineEnd == text.Length)
			{
				break;
			}

			lineStart = lineEnd + 1;
		}

		return result;
	}

	private sealed record HeaderMatch(
			int Start,
			int ClassId,
			long FileId,
			bool IsStripped);
}
