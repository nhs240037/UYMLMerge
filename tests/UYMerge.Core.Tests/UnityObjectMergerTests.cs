using UYMerge;
using Xunit;

namespace UYMerge.Core.Tests;

public sealed class UnityObjectMergerTests
{
	private readonly UnityYamlParser _parser = new();
	private readonly UnityObjectMerger _merger = new();

	private UnityYamlParseResult Parse(string objects)
	{
		const string preamble =
				"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

		return _parser.ParseDetailed(
				preamble + objects,
				allowEmpty: true);
	}

	[Fact]
	public void Merge_OnlyOursChanged_UsesOurs()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");
		var o = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Ours\n");
		var t = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");

		var result = _merger.Merge(b, o, t);

		Assert.False(result.HasConflicts);
		Assert.Contains("m_Name: Ours", Assert.Single(result.Documents).RawText);
	}

	[Fact]
	public void Merge_OnlyTheirsChanged_UsesTheirs()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");
		var o = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");
		var t = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Theirs\n");

		var result = _merger.Merge(b, o, t);

		Assert.False(result.HasConflicts);
		Assert.Contains("m_Name: Theirs", Assert.Single(result.Documents).RawText);
	}

	[Fact]
	public void Merge_SameChangeOnBothSides_Succeeds()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");
		var o = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Shared\n");
		var t = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Shared\n");

		var result = _merger.Merge(b, o, t);

		Assert.False(result.HasConflicts);
		Assert.Contains("m_Name: Shared", Assert.Single(result.Documents).RawText);
	}

	[Fact]
	public void Merge_DifferentChangesOnBothSides_ReportsConflict()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");
		var o = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Ours\n");
		var t = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Theirs\n");

		var result = _merger.Merge(b, o, t);

		Assert.True(result.HasConflicts);
		Assert.Equal(
				MergeConflictKind.BothModified,
				Assert.Single(result.Conflicts).Kind);
	}

	[Fact]
	public void Merge_OneSideDeletesOtherUnchanged_DeletesObject()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");
		var o = Parse("");
		var t = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");

		var result = _merger.Merge(b, o, t);

		Assert.False(result.HasConflicts);
		Assert.Empty(result.Documents);
	}

	[Fact]
	public void Merge_OneSideDeletesOtherModifies_ReportsConflict()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Base\n");
		var o = Parse("");
		var t = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Changed\n");

		var result = _merger.Merge(b, o, t);

		Assert.Equal(
				MergeConflictKind.DeleteVsModify,
				Assert.Single(result.Conflicts).Kind);
	}

	[Fact]
	public void Merge_OneSideAddsObject_IncludesIt()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Existing\n");
		var o = Parse(
				"--- !u!1 &1\nGameObject:\n  m_Name: Existing\n" +
				"--- !u!4 &2\nTransform:\n  m_GameObject: {fileID: 1}\n");
		var t = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Existing\n");

		var result = _merger.Merge(b, o, t);

		Assert.False(result.HasConflicts);
		Assert.Contains(result.Documents, d => d.FileId == 2);
	}

	[Fact]
	public void Merge_DifferentObjectsAddedWithSameId_ReportsConflict()
	{
		var b = Parse("--- !u!1 &1\nGameObject:\n  m_Name: Existing\n");
		var o = Parse(
				"--- !u!1 &1\nGameObject:\n  m_Name: Existing\n" +
				"--- !u!4 &2\nTransform:\n  m_LocalPosition: {x: 1, y: 0, z: 0}\n");
		var t = Parse(
				"--- !u!1 &1\nGameObject:\n  m_Name: Existing\n" +
				"--- !u!4 &2\nTransform:\n  m_LocalPosition: {x: 2, y: 0, z: 0}\n");

		var result = _merger.Merge(b, o, t);

		Assert.Equal(
				MergeConflictKind.AddAdd,
				Assert.Single(result.Conflicts).Kind);
	}
	[Fact]
	public void Merge_WithConflicts_CannotSave()
	{
		var baseResult = Parse("""
        --- !u!1 &100
        GameObject:
          m_Name: Base
        """);

		var oursResult = Parse("""
        --- !u!1 &100
        GameObject:
          m_Name: Ours
        """);

		var theirsResult = Parse("""
        --- !u!1 &100
        GameObject:
          m_Name: Theirs
        """);

		var result = _merger.Merge(
				baseResult,
				oursResult,
				theirsResult);

		Assert.True(result.HasConflicts);
		Assert.False(result.CanSave);
	}

	[Fact]
	public void Merge_WithoutConflicts_CanSave()
	{
		var baseResult = Parse("""
        --- !u!1 &100
        GameObject:
          m_Name: Base
        """);

		var oursResult = Parse("""
        --- !u!1 &100
        GameObject:
          m_Name: Ours
        """);

		var theirsResult = Parse("""
        --- !u!1 &100
        GameObject:
          m_Name: Base
        """);

		var result = _merger.Merge(
				baseResult,
				oursResult,
				theirsResult);

		Assert.False(result.HasConflicts);
		Assert.True(result.CanSave);
	}
}