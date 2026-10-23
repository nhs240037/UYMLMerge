using System.Text;

namespace UYMerge.Core;

public sealed class UnityYamlFileWriter
{
	private readonly UnityYamlWriter _writer = new();

	/// <summary>
	/// Unity YAMLを一時ファイルに書き込み、完成後に出力先へ移動する。
	/// 出力先に既存ファイルがある場合は上書きしない。
	/// </summary>
	public async Task SaveAsync(
			string outputPath,
			string preamble,
			ObjectMergeResult result,
			CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
		ArgumentNullException.ThrowIfNull(preamble);
		ArgumentNullException.ThrowIfNull(result);

		// 競合がある場合は、ファイル操作を始める前に拒否する。
		if (!result.CanSave)
		{
			throw new InvalidOperationException(
					"Cannot save Unity YAML while merge conflicts remain.");
		}

		var fullPath = Path.GetFullPath(outputPath);
		var directory = Path.GetDirectoryName(fullPath)
				?? throw new InvalidOperationException(
						"Output directory could not be determined.");

		Directory.CreateDirectory(directory);

		// 既存の出力ファイルは上書きしない。
		if (File.Exists(fullPath))
		{
			throw new IOException(
					$"Output file already exists: {fullPath}");
		}

		var content = _writer.Write(preamble, result);

		// 一時ファイルは出力先と同じディレクトリに作る。
		var tempPath = Path.Combine(
				directory,
				$".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

		try
		{
			await File.WriteAllTextAsync(
					tempPath,
					content,
					new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
					cancellationToken);

			cancellationToken.ThrowIfCancellationRequested();

			// 上書きせず移動する。既存ファイルがあれば失敗する。
			File.Move(tempPath, fullPath);
		}
		finally
		{
			// 保存に失敗した場合、一時ファイルを掃除する。
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
	}
}