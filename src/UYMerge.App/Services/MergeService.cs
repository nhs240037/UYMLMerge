using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UYMerge.Core;

namespace UYMerge.App.Services;

public sealed class MergeService
{
	private readonly UnityYamlParser _parser = new();
	private readonly UnityObjectMerger _merger = new();

	public async Task<MergeServiceResult> MergeAsync(
			string basePath,
			string oursPath,
			string theirsPath,
			CancellationToken cancellationToken = default)
	{
		var baseText = await File.ReadAllTextAsync(
				basePath, cancellationToken);

		var oursText = await File.ReadAllTextAsync(
				oursPath, cancellationToken);

		var theirsText = await File.ReadAllTextAsync(
				theirsPath, cancellationToken);

		var baseResult = _parser.ParseDetailed(
				baseText, allowEmpty: true);

		var oursResult = _parser.ParseDetailed(
				oursText, allowEmpty: true);

		var theirsResult = _parser.ParseDetailed(
				theirsText, allowEmpty: true);

		var mergeResult = _merger.Merge(
				baseResult, oursResult, theirsResult);

		return new MergeServiceResult(
				oursResult.Preamble,
				mergeResult);
	}

	public async Task SaveAsync(
		string outputPath,
		MergeServiceResult result,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(result);

		if (!result.CanSave)
		{
			throw new InvalidOperationException(
					"競合が残っているため保存できません。");
		}

		var writer = new UnityYamlFileWriter();

		await writer.SaveAsync(
				outputPath,
				result.Preamble,
				result.MergeResult,
				cancellationToken);
	}
}

public sealed record MergeServiceResult(
		string Preamble,
		ObjectMergeResult MergeResult)
{
	public bool HasConflicts => MergeResult.HasConflicts;

	public bool CanSave => MergeResult.CanSave;
}