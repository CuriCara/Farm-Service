using AutoMapper;
using BusinessLogic.Users.Model;
using DataAccess.Entity;

namespace BusinessLogic.Mapper;

public class UsersBLProfile : Profile
{
    public UsersBLProfile()
    {
        CreateMap<User, UserModel>()
            .ForMember(x => x.Id, opt => opt.MapFrom(scr => scr.Id))
            .ForMember(x => x.UserName, opt => opt.MapFrom(scr => scr.UserName))
            //.ForMember(x => x.RoleId, opt => opt.MapFrom(scr => scr.RoleId))
            .ForMember(x => x.Email, opt => opt.MapFrom(scr => scr.Email))
            .ForMember(x => x.PasswordHash, opt => opt.MapFrom(scr => scr.PasswordHash));

        CreateMap<User, CreateUserModel>()
            .ForMember(x => x.UserName, opt => opt.MapFrom(scr => scr.UserName))
            .ForMember(x => x.PasswordHash, opt => opt.MapFrom(scr => scr.PasswordHash));
        
        CreateMap<UserModel, UpdateUserModel>()
            .ForMember(x => x.Id, opt => opt.MapFrom(scr => scr.Id))
            .ForMember(x => x.UserName, opt => opt.MapFrom(scr => scr.UserName))
            .ForMember(x => x.PasswordHash, opt => opt.MapFrom(scr => scr.PasswordHash));
        ;
    }
}