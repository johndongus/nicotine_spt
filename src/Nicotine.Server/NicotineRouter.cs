using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace Nicotine.Server;

[Injectable]
public sealed class NicotineRouter(JsonUtil jsonUtil, NicotineStore store, NicotineContext context) : StaticRouter(jsonUtil,
[
    new RouteAction<EmptyRequestData>("/nicotine/state", (url, info, sessionId, output, token) =>
        new ValueTask<string>(JsonSerializer.Serialize(new { state = store.Load(sessionId.ToString()), config = context.Config }, NicotineJson.Write))),
    new RouteAction<NicotineSaveRequest>("/nicotine/save", (url, info, sessionId, output, token) =>
        new ValueTask<string>(JsonSerializer.Serialize(new { ok = true, state = store.Save(sessionId.ToString(), info) }, NicotineJson.Write))),
]);
