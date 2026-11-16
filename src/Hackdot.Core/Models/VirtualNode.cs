using Hackdot.Core.Services;

namespace Hackdot.Core.Models;

/// <summary>A file or directory in the in-memory file system.</summary>
public abstract class VirtualNode
{
    protected VirtualNode(string name, string owner, int mode, DateTime modified)
    {
        Name = name;
        Owner = owner;
        Mode = mode;
        Modified = modified;
    }

    public string Name { get; }

    public VirtualDirectory? Parent { get; internal set; }

    /// <summary>User name of the owner. The owner's group has the same name (like on most Linux systems).</summary>
    public string Owner { get; set; }

    /// <summary>Permission bits, e.g. <see cref="FilePermissions.DefaultFile"/>.</summary>
    public int Mode { get; set; }

    public DateTime Modified { get; set; }

    public abstract bool IsDirectory { get; }

    public abstract long Size { get; }

    /// <summary>Names starting with a dot are hidden unless you ask for them (<c>ls -a</c>).</summary>
    public bool IsHidden => VirtualPath.IsHidden(Name);

    /// <summary>Absolute path, e.g. <c>/var/www/.secret_flag</c>.</summary>
    public string Path
    {
        get
        {
            if (Parent is null)
            {
                return VirtualPath.Root;
            }

            var parts = new Stack<string>();

            for (var node = this; node.Parent is not null; node = node.Parent)
            {
                parts.Push(node.Name);
            }

            return VirtualPath.Root + string.Join(VirtualPath.Separator, parts);
        }
    }

    /// <summary>The permission column from <c>ls -l</c>, e.g. <c>drwxr-xr-x</c>.</summary>
    public string PermissionString => (IsDirectory ? "d" : "-") + FilePermissions.ToSymbolic(Mode);
}
