using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Dtos
{
    public class AdminUpdatePatientRequest
    {

        private string _fullName = "";
        private string? _address;

        [MaxLength(70, ErrorMessage = "Họ và tên không được vượt quá 70 ký tự.")]
        public string FullName
        {
            get => _fullName;
            set => _fullName = InputText.TrimRequired(value);
        }

        public byte Gender { get; set; }

        [MaxLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự.")]
        public string? Address
        {
            get => _address;
            set => _address = InputText.TrimOptional(value);
        }

        public string RowVersion { get; set; } = "";
        public string? AccountRowVersion { get; set; }
    }
}
