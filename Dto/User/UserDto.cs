using System.ComponentModel.DataAnnotations;

namespace FrancProject.Dto
{
    public class UserDto
    {


        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string PasswordHash { get; set; } = null!;
    }
}
