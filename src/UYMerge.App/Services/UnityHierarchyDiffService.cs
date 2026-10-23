
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

      var baseSignature = Signature(b);
      var oursSignature = Signature(o);
      var theirsSignature = Signature(t);

      var oursChanged = !Equivalent(baseSignature, oursSignature);
      var theirsChanged = !Equivalent(baseSignature, theirsSignature);

      var conflict = oursChanged && theirsChanged &&
                     !Equivalent(oursSignature, theirsSignature);

      SetObjectChange(b, o, t, conflict);
      CompareComponents(b, o, t);
    }

    AggregateDescendants(baseNodes);
    AggregateDescendants(oursNodes);
    AggregateDescendants(theirsNodes);
  }

  private static void SetObjectChange(
      UnityHierarchyNode? b,
      UnityHierarchyNode? o,
      UnityHierarchyNode? t,
      bool conflict)
  {
    if (conflict)
    {
      if (b is not null) b.Change = ChangeKind.Conflict;
      if (o is not null) o.Change = ChangeKind.Conflict;
      if (t is not null) t.Change = ChangeKind.Conflict;
      return;
    }

    // Base にだけ存在する場合、両ブランチで削除された。
    if (b is not null && o is null && t is null)
    {
      b.Change = ChangeKind.Deleted;
      return;
    }

    // 片方だけが削除し、もう片方が Base と同じなら削除。
    if (b is not null && o is null)
    {
      b.Change = ChangeKind.Deleted;
      if (t is not null) t.Change = ChangeKind.None;
      return;
    }

    if (b is not null && t is null)
    {
      b.Change = ChangeKind.Deleted;
      if (o is not null) o.Change = ChangeKind.None;
      return;
    }

    if (b is null)
    {
      if (o is not null) o.Change = ChangeKind.Added;
      if (t is not null) t.Change = ChangeKind.Added;

      // 両ブランチで同じ ID の要素を追加していても、
      // 内容が異なれば競合として扱う。
      if (o is not null && t is not null &&
          !Equivalent(Signature(o), Signature(t)))
      {
        o.Change = ChangeKind.Conflict;
        t.Change = ChangeKind.Conflict;
      }

      return;
    }

    if (o is not null)
      o.Change = Equivalent(Signature(b), Signature(o))
          ? ChangeKind.None
          : ChangeKind.Modified;

    if (t is not null)
      t.Change = Equivalent(Signature(b), Signature(t))
          ? ChangeKind.None
          : ChangeKind.Modified;

    // Base の行には、ブランチの変更を直接マーカーとして付けない。
    b.Change = ChangeKind.None;
  }

  private static void CompareComponents(
      UnityHierarchyNode? b,
      UnityHierarchyNode? o,
      UnityHierarchyNode? t)
  {
    var baseMap = ComponentsById(b);
    var oursMap = ComponentsById(o);
    var theirsMap = ComponentsById(t);

    foreach (var id in baseMap.Keys
                 .Union(oursMap.Keys)
                 .Union(theirsMap.Keys))
    {
      baseMap.TryGetValue(id, out var bc);
      oursMap.TryGetValue(id, out var oc);
      theirsMap.TryGetValue(id, out var tc);

      var bs = ComponentSignature(bc);
      var os = ComponentSignature(oc);
      var ts = ComponentSignature(tc);

      var oursChanged = !Equivalent(bs, os);
      var theirsChanged = !Equivalent(bs, ts);

      var conflict = oursChanged && theirsChanged &&
                     !Equivalent(os, ts);

      if (conflict)
      {
        if (bc is not null) bc.Change = ChangeKind.Conflict;
        if (oc is not null) oc.Change = ChangeKind.Conflict;
        if (tc is not null) tc.Change = ChangeKind.Conflict;
        continue;
      }

      if (bc is not null)
      {
        // 削除マーカーは Base に残ったコンポーネントに表示。
        bc.Change = oc is null || tc is null
            ? ChangeKind.Deleted
            : ChangeKind.None;
      }

      if (oc is not null)
      {
        oc.Change = bc is null
            ? ChangeKind.Added
            : oursChanged ? ChangeKind.Modified : ChangeKind.None;
      }

      if (tc is not null)
      {
        tc.Change = bc is null
            ? ChangeKind.Added
            : theirsChanged ? ChangeKind.Modified : ChangeKind.None;
      }
    }
  }

  private static Dictionary<long, UnityHierarchyNode> Flatten(
      IReadOnlyList<UnityHierarchyNode> roots)
  {
    var result = new Dictionary<long, UnityHierarchyNode>();
    var stack = new Stack<UnityHierarchyNode>(roots.Reverse());

    while (stack.Count > 0)
    {
      var node = stack.Pop();
      result.TryAdd(node.FileId, node);

      foreach (var child in node.Children.Reverse())
        stack.Push(child);
    }

    return result;
  }

  private static Dictionary<long, UnityInspectorComponent> ComponentsById(
      UnityHierarchyNode? node)
  {
    return node is null
        ? new Dictionary<long, UnityInspectorComponent>()
        : node.Components
            .GroupBy(component => component.FileId)
            .ToDictionary(group => group.Key, group => group.First());
  }

  private static string? Signature(UnityHierarchyNode? node)
  {
    if (node is null)
      return null;

    var components = node.Components
        .OrderBy(component => component.FileId)
        .Select(component =>
            $"{component.FileId}:{component.ClassId}:{component.RawText}");

    return $"{node.Name}\n{string.Join("\n", components)}";
  }

  private static string? ComponentSignature(
      UnityInspectorComponent? component)
  {
    return component is null
        ? null
        : $"{component.ClassId}\n{component.RawText}";
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
    var hasConflict = false;

    foreach (var child in node.Children)
    {
      var childAggregate = AggregateNode(child);

      if (child.Change != ChangeKind.None)
        kinds.Add(child.Change);

      if (childAggregate != ChangeKind.None)
        kinds.Add(childAggregate);

      hasConflict |= child.Change == ChangeKind.Conflict ||
                     child.HasDescendantConflict;
    }

    foreach (var component in node.Components)
    {
      if (component.Change != ChangeKind.None)
        kinds.Add(component.Change);

      hasConflict |= component.Change == ChangeKind.Conflict;
    }

    node.HasDescendantConflict = hasConflict;

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
