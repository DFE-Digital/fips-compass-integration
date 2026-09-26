using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fips.Data;

/// <summary>
/// Reads user-group children as either a list of names or a list of nested group objects.
/// </summary>
public sealed class CompassUserGroupChildrenConverter : JsonConverter<IReadOnlyList<CompassFipsUserGroupNode>>
{
    public override IReadOnlyList<CompassFipsUserGroupNode> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Array.Empty<CompassFipsUserGroupNode>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("User group children must be an array.");

        var children = new List<CompassFipsUserGroupNode>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var name = reader.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    children.Add(new CompassFipsUserGroupNode { Name = name.Trim() });
                continue;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var node = JsonSerializer.Deserialize<CompassFipsUserGroupNode>(ref reader, options);
                if (node is not null && !string.IsNullOrWhiteSpace(node.Name))
                    children.Add(node);
                continue;
            }

            reader.Skip();
        }

        return children;
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<CompassFipsUserGroupNode> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var child in value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", child.Id);
            writer.WriteString("name", child.Name);
            if (child.Description is not null)
                writer.WriteString("description", child.Description);
            writer.WriteNumber("displayOrder", child.DisplayOrder);
            writer.WriteBoolean("active", child.Active);
            writer.WritePropertyName("children");
            Write(writer, child.Children, options);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
