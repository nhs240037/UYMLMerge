namespace UYMerge.Core;

public sealed class UnityObjectMerger
{
	public ObjectMergeResult Merge(
			UnityYamlParseResult @base,
			UnityYamlParseResult ours,
			UnityYamlParseResult theirs)
	{
		ArgumentNullException.ThrowIfNull(@base);
		ArgumentNullException.ThrowIfNull(ours);
		ArgumentNullException.ThrowIfNull(theirs);

		var baseMap = ToMap(@base.Documents, "Base");
		var oursMap = ToMap(ours.Documents, "Ours");
		var theirsMap = ToMap(theirs.Documents, "Theirs");

		// 決定的な出力順にするため、Ours → Theirs → Base の順にIDを列挙。
		// Unityの意味的な順序を保証するものではない。
		var order = new List<long>();
		AddIds(order, ours.Documents);
		AddIds(order, theirs.Documents);
		AddIds(order, @base.Documents);

		var output = new List<UnityYamlDocument>();
		var conflicts = new List<MergeConflict>();

		foreach (var id in order)
		{
			baseMap.TryGetValue(id, out var b);
			oursMap.TryGetValue(id, out var o);
			theirsMap.TryGetValue(id, out var t);

			// Baseにない = 新規追加
			if (b is null)
			{
				if (o is null && t is not null)
				{
					output.Add(t);
				}
				else if (t is null && o is not null)
				{
					output.Add(o);
				}
				else if (o is not null && t is not null)
				{
					if (Equivalent(o, t))
						output.Add(o);
					else
						conflicts.Add(new MergeConflict(
								id,
								MergeConflictKind.AddAdd,
								null, o, t,
								$"fileID {id}: 両側で異なるオブジェクトが追加されました。"));
				}

				continue;
			}

			// 両側で削除
			if (o is null && t is null)
				continue;

			// Ours側で削除
			if (o is null)
			{
				if (Equivalent(b, t!))
				{
					// Theirs側は未変更なので削除を採用
					continue;
				}

				conflicts.Add(new MergeConflict(
						id,
						MergeConflictKind.DeleteVsModify,
						b, null, t,
						$"fileID {id}: 一方で削除、もう一方で変更されています。"));

				continue;
			}

			// Theirs側で削除
			if (t is null)
			{
				if (Equivalent(b, o))
				{
					// Ours側は未変更なので削除を採用
					continue;
				}

				conflicts.Add(new MergeConflict(
						id,
						MergeConflictKind.DeleteVsModify,
						b, o, null,
						$"fileID {id}: 一方で変更、もう一方で削除されています。"));

				continue;
			}

			// 同じfileIDのClassIDが変わっている場合は自動統合しない
			if (b.ClassId != o.ClassId ||
					b.ClassId != t.ClassId ||
					o.ClassId != t.ClassId)
			{
				conflicts.Add(new MergeConflict(
						id,
						MergeConflictKind.ObjectTypeMismatch,
						b, o, t,
						$"fileID {id}: ClassID が一致しません。"));

				continue;
			}

			var oursChanged = !Equivalent(b, o);
			var theirsChanged = !Equivalent(b, t);

			if (!oursChanged && !theirsChanged)
			{
				output.Add(o);
			}
			else if (oursChanged && !theirsChanged)
			{
				output.Add(o);
			}
			else if (!oursChanged && theirsChanged)
			{
				output.Add(t);
			}
			else if (Equivalent(o, t))
			{
				// 両側が同じ変更を行った
				output.Add(o);
			}
			else
			{
				conflicts.Add(new MergeConflict(
						id,
						MergeConflictKind.BothModified,
						b, o, t,
						$"fileID {id}: 両側で異なる変更が行われました。"));
			}
		}

		return new ObjectMergeResult(output, conflicts);
	}

	private static Dictionary<long, UnityYamlDocument> ToMap(
			IReadOnlyList<UnityYamlDocument> documents,
			string label)
	{
		var map = new Dictionary<long, UnityYamlDocument>();

		foreach (var document in documents)
		{
			if (!map.TryAdd(document.FileId, document))
			{
				throw new ArgumentException(
						$"{label}: fileID {document.FileId} が重複しています。");
			}
		}

		return map;
	}

	private static bool Equivalent(
			UnityYamlDocument a,
			UnityYamlDocument b)
	{
		return a.ClassId == b.ClassId
				&& a.FileId == b.FileId
				&& a.IsStripped == b.IsStripped
				&& string.Equals(
						a.RawText,
						b.RawText,
						StringComparison.Ordinal);
	}

	private static void AddIds(
			List<long> order,
			IReadOnlyList<UnityYamlDocument> documents)
	{
		foreach (var document in documents)
		{
			if (!order.Contains(document.FileId))
				order.Add(document.FileId);
		}
	}
}