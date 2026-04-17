using System.Text.Json;

namespace LayerZero.Data;

/// <summary>
/// Converts values between model and provider representations.
/// </summary>
public interface IDataValueConverter
{
    /// <summary>
    /// Gets the model CLR type.
    /// </summary>
    Type ModelType { get; }

    /// <summary>
    /// Gets the provider CLR type.
    /// </summary>
    Type ProviderType { get; }

    /// <summary>
    /// Converts a model value to a provider value.
    /// </summary>
    /// <param name="value">The model value.</param>
    /// <returns>The provider value.</returns>
    object? ConvertToProvider(object? value);

    /// <summary>
    /// Converts a provider value to a model value.
    /// </summary>
    /// <param name="value">The provider value.</param>
    /// <returns>The model value.</returns>
    object? ConvertFromProvider(object? value);
}

/// <summary>
/// Converts values between model and provider representations.
/// </summary>
/// <typeparam name="TModel">The model CLR type.</typeparam>
/// <typeparam name="TProvider">The provider CLR type.</typeparam>
public abstract class DataValueConverter<TModel, TProvider> : IDataValueConverter
{
    /// <inheritdoc />
    public Type ModelType => typeof(TModel);

    /// <inheritdoc />
    public Type ProviderType => typeof(TProvider);

    /// <inheritdoc />
    public object? ConvertToProvider(object? value)
    {
        return value is null
            ? null
            : ConvertToProvider((TModel)value);
    }

    /// <inheritdoc />
    public object? ConvertFromProvider(object? value)
    {
        return value is null
            ? default(TModel)
            : ConvertFromProvider((TProvider)value);
    }

    /// <summary>
    /// Converts a model value to a provider value.
    /// </summary>
    /// <param name="value">The model value.</param>
    /// <returns>The provider value.</returns>
    protected abstract TProvider ConvertToProvider(TModel value);

    /// <summary>
    /// Converts a provider value to a model value.
    /// </summary>
    /// <param name="value">The provider value.</param>
    /// <returns>The model value.</returns>
    protected abstract TModel ConvertFromProvider(TProvider value);
}

/// <summary>
/// Provides common value converters for LayerZero data mappings.
/// </summary>
public static class DataValueConverters
{
    /// <summary>
    /// Creates a JSON string converter for one CLR type.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="options">The optional serializer options.</param>
    /// <returns>The converter.</returns>
    public static DataValueConverter<TModel, string> Json<TModel>(JsonSerializerOptions? options = null) =>
        new JsonValueConverter<TModel>(options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>
    /// Creates a string-backed enum converter.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <returns>The converter.</returns>
    public static DataValueConverter<TEnum, string> EnumAsString<TEnum>()
        where TEnum : struct, Enum =>
        new EnumAsStringValueConverter<TEnum>();

    private sealed class JsonValueConverter<TModel>(JsonSerializerOptions? options) : DataValueConverter<TModel, string>
    {
        protected override string ConvertToProvider(TModel value) =>
            JsonSerializer.Serialize(value, options);

        protected override TModel ConvertFromProvider(string value)
        {
            var result = JsonSerializer.Deserialize<TModel>(value, options);
            if (result is null)
            {
                throw new InvalidOperationException($"JSON payload could not be deserialized to '{typeof(TModel).FullName}'.");
            }

            return result;
        }
    }

    private sealed class EnumAsStringValueConverter<TEnum> : DataValueConverter<TEnum, string>
        where TEnum : struct, Enum
    {
        protected override string ConvertToProvider(TEnum value) => value.ToString();

        protected override TEnum ConvertFromProvider(string value) =>
            Enum.Parse<TEnum>(value, ignoreCase: false);
    }
}
