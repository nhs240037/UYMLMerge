using UYMerge.Core;
using Xunit;

namespace UYMerge.Core.Tests;

public sealed class UnityYamlParserTests
{
	private readonly UnityYamlParser _parser = new();

	[Fact]
	public void Parse_OneObject_ExtractsClassIdAndFileId()
	{
		const string input = """
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!1 &100001
            GameObject:
              m_Name: Player
            """;

		var result = _parser.Parse(input);

		var document = Assert.Single(result);
		Assert.Equal(1, document.ClassId);
		Assert.Equal(100001L, document.FileId);
		Assert.Contains("m_Name: Player", document.RawText);
	}

	[Fact]
	public void Parse_MultipleObjects_PreservesOrder()
	{
		const string input = """
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!1 &100001
            GameObject:
              m_Name: Player
            --- !u!4 &100002
            Transform:
              m_GameObject: {fileID: 100001}
            """;

		var result = _parser.Parse(input);

		Assert.Equal(2, result.Count);
		Assert.Equal(100001L, result[0].FileId);
		Assert.Equal(1, result[0].ClassId);
		Assert.Equal(100002L, result[1].FileId);
		Assert.Equal(4, result[1].ClassId);
		Assert.Contains("m_Name: Player", result[0].RawText);
		Assert.Contains("m_GameObject: {fileID: 100001}", result[1].RawText);
	}

	[Fact]
	public void Parse_DuplicateFileId_ThrowsFormatException()
	{
		const string input = """
            --- !u!1 &100001
            GameObject:
              m_Name: First
            --- !u!1 &100001
            GameObject:
              m_Name: Duplicate
            """;

		var exception = Assert.Throws<FormatException>(
				() => _parser.Parse(input));

		Assert.Contains("fileID 100001", exception.Message);
	}

	[Theory]
	[InlineData("")]
	[InlineData("GameObject:\n  m_Name: Player")]
	[InlineData("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n")]
	public void Parse_NoObjectHeaders_ThrowsFormatException(string input)
	{
		Assert.Throws<FormatException>(() => _parser.Parse(input));
	}

	[Fact]
	public void Parse_NullInput_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
	}

	[Theory]
	[InlineData("\n")]
	[InlineData("\r\n")]
	public void Parse_SupportsLfAndCrlfLineEndings(string newline)
	{
		var input = string.Join(newline, new[]
		{
						"%YAML 1.1",
						"%TAG !u! tag:unity3d.com,2011:",
						"--- !u!1 &100001",
						"GameObject:",
						"  m_Name: Player",
						"--- !u!4 &100002",
						"Transform:",
						"  m_GameObject: {fileID: 100001}",
						""
				});

		var result = _parser.Parse(input);

		Assert.Equal(2, result.Count);
		Assert.Equal(100001L, result[0].FileId);
		Assert.Equal(100002L, result[1].FileId);
		Assert.Contains("m_Name: Player", result[0].RawText);
		Assert.Contains("m_GameObject: {fileID: 100001}", result[1].RawText);
	}

	[Fact]
	public void Parse_PreservesUnknownFieldsInRawText()
	{
		const string input = """
            --- !u!1 &100001
            GameObject:
              m_Name: Player
              m_CustomFutureField: KeepMe
            """;

		var result = _parser.Parse(input);

		Assert.Contains("m_CustomFutureField: KeepMe", result[0].RawText);
		Assert.Contains(input, result[0].RawText);
	}

	[Fact]
	public void Parse_SupportsNegativeFileId()
	{
		const string input = """
            --- !u!114 &-123456
            MonoBehaviour:
              m_Enabled: 1
            """;

		var result = _parser.Parse(input);

		Assert.Equal(-123456L, Assert.Single(result).FileId);
	}
}

