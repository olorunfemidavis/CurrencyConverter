using System.ComponentModel.DataAnnotations;
namespace CurrencyConverter.Application.Helpers;

public class DateRangeAttribute : ValidationAttribute
{
    private readonly string _startDatePropertyName;

    public DateRangeAttribute(string startDatePropertyName)
    {
        _startDatePropertyName = startDatePropertyName;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var endDate = (DateTime)value;
        var startDateProperty = validationContext.ObjectType.GetProperty(_startDatePropertyName);
        if (startDateProperty == null)
        {
            return new ValidationResult($"Unknown property: {_startDatePropertyName}");
        }

        var startDate = (DateTime)startDateProperty.GetValue(validationContext.ObjectInstance);

        // Check if dates are in the future
        if (startDate > DateTime.Today || endDate > DateTime.Today)
        {
            return new ValidationResult("Dates cannot be in the future.");
        }

        // Check if endDate >= startDate
        if (endDate < startDate)
        {
            return new ValidationResult("EndDate must be greater than or equal to StartDate.");
        }

        return ValidationResult.Success;
    }
}