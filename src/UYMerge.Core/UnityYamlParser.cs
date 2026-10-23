using System.Globalization;
using System.Text.RegularExpressions;

namespace UYMerge.Core;

public sealed class UnityYamlParser
{
  private static readonly Regex HeaderRegex = new(
          @"^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)(?:\s+stripped)?\s*$",
          RegexOptions.Multiline | RegexOptions.Compiled);

  public IReadOnlyList<UnityYamlDocument> Parse(string text)
  {
    ArgumentNullException.ThrowIfNull(text);

    var headers = HeaderRegex.Matches(text);

    if (headers.Count == 0)
    {
      throw new FormatException(
              "Unity YAML のオブジェクトヘッダーが見つかりません。");
    }

    var documents = new List<UnityYamlDocument>(
            headers.Count);

    foreach (Match header in headers)
    {
      var classId = int.Parse(
              header.Groups["classId"].Value,
              CultureInfo.InvariantCulture);

      var fileId = long.Parse(
              header.Groups["fileId"].Value,
              CultureInfo.InvariantCulture);

      var start = header.Index;
      var nextIndex = header.Index + header.Length;
      var nextHeaderIndex = text.IndexOf(
              "\n--- !u!",
              nextIndex,
              StringComparison.Ordinal);

      var end = nextHeaderIndex < 0
              ? text.Length
              : nextHeaderIndex + 1;

      documents.Add(new UnityYamlDocument(
              classId,
              fileId,
              text[start..end]));
    }

    var duplicate = documents
            .GroupBy(document => document.FileId)
            .FirstOrDefault(group => group.Count() > 1);

    if (duplicate is not null)
    {
      throw new FormatException(
              $"fileID {duplicate.Key} が重複しています。");
    }

    return documents;
  }
}