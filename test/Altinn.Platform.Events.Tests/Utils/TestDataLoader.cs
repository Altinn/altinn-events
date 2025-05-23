﻿using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Tests.Utils;

public static class TestDataLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T> Load<T>(string id)
    {
        string path = $"../../../Data/{typeof(T).Name}/{id}.json";

        if (!File.Exists(path))
        {
            return default;
        }

        string fileContent = await File.ReadAllTextAsync(path);

        T data = JsonSerializer.Deserialize<T>(fileContent, _options);
        return data;
    }
}
