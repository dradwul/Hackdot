using System.Text;

namespace Hackdot.Core.Models;

public sealed class VirtualFile : VirtualNode
{
    public VirtualFile(string name, string content, string owner, int mode, DateTime modified)
        : base(name, owner, mode, modified)
    {
        Content = content;
    }

    public string Content { get; set; }

    public override bool IsDirectory => false;

    public override long Size => Encoding.UTF8.GetByteCount(Content);
}
