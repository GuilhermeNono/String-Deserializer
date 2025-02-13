using System.Globalization;
using System.Text;
using System.Text.Json;

var folderName = string.Empty;

while (true)
{
    Console.WriteLine("Informe o nome da classe base: ");
    Console.Write(">");

    folderName = Console.ReadLine();

    if (string.IsNullOrEmpty(folderName))
    {
        Console.WriteLine("\nPor favor informe um nome válido para a classe.");
        ReadAndClear();
        continue;
    }


    var jsonSerialized = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Input", "Input.json"));

    if (string.IsNullOrEmpty(jsonSerialized))
    {
        Console.WriteLine("\nPor favor informe um json valido.");
        ReadAndClear();
        continue;
    }

    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSerialized));

    Directory.CreateDirectory(GetDirectory(folderName));

    await GenerateOutput(stream, folderName);

    Console.WriteLine($"\nO json foi serializado como {folderName}.cs");

    ReadAndClear();
}


async Task GenerateOutput(MemoryStream stream, string? objectName = null)
{
    var deserializedProps = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(stream);

    await MountClass(deserializedProps, objectName);
}

async Task MountClass(Dictionary<string, object>? properties, string? objectName = null)
{
    if (properties is null)
        return;

    var outputBuilder = new StringBuilder();

    TextInfo info = CultureInfo.CurrentCulture.TextInfo;

    string className = $"{objectName ?? ""}".ToLower().Replace("_", " ");
    className = info.ToTitleCase(className).Replace(" ", string.Empty) + "Response";

    outputBuilder.Append($$"""public class {{className}}{""");

    foreach (var property in properties)
    {
        string propertyName = property.Key.ToLower().Replace("_", " ");
        propertyName = info.ToTitleCase(propertyName).Replace(" ", string.Empty);

        string propertyValue = GetCSharpType(property.Value);

        if (propertyValue.Equals("object", StringComparison.CurrentCultureIgnoreCase))
        {
            var propertyList = JsonDocument
                .Parse(property.Value.ToString())
                .RootElement
                .EnumerateObject()
                .ToList();

            var nestedObjectProperty =
                propertyList
                    .ToDictionary<JsonProperty, string, object>(prop => prop.Name, prop => prop.Value);

            await MountClass(nestedObjectProperty, $"{propertyName}");

            var nameUpdated = propertyValue.Replace("object", propertyName);

            outputBuilder.Append($"\npublic {nameUpdated} {propertyName} {{ get; set; }}");
            continue;
        }

        if (propertyValue.Equals("List<object>", StringComparison.CurrentCultureIgnoreCase))
        {
            var propertyList = JsonDocument
                .Parse(property.Value.ToString())
                .RootElement
                .EnumerateArray()
                .ToArray()[0]
                .EnumerateObject();

            var nestedObjectProperty =
                propertyList
                    .ToDictionary<JsonProperty, string, object>(prop => prop.Name, prop => prop.Value);

            await MountClass(nestedObjectProperty, $"{propertyName}");

            var nameUpdated = propertyValue.Replace("object", propertyName);

            outputBuilder.Append($"\npublic {nameUpdated} {propertyName} {{ get; set; }}");
            continue;
        }

        outputBuilder.Append($"\npublic {propertyValue} {propertyName} {{ get; set; }}");
    }

    outputBuilder.Append("\n}");
    await File.WriteAllTextAsync(GetDirectory(folderName, $"{className}.cs"),
        outputBuilder.ToString());
}

static string GetCSharpType(object value)
{
    return value switch
    {
        string => "string",
        int => "int",
        long => "long",
        bool => "bool",
        double => "double",
        JsonElement json when json.ValueKind == JsonValueKind.Array => "List<object>",
        JsonElement json when json.ValueKind == JsonValueKind.Object => "object",
        _ => "string"
    };
}

void ReadAndClear()
{
    Console.ReadKey();
    Console.Clear();
}

static string GetDirectory(params string[] param)
{
    var list = new List<string>();

    list.AddRange([AppContext.BaseDirectory, "Output"]);
    list.AddRange(param);

    return Path.Combine(list.ToArray());
}