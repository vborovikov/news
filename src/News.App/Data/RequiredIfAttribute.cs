namespace News.App.Data;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class RequiredIfAttribute : ValidationAttribute, IClientModelValidator
{
    private readonly string otherPropertyName;
    private readonly object? desiredValue;

    public RequiredIfAttribute(string otherPropertyName, Type otherPropertyType, string otherPropertyValue)
        : this(otherPropertyName, TypeDescriptor.GetConverter(otherPropertyType).ConvertFromInvariantString(otherPropertyValue))
    {
    }

    public RequiredIfAttribute(string otherPropertyName, object? otherPropertyValue)
    {
        this.otherPropertyName = otherPropertyName;
        this.desiredValue = otherPropertyValue;
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return false;
        }

        // only check string length if empty strings are not allowed
        if (value is string stringValue)
        {
            return !string.IsNullOrWhiteSpace(stringValue);
        }

        return true;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var type = instance.GetType();
        var currentValue = type.GetProperty(this.otherPropertyName)?.GetValue(instance, null);

        if (Object.ReferenceEquals(currentValue, this.desiredValue) ||
            (currentValue?.Equals(this.desiredValue) == true))
        {
            return base.IsValid(value, validationContext);
        }

        return ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        var thisFieldName = GetFieldName(context.ModelMetadata);
        var otherFieldName = GetFieldName(context.ModelMetadata.ContainerMetadata?.Properties[this.otherPropertyName]) ?? this.otherPropertyName;

        context.Attributes.TryAdd("data-val", "true");

        // error message
        var message = $"The {thisFieldName} field is required if {otherFieldName} field is not set.";
        context.Attributes["data-val-requiredif"] = context.Attributes.TryGetValue("data-val-requiredif", out var messages) ?
            string.Join('\n', messages, message) : message;

        // property and its value
        var others = (context.Attributes.TryGetValue("data-val-requiredif-others", out var othersStr) ?
            JsonSerializer.Deserialize<Dictionary<string, string>>(othersStr) : []) ?? [];
        others.TryAdd(GetInputName(this.otherPropertyName, context), this.desiredValue?.ToString() ?? string.Empty);
        context.Attributes["data-val-requiredif-others"] = JsonSerializer.Serialize(others);

        static string? GetFieldName(ModelMetadata? modelMetadata)
        {
            return modelMetadata?.DisplayName ?? modelMetadata?.Name;
        }
    }

    private static string GetInputName(string propertyName, ClientModelValidationContext context)
    {
        var inputName = propertyName;

        if (context.ModelMetadata.PropertyName is string thisPropertyName &&
            context.Attributes["name"] is string thisInputName)
        {
            var pos = thisInputName.LastIndexOf(thisPropertyName, StringComparison.Ordinal);
            if (pos > 0)
            {
                inputName = thisInputName.Remove(pos, thisPropertyName.Length).Insert(pos, propertyName);
            }
        }

        // let's hope the input name is correct
        return inputName;
    }
}
