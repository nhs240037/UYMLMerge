
using System;
using System.Collections.Generic;
using System.Linq;
using UYMerge.App.Models;

namespace UYMerge.App.Services;

public sealed class UnityHierarchyDiffService
{
  public void Compare(
      IReadOnlyList<UnityHierarchyNode> baseNodes,
      IReadOnlyList<UnityHierarchyNode> oursNodes,
      IReadOnlyList<UnityHierarchyNode> theirsNodes)
  {
    ArgumentNullException.ThrowIfNull(baseNodes);
    ArgumentNullException.ThrowIfNull(oursNodes);
    ArgumentNullException.ThrowIfNull(theirsNodes);

    var baseMap = Flatten(baseNodes);
    var oursMap = Flatten(oursNodes);
    var theirsMap = Flatten(theirsNodes);

    var allIds = baseMap.Keys
        .Union(oursMap.Keys)
        .Union(theirsMap.Keys)
        .ToArray();

    foreach (var id in allIds)
    {
      baseMap.TryGetValue(id, out var b);
      oursMap.TryGetValue(id, out var o);
      theirsMap.TryGetValue(id, out var t);

      CompareObject(b, o, t);
      CompareComponents(b, o, t);
    }

    AggregateDescendants(baseNodes);
    AggregateDescendants(oursNodes);
    AggregateDescendants(theirsNodes);
  }

  private static void CompareObject(
      UnityHierarchyNode? b,
      UnityHierarchyNode? o,
      UnityHierarchyNode? t)
  {
    if (b is null)
    {
      CompareAddedObject(o, t);
      return;
    }

    var baseSignature = Signature(b);

    // BASE にあったオブジェクトを両側で削除。
    if (o is null && t is null)
    {
      b.Change = ChangeKind.Deleted;
      return;
    }

    // OURS 側だけで削除。
    if (o is null)
    {
      if (Equivalent(baseSignature, Signature(t)))
      {
        b.Change = ChangeKind.Deleted;
        t!.Change = ChangeKind.None;
      }
      else
      {
        // 削除と変更が衝突。
        b.Change = ChangeKind.Conflict;
        t!.Change = ChangeKind.Conflict;
      }

      return;
    }

    // THEIRS 側だけで削除。
    if (t is null)
    {
      if (Equivalent(baseSignature, Signature(o)))
      {
        b.Change = ChangeKind.Deleted;
        o.Change = ChangeKind.None;
      }
      else
      {
        // 変更と削除が衝突。
        b.Change = ChangeKind.Conflict;
        o.Change = ChangeKind.Conflict;
      }

      return;
    }

    var oursSignature = Signature(o);
    var theirsSignature = Signature(t);

    var oursChanged = !Equivalent(baseSignature, oursSignature);
    var theirsChanged = !Equivalent(baseSignature, theirsSignature);

    var conflict =
        oursChanged &&
        theirsChanged &&
        !Equivalent(oursSignature, theirsSignature);

    if (conflict)
    {
      b.Change = ChangeKind.Conflict;
      o.Change = ChangeKind.Conflict;
      t.Change = ChangeKind.Conflict;
      return;
    }

    // BASE は比較元なので、通常の変更マーカーを付けない。
    b.Change = ChangeKind.None;

    o.Change = oursChanged
        ? ChangeKind.Modified
        : ChangeKind.None;

    t.Change = theirsChanged
        ? ChangeKind.Modified
        : ChangeKind.None;
  }

  private static void CompareAddedObject(
      UnityHierarchyNode? o,
      UnityHierarchyNode? t)
  {
    if (o is null && t is null)
      return;

    if (o is null)
    {
      t!.Change = ChangeKind.Added;
      return;
    }

    if (t is null)
    {
      o.Change = ChangeKind.Added;
      return;
    }

    // 両側で同じ FileID のオブジェクトが追加された。
    if (Equivalent(Signature(o), Signature(t)))
    {
      o.Change = ChangeKind.Added;
      t.Change = ChangeKind.Added;
    }
    else
    {
      // 同じ FileID だが追加内容が異なる。
      o.Change = ChangeKind.Conflict;
      t.Change = ChangeKind.Conflict;
    }
  }

  private static void CompareComponents(
      UnityHierarchyNode? b,
      UnityHierarchyNode? o,
      UnityHierarchyNode? t)
  {
    var baseMap = ComponentsById(b);
    var oursMap = ComponentsById(o);
    var theirsMap = ComponentsById(t);

    var allIds = baseMap.Keys
        .Union(oursMap.Keys)
        .Union(theirsMap.Keys)
        .ToArray();

    foreach (var id in allIds)
    {
      baseMap.TryGetValue(id, out var bc);
      oursMap.TryGetValue(id, out var oc);
      theirsMap.TryGetValue(id, out var tc);

      CompareComponent(bc, oc, tc);
    }
  }

  private static void CompareComponent(
      UnityInspectorComponent? b,
      UnityInspectorComponent? o,
      UnityInspectorComponent? t)
  {
    if (b is null)
    {
      CompareAddedComponent(o, t);
      return;
    }

    var baseSignature = ComponentSignature(b);

    if (o is null && t is null)
    {
      b.Change = ChangeKind.Deleted;
      return;
    }

    if (o is null)
    {
      if (Equivalent(baseSignature, ComponentSignature(t)))
      {
        b.Change = ChangeKind.Deleted;
        t!.Change = ChangeKind.None;
      }
      else
      {
        b.Change = ChangeKind.Conflict;
        t!.Change = ChangeKind.Conflict;
      }

      return;
    }

    if (t is null)
    {
      if (Equivalent(baseSignature, ComponentSignature(o)))
      {
        b.Change = ChangeKind.Deleted;
        o.Change = ChangeKind.None;
      }
      else
      {
        b.Change = ChangeKind.Conflict;
        o.Change = ChangeKind.Conflict;
      }

      return;
    }

    var oursSignature = ComponentSignature(o);
    var theirsSignature = ComponentSignature(t);

    var oursChanged = !Equivalent(baseSignature, oursSignature);
    var theirsChanged = !Equivalent(baseSignature, theirsSignature);

    var conflict =
        oursChanged &&
        theirsChanged &&
        !Equivalent(oursSignature, theirsSignature);

    if (conflict)
    {
      b.Change = ChangeKind.Conflict;
      o.Change = ChangeKind.Conflict;
      t.Change = ChangeKind.Conflict;
      return;
    }

    b.Change = ChangeKind.None;

    o.Change = oursChanged
        ? ChangeKind.Modified
        : ChangeKind.None;

    t.Change = theirsChanged
        ? ChangeKind.Modified
        : ChangeKind.None;
  }

  private static void CompareAddedComponent(
      UnityInspectorComponent? o,
      UnityInspectorComponent? t)
  {
    if (o is null && t is null)
      return;

    if (o is null)
    {
      t!.Change = ChangeKind.Added;
      return;
    }

    if (t is null)
    {
      o.Change = ChangeKind.Added;
      return;
    }

    if (Equivalent(ComponentSignature(o), ComponentSignature(t)))
    {
      o.Change = ChangeKind.Added;
      t.Change = ChangeKind.Added;
    }
    else
    {
      o.Change = ChangeKind.Conflict;
      t.Change = ChangeKind.Conflict;
    }
  }

  private static Dictionary<long, UnityHierarchyNode> Flatten(
      IReadOnlyList<UnityHierarchyNode> roots)
  {
    var result = new Dictionary<long, UnityHierarchyNode>();
    var stack = new Stack<UnityHierarchyNode>(
        roots.Reverse());

    while (stack.Count > 0)
    {
      var node = stack.Pop();

      if (!result.TryAdd(node.FileId, node))
      {
        throw new InvalidOperationException(
            $"Hierarchy 内で FileID {node.FileId} が重複しています。");
      }

      foreach (var child in node.Children.Reverse())
        stack.Push(child);
    }

    return result;
  }

  private static Dictionary<long, UnityInspectorComponent> ComponentsById(
      UnityHierarchyNode? node)
  {
    if (node is null)
      return new Dictionary<long, UnityInspectorComponent>();

    var result = new Dictionary<long, UnityInspectorComponent>();

    foreach (var component in node.Components)
    {
      if (!result.TryAdd(component.FileId, component))
      {
        throw new InvalidOperationException(
            $"GameObject {node.FileId} 内で " +
            $"Component FileID {component.FileId} が重複しています。");
      }
    }

    return result;
  }

  private static string Signature(UnityHierarchyNode? node)
  {
    if (node is null)
      return "<missing>";

    var components = node.Components
        .OrderBy(component => component.FileId)
        .Select(component =>
            $"{component.FileId}:{ComponentSignature(component)}");

    return $"{node.Name}\n{string.Join("\n", components)}";
  }

  private static string ComponentSignature(
      UnityInspectorComponent? component)
  {
    if (component is null)
      return "<missing>";

    return $"{component.ClassId}\n{component.RawText}";
  }

  private static bool Equivalent(string? left, string? right) =>
      string.Equals(left, right, StringComparison.Ordinal);

  private static void AggregateDescendants(
      IReadOnlyList<UnityHierarchyNode> roots)
  {
    foreach (var root in roots)
      AggregateNode(root);
  }

  private static ChangeKind AggregateNode(UnityHierarchyNode node)
  {
    var kinds = new HashSet<ChangeKind>();
    var hasDescendantConflict = false;

    foreach (var child in node.Children)
    {
      var childAggregate = AggregateNode(child);

      // 子オブジェクト自身の変更。
      if (child.Change != ChangeKind.None)
        kinds.Add(child.Change);

      // 子孫全体の変更。
      if (childAggregate != ChangeKind.None)
        kinds.Add(childAggregate);

      hasDescendantConflict |=
          child.Change == ChangeKind.Conflict ||
          child.HasDescendantConflict;
    }

    // コンポーネントの変更もオブジェクト行の集計に反映する。
    foreach (var component in node.Components)
    {
      if (component.Change != ChangeKind.None)
        kinds.Add(component.Change);

      hasDescendantConflict |=
          component.Change == ChangeKind.Conflict;
    }

    node.HasDescendantConflict = hasDescendantConflict;

    // 自分自身に直接変更がある場合、子孫の集計で上書きしない。
    if (node.Change != ChangeKind.None)
    {
      node.DescendantChange = ChangeKind.None;
      return ChangeKind.None;
    }

    node.DescendantChange = kinds.Count switch
    {
      0 => ChangeKind.None,
      1 => kinds.First(),
      _ => ChangeKind.Modified
    };

    return node.DescendantChange;
  }
}
