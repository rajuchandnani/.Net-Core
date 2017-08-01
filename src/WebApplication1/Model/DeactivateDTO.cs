using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication1.Model
{
    public class DeactivateDTO
    {
        [Required]
        public string Login { get; set; }
    }
}
