namespace News.App.Data;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
        context.Attributes.TryAdd("data-val-requiredif", $"The {thisFieldName} field is required if {otherFieldName} field is not set.");
        context.Attributes.TryAdd("data-val-requiredif-value", this.desiredValue?.ToString() ?? "");

        var otherPropertyInputName = this.otherPropertyName;

        var thisPropertyName = context.ModelMetadata.PropertyName;
        var thisPropertyInputName = context.Attributes["name"];
        if (thisPropertyName is not null && thisPropertyInputName is not null)
        {
            otherPropertyInputName = ReplaceLast(thisPropertyInputName, thisPropertyName, this.otherPropertyName);
        }

        // let's hope the input name is correct
        context.Attributes.TryAdd("data-val-requiredif-property", otherPropertyInputName);

        static string? GetFieldName(ModelMetadata? modelMetadata)
        {
            return modelMetadata?.DisplayName ?? modelMetadata?.Name;
        }

        static string ReplaceLast(string source, string search, string replace)
        {
            int place = source.LastIndexOf(search);

            if (place == -1)
                return source;

            return source.Remove(place, search.Length).Insert(place, replace);
        }
    }
}
