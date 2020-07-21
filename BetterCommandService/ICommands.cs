using System;
using System.Collections.Generic;
using System.Text;

namespace BetterCommandService
{
    public interface ICommands
    {
        string CommandName { get; }
        string CommandDescription { get; }
        string CommandHelpMessage { get; }
        string[] Prefixes { get; }
        bool RequiresPermission { get; }
    }
}
