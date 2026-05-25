using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class DbEntity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        public DbEntity()
        {
        }

        public DbEntity(int id, string title, string body)
        {
            Id = id;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["Id"] = Id,
                ["Title"] = Title,
                ["Body"] = Body
            };
        }

        public static DbEntity FromJson(JObject json)
        {
            if (json == null)
            {
                return new DbEntity();
            }

            return new DbEntity
            {
                Id = json["Id"]?.Value<int>() ?? 0,
                Title = json["Title"]?.ToString() ?? string.Empty,
                Body = json["Body"]?.ToString() ?? string.Empty
            };
        }
    }
}
