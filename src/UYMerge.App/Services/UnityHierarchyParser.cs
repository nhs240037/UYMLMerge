
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

  private static readonly Regex ComponentRegex = new(
      @"component:\s*\{fileID:\s*(?<id>-?\d+)",
      RegexOptions.Compiled);

  private static readonly Regex ComponentEntryRegex = new(
      @"^\s*-\s*component:\s*\{fileID:\s*(?<id>-?\d+)",
      RegexOptions.Multiline | RegexOptions.Compiled);

  private static readonly Regex RootTypeRegex = new(
      @"^--- !u!\d+ &-?\d+(?:\s+stripped)?\r?\n(?<type>[A-Za-z0-9_]+):",
      RegexOptions.Compiled);

  public IReadOnlyList<UnityHierarchyNode> Build(
      IReadOnlyList<UnityYamlDocument> documents)
  {
    ArgumentNullException.ThrowIfNull(documents);

    var documentMap = documents.ToDictionary(x => x.FileId);

    var gameObjects = documents
        .Where(x => x.ClassId == 1)
        .Select(x => new GameObjectInfo(
            x,
            ReadName(x.RawText, $"GameObject {x.FileId}")))
        .ToDictionary(x => x.Document.FileId);

    var transforms = documents
        .Where(x => x.ClassId is 4 or 224)
        .ToDictionary(
            x => ReadGameObjectId(x.RawText),
            x => new TransformInfo(
                x,
                ReadFatherId(x.RawText)))
        .Where(x => x.Key != 0)
        .ToDictionary(x => x.Key, x => x.Value);

    var nodes = gameObjects.ToDictionary(
        pair => pair.Key,
        pair => new UnityHierarchyNode
        {
          FileId = pair.Key,
          Name = pair.Value.Name,
          Icon = "game-object"
        });

    foreach (var pair in gameObjects)
    {
      var gameObjectId = pair.Key;
      var node = nodes[gameObjectId];

      if (!transforms.TryGetValue(gameObjectId, out var transform))
      {
        continue;
      }

      transform.GameObjectId = gameObjectId;

      node.Components.Add(new UnityInspectorComponent
      {
        FileId = transform.Document.FileId,
        ClassId = transform.Document.ClassId,
        TypeName = GetTypeName(transform.Document),
        Icon = GetIcon(transform.Document.ClassId),
        RawText = transform.Document.RawText
      });

      foreach (var componentId in ReadComponentIds(
                   pair.Value.Document.RawText))
      {
        if (!documentMap.TryGetValue(componentId, out var component))
        {
          continue;
        }

        if (component.FileId == transform.Document.FileId)
        {
          continue;
        }

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

    var roots = new List<UnityHierarchyNode>();

    foreach (var pair in nodes)
    {
      var gameObjectId = pair.Key;
      var node = pair.Value;

      if (!transforms.TryGetValue(gameObjectId, out var transform))
      {
        roots.Add(node);
        continue;
      }

      var parentTransformId = transform.ParentId;

      if (parentTransformId == 0)
      {
        roots.Add(node);
        continue;
      }

      var parentGameObjectId = FindGameObjectIdForTransform(
          parentTransformId,
          transforms);

      if (parentGameObjectId == 0 ||
          !nodes.TryGetValue(parentGameObjectId, out var parent))
      {
        roots.Add(node);
        continue;
      }

      parent.Children.Add(node);
    }

    SortRecursively(roots);

    return roots;
  }

  private static long FindGameObjectIdForTransform(
      long transformId,
      IReadOnlyDictionary<long, TransformInfo> transforms)
  {
    foreach (var pair in transforms)
    {
      if (pair.Value.Document.FileId == transformId)
      {
        return pair.Key;
      }
    }

    return 0;
  }

  private static string ReadName(string rawText, string fallback)
  {
    var match = NameRegex.Match(rawText);

    if (!match.Success)
    {
      return fallback;
    }

    var name = match.Groups["value"].Value.Trim();

    if (name.Length >= 2 &&
        name[0] == '"' &&
        name[^1] == '"')
    {
      name = name[1..^1]
          .Replace("\\\"", "\"", StringComparison.Ordinal)
          .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    return string.IsNullOrWhiteSpace(name) ? fallback : name;
  }

  private static long ReadGameObjectId(string rawText)
  {
    var match = GameObjectRegex.Match(rawText);
    return match.Success
        ? ParseId(match.Groups["id"].Value)
        : 0;
  }

  private static long ReadFatherId(string rawText)
  {
    var match = FatherRegex.Match(rawText);
    return match.Success
        ? ParseId(match.Groups["id"].Value)
        : 0;
  }

  private static IEnumerable<long> ReadComponentIds(string rawText)
  {
    foreach (Match match in ComponentEntryRegex.Matches(rawText))
    {
      yield return ParseId(match.Groups["id"].Value);
    }
  }

  private static long ParseId(string value) =>
      long.TryParse(value, out var id) ? id : 0;

  private static string GetTypeName(UnityYamlDocument document)
  {
    var match = RootTypeRegex.Match(document.RawText);

    if (match.Success)
    {
      return match.Groups["type"].Value;
    }

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

  private static string GetIcon(int classId) =>
      classId switch
      {
        1 => "game-object",
        4 or 224 => "transform",
        20 => "camera",
        108 => "light",
        23 => "mesh-renderer",
        33 => "mesh-filter",
        54 => "rigidbody",
        BoxColliderClassId => "collider",
        114 => "script",
        _ => "component"
      };

  private static void SortRecursively(List<UnityHierarchyNode> nodes)
  {
    foreach (var node in nodes)
    {
      SortRecursively(node.Children.ToList());
    }
  }

  private sealed class GameObjectInfo(
      UnityYamlDocument document,
      string name)
  {
    public UnityYamlDocument Document { get; } = document;
    public string Name { get; } = name;
  }

  private sealed class TransformInfo(
      UnityYamlDocument document,
      long parentId)
  {
    public UnityYamlDocument Document { get; } = document;
    public long ParentId { get; } = parentId;
    public long GameObjectId { get; set; }
  }

  private const int BoxColliderClassId = 65;
}
