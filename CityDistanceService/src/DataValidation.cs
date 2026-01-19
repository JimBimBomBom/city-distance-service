using FluentValidation;

public class CityInfoValidator : AbstractValidator<CityInfo>
{
    public CityInfoValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.CityName).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public class NewCityInfoValidator : AbstractValidator<NewCityInfo>
{
    public NewCityInfoValidator()
    {
        RuleFor(x => x.CityName).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public class CoordinatesValidator : AbstractValidator<Coordinates>
{
    public CoordinatesValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public class CitiesDistanceRequestValidator : AbstractValidator<CitiesDistanceRequest>
{
    public CitiesDistanceRequestValidator()
    {
        RuleFor(x => x.City1Name).NotEmpty();
        RuleFor(x => x.City2Name).NotEmpty();
    }
}

// String validator for input validation
public class StringValidator : AbstractValidator<string>
{
    public StringValidator()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("Value cannot be empty")
            .MinimumLength(1).WithMessage("Value must be at least 1 character")
            .MaximumLength(100).WithMessage("Value cannot exceed 100 characters")
            .Matches(@"^[a-zA-Z0-9\s\-\.]+$").WithMessage("Value contains invalid characters");
    }
}

public class CityIdValidator : AbstractValidator<CityId>
{
    public CityIdValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class IdValidator : AbstractValidator<int>
{
    public IdValidator()
    {
        RuleFor(x => x).GreaterThan(0);
    }
}
