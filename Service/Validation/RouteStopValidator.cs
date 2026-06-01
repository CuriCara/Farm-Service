using DataAccess.Entity;
using FluentValidation;

namespace Service.Validation;

public class RouteStopValidator : AbstractValidator<RouteStop>
{
    public RouteStopValidator()
    {
        RuleFor(x => x.LocationType).IsInEnum();

        RuleFor(x => x.FarmId)
            .NotNull()
            .When(x => x.LocationType == StopType.Farm || x.LocationType == StopType.Depot);

        RuleFor(x => x.StoreId)
            .NotNull()
            .When(x => x.LocationType == StopType.Store);

        RuleFor(x => x.ServiceDurationMunutes)
            .GreaterThan(0)
            .LessThan(240);
    }
}