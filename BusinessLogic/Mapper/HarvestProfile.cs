namespace BusinessLogic.Mapper;

using AutoMapper;
using DataAccess.Entity;
using BusinessLogic.Harvests.Model;

public class HarvestProfile : Profile
{
    public HarvestProfile()
    {
        CreateMap<Harvest, HarvestModel>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.ProductName))
            .ForMember(dest => dest.FarmName, opt => opt.MapFrom(src => src.Farm.Name))
            .ForMember(dest => dest.UnitName, opt => opt.MapFrom(src => src.Unit.Category.BaseUnit.UoM));
        
        CreateMap<HarvestModel, Harvest>()
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.Reports, opt => opt.Ignore()); //пока не сделал
    }
}
