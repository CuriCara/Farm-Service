using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DataAccess.Entity;

[Table("UnitsOfMeasurement")]
public class UnitsOfMeasurement : BaseEntity
{
    [Required(ErrorMessage = "Введите единицу измерения")]
    public string UoM { get; set; } 

    [Required(ErrorMessage = "Укажите коэффициент перевода к базовой единице")]
    [Range(0.000001, double.MaxValue)]
    public double ConversionFactor { get; set; }
    
    [Required(ErrorMessage = "Выберите категорию")]
    public int? UnitCategoryId { get; set; }
    
    [ForeignKey(nameof(UnitCategoryId))]
    public UnitCategory? Category { get; set; }
    
}