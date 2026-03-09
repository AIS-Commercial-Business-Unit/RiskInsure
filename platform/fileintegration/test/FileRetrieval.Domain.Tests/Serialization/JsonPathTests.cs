using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit.Abstractions;

namespace FileRetrieval.Domain.Tests.Serialization;

public class JsonPathAttributeTests(ITestOutputHelper output)
{
    private sealed class SampleParent
    {
        public string Id { get; init; } = string.Empty;
        public SampleChild? Child { get; init; }
    }

    private sealed class SampleChild
    {
        public string NormalValue { get; init; } = string.Empty;

        [JsonPath("/rootvalue")]
        public string TestValue { get; init; } = string.Empty;
    }

    [Fact]
    public void Serialize_ParentWhereChildHasPropertyWithJsonPathAttribute_PropertyAppearsAtJsonRoot()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        JsonPathModifierExtensions.AddJsonPathConverters(options);

        var parent = new SampleParent
        {
            Id = "parent-id",
            Child = new SampleChild
            {
                NormalValue = "some-normal-value",
                TestValue = "promoted-value"
            }
        };

        // Act: serialize the parent — the object that contains the child with the [JsonPath] attribute
        var json = JsonSerializer.Serialize(parent, options);

        output.WriteLine("Serialized JSON:");
        output.WriteLine(json);

        // Assert: TestValue is hoisted to the root of the parent JSON document under the key "rootvalue".
        // [JsonPath("/rootvalue")] on a child property causes the converter to lift the value
        // out of the child object and place it at the root of the serialized parent document.
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("rootvalue", out var rootValueElement)
            .Should().BeTrue("the [JsonPath(\"/rootvalue\")] attribute should hoist TestValue to the root of the parent JSON");

        rootValueElement.GetString().Should().Be("promoted-value");

        // The property must NOT remain nested inside Child — it was hoisted out.
        root.TryGetProperty("Child", out var childElement).Should().BeTrue();
        childElement.TryGetProperty("rootvalue", out _)
            .Should().BeFalse("rootvalue should have been hoisted to the parent root, not left inside Child");
    }

    // ---- grandchild test models ----

    private sealed class DeepSampleParent
    {
        public string Id { get; init; } = string.Empty;
        public DeepSampleChild? Child { get; init; }
    }

    private sealed class DeepSampleChild
    {
        public string NormalValue { get; init; } = string.Empty;
        public DeepSampleGrandchild? GrandChild { get; init; }
    }

    private sealed class DeepSampleGrandchild
    {
        public string OtherValue { get; init; } = string.Empty;

        [JsonPath("/grandchildvalue")]
        public string TestValue { get; init; } = string.Empty;
    }

    [Fact]
    public void Serialize_ParentWhereGrandchildHasPropertyWithJsonPathAttribute_PropertyAppearsAtJsonRoot()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        JsonPathModifierExtensions.AddJsonPathConverters(options);

        var parent = new DeepSampleParent
        {
            Id = "parent-id",
            Child = new DeepSampleChild
            {
                NormalValue = "child-value",
                GrandChild = new DeepSampleGrandchild
                {
                    OtherValue = "other-value",
                    TestValue = "grandchild-promoted-value"
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(parent, options);

        output.WriteLine("Serialized JSON:");
        output.WriteLine(json);

        // Assert: TestValue is hoisted from the grandchild all the way to the root of the
        // parent JSON document under the key "grandchildvalue".
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("grandchildvalue", out var grandchildValueElement)
            .Should().BeTrue("the [JsonPath(\"/grandchildvalue\")] attribute should hoist TestValue to the root of the parent JSON");

        grandchildValueElement.GetString().Should().Be("grandchild-promoted-value");

        // Must NOT appear in Child or GrandChild — fully hoisted out.
        root.TryGetProperty("Child", out var childEl).Should().BeTrue();
        childEl.TryGetProperty("grandchildvalue", out _)
            .Should().BeFalse("grandchildvalue should not remain in Child");
        childEl.TryGetProperty("GrandChild", out var grandChildEl).Should().BeTrue();
        grandChildEl.TryGetProperty("grandchildvalue", out _)
            .Should().BeFalse("grandchildvalue should not remain in GrandChild");
    }

    [Fact]
    public void Deserialize_JsonWithRootValueProperty_MapsToChildTestValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        JsonPathModifierExtensions.AddJsonPathConverters(options);

        // JSON with the [JsonPath("/rootvalue")] value already at the root
        var json = """{"Id":"parent-id","rootvalue":"promoted-value","Child":{"NormalValue":"some-normal-value"}}""";

        output.WriteLine("Input JSON:");
        output.WriteLine(json);

        // Act
        var parent = JsonSerializer.Deserialize<SampleParent>(json, options);

        output.WriteLine($"Deserialized Child.TestValue: {parent?.Child?.TestValue}");

        // Assert: rootvalue at the JSON root is injected back into Child.TestValue
        parent.Should().NotBeNull();
        parent!.Id.Should().Be("parent-id");
        parent.Child.Should().NotBeNull();
        parent.Child!.NormalValue.Should().Be("some-normal-value");
        parent.Child.TestValue.Should().Be("promoted-value",
            "rootvalue at the JSON root should be deserialized back into Child.TestValue via [JsonPath(\"/rootvalue\")]");
    }

    [Fact]
    public void Deserialize_JsonWithGrandchildRootValueProperty_MapsToGrandchildTestValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        JsonPathModifierExtensions.AddJsonPathConverters(options);

        // JSON with the [JsonPath("/grandchildvalue")] value already at the root
        var json = """{"Id":"parent-id","grandchildvalue":"grandchild-promoted-value","Child":{"NormalValue":"child-value","GrandChild":{"OtherValue":"other-value"}}}""";

        output.WriteLine("Input JSON:");
        output.WriteLine(json);

        // Act
        var parent = JsonSerializer.Deserialize<DeepSampleParent>(json, options);

        output.WriteLine($"Deserialized GrandChild.TestValue: {parent?.Child?.GrandChild?.TestValue}");

        // Assert: grandchildvalue at the JSON root is injected back through Child into GrandChild.TestValue
        parent.Should().NotBeNull();
        parent!.Id.Should().Be("parent-id");
        parent.Child.Should().NotBeNull();
        parent.Child!.NormalValue.Should().Be("child-value");
        parent.Child.GrandChild.Should().NotBeNull();
        parent.Child.GrandChild!.OtherValue.Should().Be("other-value");
        parent.Child.GrandChild.TestValue.Should().Be("grandchild-promoted-value",
            "grandchildvalue at the JSON root should be deserialized back into GrandChild.TestValue via [JsonPath(\"/grandchildvalue\")]");
    }

    // ---- polymorphic (ProtocolSettings-style) test models ----

    private abstract class AbstractSettings
    {
        public abstract string Kind { get; }
    }

    private sealed class ConcreteSettingsA : AbstractSettings
    {
        public override string Kind => "A";
        public string NormalProp { get; init; } = string.Empty;

        [JsonPath("/secret_a")]
        public string Secret { get; init; } = string.Empty;
    }

    private sealed class ConcreteSettingsB : AbstractSettings
    {
        public override string Kind => "B";

        [JsonPath("/secret_b")]
        public string Token { get; init; } = string.Empty;
    }

    private sealed class OwnerDocument
    {
        public string Id { get; init; } = string.Empty;

        // Declared as the abstract base type — mirrors FileRetrievalConfiguration.ProtocolSettings
        public AbstractSettings? Settings { get; init; }
    }

    [Fact]
    public void Serialize_OwnerWithPolymorphicChild_SecretAppearsAtDocumentRoot()
    {
        // Arrange — add a polymorphic converter for AbstractSettings that mirrors
        // ProtocolSettingsJsonConverter (picks concrete type from a discriminator).
        var options = new JsonSerializerOptions();
        options.Converters.Add(new AbstractSettingsConverter());
        JsonPathModifierExtensions.AddJsonPathConverters(options);

        var doc = new OwnerDocument
        {
            Id = "doc-1",
            Settings = new ConcreteSettingsA
            {
                NormalProp = "normal",
                Secret = "top-secret"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(doc, options);

        output.WriteLine("Serialized JSON:");
        output.WriteLine(json);

        // Assert: Secret is hoisted to the document root even though Settings is declared as
        // the abstract base type AbstractSettings (mirrors ProtocolSettings → FtpProtocolSettings)
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("secret_a", out var secretEl)
            .Should().BeTrue("Secret should be hoisted to the document root via [JsonPath(\"/secret_a\")] even when the declared type is abstract");

        secretEl.GetString().Should().Be("top-secret");

        // Must NOT remain inside Settings
        root.TryGetProperty("Settings", out var settingsEl).Should().BeTrue();
        settingsEl.TryGetProperty("secret_a", out _)
            .Should().BeFalse("secret_a should have been hoisted out of Settings");
    }

    [Fact]
    public void Deserialize_DocumentWithPolymorphicChildSecret_SecretMappedBackToConcreteProperty()
    {
        // Arrange — add a polymorphic converter for AbstractSettings that mirrors
        // ProtocolSettingsJsonConverter (picks concrete type from a discriminator).
        var options = new JsonSerializerOptions();
        options.Converters.Add(new AbstractSettingsConverter());
        JsonPathModifierExtensions.AddJsonPathConverters(options);

        // JSON as it would be stored in Cosmos — secret hoisted to root, Settings has no secret key
        var json = """{"Id":"doc-1","secret_a":"top-secret","Settings":{"Kind":"A","NormalProp":"normal"}}""";

        output.WriteLine("Input JSON:");
        output.WriteLine(json);

        // Act
        var doc = JsonSerializer.Deserialize<OwnerDocument>(json, options);

        output.WriteLine($"Deserialized Settings type: {doc?.Settings?.GetType().Name}");
        output.WriteLine($"Deserialized Secret: {(doc?.Settings as ConcreteSettingsA)?.Secret}");

        // Assert: secret_a from the document root is injected back into ConcreteSettingsA.Secret
        doc.Should().NotBeNull();
        doc!.Settings.Should().BeOfType<ConcreteSettingsA>();
        var settings = (ConcreteSettingsA)doc.Settings!;
        settings.NormalProp.Should().Be("normal");
        settings.Secret.Should().Be("top-secret",
            "secret_a at the document root should be deserialized back into ConcreteSettingsA.Secret via [JsonPath(\"/secret_a\")]");
    }

    /// <summary>
    /// Mirrors ProtocolSettingsJsonConverter: handles the abstract base type by delegating
    /// to the concrete type using a discriminator property, so JsonPathObjectConverter can
    /// handle hoisting for the concrete type.
    /// </summary>
    private sealed class AbstractSettingsConverter : JsonConverter<AbstractSettings>
    {
        public override AbstractSettings? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var raw = doc.RootElement.GetRawText();
            var kind = doc.RootElement.TryGetProperty("Kind", out var k) ? k.GetString() : null;

            return kind switch
            {
                "A" => JsonSerializer.Deserialize<ConcreteSettingsA>(raw, options),
                "B" => JsonSerializer.Deserialize<ConcreteSettingsB>(raw, options),
                _ => throw new JsonException($"Unknown Kind: {kind}")
            };
        }

        public override void Write(Utf8JsonWriter writer, AbstractSettings value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }
}
