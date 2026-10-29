using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenGSServer;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ServerAdminAccount))]
[JsonSerializable(typeof(List<ServerAdminAccount>))]
public partial class AdminJsonSerializerContext : JsonSerializerContext
{
}
