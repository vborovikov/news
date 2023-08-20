namespace News.App.Data;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class RequiredIfAttribute : ValidationAttribute
{
    private readonly string propertyName;
    private readonly object? desiredValue;

    public RequiredIfAttribute(string propertyName, Type propertyType, string value)
        : this(propertyName, TypeDescriptor.GetConverter(propertyType).ConvertFromInvariantString(value))
    {
    }

    public RequiredIfAttribute(string propertyName, object? value)
    {
        this.propertyName = propertyName;
        this.desiredValue = value;
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
        var currentValue = type.GetProperty(this.propertyName)?.GetValue(instance, null);

        if (Object.ReferenceEquals(currentValue, this.desiredValue) ||
            (currentValue?.Equals(this.desiredValue) == true))
        {
            return base.IsValid(value, validationContext);
        }

        return ValidationResult.Success;
    }
}
