
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UYMerge.App.Models;
using UYMerge.Core;

namespace UYMerge.App.Services;

public sealed class UnityHierarchyParser
{
  private static readonly Regex NameRegex = new(
      @"^\s*m_Name:\s*(?<value>.*?)\s*$",
      RegexOptions.Multiline | RegexOptions.Compiled);

  private static readonly Regex GameObjectRegex = new(
      @"^\s*m_GameObject:\s*\{fileID:\s*(?<id>-?\d+)",
      RegexOptions.Multiline | RegexOptions.Compiled);

  private static readonly Regex FatherRegex = new(
      @"^\s*m_Father:\s*\{fileID:\s*(?<id>-?\d+)",
      RegexOptions.Multiline | RegexOptions.Compiled);

  private static readonly Regex ComponentEntryRegex = new(
      @"^\s*-\s*component:\s*\{fileID:\s*(?<id>-?\d+)",
      RegexOptions.Multiline | RegexOptions.Compiled);

  private static readonly Regex RootTypeRegex = new(
      @"^--- !u!(?<classId>\d+) &-?\d+(?:\s+stripped)?\r?\n(?<type>[A-Za-z0-9_]+):",
      RegexOptions.Compiled);

  public IReadOnlyList<UnityHierarchyNode> Build(
      IReadOnlyList<UnityYamlDocument> documents)
  {
    ArgumentNullException.ThrowIfNull(documents);

    var documentMap = documents
        .GroupBy(document => document.FileId)
        .ToDictionary(group => group.Key, group => group.First());

    var gameObjects = documents
        .Where(document => document.ClassId == 1)
        .Select(document => new GameObjectInfo(
            document,
            ReadName(document.RawText, $"GameObject {document.FileId}")))
        .ToDictionary(info => info.Document.FileId);

    var nodes = gameObjects.ToDictionary(
        pair => pair.Key,
        pair => new UnityHierarchyNode
        {
          FileId = pair.Key,
          Name = pair.Value.Name,
          Icon = "game-object"
        });

    // Transform の fileID から GameObject ID を引けるようにする。
    var transformsByFileId = new Dictionary<long, TransformInfo>();
    var transformsByGameObjectId = new Dictionary<long, TransformInfo>();

    foreach (var document in documents.Where(
                 document => document.ClassId is 4 or 224))
    {
      var gameObjectId = ReadGameObjectId(document.RawText);

      // GameObject への参照がない Transform は階層に使用しない。
      if (gameObjectId == 0 || !gameObjects.ContainsKey(gameObjectId))
        continue;

      var info = new TransformInfo(
          document,
          gameObjectId,
          ReadFatherId(document.RawText));

      transformsByFileId[document.FileId] = info;

      // 通常の Unity シーンでは GameObject ごとに Transform または
      // RectTransform がひとつ存在する。異常な重複は最初のものを使う。
      transformsByGameObjectId.TryAdd(gameObjectId, info);
    }

    // Inspector 用コンポーネント一覧を構築する。
    foreach (var pair in gameObjects)
    {
      var gameObjectId = pair.Key;
      var node = nodes[gameObjectId];

      if (transformsByGameObjectId.TryGetValue(
              gameObjectId, out var transform))
      {
        node.Components.Add(new UnityInspectorComponent
        {
          FileId = transform.Document.FileId,
          ClassId = transform.Document.ClassId,
          TypeName = GetTypeName(transform.Document),
          Icon = GetIcon(transform.Document.ClassId),
          RawText = transform.Document.RawText
        });
      }

      foreach (var componentId in ReadComponentIds(
                   pair.Value.Document.RawText))
      {
        if (!documentMap.TryGetValue(componentId, out var component))
          continue;

        // Transform は上で登録済み。
        if (component.ClassId is 4 or 224)
          continue;

        node.Components.Add(new UnityInspectorComponent
        {
          FileId = component.FileId,
          ClassId = component.ClassId,
          TypeName = GetTypeName(component),
          Icon = GetIcon(component.ClassId),
          RawText = component.RawText
        });
      }
    }

    // Transform の m_Father は親 GameObject ではなく、
    // 親 Transform の fileID を参照する。
    var roots = new List<UnityHierarchyNode>();

    foreach (var pair in nodes)
    {
      var gameObjectId = pair.Key;
      var node = pair.Value;

      if (!transformsByGameObjectId.TryGetValue(
              gameObjectId, out var transform) ||
          transform.ParentTransformFileId == 0)
      {
        roots.Add(node);
        continue;
      }

      if (!transformsByFileId.TryGetValue(
              transform.ParentTransformFileId, out var parentTransform) ||
          !nodes.TryGetValue(parentTransform.GameObjectId, out var parent) ||
          ReferenceEquals(parent, node))
      {
        // 親が見つからない場合はルートとして残す。
        roots.Add(node);
        continue;
      }

      parent.Children.Add(node);
    }

    SortRecursively(roots);
    return roots;
  }

  private static string ReadName(string rawText, string fallback)
  {
    var match = NameRegex.Match(rawText);
    if (!match.Success)
      return fallback;

    var name = match.Groups["value"].Value.Trim();

    // Unity YAML の単純な引用符を取り除く。
    if (name.Length >= 2 &&
        ((name[0] == '"' && name[^1] == '"') ||
         (name[0] == '\'' && name[^1] == '\'')))
    {
      name = name[1..^1];
    }

    return string.IsNullOrWhiteSpace(name) ? fallback : name;
  }

  private static long ReadGameObjectId(string rawText)
  {
    var match = GameObjectRegex.Match(rawText);
    return match.Success &&
           long.TryParse(match.Groups["id"].Value, out var id)
        ? id
        : 0;
  }

  private static long ReadFatherId(string rawText)
  {
    var match = FatherRegex.Match(rawText);
    return match.Success &&
           long.TryParse(match.Groups["id"].Value, out var id)
        ? id
        : 0;
  }

  private static IEnumerable<long> ReadComponentIds(string rawText)
  {
    foreach (Match match in ComponentEntryRegex.Matches(rawText))
    {
      if (long.TryParse(match.Groups["id"].Value, out var id))
        yield return id;
    }
  }

  private static string GetTypeName(UnityYamlDocument document)
  {
    var match = RootTypeRegex.Match(document.RawText);
    if (match.Success)
      return match.Groups["type"].Value;

    return document.ClassId switch
    {
      1 => "GameObject",
      4 => "Transform",
      20 => "Camera",
      23 => "MeshRenderer",
      33 => "MeshFilter",
      54 => "Rigidbody",
      56 => "Collider",
      65 => "BoxCollider",
      108 => "Light",
      114 => "MonoBehaviour",
      224 => "RectTransform",
      _ => $"Class {document.ClassId}"
    };
  }

  private static string GetIcon(int classId) => classId switch
  {
    1 => "game-object",
    4 => "transform",
    20 => "camera",
    23 => "mesh-renderer",
    33 => "mesh-filter",
    54 => "rigidbody",
    56 or 65 => "collider",
    108 => "light",
    114 => "script",
    224 => "transform",
    _ => "component"
  };

  private static void SortRecursively(List<UnityHierarchyNode> nodes)
  {
    nodes.Sort((left, right) =>
        StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

    foreach (var node in nodes)
    {
      var children = node.Children
          .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
          .ToArray();

      node.Children.Clear();

      foreach (var child in children)
        node.Children.Add(child);

      SortRecursively(node.Children.ToList());
    }
  }

  private sealed record GameObjectInfo(
      UnityYamlDocument Document,
      string Name);

  private sealed record TransformInfo(
      UnityYamlDocument Document,
      long GameObjectId,
      long ParentTransformFileId);
}
