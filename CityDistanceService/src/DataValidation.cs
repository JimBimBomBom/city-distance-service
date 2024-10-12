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
        RuleFor(x => x.City1).NotEmpty();
        RuleFor(x => x.City2).NotEmpty();
    }
}

public class StringValidator : AbstractValidator<string>
{
    public StringValidator()
    {
        RuleFor(x => x).NotEmpty();
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
