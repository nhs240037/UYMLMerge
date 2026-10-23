using System.Text;
using Xunit;
using UYMerge.Core;

namespace UYMerge.Core.Tests;

public sealed class UnityYamlFileWriterTests
{
	[Fact]
	public async Task SaveAsync_SavesUtf8WithoutBom()
	{
		var directory = Path.Combine(
				Path.GetTempPath(),
				$"UYMergeTests_{Guid.NewGuid():N}");

		var outputPath = Path.Combine(directory, "result.unity");

		try
		{
			const string preamble =
					"%YAML 1.1\r\n" +
					"%TAG !u! tag:unity3d.com,2011:\r\n";

			const string rawText =
					"--- !u!1 &100\r\n" +
					"GameObject:\r\n" +
					"  m_Name: テスト\r\n";

			var document = new UnityYamlDocument(
					ClassId: 1,
					FileId: 100,
					RawText: rawText);

			var result = new ObjectMergeResult(
					new[] { document },
					Array.Empty<MergeConflict>());

			var fileWriter = new UnityYamlFileWriter();

			await fileWriter.SaveAsync(
					outputPath,
					preamble,
					result);

			var bytes = await File.ReadAllBytesAsync(outputPath);

			Assert.False(
					bytes.Length >= 3 &&
					bytes[0] == 0xEF &&
					bytes[1] == 0xBB &&
					bytes[2] == 0xBF);

			Assert.Equal(
					preamble + rawText,
					await File.ReadAllTextAsync(
							outputPath,
							Encoding.UTF8));
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
	}

	[Fact]
	public async Task SaveAsync_WithConflicts_DoesNotCreateFile()
	{
		var directory = Path.Combine(
				Path.GetTempPath(),
				$"UYMergeTests_{Guid.NewGuid():N}");

		var outputPath = Path.Combine(directory, "result.unity");

		try
		{
			var conflict = new MergeConflict(
					FileId: 100,
					Kind: MergeConflictKind.BothModified,
					Base: null,
					Ours: null,
					Theirs: null,
					Message: "Test conflict");

			var result = new ObjectMergeResult(
					Array.Empty<UnityYamlDocument>(),
					new[] { conflict });

			var fileWriter = new UnityYamlFileWriter();

			await Assert.ThrowsAsync<InvalidOperationException>(
					() => fileWriter.SaveAsync(
							outputPath,
							string.Empty,
							result));

			Assert.False(File.Exists(outputPath));
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
	}

	[Fact]
	public async Task SaveAsync_ExistingFile_DoesNotOverwrite()
	{
		var directory = Path.Combine(
				Path.GetTempPath(),
				$"UYMergeTests_{Guid.NewGuid():N}");

		var outputPath = Path.Combine(directory, "result.unity");

		try
		{
			Directory.CreateDirectory(directory);
			await File.WriteAllTextAsync(outputPath, "Existing content");

			var result = new ObjectMergeResult(
					Array.Empty<UnityYamlDocument>(),
					Array.Empty<MergeConflict>());

			var fileWriter = new UnityYamlFileWriter();

			await Assert.ThrowsAsync<IOException>(
					() => fileWriter.SaveAsync(
							outputPath,
							string.Empty,
							result));

			Assert.Equal(
					"Existing content",
					await File.ReadAllTextAsync(outputPath));
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
	}
}