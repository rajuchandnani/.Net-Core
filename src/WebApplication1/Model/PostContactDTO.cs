using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication1.Model
{
    public class PostContactDTO
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string Login { get; set; }

        [Required]
        public string EmailAddress { get; set; }

        public List<SLA> SLAs { get; set; }
    }

    public class SLA
    {
        public string Name { get; set; }

        public string State { get; set; }

        public DateTime ActiveDate { get; set; }
    }
}
