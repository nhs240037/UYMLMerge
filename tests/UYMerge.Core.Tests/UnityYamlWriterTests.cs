using Xunit;
using UYMerge.Core;

namespace UYMerge.Core.Tests;

public sealed class UnityYamlWriterTests
{
	private readonly UnityYamlWriter _writer = new();

	[Fact]
	public void Write_PreservesPreambleAndRawText()
	{
		const string preamble =
				"%YAML 1.1\n" +
				"%TAG !u! tag:unity3d.com,2011:\n";

		const string rawText =
				"--- !u!1 &100\n" +
				"GameObject:\n" +
				"  m_Name: TestObject\n";

		var document = new UnityYamlDocument(
				ClassId: 1,
				FileId: 100,
				RawText: rawText);

		var result = new ObjectMergeResult(
				new[] { document },
				Array.Empty<MergeConflict>());

		var output = _writer.Write(preamble, result);

		Assert.Equal(preamble + rawText, output);
	}

	[Fact]
	public void Write_WithConflicts_Throws()
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

		Assert.Throws<InvalidOperationException>(
				() => _writer.Write(string.Empty, result));
	}

	[Fact]
	public void Write_WithNoDocuments_ReturnsPreamble()
	{
		const string preamble =
				"%YAML 1.1\n" +
				"%TAG !u! tag:unity3d.com,2011:\n";

		var result = new ObjectMergeResult(
				Array.Empty<UnityYamlDocument>(),
				Array.Empty<MergeConflict>());

		var output = _writer.Write(preamble, result);

		Assert.Equal(preamble, output);
	}
	[Fact]
	public void Write_ParseThenWrite_PreservesOriginalText()
	{
		const string input =
				"%YAML 1.1\n" +
				"%TAG !u! tag:unity3d.com,2011:\n" +
				"\n" +
				"--- !u!1 &100\n" +
				"GameObject:\n" +
				"  m_Name: First\n" +
				"--- !u!4 &200\n" +
				"Transform:\n" +
				"  m_GameObject: {fileID: 100}\n";

		var parser = new UnityYamlParser();
		var parsed = parser.ParseDetailed(input);

		var result = new ObjectMergeResult(
				parsed.Documents,
				Array.Empty<MergeConflict>());

		var writer = new UnityYamlWriter();
		var output = writer.Write(parsed.Preamble, result);

		Assert.Equal(input, output);
	}
	[Fact]
	public void Write_ParseThenWrite_PreservesCrLf()
	{
		const string input =
				"%YAML 1.1\r\n" +
				"%TAG !u! tag:unity3d.com,2011:\r\n" +
				"\r\n" +
				"--- !u!1 &100\r\n" +
				"GameObject:\r\n" +
				"  m_Name: WindowsObject\r\n";

		var parser = new UnityYamlParser();
		var parsed = parser.ParseDetailed(input);

		var result = new ObjectMergeResult(
				parsed.Documents,
				Array.Empty<MergeConflict>());

		var output = new UnityYamlWriter().Write(
				parsed.Preamble,
				result);

		Assert.Equal(input, output);
	}
}