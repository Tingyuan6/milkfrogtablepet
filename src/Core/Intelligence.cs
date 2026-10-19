using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Naifrog.Core.Intelligence
{
    // Reserved contracts only. No network requests or tool execution in this release.
    public interface IConversationProvider { Task<string> ReplyAsync(string input, CancellationToken cancellation); }
    public interface IMemoryStore { Task<IDictionary<string,string>> ReadAsync(); Task ForgetAsync(string key); }
    public interface IToolRegistry { IEnumerable<string> Names { get; } }
    public interface IPetIntentBridge { void RequestIntent(string intent); }
}
