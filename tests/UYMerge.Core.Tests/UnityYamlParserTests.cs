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

		var document = Assert.Single(_parser.Parse(input));
		Assert.Equal(1, document.ClassId);
		Assert.Equal(100001L, document.FileId);
		Assert.Contains("m_Name: Player", document.RawText);
	}

	[Fact]
	public void Parse_MultipleObjects_PreservesOrderAndExactRawText()
	{
		const string first = "--- !u!1 &100001\nGameObject:\n  m_Name: Player\n";
		const string second = "--- !u!4 &100002\nTransform:\n  m_GameObject: {fileID: 100001}\n";
		var input = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + first + second;

		var result = _parser.Parse(input);

		Assert.Equal(2, result.Count);
		Assert.Equal(100001L, result[0].FileId);
		Assert.Equal(100002L, result[1].FileId);
		Assert.Equal(first, result[0].RawText);
		Assert.Equal(second, result[1].RawText);
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

		var exception = Assert.Throws<FormatException>(() => _parser.Parse(input));
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
	public void Parse_SupportsLfAndCrlfAndPreservesExactObjectText(string newline)
	{
		var first = $"--- !u!1 &100001{newline}GameObject:{newline}  m_Name: Player{newline}";
		var second = $"--- !u!4 &100002{newline}Transform:{newline}  m_GameObject: {{fileID: 100001}}{newline}";
		var input = $"%YAML 1.1{newline}%TAG !u! tag:unity3d.com,2011:{newline}" + first + second;

		var result = _parser.Parse(input);

		Assert.Equal(2, result.Count);
		Assert.Equal(first, result[0].RawText);
		Assert.Equal(second, result[1].RawText);
	}

	[Fact]
	public void Parse_PreservesPreambleOutsideObjectDocuments()
	{
		const string input = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &100001\nGameObject:\n  m_Name: Player\n";

		var result = _parser.ParseDetailed(input);

		Assert.Equal(
				"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n",
				result.Preamble);
		Assert.DoesNotContain("%YAML", Assert.Single(result.Documents).RawText);
		Assert.Equal(input, result.Preamble + string.Concat(
				result.Documents.Select(document => document.RawText)));
	}

	[Fact]
	public void Parse_PreservesUnknownFieldsInRawText()
	{
		const string input = "--- !u!1 &100001\nGameObject:\n  m_Name: Player\n  m_CustomFutureField: KeepMe\n";

		var result = _parser.Parse(input);

		Assert.Equal(input, Assert.Single(result).RawText);
	}

	[Fact]
	public void Parse_SupportsNegativeFileId()
	{
		const string input = "--- !u!114 &-123456\nMonoBehaviour:\n  m_Enabled: 1\n";

		var result = _parser.Parse(input);

		Assert.Equal(-123456L, Assert.Single(result).FileId);
	}

	[Fact]
	public void Parse_RecognizesStrippedHeader()
	{
		const string input = "--- !u!4 &100002 stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: 100001}\n";

		var result = Assert.Single(_parser.Parse(input));

		Assert.True(result.IsStripped);
		Assert.Equal(4, result.ClassId);
		Assert.Equal(100002L, result.FileId);
		Assert.Equal(input, result.RawText);
	}

	[Fact]
	public void Parse_DoesNotTreatIndentedHeaderLikeTextAsObjectBoundary()
	{
		const string input = "--- !u!1 &100001\nGameObject:\n  m_Note: text\n  m_Value: \"example\"\n  m_Description: \"--- !u!4 &999999\"\n";

		var result = Assert.Single(_parser.Parse(input));

		Assert.Equal(100001L, result.FileId);
		Assert.Equal(input, result.RawText);
	}

	[Fact]
	public void Parse_PreservesFinalObjectWithoutTrailingNewline()
	{
		const string input = "--- !u!1 &100001\nGameObject:\n  m_Name: Player";

		Assert.Equal(input, Assert.Single(_parser.Parse(input)).RawText);
	}
}
