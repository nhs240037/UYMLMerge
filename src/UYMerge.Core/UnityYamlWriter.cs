using System.Text;

namespace UYMerge.Core;

public sealed class UnityYamlWriter
{
	/// <summary>
	/// マージ結果をUnity YAML文字列に書き戻す。
	/// 競合が残っている場合は出力しない。
	/// </summary>
	public string Write(
			string preamble,
			ObjectMergeResult result)
	{
		ArgumentNullException.ThrowIfNull(preamble);
		ArgumentNullException.ThrowIfNull(result);

		if (!result.CanSave)
		{
			throw new InvalidOperationException(
					"Cannot write Unity YAML while merge conflicts remain.");
		}

		var builder = new StringBuilder();
		builder.Append(preamble);

		foreach (var document in result.Documents)
		{
			builder.Append(document.RawText);
		}

		return builder.ToString();
	}
}