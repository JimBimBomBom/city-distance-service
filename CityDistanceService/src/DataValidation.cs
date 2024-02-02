using FluentValidation;

public class CityInfoValidator : AbstractValidator<CityInfo>
{
    public CityInfoValidator()
    {
        RuleFor(x => x.CityId).GreaterThan(0);
        RuleFor(x => x.CityName).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public class NewCityInfoValidator : AbstractValidator<CityInfo>
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

public class GeocodeApiResponseValidator : AbstractValidator<GeocodeApiResponse>
{
    public GeocodeApiResponseValidator()
    {
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lon).InclusiveBetween(-180, 180);
    }
}

public class StringValidator : AbstractValidator<string>
{
    public StringValidator()
    {
        RuleFor(x => x).NotEmpty();
    }
}

public class IdValidator : AbstractValidator<int>
{
    public IdValidator()
    {
        RuleFor(x => x).GreaterThan(0);
    }
}
