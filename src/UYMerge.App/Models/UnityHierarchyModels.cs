
using System.Collections.ObjectModel;

namespace UYMerge.App.Models;

public enum ChangeKind
{
  None,
  Added,
  Deleted,
  Modified,
  Conflict
}

public sealed class UnityHierarchyNode
{
  public long FileId { get; init; }

  public string Name { get; init; } = string.Empty;

  public string Icon { get; init; } = "game-object";

  public ChangeKind Change { get; set; }

  public ChangeKind DescendantChange { get; set; }

  public bool HasDescendantConflict { get; set; }

  public ObservableCollection<UnityHierarchyNode> Children { get; } = new();

  public ObservableCollection<UnityInspectorComponent> Components { get; } = new();
}

public sealed class UnityInspectorComponent
{
  public long FileId { get; init; }

  public int ClassId { get; init; }

  public string TypeName { get; init; } = string.Empty;

  public string Icon { get; init; } = "component";

  public ChangeKind Change { get; set; }

  public string RawText { get; init; } = string.Empty;
}
