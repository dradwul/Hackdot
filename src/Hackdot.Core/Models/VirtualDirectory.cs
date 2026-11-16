namespace Hackdot.Core.Models;

public sealed class VirtualDirectory : VirtualNode
{
    private readonly Dictionary<string, VirtualNode> _children = new(StringComparer.Ordinal);

    public VirtualDirectory(string name, string owner, int mode, DateTime modified)
        : base(name, owner, mode, modified)
    {
    }

    public override bool IsDirectory => true;

    /// <summary>Directories report the classic 4096 bytes in <c>ls -l</c>.</summary>
    public override long Size => 4096;

    public IReadOnlyCollection<VirtualNode> Children => _children.Values;

    public VirtualNode? GetChild(string name) => _children.GetValueOrDefault(name);

    /// <summary>
    /// Children sorted like <c>ls</c>: case-insensitive, ignoring a leading dot,
    /// so <c>.bashrc</c> ends up next to <c>bin</c>.
    /// </summary>
    public IEnumerable<VirtualNode> SortedChildren() =>
        _children.Values
            .OrderBy(child => child.Name.TrimStart('.'), StringComparer.OrdinalIgnoreCase)
            .ThenBy(child => child.Name, StringComparer.Ordinal);

    /// <summary>Adds a child, replacing any existing child with the same name.</summary>
    internal T SetChild<T>(T node) where T : VirtualNode
    {
        if (_children.TryGetValue(node.Name, out var existing))
        {
            existing.Parent = null;
        }

        node.Parent = this;
        _children[node.Name] = node;
        return node;
    }
}
