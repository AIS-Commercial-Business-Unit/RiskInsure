using System;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class JsonPathAttribute : Attribute
{
    public string Path { get; }
    public JsonPathAttribute(string path) => Path = path ?? throw new ArgumentNullException(nameof(path));
}
``