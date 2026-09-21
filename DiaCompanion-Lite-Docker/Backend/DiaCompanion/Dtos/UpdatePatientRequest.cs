using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class UpdatePatientRequest
{
    private string _fullName = "";
    private string _phone = "";
    private string? _address;
    private string? _note;

    [Required, MaxLength(70, ErrorMessage = "Họ và tên không được vượt quá 70 ký tự.")]
    public string FullName
    {
        get => _fullName;
        set => _fullName = InputText.TrimRequired(value);
    }

    [Range(0, 2, ErrorMessage = "Giới tính không hợp lệ.")]
    public byte Gender { get; set; }

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(
    @"^\d{10,11}$",
    ErrorMessage = "Số điện thoại phải gồm 10 đến 11 chữ số.")]
    public string Phone
    {
        get => _phone;
        set => _phone = InputText.TrimRequired(value);
    }

    [MaxLength(300)]
    public string? Address
    {
        get => _address;
        set => _address = InputText.TrimOptional(value);
    }

    public byte DiabetesType { get; set; }

    public short? DiabetesDurationYears { get; set; }

    public decimal? BaselineHbA1c { get; set; }

    [MaxLength(1000)]
    public string? Note
    {
        get => _note;
        set => _note = InputText.TrimOptional(value);
    }

    [Required]
    public string RowVersion { get; set; } = "";
}